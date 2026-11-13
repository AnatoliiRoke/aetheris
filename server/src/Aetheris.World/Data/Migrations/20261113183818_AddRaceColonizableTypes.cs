using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceColonizableTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "race_colonizable_type",
                columns: table => new
                {
                    race_id = table.Column<int>(type: "integer", nullable: false),
                    object_type_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_race_colonizable_type", x => new { x.race_id, x.object_type_id });
                    table.ForeignKey(
                        name: "fk_race_colonizable_type_celestial_object_type_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "celestial_object_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_race_colonizable_type_race_race_id",
                        column: x => x.race_id,
                        principalTable: "race",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_race_colonizable_type_object_type_id",
                table: "race_colonizable_type",
                column: "object_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "race_colonizable_type");
        }
    }
}
