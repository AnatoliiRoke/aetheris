using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Processes.Data.Migrations
{
    /// <summary>
    /// The terraforming leaves the queues of the buildings: a ship starts it (task 00014,
    /// section 9). A database made before keeps every row: the kind of the terraforming, its
    /// recipes and its parameters are archived, not deleted, and the rest of the processes is
    /// not touched.
    /// </summary>
    /// <remarks>
    /// An order of the terraforming still in a queue has no handler any more, so it leaves the
    /// queue the way a cancelled order does; the kind never refunded its input, so the device is
    /// lost like on a cancel. The orders behind it move up.
    /// </remarks>
    public partial class TerraformingByShip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE process_param SET is_archived = TRUE, version = version + 1
                WHERE is_archived = FALSE
                  AND process_type_id IN (SELECT id FROM process_type WHERE handler_code = 'terraforming');

                UPDATE process_recipe SET is_archived = TRUE, version = version + 1
                WHERE is_archived = FALSE
                  AND process_type_id IN (SELECT id FROM process_type WHERE handler_code = 'terraforming');

                UPDATE process_type SET is_archived = TRUE, version = version + 1
                WHERE is_archived = FALSE AND handler_code = 'terraforming';

                DELETE FROM process_order
                WHERE process_recipe_id IN (
                    SELECT recipe.id FROM process_recipe recipe
                    JOIN process_type kind ON kind.id = recipe.process_type_id
                    WHERE kind.handler_code = 'terraforming');

                UPDATE process_order AS item SET position = ordered.position
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY planet_building_id ORDER BY position, id) AS position
                    FROM process_order) AS ordered
                WHERE item.id = ordered.id AND item.position <> ordered.position;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The archive is a decision of the data: the admin panel returns a row from it, so
            // going back changes nothing, and a deleted order does not come back.
        }
    }
}
