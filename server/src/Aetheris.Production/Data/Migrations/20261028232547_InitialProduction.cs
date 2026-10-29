using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_group",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "part_stock",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    mark_level = table.Column<int>(type: "integer", nullable: false),
                    has_race_bonus = table.Column<bool>(type: "boolean", nullable: false),
                    condition_percent = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_stock", x => x.id);
                    table.CheckConstraint("ck_part_stock_condition", "condition_percent >= 1 AND condition_percent <= 100");
                    table.CheckConstraint("ck_part_stock_quantity", "quantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "part_storage",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    capacity = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    full_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_storage", x => x.id);
                    table.CheckConstraint("ck_part_storage_capacity", "capacity >= 0");
                    table.CheckConstraint("ck_part_storage_quantity", "quantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "production_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    planet_building_id = table.Column<long>(type: "bigint", nullable: false),
                    recipe_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    produced_quantity = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_unit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finishes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_order", x => x.id);
                    table.CheckConstraint("ck_production_order_produced", "produced_quantity >= 0 AND produced_quantity <= quantity");
                    table.CheckConstraint("ck_production_order_quantity", "quantity > 0");
                });

            migrationBuilder.CreateTable(
                name: "production_queue_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    planet_building_id = table.Column<long>(type: "bigint", nullable: false),
                    extra_queue_slots = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_queue_state", x => x.id);
                    table.CheckConstraint("ck_production_queue_state_extra_slots", "extra_queue_slots >= 0");
                });

            migrationBuilder.CreateTable(
                name: "recipe",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    building_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    output_quantity = table.Column<long>(type: "bigint", nullable: false),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe", x => x.id);
                    table.CheckConstraint("ck_recipe_duration_seconds", "duration_seconds >= 0");
                    table.CheckConstraint("ck_recipe_output_quantity", "output_quantity >= 1");
                });

            migrationBuilder.CreateTable(
                name: "part_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    part_group_id = table.Column<int>(type: "integer", nullable: false),
                    mark_level = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_type", x => x.id);
                    table.CheckConstraint("ck_part_type_mark_level", "mark_level >= 1 AND mark_level <= 3");
                    table.ForeignKey(
                        name: "fk_part_type_part_group_part_group_id",
                        column: x => x.part_group_id,
                        principalTable: "part_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "race_part_bonus",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    race_id = table.Column<int>(type: "integer", nullable: false),
                    part_group_id = table.Column<int>(type: "integer", nullable: false),
                    multiplier = table.Column<decimal>(type: "numeric", nullable: false),
                    is_exclusive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_race_part_bonus", x => x.id);
                    table.CheckConstraint("ck_race_part_bonus_multiplier", "multiplier > 0");
                    table.ForeignKey(
                        name: "fk_race_part_bonus_part_group_part_group_id",
                        column: x => x.part_group_id,
                        principalTable: "part_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_order_cost",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    production_order_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_order_cost", x => x.id);
                    table.CheckConstraint("ck_production_order_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_production_order_cost_production_order_production_order_id",
                        column: x => x.production_order_id,
                        principalTable: "production_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredient",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipe_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_ingredient", x => x.id);
                    table.CheckConstraint("ck_recipe_ingredient_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_recipe_ingredient_recipe_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "part_attribute",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    attribute_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    number_value = table.Column<decimal>(type: "numeric", nullable: true),
                    code_value = table.Column<string>(type: "text", nullable: true),
                    text_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_attribute", x => x.id);
                    table.ForeignKey(
                        name: "fk_part_attribute_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_part_attribute_part_type_id_attribute_key",
                table: "part_attribute",
                columns: new[] { "part_type_id", "attribute_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_group_code",
                table: "part_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_stock_planet_id_part_type_id_mark_level_has_race_bonus~",
                table: "part_stock",
                columns: new[] { "planet_id", "part_type_id", "mark_level", "has_race_bonus", "condition_percent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_stock_sector_id",
                table: "part_stock",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_storage_planet_id",
                table: "part_storage",
                column: "planet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_storage_sector_id",
                table: "part_storage",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_type_code",
                table: "part_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_type_part_group_id_sort_order",
                table: "part_type",
                columns: new[] { "part_group_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_production_order_planet_id_planet_building_id_position",
                table: "production_order",
                columns: new[] { "planet_id", "planet_building_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_production_order_recipe_id",
                table: "production_order",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_order_sector_id",
                table: "production_order",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_order_cost_production_order_id_part_type_id",
                table: "production_order_cost",
                columns: new[] { "production_order_id", "part_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_production_order_cost_production_order_id_resource_type_id",
                table: "production_order_cost",
                columns: new[] { "production_order_id", "resource_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_production_queue_state_planet_id_planet_building_id",
                table: "production_queue_state",
                columns: new[] { "planet_id", "planet_building_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_race_part_bonus_part_group_id",
                table: "race_part_bonus",
                column: "part_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_race_part_bonus_race_id_part_group_id",
                table: "race_part_bonus",
                columns: new[] { "race_id", "part_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipe_building_type_id",
                table: "recipe",
                column: "building_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_code",
                table: "recipe",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipe_part_type_id",
                table: "recipe",
                column: "part_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_resource_type_id",
                table: "recipe",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredient_recipe_id_part_type_id",
                table: "recipe_ingredient",
                columns: new[] { "recipe_id", "part_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredient_recipe_id_resource_type_id",
                table: "recipe_ingredient",
                columns: new[] { "recipe_id", "resource_type_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_attribute");

            migrationBuilder.DropTable(
                name: "part_stock");

            migrationBuilder.DropTable(
                name: "part_storage");

            migrationBuilder.DropTable(
                name: "production_order_cost");

            migrationBuilder.DropTable(
                name: "production_queue_state");

            migrationBuilder.DropTable(
                name: "race_part_bonus");

            migrationBuilder.DropTable(
                name: "recipe_ingredient");

            migrationBuilder.DropTable(
                name: "part_type");

            migrationBuilder.DropTable(
                name: "production_order");

            migrationBuilder.DropTable(
                name: "recipe");

            migrationBuilder.DropTable(
                name: "part_group");
        }
    }
}
