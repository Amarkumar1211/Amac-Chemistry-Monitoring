using FurnaceMonitor.Application.DTOs.Batch;
using FurnaceMonitor.Application.DTOs.Realtime;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Application.Services.PdfExtraction;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using FurnaceMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurnaceMonitor.Infrastructure.Services;

public class PdfWatcherService : BackgroundService
{
    private readonly ILogger<PdfWatcherService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private FileSystemWatcher? _watcher;
    private string _directoryPath = string.Empty;
    private string _processedDirectoryPath = string.Empty;
    
    // To deduplicate concurrent events for the same file
    private readonly HashSet<string> _processingFiles = new();

    public PdfWatcherService(
        ILogger<PdfWatcherService> logger, 
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enable = _configuration.GetValue<bool>("PdfWatcher:Enable");
        if (!enable)
        {
            _logger.LogInformation("PdfWatcher is disabled in configuration.");
            return Task.CompletedTask;
        }

        var configuredPath = _configuration.GetValue<string>("PdfWatcher:DirectoryPath");
        if (string.IsNullOrWhiteSpace(configuredPath) || configuredPath.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
        {
            _directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Spectro PDF");
        }
        else
        {
            _directoryPath = configuredPath;
        }

        _processedDirectoryPath = Path.Combine(_directoryPath, "Processed");

        try
        {
            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
                _logger.LogInformation("PdfWatcher: Successfully created Desktop watch directory: {Directory}", _directoryPath);
            }

            var readmePath = Path.Combine(_directoryPath, "README_SPECTRO.txt");
            if (!File.Exists(readmePath))
            {
                File.WriteAllText(readmePath,
                    "==========================================================\r\n" +
                    "  AMAC QUALITY IT - SPECTRO PDF WATCH FOLDER\r\n" +
                    "==========================================================\r\n" +
                    "Drop or save your spectrometer PDF analysis reports directly into this folder.\r\n" +
                    "Furnace Monitor will automatically detect, extract chemistry, and update the active batch in real time.\r\n" +
                    "Once parsed, the PDF will be safely archived into the 'Processed' folder.\r\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PdfWatcher: Could not create directory '{Directory}'. " +
                "Please verify permissions. PDF watching is temporarily disabled.",
                _directoryPath);
            return Task.CompletedTask;
        }
        
        if (!Directory.Exists(_processedDirectoryPath))
        {
            try
            {
                Directory.CreateDirectory(_processedDirectoryPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PdfWatcher: Could not create Processed sub-directory. PDF archiving will be skipped.");
            }
        }

        _watcher = new FileSystemWatcher(_directoryPath, "*.pdf")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
            InternalBufferSize = 65536
        };

        _watcher.Created += OnPdfCreated;
        _watcher.Renamed += OnPdfRenamed;
        _logger.LogInformation("PdfWatcher started monitoring {Directory}", _directoryPath);

        return Task.CompletedTask;
    }

    private async void OnPdfRenamed(object sender, RenamedEventArgs e)
    {
        if (!e.FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return;

        var fileName = Path.GetFileName(e.FullPath);
        _logger.LogInformation("PdfWatcher: Detected renamed PDF file: {File}", fileName);

        await HandlePdfFileAsync(e.FullPath);
    }

    private async void OnPdfCreated(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Created) return;
        await HandlePdfFileAsync(e.FullPath);
    }

    private async Task HandlePdfFileAsync(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);

        if (!fileName.StartsWith("K", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping PDF {File}: Name does not start with 'K'.", fileName);
            return;
        }

        if (!fileName.Contains("MT", StringComparison.OrdinalIgnoreCase) &&
            !fileName.Contains("First", StringComparison.OrdinalIgnoreCase) &&
            !fileName.Contains("Middle", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping PDF {File}: Does not contain MT, First, or Middle in name.", fileName);
            return;
        }

        lock (_processingFiles)
        {
            if (!_processingFiles.Add(fullPath)) return;
        }

        try
        {
            await ProcessFileAsync(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PDF {File}", fullPath);
        }
        finally
        {
            lock (_processingFiles)
            {
                _processingFiles.Remove(fullPath);
            }
        }
    }

    private async Task ProcessFileAsync(string filePath)
    {
        await Task.Delay(3000);
        
        var retries = 10;
        FileStream? stream = null;
        while (retries > 0)
        {
            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                break;
            }
            catch (IOException)
            {
                retries--;
                await Task.Delay(1000);
            }
        }

        if (stream == null)
        {
            _logger.LogWarning("Could not acquire lock on file {File} after 10 retries.", filePath);
            return;
        }

        _logger.LogInformation("Processing new PDF: {File}", filePath);

        using var scope = _scopeFactory.CreateScope();
        var pdfExtraction = scope.ServiceProvider.GetRequiredService<IPdfExtractionService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dto = pdfExtraction.ParseSpectroResult(stream);
        stream.Dispose();

        if (dto.FurnaceNumber == null && string.IsNullOrWhiteSpace(dto.BatchNumber))
        {
            _logger.LogWarning("Could not determine Furnace Number or Batch Number from PDF {File}.", filePath);
            MoveToProcessed(filePath, failed: true);
            return;
        }

        // -----------------------------------------------------------------
        // 3RD LETTER CONCEPT & AUTO-ACTIVATION:
        //
        // "if Furnace is in any status if Batch will came for that particular
        // furnace they do Furnace in active mode and Display Batch info as per
        // the Furnace number as per the 3rd letter concept"
        // -----------------------------------------------------------------
        if (dto.FurnaceNumber.HasValue && dto.FurnaceNumber.Value > 0)
        {
            try
            {
                var targetFurnaceNum = $"F{dto.FurnaceNumber.Value}";
                var targetFurnace = await dbContext.Furnaces
                    .FirstOrDefaultAsync(f => f.Number == targetFurnaceNum || f.Number == dto.FurnaceNumber.Value.ToString());

                if (targetFurnace == null)
                {
                    targetFurnace = new Furnace
                    {
                        Number = targetFurnaceNum,
                        Name = $"Furnace {dto.FurnaceNumber.Value}",
                        Status = FurnaceStatus.Active,
                        IsActive = true,
                        OperationalStatus = "Melting",
                        OperationalStatusUpdatedAtUtc = DateTime.UtcNow,
                        SortOrder = dto.FurnaceNumber.Value,
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedBy = "spectro"
                    };
                    await dbContext.Furnaces.AddAsync(targetFurnace);
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    // If furnace is in ANY status (Breakdown, Maintenance, Empty, Disabled, etc.):
                    // Put Furnace in ACTIVE mode!
                    targetFurnace.IsActive = true;
                    targetFurnace.Status = FurnaceStatus.Active;
                    targetFurnace.OperationalStatus = "Melting";
                    targetFurnace.OperationalRemarks = null;
                    targetFurnace.OperationalStatusUpdatedAtUtc = DateTime.UtcNow;
                    dbContext.Furnaces.Update(targetFurnace);
                    await dbContext.SaveChangesAsync();
                }

                // Broadcast furnace activation so all screens see the furnace is now Active
                await notificationService.PushFurnaceStatusChangedAsync(new FurnaceStatusChangedDto
                {
                    FurnaceId = targetFurnace.Id,
                    FurnaceNumber = targetFurnace.Number,
                    OperationalStatus = "Melting",
                    OperationalRemarks = null,
                    UpdatedAtUtc = DateTime.UtcNow
                });

                // Auto-create or auto-update the batch in the database so display screens
                // and smart TVs receive the live batch info immediately:
                if (!string.IsNullOrWhiteSpace(dto.BatchNumber) && (dto.Warnings == null || dto.Warnings.Count == 0))
                {
                    decimal si = 0, ti = 0, mg = 0, sr = 0;
                    if (dto.CalculatedAdditions != null)
                    {
                        foreach (var kvp in dto.CalculatedAdditions)
                        {
                            var lower = kvp.Key.ToLowerInvariant();
                            if (lower == "si" || lower == "silicon") si = Convert.ToDecimal(kvp.Value);
                            else if (lower == "ti" || lower == "titanium" || lower == "altib") ti = Convert.ToDecimal(kvp.Value);
                            else if (lower == "mg" || lower == "magnesium") mg = Convert.ToDecimal(kvp.Value);
                            else if (lower == "sr" || lower == "strontium" || lower == "alsr") sr = Convert.ToDecimal(kvp.Value);
                        }
                    }

                    var hasAdditions = (si > 0 || ti > 0 || mg > 0 || sr > 0);
                    var instruction = hasAdditions ? "Add these chemicals" : "Start Pouring";

                    var existingActive = await dbContext.FurnaceBatches
                        .Include(b => b.ChemicalHistory)
                        .FirstOrDefaultAsync(b => b.FurnaceId == targetFurnace.Id && b.IsActive && b.Status == BatchStatus.Running);

                    if (existingActive != null)
                    {
                        if (existingActive.BatchNumber == dto.BatchNumber)
                        {
                            existingActive.Instruction = instruction;
                            existingActive.ColorCode = dto.ColorCode ?? existingActive.ColorCode;
                            dbContext.FurnaceBatches.Update(existingActive);

                            var chem = await dbContext.FurnaceChemicals.FirstOrDefaultAsync(c => c.BatchId == existingActive.Id);
                            if (chem != null)
                            {
                                chem.Si = si;
                                chem.Ti = ti;
                                chem.Mg = mg;
                                chem.FutureChemical1 = sr;
                                chem.Instruction = instruction;
                                chem.ColorCode = existingActive.ColorCode;
                                chem.CapturedAtUtc = DateTime.UtcNow;
                                dbContext.FurnaceChemicals.Update(chem);
                            }
                        }
                        else
                        {
                            // Close previous batch and start new
                            existingActive.IsActive = false;
                            existingActive.Status = BatchStatus.Closed;
                            existingActive.EndedAtUtc = DateTime.UtcNow;
                            existingActive.ClosedBy = "spectro";
                            existingActive.ClosedTimeUtc = DateTime.UtcNow;
                            dbContext.FurnaceBatches.Update(existingActive);

                            var newBatch = new FurnaceBatch
                            {
                                BatchNumber = dto.BatchNumber.Trim(),
                                FurnaceId = targetFurnace.Id,
                                OperatorId = 1,
                                Status = BatchStatus.Running,
                                IsActive = true,
                                StartedAtUtc = DateTime.UtcNow,
                                Indicator = IndicatorColor.Green,
                                Instruction = instruction,
                                ColorCode = dto.ColorCode ?? "Yellow-blue",
                                CreatedBy = "spectro"
                            };
                            await dbContext.FurnaceBatches.AddAsync(newBatch);
                            await dbContext.SaveChangesAsync();

                            var newChem = new FurnaceChemical
                            {
                                BatchId = newBatch.Id,
                                Si = si,
                                Ti = ti,
                                Mg = mg,
                                FutureChemical1 = sr,
                                ColorCode = newBatch.ColorCode,
                                Indicator = IndicatorColor.Green,
                                Instruction = instruction,
                                CapturedAtUtc = DateTime.UtcNow,
                                CreatedBy = "spectro"
                            };
                            await dbContext.FurnaceChemicals.AddAsync(newChem);
                        }
                    }
                    else
                    {
                        var newBatch = new FurnaceBatch
                        {
                            BatchNumber = dto.BatchNumber.Trim(),
                            FurnaceId = targetFurnace.Id,
                            OperatorId = 1,
                            Status = BatchStatus.Running,
                            IsActive = true,
                            StartedAtUtc = DateTime.UtcNow,
                            Indicator = IndicatorColor.Green,
                            Instruction = instruction,
                            ColorCode = dto.ColorCode ?? "Yellow-blue",
                            CreatedBy = "spectro"
                        };
                        await dbContext.FurnaceBatches.AddAsync(newBatch);
                        await dbContext.SaveChangesAsync();

                        var newChem = new FurnaceChemical
                        {
                            BatchId = newBatch.Id,
                            Si = si,
                            Ti = ti,
                            Mg = mg,
                            FutureChemical1 = sr,
                            ColorCode = newBatch.ColorCode,
                            Indicator = IndicatorColor.Green,
                            Instruction = instruction,
                            CapturedAtUtc = DateTime.UtcNow,
                            CreatedBy = "spectro"
                        };
                        await dbContext.FurnaceChemicals.AddAsync(newChem);
                    }

                    await dbContext.SaveChangesAsync();

                    // Push direct furnace update to all screens
                    await notificationService.PushFurnaceUpdateAsync(new FurnaceUpdateDto
                    {
                        FurnaceNumber = targetFurnace.Number,
                        BatchNumber = dto.BatchNumber,
                        Si = si,
                        Ti = ti,
                        Mg = mg,
                        Sr = sr,
                        FutureChemical1 = sr,
                        ColorCode = dto.ColorCode ?? "Yellow-blue",
                        Indicator = IndicatorColor.Green,
                        Instruction = instruction,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-activate furnace {FurnaceNumber} or record batch.", dto.FurnaceNumber);
            }
        }

        try
        {
            _logger.LogInformation("Broadcasting Parsed PDF {File} via SignalR (Furnace {FurnaceNumber})", filePath, dto.FurnaceNumber);
            await notificationService.PushPdfParsedAsync(dto);
            MoveToProcessed(filePath, failed: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast parsed PDF {File}", filePath);
            MoveToProcessed(filePath, failed: true);
        }
    }

    private void MoveToProcessed(string filePath, bool failed)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var suffix = failed ? "_FAILED" : "";
            var newFileName = $"{fileName}_{timestamp}{suffix}{ext}";
            var destFile = Path.Combine(_processedDirectoryPath, newFileName);

            File.Move(filePath, destFile, overwrite: true);
            _logger.LogInformation("Moved PDF to Processed: {Dest}", destFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move PDF {File} to Processed folder.", filePath);
        }
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
