using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <summary>
    /// The review of task 00013: a cost row of a batch remembers whether the parts it took
    /// carried the mark of a race, so cancelling gives back the same stack; and only the new
    /// parts are one stack by their kind, so a damaged part is always a row of its own.
    /// </summary>
    public partial class ProductionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_part_stock_planet_id_part_type_id_mark_level_has_race_bonus~",
                table: "part_stock");

            migrationBuilder.AddColumn<bool>(
                name: "has_race_bonus",
                table: "production_order_cost",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_part_stock_planet_id_part_type_id_mark_level_has_race_bonus~",
                table: "part_stock",
                columns: new[] { "planet_id", "part_type_id", "mark_level", "has_race_bonus", "condition_percent" },
                unique: true,
                filter: "condition_percent = 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_part_stock_planet_id_part_type_id_mark_level_has_race_bonus~",
                table: "part_stock");

            migrationBuilder.DropColumn(
                name: "has_race_bonus",
                table: "production_order_cost");

            migrationBuilder.CreateIndex(
                name: "ix_part_stock_planet_id_part_type_id_mark_level_has_race_bonus~",
                table: "part_stock",
                columns: new[] { "planet_id", "part_type_id", "mark_level", "has_race_bonus", "condition_percent" },
                unique: true);
        }
    }
}
