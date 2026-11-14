using Microsoft.EntityFrameworkCore.Migrations;
using Aetheris.Fleet.Data;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "power_priority",
                table: "ship_design_slot",
                type: "smallint",
                nullable: false,
                defaultValue: (short)100);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ship_design_slot_power_priority",
                table: "ship_design_slot",
                sql: "power_priority >= 0 AND power_priority <= 1000");

            // The column default covers a row written without the column. Existing slots take the
            // group default of their part. Run once, before a player reorders the slots.
            migrationBuilder.Sql(ShipDesignSlotPowerPriority.BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ship_design_slot_power_priority",
                table: "ship_design_slot");

            migrationBuilder.DropColumn(
                name: "power_priority",
                table: "ship_design_slot");
        }
    }
}
