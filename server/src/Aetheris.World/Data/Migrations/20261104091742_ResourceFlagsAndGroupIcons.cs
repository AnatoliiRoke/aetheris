using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Gives the reference data the flags the code used to find by codes (code audit of
    /// 04.11.2026): the resource paid by the inhabitants of a body, the groups that hold balances
    /// and not a stock, and the shape and the tone of the icon of every group.
    /// </summary>
    /// <remarks>
    /// A database that already holds the starting data gets the values the code had for its
    /// codes, so nothing changes on the screens; a fresh database gets them from the seed. A value
    /// is written only where none is set, so a choice of the administrator stays.
    /// </remarks>
    public partial class ResourceFlagsAndGroupIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "paid_by_population",
                table: "resource_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "icon_shape",
                table: "resource_group",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon_tone",
                table: "resource_group",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_balance",
                table: "resource_group",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The population of the starting data is paid by the inhabitants of a body.
            migrationBuilder.Sql(
                """
                UPDATE resource_type
                SET paid_by_population = TRUE
                WHERE code = 'resource_30'
                  AND NOT EXISTS (SELECT 1 FROM resource_type WHERE paid_by_population);
                """);

            // The balances of the civilization of the starting data are not a stock of the storage.
            migrationBuilder.Sql(
                """
                UPDATE resource_group
                SET is_balance = TRUE
                WHERE code = 'resource_group_6'
                  AND NOT EXISTS (SELECT 1 FROM resource_group WHERE is_balance);
                """);

            // The icons the client drew for the groups of the starting data.
            migrationBuilder.Sql(
                """
                UPDATE resource_group AS target
                SET icon_shape = icons.shape, icon_tone = icons.tone
                FROM (VALUES
                    ('resource_group_1', 'square', 'metal'),
                    ('resource_group_2', 'gem', 'gem'),
                    ('resource_group_3', 'drop', 'flame'),
                    ('resource_group_4', 'spark', 'gem'),
                    ('resource_group_5', 'diamond', 'made'),
                    ('resource_group_6', 'hexagon', 'credits')) AS icons (code, shape, tone)
                WHERE target.code = icons.code
                  AND target.icon_shape IS NULL
                  AND target.icon_tone IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "paid_by_population",
                table: "resource_type");

            migrationBuilder.DropColumn(
                name: "icon_shape",
                table: "resource_group");

            migrationBuilder.DropColumn(
                name: "icon_tone",
                table: "resource_group");

            migrationBuilder.DropColumn(
                name: "is_balance",
                table: "resource_group");
        }
    }
}
