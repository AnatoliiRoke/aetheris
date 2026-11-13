using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartFuel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_fuel",
                columns: table => new
                {
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    share = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_fuel", x => new { x.part_type_id, x.resource_type_id });
                    table.CheckConstraint("ck_part_fuel_share", "share > 0 AND share <= 1");
                    table.ForeignKey(
                        name: "fk_part_fuel_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_part_fuel_resource_type_id",
                table: "part_fuel",
                column: "resource_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_fuel");
        }
    }
}
