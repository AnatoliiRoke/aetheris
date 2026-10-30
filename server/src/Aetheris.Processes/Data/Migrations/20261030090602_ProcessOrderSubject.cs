using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Processes.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcessOrderSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "condition_percent",
                table: "process_order_cost",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_race_bonus",
                table: "process_order_cost",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "mark_level",
                table: "process_order_cost",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "part_condition_percent",
                table: "process_order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "part_type_id",
                table: "process_order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_resource_type_id",
                table: "process_order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_resource_type_id",
                table: "process_order",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "unit_seconds",
                table: "process_order",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_process_order_unit_seconds",
                table: "process_order",
                sql: "unit_seconds >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_process_order_unit_seconds",
                table: "process_order");

            migrationBuilder.DropColumn(
                name: "condition_percent",
                table: "process_order_cost");

            migrationBuilder.DropColumn(
                name: "has_race_bonus",
                table: "process_order_cost");

            migrationBuilder.DropColumn(
                name: "mark_level",
                table: "process_order_cost");

            migrationBuilder.DropColumn(
                name: "part_condition_percent",
                table: "process_order");

            migrationBuilder.DropColumn(
                name: "part_type_id",
                table: "process_order");

            migrationBuilder.DropColumn(
                name: "source_resource_type_id",
                table: "process_order");

            migrationBuilder.DropColumn(
                name: "target_resource_type_id",
                table: "process_order");

            migrationBuilder.DropColumn(
                name: "unit_seconds",
                table: "process_order");
        }
    }
}
