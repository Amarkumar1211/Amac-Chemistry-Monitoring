using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurnaceMonitor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRefreshTokenToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiryUtc",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "UserSessions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiryUtc",
                table: "UserSessions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiryUtc",
                table: "UserSessions");

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiryUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }
    }
}
