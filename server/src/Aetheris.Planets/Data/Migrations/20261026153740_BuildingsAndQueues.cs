using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuildingsAndQueues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "building_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planet_state",
                columns: table => new
                {
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    owner_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    extra_queue_slots = table.Column<int>(type: "integer", nullable: false),
                    resources_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planet_state", x => x.planet_id);
                    table.CheckConstraint("ck_planet_state_extra_queue_slots", "extra_queue_slots >= 0");
                });

            migrationBuilder.CreateTable(
                name: "requirement",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subject_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_id = table.Column<int>(type: "integer", nullable: false),
                    subject_level = table.Column<int>(type: "integer", nullable: false),
                    requirement_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    target_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requirement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "building_level",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    building_type_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_level", x => x.id);
                    table.CheckConstraint("ck_building_level_duration", "duration_seconds >= 0");
                    table.ForeignKey(
                        name: "fk_building_level_building_type_building_type_id",
                        column: x => x.building_type_id,
                        principalTable: "building_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "construction_queue_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    building_type_id = table.Column<int>(type: "integer", nullable: false),
                    target_level = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finishes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_construction_queue_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_construction_queue_item_building_type_building_type_id",
                        column: x => x.building_type_id,
                        principalTable: "building_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_construction_queue_item_planet_state_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet_state",
                        principalColumn: "planet_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planet_building",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    building_type_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planet_building", x => x.id);
                    table.CheckConstraint("ck_planet_building_level", "level >= 0");
                    table.ForeignKey(
                        name: "fk_planet_building_building_type_building_type_id",
                        column: x => x.building_type_id,
                        principalTable: "building_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planet_building_planet_state_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet_state",
                        principalColumn: "planet_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planet_resource",
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
                    table.PrimaryKey("pk_planet_resource", x => x.id);
                    table.CheckConstraint("ck_planet_resource_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_planet_resource_planet_state_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet_state",
                        principalColumn: "planet_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planet_storage_group",
                columns: table => new
                {
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_group_id = table.Column<int>(type: "integer", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    full_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planet_storage_group", x => new { x.planet_id, x.resource_group_id });
                    table.ForeignKey(
                        name: "fk_planet_storage_group_planet_state_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet_state",
                        principalColumn: "planet_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "building_level_cost",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    building_level_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_level_cost", x => x.id);
                    table.CheckConstraint("ck_building_level_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_building_level_cost_building_level_building_level_id",
                        column: x => x.building_level_id,
                        principalTable: "building_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "building_level_effect",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    building_level_id = table.Column<int>(type: "integer", nullable: false),
                    effect_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    resource_group_id = table.Column<int>(type: "integer", nullable: true),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_level_effect", x => x.id);
                    table.ForeignKey(
                        name: "fk_building_level_effect_building_level_building_level_id",
                        column: x => x.building_level_id,
                        principalTable: "building_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "construction_queue_cost",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    construction_queue_item_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_construction_queue_cost", x => x.id);
                    table.CheckConstraint("ck_construction_queue_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_construction_queue_cost_construction_queue_item_constructio~",
                        column: x => x.construction_queue_item_id,
                        principalTable: "construction_queue_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_building_level_building_type_id_level",
                table: "building_level",
                columns: new[] { "building_type_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_level_cost_building_level_id_resource_type_id",
                table: "building_level_cost",
                columns: new[] { "building_level_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_level_effect_building_level_id",
                table: "building_level_effect",
                column: "building_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_building_type_code",
                table: "building_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_construction_queue_cost_construction_queue_item_id_resource~",
                table: "construction_queue_cost",
                columns: new[] { "construction_queue_item_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_construction_queue_item_building_type_id",
                table: "construction_queue_item",
                column: "building_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_construction_queue_item_planet_id_position",
                table: "construction_queue_item",
                columns: new[] { "planet_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_construction_queue_item_sector_id",
                table: "construction_queue_item",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_building_building_type_id",
                table: "planet_building",
                column: "building_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_building_planet_id_building_type_id",
                table: "planet_building",
                columns: new[] { "planet_id", "building_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planet_building_sector_id",
                table: "planet_building",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_resource_planet_id_resource_type_id",
                table: "planet_resource",
                columns: new[] { "planet_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planet_resource_sector_id",
                table: "planet_resource",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_state_owner_player_id",
                table: "planet_state",
                column: "owner_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_state_sector_id",
                table: "planet_state",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_storage_group_sector_id",
                table: "planet_storage_group",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_requirement_subject_type_subject_id",
                table: "requirement",
                columns: new[] { "subject_type", "subject_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "building_level_cost");

            migrationBuilder.DropTable(
                name: "building_level_effect");

            migrationBuilder.DropTable(
                name: "construction_queue_cost");

            migrationBuilder.DropTable(
                name: "planet_building");

            migrationBuilder.DropTable(
                name: "planet_resource");

            migrationBuilder.DropTable(
                name: "planet_storage_group");

            migrationBuilder.DropTable(
                name: "requirement");

            migrationBuilder.DropTable(
                name: "building_level");

            migrationBuilder.DropTable(
                name: "construction_queue_item");

            migrationBuilder.DropTable(
                name: "building_type");

            migrationBuilder.DropTable(
                name: "planet_state");
        }
    }
}
