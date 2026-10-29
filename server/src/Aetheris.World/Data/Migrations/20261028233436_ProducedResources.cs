using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Adds the group of the produced goods and the five resources made from raw materials
    /// (task 00013, decision 2.12).
    /// </summary>
    /// <remarks>
    /// The rows are data, so the migration adds them: a fresh database and an existing one get
    /// the same group and the same resources. The identifiers of the rows that are already
    /// there do not change, and the texts are added without touching the ones the administrator
    /// has edited.
    /// </remarks>
    public partial class ProducedResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO resource_group (code, name_key, description_key, is_archived, version, allowed_on_planet)
                SELECT 'resource_group_6',
                       'world.resource_group.resource_group_6.name',
                       'world.resource_group.resource_group_6.description',
                       false, 1, true
                WHERE NOT EXISTS (SELECT 1 FROM resource_group WHERE code = 'resource_group_6');
                """);

            migrationBuilder.Sql("""
                INSERT INTO resource_type (code, resource_group_id, name_key, description_key, is_archived, version)
                SELECT v.code,
                       g.id,
                       'world.resource.' || v.code || '.name',
                       'world.resource.' || v.code || '.description',
                       false, 1
                FROM (VALUES ('resource_13'), ('resource_14'), ('resource_15'), ('resource_16'), ('resource_17')) AS v(code)
                CROSS JOIN resource_group g
                WHERE g.code = 'resource_group_6'
                  AND NOT EXISTS (SELECT 1 FROM resource_type WHERE code = v.code);
                """);

            migrationBuilder.Sql(NeutralCodeSql.Raise("resource_group", "resource_group"));
            migrationBuilder.Sql(NeutralCodeSql.Raise("resource_type", "resource"));

            migrationBuilder.Sql(NeutralCodeSql.Texts(
                [
                    new MigrationText("world.resource_group.resource_group_6.name", "uk", "Вироблена продукція"),
                    new MigrationText("world.resource_group.resource_group_6.name", "en", "Produced goods"),
                    new MigrationText("world.resource_group.resource_group_6.description", "uk", "Матеріали, зроблені з сировини на заводах."),
                    new MigrationText("world.resource_group.resource_group_6.description", "en", "The materials made from raw materials in the factories."),

                    new MigrationText("world.resource.resource_13.name", "uk", "Будматеріали"),
                    new MigrationText("world.resource.resource_13.name", "en", "Building materials"),
                    new MigrationText("world.resource.resource_13.description", "uk", "Плити й балки для будівель і корпусів."),
                    new MigrationText("world.resource.resource_13.description", "en", "The plates and the beams for the buildings and the hulls."),

                    new MigrationText("world.resource.resource_14.name", "uk", "Оптоволокно"),
                    new MigrationText("world.resource.resource_14.name", "en", "Optical fibre"),
                    new MigrationText("world.resource.resource_14.description", "uk", "Нитки світла для сенсорів і зв'язку."),
                    new MigrationText("world.resource.resource_14.description", "en", "The threads of light for the sensors and the communication."),

                    new MigrationText("world.resource.resource_15.name", "uk", "Мікропроцесори"),
                    new MigrationText("world.resource.resource_15.name", "en", "Microprocessors"),
                    new MigrationText("world.resource.resource_15.description", "uk", "Серце будь-якої електроніки."),
                    new MigrationText("world.resource.resource_15.description", "en", "The heart of any electronics."),

                    new MigrationText("world.resource.resource_16.name", "uk", "Композити"),
                    new MigrationText("world.resource.resource_16.name", "en", "Composites"),
                    new MigrationText("world.resource.resource_16.description", "uk", "Міцні матеріали для важких корпусів і броні."),
                    new MigrationText("world.resource.resource_16.description", "en", "The strong materials for the heavy hulls and the armour."),

                    new MigrationText("world.resource.resource_17.name", "uk", "Енергострижні"),
                    new MigrationText("world.resource.resource_17.name", "en", "Power strips"),
                    new MigrationText("world.resource.resource_17.description", "uk", "Джерела енергії для генераторів."),
                    new MigrationText("world.resource.resource_17.description", "en", "The sources of energy for the generators."),
                ],
                "SELECT key FROM localization_text WHERE key LIKE 'world.resource_group.resource_group_6.%' OR key IN ('world.resource.resource_13.name', 'world.resource.resource_13.description', 'world.resource.resource_14.name', 'world.resource.resource_14.description', 'world.resource.resource_15.name', 'world.resource.resource_15.description', 'world.resource.resource_16.name', 'world.resource.resource_16.description', 'world.resource.resource_17.name', 'world.resource.resource_17.description')"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM resource_type WHERE code IN ('resource_13', 'resource_14', 'resource_15', 'resource_16', 'resource_17');");
            migrationBuilder.Sql("DELETE FROM resource_group WHERE code = 'resource_group_6';");
        }
    }
}
