using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Research.Data.Migrations
{
    /// <summary>
    /// Gives «Industrial automation» its own multiplier of the production time next to the
    /// multiplier of the construction time (task 00013, decision 14).
    /// </summary>
    /// <remarks>
    /// The effect is data, so the migration adds it to a database that already has the starting
    /// data, with the values of the construction time of the same levels, like the seed does. A
    /// level that already has the effect keeps it, and a description the administrator has edited
    /// keeps its text.
    /// </remarks>
    public partial class ProductionTimeEffect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO technology_level_effect (technology_level_id, effect_type, value)
                SELECT e.technology_level_id, 'production_time', e.value
                FROM technology_level_effect e
                JOIN technology_level l ON l.id = e.technology_level_id
                JOIN technology t ON t.id = l.technology_id
                WHERE t.code = 'tech_5'
                  AND e.effect_type = 'construction_time'
                  AND NOT EXISTS (
                    SELECT 1 FROM technology_level_effect p
                    WHERE p.technology_level_id = e.technology_level_id AND p.effect_type = 'production_time');
                """);

            migrationBuilder.Sql("""
                UPDATE localization_text
                SET text = CASE language WHEN 'uk' THEN 'Прискорює будівництво й виробництво.' ELSE 'Speeds up the construction and the production.' END,
                    version = version + 1,
                    updated_at = now()
                WHERE key = 'research.technology.tech_5.description'
                  AND ((language = 'uk' AND text = 'Будівлі будуються швидше.')
                    OR (language = 'en' AND text = 'The buildings are built faster.'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM technology_level_effect e
                USING technology_level l, technology t
                WHERE l.id = e.technology_level_id
                  AND t.id = l.technology_id
                  AND t.code = 'tech_5'
                  AND e.effect_type = 'production_time';
                """);
        }
    }
}
