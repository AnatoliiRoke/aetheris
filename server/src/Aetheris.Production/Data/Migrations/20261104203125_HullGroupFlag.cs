using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class HullGroupFlag : Migration
    {
        /// <summary>
        /// The codes of the groups of the starting data whose parts are hulls.
        /// </summary>
        private static readonly string[] HullGroups =
        [
            "part_group_12",
            "part_group_13",
            "part_group_14",
            "part_group_15",
            "part_group_16",
            "part_group_17",
            "part_group_18",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_hull_group",
                table: "part_group",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_part_group_is_hull_group",
                table: "part_group",
                sql: "is_hull_group IN (true, false)");

            // The groups of the hulls are the groups whose parts are hulls (task 00042). The flag
            // is data, so the groups of the starting data get it from the seed; a group that an
            // administrator changed keeps its own flag.
            migrationBuilder.Sql(
                "UPDATE part_group SET is_hull_group = true WHERE code IN ("
                + string.Join(", ", HullGroups.Select(code => NeutralCodeSql.Quote(code)))
                + ");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_part_group_is_hull_group",
                table: "part_group");

            migrationBuilder.DropColumn(
                name: "is_hull_group",
                table: "part_group");
        }
    }
}
