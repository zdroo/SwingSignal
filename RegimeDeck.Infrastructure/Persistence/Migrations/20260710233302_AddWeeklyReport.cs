using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegimeDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastWeeklyReportAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            // Existing users default to enabled, matching the entity default —
            // the report is opt-out, not opt-in
            migrationBuilder.AddColumn<bool>(
                name: "WeeklyReportEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWeeklyReportAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WeeklyReportEnabled",
                table: "Users");
        }
    }
}
