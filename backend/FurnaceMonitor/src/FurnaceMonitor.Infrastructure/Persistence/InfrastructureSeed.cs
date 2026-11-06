using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PasswordHasher = FurnaceMonitor.Infrastructure.Security.PasswordHasher;

namespace FurnaceMonitor.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and seeds the initial roles, the default admin
/// account and the F1–F25 furnace masters.
/// </summary>
public static class InfrastructureSeed
{
    /// <summary>
    /// Migrates the database to the latest schema and seeds baseline data.
    /// </summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Use EnsureCreated for a clean, fresh database built directly from the
            // current model. This avoids PendingModelChangesWarning drift between the
            // pre-existing migration snapshot and the live entity model.
            await context.Database.EnsureCreatedAsync();

            // Ensure ColorCode column exists in existing database tables (SQL Server only)
            if (context.Database.IsSqlServer())
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[FurnaceBatches]') AND name = 'ColorCode')
                        BEGIN
                            ALTER TABLE [FurnaceBatches] ADD [ColorCode] nvarchar(50) NULL;
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[FurnaceChemicals]') AND name = 'ColorCode')
                        BEGIN
                            ALTER TABLE [FurnaceChemicals] ADD [ColorCode] nvarchar(50) NULL;
                        END;
                        
                        -- Update Remarks length to max
                        ALTER TABLE [FurnaceBatches] ALTER COLUMN [Remarks] nvarchar(max) NULL;
                        ALTER TABLE [FurnaceChemicals] ALTER COLUMN [Remarks] nvarchar(max) NULL;

