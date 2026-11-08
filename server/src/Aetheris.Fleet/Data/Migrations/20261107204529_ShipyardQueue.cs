using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShipyardQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ship",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    ship_design_id = table.Column<long>(type: "bigint", nullable: false),
                    crew = table.Column<long>(type: "bigint", nullable: false),
                    upkeep_per_hour = table.Column<decimal>(type: "numeric", nullable: false),
                    state_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship", x => x.id);
                    table.CheckConstraint("ck_ship_crew", "crew >= 0");
                    table.ForeignKey(
                        name: "fk_ship_ship_design_ship_design_id",
                        column: x => x.ship_design_id,
                        principalTable: "ship_design",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipyard_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    planet_building_id = table.Column<long>(type: "bigint", nullable: false),
                    ship_design_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    base_duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                    crew = table.Column<long>(type: "bigint", nullable: false),
                    upkeep_per_hour = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finishes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipyard_order", x => x.id);
                    table.CheckConstraint("ck_shipyard_order_crew", "crew >= 0");
                    table.CheckConstraint("ck_shipyard_order_duration", "base_duration_seconds >= 1");
                    table.CheckConstraint("ck_shipyard_order_position", "position >= 1");
                    table.ForeignKey(
                        name: "fk_shipyard_order_ship_design_ship_design_id",
                        column: x => x.ship_design_id,
                        principalTable: "ship_design",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipyard_queue_state",
                columns: table => new
                {
                    planet_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipyard_queue_state", x => x.planet_id);
                });

            migrationBuilder.CreateTable(
                name: "ship_cost",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ship_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship_cost", x => x.id);
                    table.CheckConstraint("ck_ship_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_ship_cost_ship_ship_id",
                        column: x => x.ship_id,
                        principalTable: "ship",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ship_module",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ship_id = table.Column<long>(type: "bigint", nullable: false),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    mark_level = table.Column<int>(type: "integer", nullable: false),
                    has_race_bonus = table.Column<bool>(type: "boolean", nullable: false),
                    condition_percent = table.Column<int>(type: "integer", nullable: false),
                    condition_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    is_hull = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship_module", x => x.id);
                    table.CheckConstraint("ck_ship_module_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_ship_module_ship_ship_id",
                        column: x => x.ship_id,
                        principalTable: "ship",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipyard_order_cost",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipyard_order_cost", x => x.id);
                    table.CheckConstraint("ck_shipyard_order_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_shipyard_order_cost_shipyard_order_order_id",
                        column: x => x.order_id,
                        principalTable: "shipyard_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipyard_order_part",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    mark_level = table.Column<int>(type: "integer", nullable: false),
                    has_race_bonus = table.Column<bool>(type: "boolean", nullable: false),
                    condition_percent = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    is_hull = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipyard_order_part", x => x.id);
                    table.CheckConstraint("ck_shipyard_order_part_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_shipyard_order_part_shipyard_order_order_id",
                        column: x => x.order_id,
                        principalTable: "shipyard_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ship_player_id",
                table: "ship",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_ship_player_id_planet_id",
                table: "ship",
                columns: new[] { "player_id", "planet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ship_ship_design_id",
                table: "ship",
                column: "ship_design_id");

            migrationBuilder.CreateIndex(
                name: "ix_ship_cost_ship_id_resource_type_id",
                table: "ship_cost",
                columns: new[] { "ship_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ship_module_ship_id",
                table: "ship_module",
                column: "ship_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipyard_order_planet_id_position",
                table: "shipyard_order",
                columns: new[] { "planet_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipyard_order_player_id",
                table: "shipyard_order",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipyard_order_ship_design_id",
                table: "shipyard_order",
                column: "ship_design_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipyard_order_cost_order_id_resource_type_id",
                table: "shipyard_order_cost",
                columns: new[] { "order_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipyard_order_part_order_id",
                table: "shipyard_order_part",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ship_cost");

            migrationBuilder.DropTable(
                name: "ship_module");

            migrationBuilder.DropTable(
                name: "shipyard_order_cost");

            migrationBuilder.DropTable(
                name: "shipyard_order_part");

            migrationBuilder.DropTable(
                name: "shipyard_queue_state");

            migrationBuilder.DropTable(
                name: "ship");

            migrationBuilder.DropTable(
                name: "shipyard_order");
        }
    }
}
