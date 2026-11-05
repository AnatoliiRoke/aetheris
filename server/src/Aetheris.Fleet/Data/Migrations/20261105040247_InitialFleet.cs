using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFleet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ship_design",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    hull_part_type_id = table.Column<int>(type: "integer", nullable: false),
                    has_race_bonus_slot = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship_design", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ship_design_slot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    design_id = table.Column<long>(type: "bigint", nullable: false),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    mark_level = table.Column<int>(type: "integer", nullable: false),
                    has_race_bonus = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship_design_slot", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ship_design_created_at",
                table: "ship_design",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ship_design_owner_player_id",
                table: "ship_design",
                column: "owner_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_ship_design_owner_player_id_is_archived",
                table: "ship_design",
                columns: new[] { "owner_player_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_ship_design_planet_id",
                table: "ship_design",
                column: "planet_id");

            migrationBuilder.CreateIndex(
                name: "ix_ship_design_slot_design_id",
                table: "ship_design_slot",
                column: "design_id");

            migrationBuilder.CreateIndex(
                name: "ix_ship_design_slot_design_id_slot_index",
                table: "ship_design_slot",
                columns: new[] { "design_id", "slot_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ship_design");

            migrationBuilder.DropTable(
                name: "ship_design_slot");
        }
    }
}