                        -- Ensure OperationalStatus columns exist on Furnaces
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Furnaces]') AND name = 'OperationalStatus')
                        BEGIN
                            ALTER TABLE [Furnaces] ADD [OperationalStatus] nvarchar(50) NOT NULL DEFAULT 'WaitingForBatch';
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Furnaces]') AND name = 'OperationalRemarks')
                        BEGIN
                            ALTER TABLE [Furnaces] ADD [OperationalRemarks] nvarchar(250) NULL;
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Furnaces]') AND name = 'OperationalStatusUpdatedAtUtc')
                        BEGIN
                            ALTER TABLE [Furnaces] ADD [OperationalStatusUpdatedAtUtc] datetime2 NULL;
                        END;
                    ");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not execute automatic column schema update for ColorCode/OperationalStatus.");
                }
            }

            // ---------------------------------------------------------------------------
            // SQLite: Idempotent schema migration — adds any missing columns to existing
            // databases without touching or deleting any existing data.
            // This ensures that when the application is updated and run against an
            // existing furnaces.db, all new columns are automatically added and the
            // Master Specs (PartSpecifications) and all other data survive intact.
            // Rule: "whenever an improvement happens in the application, don't need to
            // adjust DB — only update the application, it will automatically fetch the DB."
            // ---------------------------------------------------------------------------
            if (context.Database.IsSqlite())
            {
                await ApplySqliteMigrationsAsync(context, logger);
            }

            await SeedAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration / seeding failed.");
            throw;
        }
    }

    /// <summary>
    /// SQLite-specific idempotent column migration.
    /// Uses PRAGMA table_info to detect missing columns and adds them safely.
    /// Never drops or recreates tables — existing data (including Master Specs) is always preserved.
    /// </summary>
    private static async Task ApplySqliteMigrationsAsync(AppDbContext context, ILogger logger)
    {
        // Enable WAL mode (Write-Ahead Logging) and normal synchronous mode for atomic crash resilience
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not set SQLite WAL mode.");
        }

        // Helper: check if a column exists in a SQLite table
        static async Task<bool> ColumnExists(AppDbContext ctx, string table, string column)
        {
            var found = false;
            await using var conn = ctx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var colName = reader["name"]?.ToString() ?? string.Empty;
                if (string.Equals(colName, column, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            return found;
        }

        // Helper: add a column if it does not exist (SQLite ALTER TABLE ADD COLUMN)
        static async Task EnsureColumn(AppDbContext ctx, ILogger log, string table, string column, string definition)
        {
            try
            {
                if (!await ColumnExists(ctx, table, column))
                {
                    await ctx.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}");
                    log.LogInformation("SQLite migration: added column [{Table}].[{Column}].", table, column);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "SQLite migration: could not add column [{Table}].[{Column}].", table, column);
            }
        }

        // ---- FurnaceBatches ----
        await EnsureColumn(context, logger, "FurnaceBatches", "ColorCode",    "TEXT NULL");
        await EnsureColumn(context, logger, "FurnaceBatches", "Remarks",      "TEXT NULL");
        await EnsureColumn(context, logger, "FurnaceBatches", "Instruction",  "TEXT NULL");
        await EnsureColumn(context, logger, "FurnaceBatches", "ActualOperatorName", "TEXT NULL");

        // ---- FurnaceChemicals ----
        await EnsureColumn(context, logger, "FurnaceChemicals", "ColorCode",  "TEXT NULL");
        await EnsureColumn(context, logger, "FurnaceChemicals", "Remarks",    "TEXT NULL");

        // ---- Furnaces ----
        await EnsureColumn(context, logger, "Furnaces", "OperationalStatus",           "TEXT NOT NULL DEFAULT 'WaitingForBatch'");
        await EnsureColumn(context, logger, "Furnaces", "OperationalRemarks",          "TEXT NULL");
        await EnsureColumn(context, logger, "Furnaces", "OperationalStatusUpdatedAtUtc", "TEXT NULL");

        // ---- PartSpecifications ----
        // These are the Master Specs that must NEVER be lost on app update.
        await EnsureColumn(context, logger, "PartSpecifications", "ColorCode",             "TEXT NULL");
        await EnsureColumn(context, logger, "PartSpecifications", "MinTargetsJson",        "TEXT NULL");
        await EnsureColumn(context, logger, "PartSpecifications", "MaxTargetsJson",        "TEXT NULL");
        await EnsureColumn(context, logger, "PartSpecifications", "CustomerMaxTargetsJson","TEXT NULL");
        await EnsureColumn(context, logger, "PartSpecifications", "CustomerMinTargetsJson","TEXT NULL");

        logger.LogInformation("SQLite idempotent schema migration completed — existing data preserved.");
    }

    private static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        // Roles -------------------------------------------------------------
        var adminRole = await EnsureRoleAsync(context, UserRole.Admin, "Administrator");
        var operatorRole = await EnsureRoleAsync(context, UserRole.Operator, "Furnace Operator");
        var viewerRole = await EnsureRoleAsync(context, UserRole.Viewer, "Display / Read-only");

        // Default admin user ------------------------------------------------
        if (!await context.Users.AnyAsync(u => u.UserName == "Amac-u5"))
        {
            var hasher = new PasswordHasher();
            context.Users.Add(new User
            {
                UserName = "Amac-u5",
                DisplayName = "System Administrator",
                PasswordHash = hasher.Hash("Amac-u5@123"),
                RoleId = adminRole.Id,
                IsActive = true,
                IsSystem = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
            logger.LogInformation("Seeded default admin user.");
        }

        // Default operator user ---------------------------------------------
        if (!await context.Users.AnyAsync(u => u.UserName == "operator"))
        {
            var hasher = new PasswordHasher();
            context.Users.Add(new User
            {
                UserName = "operator",
                DisplayName = "Furnace Operator",
                PasswordHash = hasher.Hash("Operator@123"),
                RoleId = operatorRole.Id,
                IsActive = true,
                IsSystem = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
            logger.LogInformation("Seeded default operator user.");
        }

        // Furnaces F1–F25 ----------------------------------------------------
        for (var i = 1; i <= 25; i++)
        {
            var num = $"F{i}";
            if (!await context.Furnaces.AnyAsync(f => f.Number == num))
            {
                context.Furnaces.Add(new Furnace
                {
                    Number = num,
                    Name = $"Furnace {i}",
                    Status = FurnaceStatus.Active,
                    IsActive = true,
                    SortOrder = i,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "system"
                });
            }
        }
        logger.LogInformation("Ensured 25 furnaces (F1–F25) exist.");

        await context.SaveChangesAsync();
    }

    private static async Task<Role> EnsureRoleAsync(AppDbContext context, UserRole type, string description)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.RoleType == type);
        if (role is not null)
            return role;

        role = new Role
        {
            Name = type.ToString(),
            Description = description,
            RoleType = type,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }
}
