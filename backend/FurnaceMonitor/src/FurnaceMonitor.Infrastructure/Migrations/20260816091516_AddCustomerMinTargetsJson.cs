using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurnaceMonitor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMinTargetsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerMinTargetsJson",
                table: "PartSpecifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerMinTargetsJson",
                table: "PartSpecifications");
        }
    }
}
