using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Economy.Data.Migrations
{
    /// <inheritdoc />
    public partial class BalanceVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "civilization_balance",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "civilization_balance");
        }
    }
}
