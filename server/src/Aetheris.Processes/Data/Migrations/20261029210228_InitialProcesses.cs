using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Processes.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "process_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    planet_building_id = table.Column<long>(type: "bigint", nullable: false),
                    process_recipe_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    produced_quantity = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_unit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finishes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_order", x => x.id);
                    table.CheckConstraint("ck_process_order_produced", "produced_quantity >= 0 AND produced_quantity <= quantity");
                    table.CheckConstraint("ck_process_order_quantity", "quantity > 0");
                });

            migrationBuilder.CreateTable(
                name: "process_population_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    crew_pending = table.Column<long>(type: "bigint", nullable: false),
                    is_over_limit = table.Column<bool>(type: "boolean", nullable: false),
                    over_limit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_population_state", x => x.id);
                    table.CheckConstraint("ck_process_population_state_crew", "crew_pending >= 0");
                });

            migrationBuilder.CreateTable(
                name: "process_queue_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    planet_building_id = table.Column<long>(type: "bigint", nullable: false),
                    extra_slots = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_queue_state", x => x.id);
                    table.CheckConstraint("ck_process_queue_state_extra_slots", "extra_slots >= 0");
                });

            migrationBuilder.CreateTable(
                name: "process_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    handler_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    refunds_input = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "process_order_cost",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    process_order_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_order_cost", x => x.id);
                    table.CheckConstraint("ck_process_order_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_process_order_cost_process_order_process_order_id",
                        column: x => x.process_order_id,
                        principalTable: "process_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "process_param",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    process_type_id = table.Column<int>(type: "integer", nullable: false),
                    param_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    level_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    level_target_id = table.Column<int>(type: "integer", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    value_max = table.Column<decimal>(type: "numeric", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_param", x => x.id);
                    table.CheckConstraint("ck_process_param_level", "level >= 1");
                    table.ForeignKey(
                        name: "fk_process_param_process_type_process_type_id",
                        column: x => x.process_type_id,
                        principalTable: "process_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_recipe",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    process_type_id = table.Column<int>(type: "integer", nullable: false),
                    building_type_id = table.Column<int>(type: "integer", nullable: true),
                    source_group_id = table.Column<int>(type: "integer", nullable: true),
                    target_group_id = table.Column<int>(type: "integer", nullable: true),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                    ship_stub = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_recipe", x => x.id);
                    table.CheckConstraint("ck_process_recipe_duration_seconds", "duration_seconds >= 0");
                    table.ForeignKey(
                        name: "fk_process_recipe_process_type_process_type_id",
                        column: x => x.process_type_id,
                        principalTable: "process_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_recipe_ingredient",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    process_recipe_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_recipe_ingredient", x => x.id);
                    table.CheckConstraint("ck_process_recipe_ingredient_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_process_recipe_ingredient_process_recipe_process_recipe_id",
                        column: x => x.process_recipe_id,
                        principalTable: "process_recipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_process_order_planet_id_planet_building_id_position",
                table: "process_order",
                columns: new[] { "planet_id", "planet_building_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_process_order_process_recipe_id",
                table: "process_order",
                column: "process_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_order_sector_id",
                table: "process_order",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_order_cost_process_order_id_part_type_id",
                table: "process_order_cost",
                columns: new[] { "process_order_id", "part_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_process_order_cost_process_order_id_resource_type_id",
                table: "process_order_cost",
                columns: new[] { "process_order_id", "resource_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_process_param_code",
                table: "process_param",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_param_process_type_id",
                table: "process_param",
                column: "process_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_param_process_type_id_param_kind_level_source_level~",
                table: "process_param",
                columns: new[] { "process_type_id", "param_kind", "level_source", "level_target_id", "level" });

            migrationBuilder.CreateIndex(
                name: "ix_process_population_state_planet_id",
                table: "process_population_state",
                column: "planet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_queue_state_planet_id_planet_building_id",
                table: "process_queue_state",
                columns: new[] { "planet_id", "planet_building_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_recipe_building_type_id",
                table: "process_recipe",
                column: "building_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_recipe_code",
                table: "process_recipe",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_recipe_process_type_id",
                table: "process_recipe",
                column: "process_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_recipe_source_group_id_target_group_id",
                table: "process_recipe",
                columns: new[] { "source_group_id", "target_group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_process_recipe_ingredient_process_recipe_id_part_type_id",
                table: "process_recipe_ingredient",
                columns: new[] { "process_recipe_id", "part_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_process_recipe_ingredient_process_recipe_id_resource_type_id",
                table: "process_recipe_ingredient",
                columns: new[] { "process_recipe_id", "resource_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_process_type_code",
                table: "process_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_type_handler_code",
                table: "process_type",
                column: "handler_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_order_cost");

            migrationBuilder.DropTable(
                name: "process_param");

            migrationBuilder.DropTable(
                name: "process_population_state");

            migrationBuilder.DropTable(
                name: "process_queue_state");

            migrationBuilder.DropTable(
                name: "process_recipe_ingredient");

            migrationBuilder.DropTable(
                name: "process_order");

            migrationBuilder.DropTable(
                name: "process_recipe");

            migrationBuilder.DropTable(
                name: "process_type");
        }
    }
}
