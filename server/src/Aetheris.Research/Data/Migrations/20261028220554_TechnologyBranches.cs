using Aetheris.Research.Services;
using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Research.Data.Migrations
{
    /// <summary>
    /// Moves the branches of the tree of the technologies from a text column into their own table
    /// with neutral codes (task 00023, decision 8), and gives the technologies a version, the
    /// archive and a picture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The six branches of task 00012 become <c>branch_1</c>…<c>branch_6</c> in their old order,
    /// the empty ones included. A text the column held that is none of them gets the next number.
    /// The key technologies of task 00012 named a branch of their own each, which the tree showed
    /// without a name; they move to the branch of game-design/04 they belong to.
    /// </para>
    /// <para>
    /// The identifiers of the technologies and every link to them stay as they were. The names of
    /// the branches move to the new keys; a name changed in the admin panel moves as it is.
    /// </para>
    /// </remarks>
    public partial class TechnologyBranches : Migration
    {
        /// <summary>
        /// The old codes of the branches in their old order; the position is the number of the new code.
        /// </summary>
        private static readonly string[] OldBranches = ["economy", "fleet", "engines_and_logistics", "defence", "colonisation", "trade"];

        /// <summary>
        /// The branches of the key technologies of task 00012 by the branch they move to.
        /// </summary>
        private static readonly Dictionary<string, string> KeyTechnologyBranches = new(StringComparer.Ordinal)
        {
            ["titanium_mining"] = "economy",
            ["iridium_mining"] = "economy",
            ["crystal_mining"] = "economy",
            ["factory_building"] = "economy",
            ["recycling_building"] = "economy",
            ["shipyard_building"] = "fleet",
            ["radar_building"] = "defence",
            ["defence_building"] = "defence",
            ["bunker_building"] = "defence",
            ["portal_building"] = "trade",
            ["trade_center_building"] = "trade",
        };

        /// <summary>
        /// Gets the texts of the branches by the new keys, for the keys that have none after the
        /// names were moved.
        /// </summary>
        public static IReadOnlyList<MigrationText> BranchTexts { get; } =
        [
            new("research.technology_branch.branch_1.name", "uk", "Економіка"),
            new("research.technology_branch.branch_1.name", "en", "Economy"),
            new("research.technology_branch.branch_1.description", "uk", "Видобуток, виробництво, склади й енергія планет."),
            new("research.technology_branch.branch_1.description", "en", "The extraction, the production, the storages and the energy of the planets."),
            new("research.technology_branch.branch_2.name", "uk", "Флот"),
            new("research.technology_branch.branch_2.name", "en", "Fleet"),
            new("research.technology_branch.branch_2.description", "uk", "Кораблі, верфі та озброєння."),
            new("research.technology_branch.branch_2.description", "en", "The ships, the shipyards and the weapons."),
            new("research.technology_branch.branch_3.name", "uk", "Двигуни й логістика"),
            new("research.technology_branch.branch_3.name", "en", "Engines and logistics"),
            new("research.technology_branch.branch_3.description", "uk", "Двигуни, швидкість і перевезення."),
            new("research.technology_branch.branch_3.description", "en", "The engines, the speed and the transport."),
            new("research.technology_branch.branch_4.name", "uk", "Оборона"),
            new("research.technology_branch.branch_4.name", "en", "Defence"),
            new("research.technology_branch.branch_4.description", "uk", "Захист планет, радари й сховища."),
            new("research.technology_branch.branch_4.description", "en", "The defence of the planets, the radars and the shelters."),
            new("research.technology_branch.branch_5.name", "uk", "Колонізація"),
            new("research.technology_branch.branch_5.name", "en", "Colonisation"),
            new("research.technology_branch.branch_5.description", "uk", "Освоєння нових планет і простору."),
            new("research.technology_branch.branch_5.description", "en", "The settlement of new planets and of the space."),
            new("research.technology_branch.branch_6.name", "uk", "Торгівля"),
            new("research.technology_branch.branch_6.name", "en", "Trade"),
            new("research.technology_branch.branch_6.description", "uk", "Торгівля, ринок і портали."),
            new("research.technology_branch.branch_6.description", "en", "The trade, the market and the portals."),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "technology_branch",
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
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    old_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technology_branch", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_technology_branch_code",
                table: "technology_branch",
                column: "code",
                unique: true);

            // The key technologies move to their branch first, so every old code is one of the six
            // or a text a designer wrote by hand.
            migrationBuilder.Sql(
                "UPDATE technology AS t SET branch = m.branch FROM (VALUES "
                + string.Join(", ", KeyTechnologyBranches.Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {NeutralCodeSql.Quote(entry.Value)})"))
                + ") AS m(old_branch, branch) WHERE t.branch = m.old_branch;");

            migrationBuilder.Sql(
                "INSERT INTO technology_branch (code, name_key, description_key, sort_order, old_code) SELECT "
                + $"{NeutralCodeSql.Quote(ResearchBranchTextKeys.CodeGroup + "_")} || o.position, "
                + $"{NeutralCodeSql.Quote(ResearchBranchTextKeys.Branch + "." + ResearchBranchTextKeys.CodeGroup + "_")} || o.position || '.name', "
                + $"{NeutralCodeSql.Quote(ResearchBranchTextKeys.Branch + "." + ResearchBranchTextKeys.CodeGroup + "_")} || o.position || '.description', "
                + "o.position, o.old_code FROM (VALUES "
                + string.Join(", ", OldBranches.Select((branch, index) => $"({NeutralCodeSql.Quote(branch)}, {index + 1})"))
                + ") AS o(old_code, position);");

            migrationBuilder.Sql(
                $"""
                WITH others AS (
                    SELECT branch AS old_code, row_number() OVER (ORDER BY branch) + {OldBranches.Length} AS position
                    FROM (SELECT DISTINCT branch FROM technology WHERE branch NOT IN (SELECT old_code FROM technology_branch)) AS found)
                INSERT INTO technology_branch (code, name_key, description_key, sort_order, old_code)
                SELECT {NeutralCodeSql.Quote(ResearchBranchTextKeys.CodeGroup + "_")} || position,
                       {NeutralCodeSql.Quote(ResearchBranchTextKeys.Branch + "." + ResearchBranchTextKeys.CodeGroup + "_")} || position || '.name',
                       {NeutralCodeSql.Quote(ResearchBranchTextKeys.Branch + "." + ResearchBranchTextKeys.CodeGroup + "_")} || position || '.description',
                       position,
                       old_code
                FROM others;
                """);

            migrationBuilder.AddColumn<int>(
                name: "branch_id",
                table: "technology",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE technology AS t SET branch_id = b.id FROM technology_branch AS b WHERE b.old_code = t.branch;");

            migrationBuilder.AlterColumn<int>(
                name: "branch_id",
                table: "technology",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // A name changed in the admin panel moves to the new key as it is; the texts of the
            // migration fill the keys that are still empty.
            migrationBuilder.Sql(
                """
                INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
                SELECT b.name_key, t.language, t.text, 1, now(), t.updated_by
                FROM technology_branch AS b
                JOIN localization_text AS t ON t.key = 'research.branch.' || b.old_code || '.name'
                ON CONFLICT (key, language) DO NOTHING;
                """);
            migrationBuilder.Sql(NeutralCodeSql.Texts(
                BranchTexts,
                "SELECT name_key FROM technology_branch UNION ALL SELECT description_key FROM technology_branch"));
            migrationBuilder.Sql(NeutralCodeSql.Raise("technology_branch", ResearchBranchTextKeys.CodeGroup));

            migrationBuilder.DropIndex(
                name: "ix_technology_branch_sort_order",
                table: "technology");

            migrationBuilder.DropColumn(
                name: "branch",
                table: "technology");

            migrationBuilder.DropColumn(
                name: "old_code",
                table: "technology_branch");

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "technology",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "technology",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "technology",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_technology_branch_id_sort_order",
                table: "technology",
                columns: new[] { "branch_id", "sort_order" });

            migrationBuilder.AddForeignKey(
                name: "fk_technology_technology_branch_branch_id",
                table: "technology",
                column: "branch_id",
                principalTable: "technology_branch",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>The branch of a technology becomes the neutral code of its branch; the texts stay as they are.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_technology_technology_branch_branch_id",
                table: "technology");

            migrationBuilder.DropIndex(
                name: "ix_technology_branch_id_sort_order",
                table: "technology");

            migrationBuilder.AddColumn<string>(
                name: "branch",
                table: "technology",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE technology AS t SET branch = b.code FROM technology_branch AS b WHERE b.id = t.branch_id;");

            migrationBuilder.DropTable(
                name: "technology_branch");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "technology");

            migrationBuilder.DropColumn(
                name: "image_key",
                table: "technology");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "technology");

            migrationBuilder.DropColumn(
                name: "version",
                table: "technology");

            migrationBuilder.CreateIndex(
                name: "ix_technology_branch_sort_order",
                table: "technology",
                columns: new[] { "branch", "sort_order" });
        }
    }
}
