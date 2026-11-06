using ClosedXML.Excel;
using FurnaceMonitor.Application.DTOs.PartSpecification;
using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using System.IO;
using System.Text.Json;

namespace FurnaceMonitor.Infrastructure.Services;

public class PartSpecificationService : IPartSpecificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public PartSpecificationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PartSpecificationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var specs = await _unitOfWork.PartSpecifications.ListAsync(ct);
        return specs.OrderBy(x => x.PartName).Select(MapToDto).ToList();
    }

    public async Task<PartSpecificationDto> CreateAsync(SavePartSpecificationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PartName) ||
            string.IsNullOrWhiteSpace(request.PartNo) ||
            string.IsNullOrWhiteSpace(request.GradeName))
        {
            throw new ArgumentException("Part Name, Part No, and Grade are required.");
        }

        var specs = await _unitOfWork.PartSpecifications.ListAsync(ct);
        bool exists = specs.Any(x =>
            x.PartName == request.PartName &&
            x.PartNo == request.PartNo &&
            x.GradeName == request.GradeName);

        if (exists)
        {
            throw new InvalidOperationException("A specification with this Part Name, No, and Grade already exists.");
        }

        var spec = new PartSpecification
        {
            PartName = request.PartName,
            PartNo = request.PartNo,
            GradeName = request.GradeName,
            ColorCode = request.ColorCode,
            MinTargetsJson = request.MinTargetsJson,
            MaxTargetsJson = request.MaxTargetsJson,
            CustomerMaxTargetsJson = request.CustomerMaxTargetsJson,
            CustomerMinTargetsJson = request.CustomerMinTargetsJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.PartSpecifications.AddAsync(spec, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        SyncPermanentBackup();

        return MapToDto(spec);
    }

    public async Task UpdateAsync(int id, SavePartSpecificationRequest request, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.PartSpecifications.GetByIdAsync(id, ct);
        if (existing == null) throw new KeyNotFoundException("Specification not found.");

        existing.PartName = request.PartName;
        existing.PartNo = request.PartNo;
        existing.GradeName = request.GradeName;
        existing.ColorCode = request.ColorCode;
        existing.MinTargetsJson = request.MinTargetsJson;
        existing.MaxTargetsJson = request.MaxTargetsJson;
        existing.CustomerMaxTargetsJson = request.CustomerMaxTargetsJson;
        existing.CustomerMinTargetsJson = request.CustomerMinTargetsJson;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        _unitOfWork.PartSpecifications.Update(existing);
        await _unitOfWork.SaveChangesAsync(ct);
        SyncPermanentBackup();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.PartSpecifications.GetByIdAsync(id, ct);
        if (existing == null) throw new KeyNotFoundException("Specification not found.");

        _unitOfWork.PartSpecifications.Remove(existing);
        await _unitOfWork.SaveChangesAsync(ct);
        SyncPermanentBackup();
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteAsync(async () =>
        {
            var specs = await _unitOfWork.PartSpecifications.ListAsync(ct);
            foreach (var spec in specs)
            {
                _unitOfWork.PartSpecifications.Remove(spec);
            }
            await _unitOfWork.SaveChangesAsync(ct);
            SyncPermanentBackup();
        }, ct);
    }

    public async Task ImportFromExcelAsync(Stream excelStream, string actor, bool force = false, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) throw new InvalidOperationException("No worksheet found in the Excel file.");

        var rows = worksheet.RowsUsed().Skip(1); // Skip 1 header row
        var parsedSpecs = new List<PartSpecification>();

        var elements = new[] { "Si", "Fe", "Cu", "Mn", "Mg", "Zn", "Ti", "Sr", "Pb", "Sn", "Ni", "Cr" };

        foreach (var row in rows)
        {
            var dbPartName = row.Cell(2).GetString()?.Trim();
            var dbPartNo = row.Cell(3).GetString()?.Trim();
            var dbGradeName = row.Cell(4).GetString()?.Trim();
            if (string.IsNullOrEmpty(dbPartName) || string.IsNullOrEmpty(dbPartNo) || string.IsNullOrEmpty(dbGradeName))
                continue;

            var minTargets = new Dictionary<string, double>();
            var maxTargets = new Dictionary<string, double>();
            var custMinTargets = new Dictionary<string, double>();
            var custMaxTargets = new Dictionary<string, double>();

            int startCol = 6;
            foreach (var el in elements)
            {
                if (double.TryParse(row.Cell(startCol).GetString()?.Trim(), out double custMin)) custMinTargets[el] = custMin;
                if (double.TryParse(row.Cell(startCol + 1).GetString()?.Trim(), out double custMax)) custMaxTargets[el] = custMax;
                if (double.TryParse(row.Cell(startCol + 2).GetString()?.Trim(), out double ourMin)) minTargets[el] = ourMin;
                if (double.TryParse(row.Cell(startCol + 3).GetString()?.Trim(), out double ourMax)) maxTargets[el] = ourMax;
                
                startCol += 4;
            }

            parsedSpecs.Add(new PartSpecification
            {
                PartName = dbPartName,
                PartNo = dbPartNo,
                GradeName = dbGradeName,
                ColorCode = row.Cell(5).GetString()?.Trim(),
                MinTargetsJson = minTargets.Any() ? JsonSerializer.Serialize(minTargets) : null,
                MaxTargetsJson = maxTargets.Any() ? JsonSerializer.Serialize(maxTargets) : null,
                CustomerMinTargetsJson = custMinTargets.Any() ? JsonSerializer.Serialize(custMinTargets) : null,
                CustomerMaxTargetsJson = custMaxTargets.Any() ? JsonSerializer.Serialize(custMaxTargets) : null,
            });
        }

        await _unitOfWork.ExecuteAsync(async () =>
        {
            var existingSpecs = await _unitOfWork.PartSpecifications.ListAsync(ct);

            if (!force)
            {
                bool hasConflict = false;
                foreach (var spec in parsedSpecs)
                {
                    if (existingSpecs.Any(x => string.Equals(x.PartNo, spec.PartNo, StringComparison.OrdinalIgnoreCase) && 
                                               string.Equals(x.PartName, spec.PartName, StringComparison.OrdinalIgnoreCase) && 
                                               string.Equals(x.GradeName, spec.GradeName, StringComparison.OrdinalIgnoreCase)))
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (hasConflict)
                {
                    throw new InvalidOperationException("CONFLICT_EXISTING");
                }
            }

            foreach (var parsedSpec in parsedSpecs)
            {
                var existing = existingSpecs.FirstOrDefault(x => 
                    x.PartNo == parsedSpec.PartNo && 
                    x.PartName == parsedSpec.PartName && 
                    x.GradeName == parsedSpec.GradeName);

                if (existing != null)
                {
                    existing.ColorCode = parsedSpec.ColorCode;
                    existing.MinTargetsJson = parsedSpec.MinTargetsJson;
                    existing.MaxTargetsJson = parsedSpec.MaxTargetsJson;
                    existing.CustomerMaxTargetsJson = parsedSpec.CustomerMaxTargetsJson;
                    existing.CustomerMinTargetsJson = parsedSpec.CustomerMinTargetsJson;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                    _unitOfWork.PartSpecifications.Update(existing);
                }
                else
                {
                    parsedSpec.CreatedAtUtc = DateTime.UtcNow;
                    await _unitOfWork.PartSpecifications.AddAsync(parsedSpec, ct);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
            SyncPermanentBackup();
        }, ct);
    }

    public async Task<byte[]> ExportExcelAsync(CancellationToken ct = default)
    {
        var specs = await _unitOfWork.PartSpecifications.ListAsync(ct);
        specs = specs.OrderBy(x => x.PartName).ThenBy(x => x.PartNo).ToList();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("MasterSpecs");

        ws.Cell(1, 1).Value = "SL NO";
        ws.Cell(1, 2).Value = "PART NAME";
        ws.Cell(1, 3).Value = "PART NO";
        ws.Cell(1, 4).Value = "GRADE NAME";
        ws.Cell(1, 5).Value = "COLOUR CODE";
        
        var elements = new[] { "Si", "Fe", "Cu", "Mn", "Mg", "Zn", "Ti", "Sr", "Pb", "Sn", "Ni", "Cr" };
        int col = 6;
        foreach (var el in elements)
        {
            ws.Cell(1, col++).Value = $"{el} Cus Min";
            ws.Cell(1, col++).Value = $"{el} Cus Max";
            ws.Cell(1, col++).Value = $"{el} Our Min";
            ws.Cell(1, col++).Value = $"{el} Our Max";
        }

        ws.Range(1, 1, 1, col - 1).Style.Font.Bold = true;
        ws.Range(1, 1, 1, col - 1).Style.Fill.BackgroundColor = XLColor.LightGray;

        int row = 2;
        int slNo = 1;

        foreach (var spec in specs)
        {
            var minTargets = string.IsNullOrEmpty(spec.MinTargetsJson) ? new Dictionary<string, double>() : JsonSerializer.Deserialize<Dictionary<string, double>>(spec.MinTargetsJson);
            var maxTargets = string.IsNullOrEmpty(spec.MaxTargetsJson) ? new Dictionary<string, double>() : JsonSerializer.Deserialize<Dictionary<string, double>>(spec.MaxTargetsJson);
            var custMaxTargets = string.IsNullOrEmpty(spec.CustomerMaxTargetsJson) ? new Dictionary<string, double>() : JsonSerializer.Deserialize<Dictionary<string, double>>(spec.CustomerMaxTargetsJson);
            var custMinTargets = string.IsNullOrEmpty(spec.CustomerMinTargetsJson) ? new Dictionary<string, double>() : JsonSerializer.Deserialize<Dictionary<string, double>>(spec.CustomerMinTargetsJson);

            ws.Cell(row, 1).Value = slNo++;
            ws.Cell(row, 2).Value = spec.PartName;
            ws.Cell(row, 3).Value = spec.PartNo;
            ws.Cell(row, 4).Value = spec.GradeName;
            ws.Cell(row, 5).Value = spec.ColorCode ?? "";

            int startCol = 6;
            foreach (var el in elements)
            {
                if (custMinTargets!.TryGetValue(el, out var cMin)) ws.Cell(row, startCol).Value = cMin;
                if (custMaxTargets!.TryGetValue(el, out var cMax)) ws.Cell(row, startCol + 1).Value = cMax;
                if (minTargets!.TryGetValue(el, out var oMin)) ws.Cell(row, startCol + 2).Value = oMin;
                if (maxTargets!.TryGetValue(el, out var oMax)) ws.Cell(row, startCol + 3).Value = oMax;
                startCol += 4;
            }
            row++;
        }

        ws.Columns().AdjustToContents();

        var colorCodes = specs
            .Select(s => s.ColorCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        if (!colorCodes.Contains("GREEN & RED"))
        {
            colorCodes.Add("GREEN & RED");
        }
        
        colorCodes = colorCodes.OrderBy(c => c).ToList();

        if (colorCodes.Any())
        {
            var joinedColors = string.Join(",", colorCodes.Select(c => c!.Replace(",", "")));
            if (joinedColors.Length <= 255)
            {
                var validation = ws.Range("E2:E1048576").CreateDataValidation();
                validation.AllowedValues = XLAllowedValues.List;
                validation.List($"\"{joinedColors}\"");
            }
            else
            {
                var colorsSheet = workbook.Worksheets.Add("Colors");
                colorsSheet.Hide();
                for (int i = 0; i < colorCodes.Count; i++)
                {
                    colorsSheet.Cell(i + 1, 1).Value = colorCodes[i];
                }
                var validation = ws.Range("E2:E1048576").CreateDataValidation();
                validation.AllowedValues = XLAllowedValues.List;
                validation.List($"=Colors!$A$1:$A${colorCodes.Count}");
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("MasterSpecs");

        ws.Cell(1, 1).Value = "SL NO";
        ws.Cell(1, 2).Value = "PART NAME";
        ws.Cell(1, 3).Value = "PART NO";
        ws.Cell(1, 4).Value = "GRADE NAME";
        ws.Cell(1, 5).Value = "COLOUR CODE";
        
        var elements = new[] { "Si", "Fe", "Cu", "Mn", "Mg", "Zn", "Ti", "Sr", "Pb", "Sn", "Ni", "Cr" };
        int col = 6;
        foreach (var el in elements)
        {
            ws.Cell(1, col++).Value = $"{el} Cus Min";
            ws.Cell(1, col++).Value = $"{el} Cus Max";
            ws.Cell(1, col++).Value = $"{el} Our Min";
            ws.Cell(1, col++).Value = $"{el} Our Max";
        }

        ws.Range(1, 1, 1, col - 1).Style.Font.Bold = true;
        ws.Range(1, 1, 1, col - 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Columns().AdjustToContents();

        var specs = await _unitOfWork.PartSpecifications.ListAsync(ct);
        var colorCodes = specs
            .Select(s => s.ColorCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        if (!colorCodes.Contains("GREEN & RED"))
        {
            colorCodes.Add("GREEN & RED");
        }
        
        colorCodes = colorCodes.OrderBy(c => c).ToList();

        if (colorCodes.Any())
        {
            var joinedColors = string.Join(",", colorCodes.Select(c => c!.Replace(",", "")));
            if (joinedColors.Length <= 255)
            {
                var validation = ws.Range("E2:E1048576").CreateDataValidation();
                validation.AllowedValues = XLAllowedValues.List;
                validation.List($"\"{joinedColors}\"");
            }
            else
            {
                var colorsSheet = workbook.Worksheets.Add("Colors");
                colorsSheet.Hide();
                for (int i = 0; i < colorCodes.Count; i++)
                {
                    colorsSheet.Cell(i + 1, 1).Value = colorCodes[i];
                }
                var validation = ws.Range("E2:E1048576").CreateDataValidation();
                validation.AllowedValues = XLAllowedValues.List;
                validation.List($"=Colors!$A$1:$A${colorCodes.Count}");
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SyncPermanentBackup()
    {
        try
        {
            var permanentDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AMAC_FurnaceMonitor",
                "Data");
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AMAC_FurnaceMonitor",
                "Backups");
            var permanentDb = Path.Combine(permanentDir, "furnaces.db");
            var localDb = Path.Combine(AppContext.BaseDirectory, "Data", "furnaces.db");

            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            // Determine active database source
            var sourceDb = File.Exists(permanentDb) ? permanentDb : (File.Exists(localDb) ? localDb : null);
            if (sourceDb == null) return;

            // 1. Keep both permanent and local mirrored
            if (sourceDb == permanentDb)
            {
                var localDir = Path.GetDirectoryName(localDb);
                if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir)) Directory.CreateDirectory(localDir);
                File.Copy(permanentDb, localDb, overwrite: true);
            }
            else
            {
                if (!Directory.Exists(permanentDir)) Directory.CreateDirectory(permanentDir);
                File.Copy(localDb, permanentDb, overwrite: true);
            }

            // 2. Snapshot to rolling Backups directory
            var snapshotPath = Path.Combine(backupDir, $"furnaces_spec_changed_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
            File.Copy(sourceDb, snapshotPath, overwrite: true);

            // Keep latest 15 specification backups
            var oldBackups = new DirectoryInfo(backupDir).GetFiles("furnaces_spec_changed_*.db")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(15);
            foreach (var f in oldBackups)
            {
                try { f.Delete(); } catch { }
            }
        }
        catch
        {
            // Non-fatal background sync
        }
    }

    private static PartSpecificationDto MapToDto(PartSpecification spec) => new()
    {
        Id = spec.Id,
        PartName = spec.PartName,
        PartNo = spec.PartNo,
        GradeName = spec.GradeName,
        ColorCode = spec.ColorCode,
        MinTargetsJson = spec.MinTargetsJson,
        MaxTargetsJson = spec.MaxTargetsJson,
        CustomerMaxTargetsJson = spec.CustomerMaxTargetsJson,
        CustomerMinTargetsJson = spec.CustomerMinTargetsJson
    };
}
