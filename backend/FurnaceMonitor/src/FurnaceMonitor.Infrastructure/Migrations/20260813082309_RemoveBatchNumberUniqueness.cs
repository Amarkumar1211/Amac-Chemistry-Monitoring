using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurnaceMonitor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBatchNumberUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FurnaceBatches_BatchNumber",
                table: "FurnaceBatches");

            migrationBuilder.CreateIndex(
                name: "IX_FurnaceBatches_BatchNumber",
                table: "FurnaceBatches",
                column: "BatchNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FurnaceBatches_BatchNumber",
                table: "FurnaceBatches");

            migrationBuilder.CreateIndex(
                name: "IX_FurnaceBatches_BatchNumber",
                table: "FurnaceBatches",
                column: "BatchNumber",
                unique: true);
        }
    }
}
