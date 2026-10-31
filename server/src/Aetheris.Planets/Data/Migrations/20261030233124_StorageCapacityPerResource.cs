using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <inheritdoc />
    public partial class StorageCapacityPerResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planet_storage_group");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "full_at",
                table: "planet_resource",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "full_at",
                table: "planet_resource");

            migrationBuilder.CreateTable(
                name: "planet_storage_group",
                columns: table => new
                {
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_group_id = table.Column<int>(type: "integer", nullable: false),
                    full_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sector_id = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_planet_storage_group_sector_id",
                table: "planet_storage_group",
                column: "sector_id");
        }
    }
}
