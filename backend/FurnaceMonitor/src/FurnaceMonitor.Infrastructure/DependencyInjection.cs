using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Security;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Infrastructure.Persistence;
using FurnaceMonitor.Infrastructure.Security;
using FurnaceMonitor.Infrastructure.Services;
using FurnaceMonitor.Infrastructure.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FurnaceMonitor.Infrastructure;

/// <summary>
/// Registers persistence, repositories, security and application services along
/// with the wiring that routes domain events to SignalR pushes.
/// </summary>
public static class DependencyInjection
{
    public static string ActiveDatabasePath { get; private set; } = "Data/furnaces.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=Data/furnaces.db";

        // Support both SQLite (zero installation / fully portable) and SQL Server:
        var isSqlite = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase)
            || (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) 
                && !connectionString.Contains("Server=") 
                && !connectionString.Contains("Trusted_Connection="));

        if (isSqlite)
        {
            var parts = connectionString.Split('=', 2);
            var dbRelativePath = parts.Length > 1 ? parts[1].Trim() : "Data/furnaces.db";
            var dbFullPath = Path.IsPathRooted(dbRelativePath)
                ? dbRelativePath
                : Path.Combine(AppContext.BaseDirectory, dbRelativePath);

            var dir = Path.GetDirectoryName(dbFullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // ---------------------------------------------------------------------------
            // PERMANENT SYSTEM DATABASE AUTO-PRESERVATION & AUTO-FETCH
            //
            // Goal: "when any update, reinstall or upgrade happens, start exactly where
            // the last application left off — never lose Master Specs or batch history."
            //
            // Primary storage: %ProgramData%\AMAC_FurnaceMonitor\Data\furnaces.db
            // Synchronized mirror: AppContext.BaseDirectory\Data\furnaces.db
            // Rolling backups: %ProgramData%\AMAC_FurnaceMonitor\Backups\
            // ---------------------------------------------------------------------------
            string activeDbPath = dbFullPath;
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
                var permanentDbPath = Path.Combine(permanentDir, "furnaces.db");

                if (!Directory.Exists(permanentDir)) Directory.CreateDirectory(permanentDir);
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                var localExists = File.Exists(dbFullPath);
                var permExists = File.Exists(permanentDbPath);

                if (permExists)
                {
                    // Existing permanent database detected — has all user data, Master Specs & batch history.
                    // 1. Create a safe startup snapshot backup:
                    try
                    {
                        var startupBackup = Path.Combine(backupDir, $"furnaces_startup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");
                        File.Copy(permanentDbPath, startupBackup, overwrite: true);

                        // Keep latest 10 startup backups
                        var oldStartupBackups = new DirectoryInfo(backupDir).GetFiles("furnaces_startup_*.db")
                            .OrderByDescending(f => f.CreationTimeUtc)
                            .Skip(10);
                        foreach (var f in oldStartupBackups)
                        {
                            try { f.Delete(); } catch { }
                        }
                    }
                    catch { }

                    // 2. Also mirror permanent DB to local app directory so local folder is always up to date
                    try
                    {
                        var localDir = Path.GetDirectoryName(dbFullPath);
                        if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir)) Directory.CreateDirectory(localDir);
                        File.Copy(permanentDbPath, dbFullPath, overwrite: true);
                    }
                    catch { }

                    // 3. Connect directly to the permanent database!
                    activeDbPath = permanentDbPath;
                }
                else
                {
                    // Brand new machine installation: copy the bundled template to permanent storage
                    if (localExists)
                    {
                        File.Copy(dbFullPath, permanentDbPath, overwrite: true);
                    }
                    activeDbPath = permanentDbPath;
                }
            }
            catch
            {
                // Fallback to local DB if ProgramData has unexpected environment restriction
                activeDbPath = dbFullPath;
            }

            ActiveDatabasePath = activeDbPath;

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={activeDbPath}"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(5);
                    sql.CommandTimeout(60);
                }));
        }

        // Security settings bound from appsettings.
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ISecuritySettings>(sp =>
        {
            var settings = new JwtSettings();
            configuration.GetSection("Jwt").Bind(settings);
            return settings;
        });

        // Repositories / unit-of-work.
        services.AddScoped<UnitOfWorkAccessor>(sp =>
        {
            var ctx = sp.GetRequiredService<AppDbContext>();
            return new UnitOfWorkAccessor(ctx);
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Security services.
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Application services.
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IFurnaceService, FurnaceService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<ISignalRConnectionService, SignalRConnectionService>();
        services.AddScoped<INotificationService, RealtimeNotificationService>();
        services.AddScoped<FurnaceMonitor.Application.Services.PdfExtraction.IPdfExtractionService, FurnaceMonitor.Infrastructure.Services.PdfExtraction.PdfExtractionService>();

        services.AddScoped<IPartSpecificationService, PartSpecificationService>();

        services.AddScoped<FurnaceMonitor.Application.Services.Calculation.IMasterDatabaseService, FurnaceMonitor.Infrastructure.Services.Calculation.DatabaseMasterDatabaseService>();
        services.AddScoped<FurnaceMonitor.Application.Services.Calculation.IChemicalCalculationService, FurnaceMonitor.Application.Services.Calculation.ChemicalCalculationService>();

        services.AddHostedService<PdfWatcherService>();
        services.AddHostedService<BatchTimeoutService>();

        return services;
    }

    /// <summary>
    /// Wires the batch service events to the SignalR notification service so that
    /// furnace updates and clear events are pushed to the correct furnace groups.
    /// </summary>
    public static void WireRealtimeEvents(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var batchService = scope.ServiceProvider.GetRequiredService<IBatchService>()
            as BatchService ?? throw new InvalidOperationException("BatchService is not registered.");
        var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();

        batchService.OnFurnaceUpdated += async update =>
        {
            await notification.PushFurnaceUpdateAsync(update);
        };
        batchService.OnFurnaceCleared += async cleared =>
        {
            await notification.PushFurnaceClearedAsync(cleared);
        };
    }
}
