using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// A nebula has no mass (task 00053): it is an endless source, so the columns of the mass go.
    /// </summary>
    public partial class NebulaNoMass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_nebula_mass",
                table: "nebula");

            migrationBuilder.DropColumn(
                name: "initial_mass",
                table: "nebula");

            migrationBuilder.DropColumn(
                name: "mass",
                table: "nebula");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "initial_mass",
                table: "nebula",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "mass",
                table: "nebula",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_nebula_mass",
                table: "nebula",
                sql: "mass >= 0");
        }
    }
}
