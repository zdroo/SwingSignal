using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegimeDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSectorRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorRotationRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Odds3M = table.Column<double>(type: "float", nullable: true),
                    Edge3M = table.Column<double>(type: "float", nullable: true),
                    Stance = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RelStrength3M = table.Column<double>(type: "float", nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorRotationRows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectorRotationRows_Symbol",
                table: "SectorRotationRows",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorRotationRows");
        }
    }
}
