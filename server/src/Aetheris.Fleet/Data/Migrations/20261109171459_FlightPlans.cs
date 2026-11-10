using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class FlightPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flight_plan",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    fleet_id = table.Column<long>(type: "bigint", nullable: false),
                    state_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flight_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_flight_plan_fleet_fleet_id",
                        column: x => x.fleet_id,
                        principalTable: "fleet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flight_plan_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    flight_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    type_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_sector_id = table.Column<int>(type: "integer", nullable: false),
                    target_system_id = table.Column<long>(type: "bigint", nullable: false),
                    target_object_id = table.Column<long>(type: "bigint", nullable: false),
                    target_x = table.Column<decimal>(type: "numeric", nullable: false),
                    target_y = table.Column<decimal>(type: "numeric", nullable: false),
                    target_z = table.Column<decimal>(type: "numeric", nullable: false),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                    parameters = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flight_plan_order", x => x.id);
                    table.CheckConstraint("ck_flight_plan_order_duration", "duration_seconds >= 0");
                    table.CheckConstraint("ck_flight_plan_order_position", "position >= 1");
                    table.ForeignKey(
                        name: "fk_flight_plan_order_flight_plan_flight_plan_id",
                        column: x => x.flight_plan_id,
                        principalTable: "flight_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flight_plan_fleet_id",
                table: "flight_plan",
                column: "fleet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_flight_plan_player_id",
                table: "flight_plan",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_flight_plan_order_flight_plan_id",
                table: "flight_plan_order",
                column: "flight_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_flight_plan_order_flight_plan_id_position",
                table: "flight_plan_order",
                columns: new[] { "flight_plan_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flight_plan_order");

            migrationBuilder.DropTable(
                name: "flight_plan");
        }
    }
}
