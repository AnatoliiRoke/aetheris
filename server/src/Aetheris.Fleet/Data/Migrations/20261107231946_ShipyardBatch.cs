using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShipyardBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "produced_quantity",
                table: "shipyard_order",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "quantity",
                table: "shipyard_order",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipyard_order_produced",
                table: "shipyard_order",
                sql: "produced_quantity >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipyard_order_produced_within",
                table: "shipyard_order",
                sql: "produced_quantity <= quantity");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipyard_order_quantity",
                table: "shipyard_order",
                sql: "quantity >= 1");

            // The defaults only fill the rows that already exist. The model does not keep them.
            migrationBuilder.AlterColumn<long>(
                name: "produced_quantity",
                table: "shipyard_order",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 0L);

            migrationBuilder.AlterColumn<long>(
                name: "quantity",
                table: "shipyard_order",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shipyard_order_produced",
                table: "shipyard_order");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shipyard_order_produced_within",
                table: "shipyard_order");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shipyard_order_quantity",
                table: "shipyard_order");

            migrationBuilder.DropColumn(
                name: "produced_quantity",
                table: "shipyard_order");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "shipyard_order");
        }
    }
}
