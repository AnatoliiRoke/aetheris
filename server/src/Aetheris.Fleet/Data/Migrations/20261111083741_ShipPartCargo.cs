using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShipPartCargo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ship_part_cargo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ship_id = table.Column<long>(type: "bigint", nullable: false),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    mark_level = table.Column<int>(type: "integer", nullable: false),
                    has_race_bonus = table.Column<bool>(type: "boolean", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ship_part_cargo", x => x.id);
                    table.CheckConstraint("ck_ship_part_cargo_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_ship_part_cargo_ship_ship_id",
                        column: x => x.ship_id,
                        principalTable: "ship",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ship_part_cargo_ship_id",
                table: "ship_part_cargo",
                column: "ship_id");

            migrationBuilder.CreateIndex(
                name: "ix_ship_part_cargo_ship_id_part_type_id_mark_level_has_race_bo~",
                table: "ship_part_cargo",
                columns: new[] { "ship_id", "part_type_id", "mark_level", "has_race_bonus" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ship_part_cargo");
        }
    }
}
