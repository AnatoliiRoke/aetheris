using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Economy.Data.Migrations
{
    /// <inheritdoc />
    public partial class CurrencyDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_civilization_balance_amount",
                table: "civilization_balance");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_civilization_balance_amount",
                table: "civilization_balance",
                sql: "amount >= 0");
        }
    }
}
