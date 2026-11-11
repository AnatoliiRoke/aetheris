using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <summary>
    /// The flights of the fleets (task 00053): the position of a fleet, the flight that is under way
    /// and the progress of a plan through its orders.
    /// </summary>
    public partial class FleetFlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_position",
                table: "flight_plan",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "pause_code",
                table: "flight_plan",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "object_id",
                table: "fleet",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orbit",
                table: "fleet",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "position_x",
                table: "fleet",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "position_y",
                table: "fleet",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "position_z",
                table: "fleet",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "system_id",
                table: "fleet",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fleet_flight",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fleet_id = table.Column<long>(type: "bigint", nullable: false),
                    flight_plan_order_id = table.Column<long>(type: "bigint", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    arrives_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    distance = table.Column<double>(type: "double precision", nullable: false),
                    speed = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    from_sector_id = table.Column<int>(type: "integer", nullable: false),
                    from_system_id = table.Column<long>(type: "bigint", nullable: true),
                    from_orbit = table.Column<int>(type: "integer", nullable: true),
                    from_x = table.Column<double>(type: "double precision", nullable: false),
                    from_y = table.Column<double>(type: "double precision", nullable: false),
                    from_z = table.Column<double>(type: "double precision", nullable: false),
                    to_sector_id = table.Column<int>(type: "integer", nullable: false),
                    to_system_id = table.Column<long>(type: "bigint", nullable: true),
                    to_object_id = table.Column<long>(type: "bigint", nullable: true),
                    to_planet_id = table.Column<long>(type: "bigint", nullable: true),
                    to_orbit = table.Column<int>(type: "integer", nullable: true),
                    to_x = table.Column<double>(type: "double precision", nullable: false),
                    to_y = table.Column<double>(type: "double precision", nullable: false),
                    to_z = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fleet_flight", x => x.id);
                    table.CheckConstraint("ck_fleet_flight_distance", "distance >= 0");
                    table.CheckConstraint("ck_fleet_flight_time", "arrives_at >= started_at");
                    table.ForeignKey(
                        name: "fk_fleet_flight_fleet_fleet_id",
                        column: x => x.fleet_id,
                        principalTable: "fleet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fleet_object_id",
                table: "fleet",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ix_fleet_flight_fleet_id",
                table: "fleet_flight",
                column: "fleet_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fleet_flight");

            migrationBuilder.DropIndex(
                name: "ix_fleet_object_id",
                table: "fleet");

            migrationBuilder.DropColumn(
                name: "current_position",
                table: "flight_plan");

            migrationBuilder.DropColumn(
                name: "pause_code",
                table: "flight_plan");

            migrationBuilder.DropColumn(
                name: "object_id",
                table: "fleet");

            migrationBuilder.DropColumn(
                name: "orbit",
                table: "fleet");

            migrationBuilder.DropColumn(
                name: "position_x",
                table: "fleet");

            migrationBuilder.DropColumn(
                name: "position_y",
                table: "fleet");

            migrationBuilder.DropColumn(
                name: "position_z",
                table: "fleet");

            migrationBuilder.DropColumn(
                name: "system_id",
                table: "fleet");
        }
    }
}
