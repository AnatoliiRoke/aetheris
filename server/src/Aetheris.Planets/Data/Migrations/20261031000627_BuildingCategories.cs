using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Gives the building types a mandatory category (task 00020, section 3.1): the group of the
    /// tile on the building page, now a column of the data instead of a guess of the client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A database that already holds buildings gets the nine categories of the starting data
    /// (<c>group_1</c>…<c>group_9</c>) with their texts, and every building gets the category that
    /// fits it: by the neutral code of the starting buildings, then by the class of the building,
    /// and a building that fits neither (one added by hand) gets the first category; the
    /// administrator moves it in the admin panel. A fresh database gets no row here: the seed of the
    /// module writes the categories and gives every building its category.
    /// </para>
    /// </remarks>
    public partial class BuildingCategories : Migration
    {
        /// <summary>
        /// The number of the categories of the starting data: <c>group_1</c>…<c>group_9</c>, in the
        /// order of their texts below.
        /// </summary>
        private const int CategoryCount = 9;

        /// <summary>
        /// The categories of the buildings of the starting data by the neutral code of the building.
        /// </summary>
        private static readonly Dictionary<string, string> StartingBuildingCategories = new(StringComparer.Ordinal)
        {
            ["building_1"] = "group_5",
            ["building_2"] = "group_8",
            ["building_3"] = "group_5",
            ["building_4"] = "group_2",
            ["building_5"] = "group_1",
            ["building_6"] = "group_1",
            ["building_7"] = "group_4",
            ["building_8"] = "group_4",
            ["building_9"] = "group_4",
            ["building_10"] = "group_1",
            ["building_11"] = "group_1",
            ["building_12"] = "group_1",
            ["building_13"] = "group_3",
            ["building_14"] = "group_6",
            ["building_15"] = "group_7",
            ["building_16"] = "group_7",
            ["building_17"] = "group_9",
            ["building_18"] = "group_9",
            ["building_19"] = "group_7",
            ["building_20"] = "group_3",
            ["building_21"] = "group_4",
            ["building_22"] = "group_3",
            ["building_23"] = "group_6",
            ["building_24"] = "group_6",
            ["building_25"] = "group_5",
        };

        /// <summary>
        /// The categories by the class of a building, for a building the table above does not know.
        /// </summary>
        private static readonly Dictionary<string, string> ClassCategories = new(StringComparer.Ordinal)
        {
            ["mine"] = "group_1",
            ["production"] = "group_3",
            ["recycling"] = "group_3",
            ["converter"] = "group_3",
            ["shipyard"] = "group_6",
            ["module_repair"] = "group_6",
            ["orbital_workshop"] = "group_6",
            ["sensor"] = "group_7",
            ["defence"] = "group_7",
            ["research_center"] = "group_8",
            ["trade_center"] = "group_9",
            ["portal"] = "group_9",
        };

        /// <summary>
        /// Gets the texts of the categories by their keys, for the keys that have none.
        /// </summary>
        public static IReadOnlyList<MigrationText> CategoryTexts { get; } =
        [
            new("planets.building_category.group_1.name", "uk", "Видобуток"),
            new("planets.building_category.group_1.name", "en", "Extraction"),
            new("planets.building_category.group_1.description", "uk", "Шахти й видобуток ресурсів із родовищ планети."),
            new("planets.building_category.group_1.description", "en", "The mines and the extraction of the resources from the deposits of the planet."),
            new("planets.building_category.group_2.name", "uk", "Енергія"),
            new("planets.building_category.group_2.name", "en", "Energy"),
            new("planets.building_category.group_2.description", "uk", "Будівлі, що виробляють енергію для планети."),
            new("planets.building_category.group_2.description", "en", "The buildings that produce the energy of the planet."),
            new("planets.building_category.group_3.name", "uk", "Виробництво"),
            new("planets.building_category.group_3.name", "en", "Production"),
            new("planets.building_category.group_3.description", "uk", "Заводи й переробка: з сировини в продукцію."),
            new("planets.building_category.group_3.description", "en", "The factories and the processing: from raw materials to goods."),
            new("planets.building_category.group_4.name", "uk", "Склад"),
            new("planets.building_category.group_4.name", "en", "Storage"),
            new("planets.building_category.group_4.description", "uk", "Склади й резервуари, що збільшують місткість планети."),
            new("planets.building_category.group_4.description", "en", "The storages and the tanks that raise the capacity of the planet."),
            new("planets.building_category.group_5.name", "uk", "Населення"),
            new("planets.building_category.group_5.name", "en", "Population"),
            new("planets.building_category.group_5.description", "uk", "Будівлі, що впливають на населення й керування колонією."),
            new("planets.building_category.group_5.description", "en", "The buildings that affect the population and the government of the colony."),
            new("planets.building_category.group_6.name", "uk", "Флот"),
            new("planets.building_category.group_6.name", "en", "Fleet"),
            new("planets.building_category.group_6.description", "uk", "Будівлі для будівництва й ремонту кораблів."),
            new("planets.building_category.group_6.description", "en", "The buildings for the construction and the repair of the ships."),
            new("planets.building_category.group_7.name", "uk", "Оборона"),
            new("planets.building_category.group_7.name", "en", "Defence"),
            new("planets.building_category.group_7.description", "uk", "Захист планети й спостереження за небом."),
            new("planets.building_category.group_7.description", "en", "The defence of the planet and the watch of the sky."),
            new("planets.building_category.group_8.name", "uk", "Дослідження"),
            new("planets.building_category.group_8.name", "en", "Research"),
            new("planets.building_category.group_8.description", "uk", "Будівлі, у яких вивчають технології."),
            new("planets.building_category.group_8.description", "en", "The buildings where the technologies are researched."),
            new("planets.building_category.group_9.name", "uk", "Торгівля"),
            new("planets.building_category.group_9.name", "en", "Trade"),
            new("planets.building_category.group_9.description", "uk", "Торгівля, ринок і зв'язок із далекими системами."),
            new("planets.building_category.group_9.description", "en", "The trade, the market and the link with the distant systems."),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "building_category",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_category", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_building_category_code",
                table: "building_category",
                column: "code",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "building_type",
                type: "integer",
                nullable: true);

            // A database with buildings gets the categories of the starting data and gives every
            // building one; an empty table gets nothing, the seed writes the rows then.
            migrationBuilder.Sql(
                "INSERT INTO building_category (code, name_key, description_key, sort_order) SELECT "
                + $"{NeutralCodeSql.Quote(PlanetsTextKeys.CategoryCodeGroup + "_")} || o.position, "
                + $"{NeutralCodeSql.Quote(PlanetsTextKeys.Category + "." + PlanetsTextKeys.CategoryCodeGroup + "_")} || o.position || '.name', "
                + $"{NeutralCodeSql.Quote(PlanetsTextKeys.Category + "." + PlanetsTextKeys.CategoryCodeGroup + "_")} || o.position || '.description', "
                + "o.position FROM (VALUES "
                + string.Join(", ", Enumerable.Range(1, CategoryCount).Select(position => $"({position})"))
                + ") AS o(position) WHERE EXISTS (SELECT 1 FROM building_type);");

            migrationBuilder.Sql(
                "UPDATE building_type AS t SET category_id = c.id FROM (VALUES "
                + string.Join(", ", StartingBuildingCategories.Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {NeutralCodeSql.Quote(entry.Value)})"))
                + ") AS m(building_code, category_code) JOIN building_category AS c ON c.code = m.category_code WHERE t.code = m.building_code;");
            migrationBuilder.Sql(
                "UPDATE building_type AS t SET category_id = c.id FROM (VALUES "
                + string.Join(", ", ClassCategories.Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {NeutralCodeSql.Quote(entry.Value)})"))
                + ") AS m(building_class, category_code) JOIN building_category AS c ON c.code = m.category_code "
                + "WHERE t.category_id IS NULL AND t.building_class = m.building_class;");
            migrationBuilder.Sql(
                "UPDATE building_type SET category_id = (SELECT id FROM building_category ORDER BY sort_order, id LIMIT 1) WHERE category_id IS NULL;");

            migrationBuilder.Sql(NeutralCodeSql.Texts(
                CategoryTexts,
                "SELECT name_key FROM building_category UNION ALL SELECT description_key FROM building_category"));
            migrationBuilder.Sql(NeutralCodeSql.Raise("building_category", PlanetsTextKeys.CategoryCodeGroup));

            migrationBuilder.AlterColumn<int>(
                name: "category_id",
                table: "building_type",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_building_type_category_id",
                table: "building_type",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_building_type_building_category_category_id",
                table: "building_type",
                column: "category_id",
                principalTable: "building_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_building_type_building_category_category_id",
                table: "building_type");

            migrationBuilder.DropTable(
                name: "building_category");

            migrationBuilder.DropIndex(
                name: "ix_building_type_category_id",
                table: "building_type");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "building_type");
        }
    }
}
