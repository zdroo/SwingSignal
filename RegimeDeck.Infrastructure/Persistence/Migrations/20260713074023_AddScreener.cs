using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegimeDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreener : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScreenerRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MarketType = table.Column<int>(type: "int", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    Odds3M = table.Column<double>(type: "float", nullable: true),
                    BaseRate3M = table.Column<double>(type: "float", nullable: true),
                    Edge3M = table.Column<double>(type: "float", nullable: true),
                    Stance = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Strength = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenerRows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenerRows_Symbol",
                table: "ScreenerRows",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScreenerRows");
        }
    }
}
