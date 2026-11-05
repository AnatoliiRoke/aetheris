using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShipDesignSlotLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_ship_design_slot_ship_design_design_id",
                table: "ship_design_slot",
                column: "design_id",
                principalTable: "ship_design",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ship_design_slot_ship_design_design_id",
                table: "ship_design_slot");
        }
    }
}
