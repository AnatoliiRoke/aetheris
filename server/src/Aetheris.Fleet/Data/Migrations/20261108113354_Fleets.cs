using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fleets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fleet",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    home_planet_id = table.Column<long>(type: "bigint", nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    state_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fleet", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fleet_player_state",
                columns: table => new
                {
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fleet_player_state", x => x.player_id);
                });

            migrationBuilder.CreateTable(
                name: "fleet_ship",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fleet_id = table.Column<long>(type: "bigint", nullable: false),
                    ship_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fleet_ship", x => x.id);
                    table.CheckConstraint("ck_fleet_ship_position", "position >= 1");
                    table.ForeignKey(
                        name: "fk_fleet_ship_fleet_fleet_id",
                        column: x => x.fleet_id,
                        principalTable: "fleet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fleet_ship_ship_ship_id",
                        column: x => x.ship_id,
                        principalTable: "ship",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fleet_home_planet_id",
                table: "fleet",
                column: "home_planet_id");

            migrationBuilder.CreateIndex(
                name: "ix_fleet_player_id",
                table: "fleet",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_fleet_player_id_planet_id",
                table: "fleet",
                columns: new[] { "player_id", "planet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fleet_ship_fleet_id_position",
                table: "fleet_ship",
                columns: new[] { "fleet_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fleet_ship_ship_id",
                table: "fleet_ship",
                column: "ship_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fleet_player_state");

            migrationBuilder.DropTable(
                name: "fleet_ship");

            migrationBuilder.DropTable(
                name: "fleet");
        }
    }
}
