using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Economy.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "civilization_balance",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "text", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    amount_fraction = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_civilization_balance", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planet_population",
                columns: table => new
                {
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    owner_player_id = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    amount_fraction = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planet_population", x => x.planet_id);
                    table.CheckConstraint("ck_planet_population_amount", "amount >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_civilization_balance_player_id_resource_type_id",
                table: "civilization_balance",
                columns: new[] { "player_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planet_population_owner_player_id",
                table: "planet_population",
                column: "owner_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "civilization_balance");

            migrationBuilder.DropTable(
                name: "planet_population");
        }
    }
}
