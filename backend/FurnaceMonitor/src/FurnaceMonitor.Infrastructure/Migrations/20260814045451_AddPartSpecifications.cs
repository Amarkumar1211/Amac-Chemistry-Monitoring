using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurnaceMonitor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GradeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ColorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinTargetsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaxTargetsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomerMaxTargetsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartSpecifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartSpecifications_PartNo_PartName_GradeName",
                table: "PartSpecifications",
                columns: new[] { "PartNo", "PartName", "GradeName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartSpecifications");
        }
    }
}
