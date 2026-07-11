using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwingSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing users default to enabled, matching the entity default —
            // alerts are opt-out, not opt-in
            migrationBuilder.AddColumn<bool>(
                name: "AlertsEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AlertStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertStates_Key",
                table: "AlertStates",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertStates");

            migrationBuilder.DropColumn(
                name: "AlertsEnabled",
                table: "Users");
        }
    }
}
