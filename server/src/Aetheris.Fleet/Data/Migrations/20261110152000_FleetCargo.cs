using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <summary>
    /// The cargo of the ships and of the fleets (task 00050).
    /// </summary>
    public partial class FleetCargo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ship_cargo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ship_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship_cargo", x => x.id);
                    table.ForeignKey(
                        name: "fk_ship_cargo_ship_ship_id",
                        column: x => x.ship_id,
                        principalTable: "ship",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.CheckConstraint("ck_ship_cargo_amount", "amount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "fleet_cargo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fleet_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fleet_cargo", x => x.id);
                    table.ForeignKey(
                        name: "fk_fleet_cargo_fleet_fleet_id",
                        column: x => x.fleet_id,
                        principalTable: "fleet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.CheckConstraint("ck_fleet_cargo_amount", "amount >= 0");
                });

            migrationBuilder.CreateIndex(name: "ix_ship_cargo_ship_id", table: "ship_cargo", column: "ship_id");
            migrationBuilder.CreateIndex(name: "ix_ship_cargo_ship_id_resource_id", table: "ship_cargo", columns: new[] { "ship_id", "resource_id" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_fleet_cargo_fleet_id", table: "fleet_cargo", column: "fleet_id");
            migrationBuilder.CreateIndex(name: "ix_fleet_cargo_fleet_id_resource_id", table: "fleet_cargo", columns: new[] { "fleet_id", "resource_id" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fleet_cargo");
            migrationBuilder.DropTable(name: "ship_cargo");
        }
    }
}
