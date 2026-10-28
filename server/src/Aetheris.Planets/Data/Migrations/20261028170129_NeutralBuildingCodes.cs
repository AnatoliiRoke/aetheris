using Aetheris.Planets.Data;
using Aetheris.World.Data;
using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Moves the building types to neutral codes and their texts into the table of texts
    /// (task 00022).
    /// </summary>
    /// <remarks>
    /// The identifiers of the types and every link to them stay as they were. The start building
    /// of the configuration becomes a flag of the type, and the storage named after the group
    /// that became «Мінерали» gets the matching name.
    /// </remarks>
    public partial class NeutralBuildingCodes : Migration
    {
        /// <summary>
        /// The keys the building table uses after the new keys are written.
        /// </summary>
        private const string UsedKeys = "SELECT name_key FROM building_type UNION ALL SELECT description_key FROM building_type";

        /// <summary>
        /// Gets the texts of the building types that the client kept in its dictionaries, by the new keys.
        /// </summary>
        public static IReadOnlyList<MigrationText> MovedTexts { get; } =
        [
            new("planets.building.building_1.name", "uk", "Адміністративний центр"),
            new("planets.building.building_1.name", "en", "Administrative center"),
            new("planets.building.building_1.description", "uk", "Головна будівля планети. Інші будівлі не можуть бути вищого рівня, ніж він."),
            new("planets.building.building_1.description", "en", "The main building of the planet. No other building can have a higher level."),
            new("planets.building.building_2.name", "uk", "Науковий центр"),
            new("planets.building.building_2.name", "en", "Science center"),
            new("planets.building.building_2.description", "uk", "Відкриває дослідження технологій."),
            new("planets.building.building_2.description", "en", "Opens the research of technologies."),
            new("planets.building.building_3.name", "uk", "Житловий комплекс"),
            new("planets.building.building_3.name", "en", "Residential complex"),
            new("planets.building.building_3.description", "uk", "Збільшує максимум населення планети."),
            new("planets.building.building_3.description", "en", "Raises the population limit of the planet."),
            new("planets.building.building_4.name", "uk", "Електростанція"),
            new("planets.building.building_4.name", "en", "Power plant"),
            new("planets.building.building_4.description", "uk", "Виробляє енергію для інших будівель."),
            new("planets.building.building_4.description", "en", "Produces energy for the other buildings."),
            new("planets.building.building_5.name", "uk", "Шахта заліза"),
            new("planets.building.building_5.name", "en", "Iron mine"),
            new("planets.building.building_5.description", "uk", "Видобуває залізо з родовища планети."),
            new("planets.building.building_5.description", "en", "Mines iron from the deposit of the planet."),
            new("planets.building.building_6.name", "uk", "Шахта міді"),
            new("planets.building.building_6.name", "en", "Copper mine"),
            new("planets.building.building_6.description", "uk", "Видобуває мідь з родовища планети."),
            new("planets.building.building_6.description", "en", "Mines copper from the deposit of the planet."),
            new("planets.building.building_7.name", "uk", "Склад металів"),
            new("planets.building.building_7.name", "en", "Metal storage"),
            new("planets.building.building_7.description", "uk", "Збільшує місткість для металів."),
            new("planets.building.building_7.description", "en", "Adds room for metals."),
            new("planets.building.building_8.name", "uk", "Склад мінералів"),
            new("planets.building.building_8.name", "en", "Mineral storage"),
            new("planets.building.building_8.description", "uk", "Збільшує місткість для мінералів."),
            new("planets.building.building_8.description", "en", "Adds room for minerals."),
            new("planets.building.building_9.name", "uk", "Паливний резервуар"),
            new("planets.building.building_9.name", "en", "Fuel tank"),
            new("planets.building.building_9.description", "uk", "Збільшує місткість для палива."),
            new("planets.building.building_9.description", "en", "Adds room for fuel."),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_start_building",
                table: "building_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            IReadOnlyDictionary<string, string> map = NeutralCodeMap.Load()[PlanetsTextKeys.CodeGroup];

            migrationBuilder.Sql(NeutralCodeSql.Rename("building_type", PlanetsTextKeys.CodeGroup, map));
            migrationBuilder.Sql(NeutralCodeSql.Keys("building_type", PlanetsTextKeys.Building));

            migrationBuilder.Sql(
                "UPDATE building_type AS t SET is_start_building = true FROM (VALUES "
                + string.Join(", ", map.Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {NeutralCodeSql.Quote(entry.Value)})"))
                + """
                ) AS m(old_code, new_code)
                WHERE t.code = m.new_code
                  AND m.old_code = coalesce((SELECT value FROM config_parameter WHERE key = 'planets.start.building_code'), 'admin_center');
                DELETE FROM config_parameter WHERE key = 'planets.start.building_code';
                """);

            migrationBuilder.Sql(NeutralCodeSql.Texts(MovedTexts, UsedKeys));
            migrationBuilder.Sql(NeutralCodeSql.Raise("building_type", PlanetsTextKeys.CodeGroup));
        }

        /// <inheritdoc />
        /// <remarks>The flag is removed; the codes and the texts stay as they are.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_start_building",
                table: "building_type");
        }
    }
}
