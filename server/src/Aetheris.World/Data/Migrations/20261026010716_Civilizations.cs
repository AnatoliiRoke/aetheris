using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class Civilizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "planet",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "planet_storage",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planet_storage", x => x.id);
                    table.CheckConstraint("ck_planet_storage_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_planet_storage_planet_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_planet_storage_resource_type_resource_type_id",
                        column: x => x.resource_type_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "race",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    preferred_object_type_id = table.Column<int>(type: "integer", nullable: true),
                    start_building_type_id = table.Column<int>(type: "integer", nullable: true),
                    start_building_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_race", x => x.id);
                    table.ForeignKey(
                        name: "fk_race_celestial_object_type_preferred_object_type_id",
                        column: x => x.preferred_object_type_id,
                        principalTable: "celestial_object_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "start_building_grant",
                columns: table => new
                {
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    building_type_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_start_building_grant", x => x.planet_id);
                    table.ForeignKey(
                        name: "fk_start_building_grant_planet_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_civilization",
                columns: table => new
                {
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    race_id = table.Column<int>(type: "integer", nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    founded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_civilization", x => x.player_id);
                    table.ForeignKey(
                        name: "fk_player_civilization_planet_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_player_civilization_race_race_id",
                        column: x => x.race_id,
                        principalTable: "race",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "race_start_resource",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    race_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_race_start_resource", x => x.id);
                    table.ForeignKey(
                        name: "fk_race_start_resource_race_race_id",
                        column: x => x.race_id,
                        principalTable: "race",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_race_start_resource_resource_type_resource_type_id",
                        column: x => x.resource_type_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_planet_storage_planet_id_resource_type_id",
                table: "planet_storage",
                columns: new[] { "planet_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planet_storage_resource_type_id",
                table: "planet_storage",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_storage_sector_id",
                table: "planet_storage",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_civilization_planet_id",
                table: "player_civilization",
                column: "planet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_civilization_race_id",
                table: "player_civilization",
                column: "race_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_civilization_sector_id",
                table: "player_civilization",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_race_code",
                table: "race",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_race_preferred_object_type_id",
                table: "race",
                column: "preferred_object_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_race_start_resource_race_id_resource_type_id",
                table: "race_start_resource",
                columns: new[] { "race_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_race_start_resource_resource_type_id",
                table: "race_start_resource",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_start_building_grant_sector_id",
                table: "start_building_grant",
                column: "sector_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planet_storage");

            migrationBuilder.DropTable(
                name: "player_civilization");

            migrationBuilder.DropTable(
                name: "race_start_resource");

            migrationBuilder.DropTable(
                name: "start_building_grant");

            migrationBuilder.DropTable(
                name: "race");

            migrationBuilder.DropColumn(
                name: "name",
                table: "planet");
        }
    }
}
