using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Research.Data.Migrations
{
    /// <summary>
    /// Levels the four minerals among the technologies (code audit of 04.11.2026, decision of the
    /// author): the technology <c>tech_21</c> swaps its two minerals, so its main mineral is
    /// quartzite and its second one is orex, as in the starting data.
    /// </summary>
    /// <remarks>
    /// The starting data of 02.11.2026 made orex the main mineral of six technologies and
    /// quartzite of four, so the units of the minerals to take every technology to the top
    /// differed by 29 per cent. A database that holds the technology gets the same swap; a fresh
    /// database gets the costs from the seed.
    /// </remarks>
    public partial class LevelMineralsOfTechnologies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Swap);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The swap is its own inverse.
            migrationBuilder.Sql(Swap);
        }

        // A level that has both minerals swaps their amounts, so the unique pair of a level and
        // a resource is never broken; a level that has one of them changes its resource.
        private const string Swap =
            """
            WITH minerals AS (
                SELECT
                    (SELECT id FROM resource_type WHERE code = 'resource_11') AS orex,
                    (SELECT id FROM resource_type WHERE code = 'resource_8') AS quartzite),
            subject_costs AS (
                SELECT cost.id, cost.technology_level_id, cost.resource_type_id, cost.amount
                FROM technology_level_cost AS cost
                JOIN technology_level AS level ON level.id = cost.technology_level_id
                JOIN technology AS subject ON subject.id = level.technology_id
                CROSS JOIN minerals
                WHERE subject.code = 'tech_21'
                  AND cost.resource_type_id IN (minerals.orex, minerals.quartzite)),
            paired AS (
                SELECT first.id, second.amount AS other_amount
                FROM subject_costs AS first
                JOIN subject_costs AS second
                    ON second.technology_level_id = first.technology_level_id AND second.id <> first.id),
            swapped_amounts AS (
                UPDATE technology_level_cost AS cost
                SET amount = paired.other_amount
                FROM paired
                WHERE cost.id = paired.id
                RETURNING cost.id)
            UPDATE technology_level_cost AS cost
            SET resource_type_id = CASE cost.resource_type_id WHEN minerals.orex THEN minerals.quartzite ELSE minerals.orex END
            FROM subject_costs, minerals
            WHERE cost.id = subject_costs.id
              AND subject_costs.id NOT IN (SELECT id FROM paired);
            """;
    }
}
