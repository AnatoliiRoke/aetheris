using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Economy.Data.Migrations
{
    /// <inheritdoc />
    public partial class EconomyColumnsAndFractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlanetId",
                table: "planet_population",
                newName: "planet_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "civilization_balance",
                newName: "id");

            migrationBuilder.AlterColumn<long>(
                name: "planet_id",
                table: "planet_population",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_fraction",
                table: "planet_population",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_fraction",
                table: "civilization_balance",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_planet_population_owner_player_id",
                table: "planet_population",
                column: "owner_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_planet_population_owner_player_id",
                table: "planet_population");

            migrationBuilder.DropColumn(
                name: "amount_fraction",
                table: "planet_population");

            migrationBuilder.DropColumn(
                name: "amount_fraction",
                table: "civilization_balance");

            migrationBuilder.RenameColumn(
                name: "planet_id",
                table: "planet_population",
                newName: "PlanetId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "civilization_balance",
                newName: "Id");

            migrationBuilder.AlterColumn<long>(
                name: "PlanetId",
                table: "planet_population",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
