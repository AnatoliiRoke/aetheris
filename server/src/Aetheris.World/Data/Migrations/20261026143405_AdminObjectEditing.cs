using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminObjectEditing : Migration
    {
        /// <summary>
        /// The deposits of the planets get the ranges of task 00009, decision 12: an ordinary
        /// planet 0.8–1.4, a rich one 1.4–1.9. The numbers are starting data like the seed,
        /// tuned later in the admin panel.
        /// </summary>
        private const string PlanetDepositRanges = """
            UPDATE generation_rule
            SET coefficient_min = 0.8, coefficient_max = 1.4, rich_coefficient_min = 1.4, rich_coefficient_max = 1.9
            WHERE rule_group_id IN (
                SELECT rule_group.id
                FROM generation_rule_group rule_group
                JOIN celestial_object_type object_type ON object_type.id = rule_group.object_type_id
                WHERE object_type.object_class = 'planet');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "star_system",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "planet",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "generation_rule_group",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "generation_rule",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rich_coefficient_max",
                table: "generation_rule",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rich_coefficient_min",
                table: "generation_rule",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "generation_rule",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "celestial_object_type",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "asteroid_field",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(PlanetDepositRanges);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "star_system");

            migrationBuilder.DropColumn(
                name: "version",
                table: "planet");

            migrationBuilder.DropColumn(
                name: "version",
                table: "generation_rule_group");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "generation_rule");

            migrationBuilder.DropColumn(
                name: "rich_coefficient_max",
                table: "generation_rule");

            migrationBuilder.DropColumn(
                name: "rich_coefficient_min",
                table: "generation_rule");

            migrationBuilder.DropColumn(
                name: "version",
                table: "generation_rule");

            migrationBuilder.DropColumn(
                name: "version",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "version",
                table: "asteroid_field");
        }
    }
}
