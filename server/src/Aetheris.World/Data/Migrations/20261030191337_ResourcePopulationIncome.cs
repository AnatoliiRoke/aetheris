using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Adds the flag of the income from the population to the resources (task 00015).
    /// </summary>
    public partial class ResourcePopulationIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "earns_population_income",
                table: "resource_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Until now the population brought income to every currency of a civilization, so the
            // currencies that exist keep earning it; a currency added later gets the flag from its
            // own row. No code of a currency is named here.
            migrationBuilder.Sql("UPDATE resource_type SET earns_population_income = TRUE WHERE scope = 'civilization';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "earns_population_income",
                table: "resource_type");
        }
    }
}
