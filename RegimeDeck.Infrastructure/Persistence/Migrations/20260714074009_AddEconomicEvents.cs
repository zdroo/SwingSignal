using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegimeDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomicEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Impact = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicEvents_Date",
                table: "EconomicEvents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicEvents_ReleaseId_Date",
                table: "EconomicEvents",
                columns: new[] { "ReleaseId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomicEvents");
        }
    }
}
