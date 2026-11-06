using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurnaceMonitor.Infrastructure.Migrations;

/// <inheritdoc />
/// <summary>
/// Initial database schema: Users, Roles, Furnaces, FurnaceBatches,
/// FurnaceChemicals, SignalRConnections, AuditLogs and UserSessions.
/// </summary>
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RoleType = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Furnaces",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Number = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Furnaces", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                IsSystem = table.Column<bool>(type: "bit", nullable: false),
                LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RoleId = table.Column<int>(type: "int", nullable: false),
                RefreshToken = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                RefreshTokenExpiryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
                table.ForeignKey("FK_Users_Roles_RoleId", x => x.RoleId, "Roles", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FurnaceBatches",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                FurnaceId = table.Column<int>(type: "int", nullable: false),
                OperatorId = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Indicator = table.Column<int>(type: "int", nullable: false),
                Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ClosedTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FurnaceBatches", x => x.Id);
                table.ForeignKey("FK_FurnaceBatches_Furnaces_FurnaceId", x => x.FurnaceId, "Furnaces", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FurnaceBatches_Users_OperatorId", x => x.OperatorId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                TokenId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", x => x.Id);
                table.ForeignKey("FK_UserSessions_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FurnaceChemicals",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                BatchId = table.Column<int>(type: "int", nullable: false),
                Si = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                Ti = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                Mg = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                FutureChemical1 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                FutureChemical2 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                FutureChemical3 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                Indicator = table.Column<int>(type: "int", nullable: false),
                Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FurnaceChemicals", x => x.Id);
                table.ForeignKey("FK_FurnaceChemicals_FurnaceBatches_BatchId", x => x.BatchId, "FurnaceBatches", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SignalRConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ConnectionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UserId = table.Column<int>(type: "int", nullable: true),
                UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                FurnaceNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                ConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                DisconnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SignalRConnections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Action = table.Column<int>(type: "int", nullable: false),
                Actor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                EntityId = table.Column<int>(type: "int", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex("IX_Furnaces_Number", "Furnaces", "Number", unique: true);
        migrationBuilder.CreateIndex("IX_Users_UserName", "Users", "UserName", unique: true);
        migrationBuilder.CreateIndex("IX_Users_RoleId", "Users", "RoleId");
        migrationBuilder.CreateIndex("IX_FurnaceBatches_BatchNumber", "FurnaceBatches", "BatchNumber", unique: true);
        migrationBuilder.CreateIndex("IX_FurnaceBatches_FurnaceId", "FurnaceBatches", "FurnaceId");
        migrationBuilder.CreateIndex("IX_FurnaceBatches_OperatorId", "FurnaceBatches", "OperatorId");
        migrationBuilder.CreateIndex("IX_FurnaceBatches_IsActive", "FurnaceBatches", new[] { "FurnaceId", "IsActive" });
        migrationBuilder.CreateIndex("IX_FurnaceChemicals_BatchId_CapturedAtUtc", "FurnaceChemicals", new[] { "BatchId", "CapturedAtUtc" });
        migrationBuilder.CreateIndex("IX_UserSessions_UserId", "UserSessions", "UserId");
        migrationBuilder.CreateIndex("IX_UserSessions_TokenId", "UserSessions", "TokenId", unique: true);
        migrationBuilder.CreateIndex("IX_SignalRConnections_ConnectionId", "SignalRConnections", "ConnectionId");
        migrationBuilder.CreateIndex("IX_SignalRConnections_FurnaceNumber", "SignalRConnections", "FurnaceNumber");
        migrationBuilder.CreateIndex("IX_AuditLogs_CreatedAtUtc", "AuditLogs", "CreatedAtUtc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropTable(name: "SignalRConnections");
        migrationBuilder.DropTable(name: "FurnaceChemicals");
        migrationBuilder.DropTable(name: "UserSessions");
        migrationBuilder.DropTable(name: "FurnaceBatches");
        migrationBuilder.DropTable(name: "Users");
        migrationBuilder.DropTable(name: "Furnaces");
        migrationBuilder.DropTable(name: "Roles");
    }
}

