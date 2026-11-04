using Aetheris.Production.Services;
using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <summary>
    /// Gives the groups of the parts a parent (task 00043): a group of the top level is a section
    /// of the production screen, and a group inside a group is a tab inside the tab of its parent,
    /// so the client builds the sections and the tabs from the data and not from a list of codes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A database that already holds groups gets the four groups of the starting data that hold
    /// other groups: the sections of the ship modules, the ship hulls and the base modules, and the
    /// weapons inside the ship modules. They take the next codes of the counter of the groups, with
    /// their texts, and the groups of the starting data (<c>part_group_1</c>…<c>part_group_18</c>)
    /// get their parents. A group that already has a parent, or one added by hand, keeps what it
    /// has; the administrator moves it in the admin panel. A fresh database gets no row here: the
    /// seed of the module writes the groups with their parents.
    /// </para>
    /// </remarks>
    public partial class PartGroupParents : Migration
    {
        /// <summary>
        /// The groups that hold other groups: the key of the row in this migration, the key of its
        /// parent, the order, and the names and the descriptions in every language.
        /// </summary>
        private static readonly (string Key, string Parent, int SortOrder, string NameUk, string NameEn)[] Parents =
        [
            ("ship_modules", null, 1, "Модулі кораблів", "Ship modules"),
            ("ship_hulls", null, 2, "Корпуси кораблів", "Ship hulls"),
            ("base_modules", null, 3, "Модулі бази", "Base modules"),
            ("weapons", "ship_modules", 4, "Зброя", "Weapons"),
        ];

        /// <summary>
        /// The parent of every group of the starting data, by the neutral code of the group.
        /// </summary>
        private static readonly Dictionary<string, string> StartingGroupParents = new(StringComparer.Ordinal)
        {
            ["part_group_1"] = "ship_modules",
            ["part_group_2"] = "ship_modules",
            ["part_group_3"] = "weapons",
            ["part_group_4"] = "weapons",
            ["part_group_5"] = "ship_modules",
            ["part_group_6"] = "ship_modules",
            ["part_group_7"] = "weapons",
            ["part_group_8"] = "ship_modules",
            ["part_group_9"] = "ship_modules",
            ["part_group_10"] = "ship_modules",
            ["part_group_11"] = "base_modules",
            ["part_group_12"] = "ship_hulls",
            ["part_group_13"] = "ship_hulls",
            ["part_group_14"] = "ship_hulls",
            ["part_group_15"] = "ship_hulls",
            ["part_group_16"] = "ship_hulls",
            ["part_group_17"] = "ship_hulls",
            ["part_group_18"] = "base_modules",
        };

        /// <summary>
        /// The order of every group of the starting data among the groups of its parent, as the
        /// author set the tabs of the production screen (04.11.2026), by the neutral code of the
        /// group, with the order the group had before.
        /// </summary>
        private static readonly (string Code, int OldSortOrder, int SortOrder)[] StartingGroupOrders =
        [
            ("part_group_8", 8, 1),
            ("part_group_1", 1, 2),
            ("part_group_2", 2, 3),
            ("part_group_5", 5, 5),
            ("part_group_6", 6, 6),
            ("part_group_9", 9, 7),
            ("part_group_10", 10, 8),
            ("part_group_3", 3, 1),
            ("part_group_4", 4, 2),
            ("part_group_7", 7, 3),
            ("part_group_17", 17, 1),
            ("part_group_12", 12, 2),
            ("part_group_14", 14, 3),
            ("part_group_15", 15, 4),
            ("part_group_16", 16, 5),
            ("part_group_13", 13, 6),
            ("part_group_11", 11, 1),
            ("part_group_18", 18, 2),
        ];

        /// <summary>
        /// The new name of the other ship modules (author, 04.11.2026): the language, the old text
        /// of the starting data and the new one.
        /// </summary>
        private static readonly (string Language, string OldText, string Text)[] OtherModulesNames =
        [
            ("uk", "Інші корабельні", "Інші модулі"),
            ("en", "Other ship modules", "Other modules"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_group_id",
                table: "part_group",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_group_parent_group_id_sort_order",
                table: "part_group",
                columns: new[] { "parent_group_id", "sort_order" });

            migrationBuilder.AddForeignKey(
                name: "fk_part_group_part_group_parent_group_id",
                table: "part_group",
                column: "parent_group_id",
                principalTable: "part_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            string group = ProductionTextKeys.PartGroupCodeGroup;
            string keyPrefix = ProductionTextKeys.PartGroup + ".";

            // The new rows take the next numbers of the group after the largest number in use, in
            // the order of the table above; an empty table gets nothing, the seed writes the rows
            // then.
            migrationBuilder.Sql(
                "CREATE TEMPORARY TABLE part_group_parent_seed AS SELECT m.row_key, m.parent_key, m.sort_order, m.name_uk, m.name_en, "
                + $"{NeutralCodeSql.Quote(group + "_")} || ({NeutralCodeSql.LastNumber("part_group", group)} + m.position) AS code "
                + "FROM (VALUES "
                + string.Join(", ", Parents.Select((entry, index) =>
                    $"({index + 1}, {NeutralCodeSql.Quote(entry.Key)}, {(entry.Parent is null ? "NULL::text" : NeutralCodeSql.Quote(entry.Parent))}, {entry.SortOrder}, "
                    + $"{NeutralCodeSql.Quote(entry.NameUk)}, {NeutralCodeSql.Quote(entry.NameEn)})"))
                + ") AS m(position, row_key, parent_key, sort_order, name_uk, name_en) "
                + "WHERE EXISTS (SELECT 1 FROM part_group);");

            migrationBuilder.Sql(
                "INSERT INTO part_group (code, name_key, description_key, sort_order, is_archived, version) "
                + $"SELECT s.code, {NeutralCodeSql.Quote(keyPrefix)} || s.code || '.name', {NeutralCodeSql.Quote(keyPrefix)} || s.code || '.description', s.sort_order, false, 1 "
                + "FROM part_group_parent_seed AS s ORDER BY s.code;");

            // The weapons lie inside the ship modules.
            migrationBuilder.Sql(
                "UPDATE part_group AS t SET parent_group_id = p.id "
                + "FROM part_group_parent_seed AS s "
                + "JOIN part_group_parent_seed AS ps ON ps.row_key = s.parent_key "
                + "JOIN part_group AS p ON p.code = ps.code "
                + "WHERE t.code = s.code;");

            // The groups of the starting data get their parents; a group with a parent keeps it.
            migrationBuilder.Sql(
                "UPDATE part_group AS t SET parent_group_id = p.id FROM (VALUES "
                + string.Join(", ", StartingGroupParents.Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {NeutralCodeSql.Quote(entry.Value)})"))
                + ") AS m(group_code, parent_key) "
                + "JOIN part_group_parent_seed AS s ON s.row_key = m.parent_key "
                + "JOIN part_group AS p ON p.code = s.code "
                + "WHERE t.code = m.group_code AND t.parent_group_id IS NULL;");

            // The texts of the new rows; a text that exists is kept.
            migrationBuilder.Sql(
                "INSERT INTO localization_text (key, language, text, version, updated_at, updated_by) "
                + $"SELECT t.key, t.language, t.text, 1, now(), {NeutralCodeSql.Quote(NeutralCodeSql.Author)} FROM ("
                + $"SELECT {NeutralCodeSql.Quote(keyPrefix)} || s.code || '.name' AS key, 'uk' AS language, s.name_uk AS text FROM part_group_parent_seed AS s "
                + $"UNION ALL SELECT {NeutralCodeSql.Quote(keyPrefix)} || s.code || '.name', 'en', s.name_en FROM part_group_parent_seed AS s "
                + $"UNION ALL SELECT {NeutralCodeSql.Quote(keyPrefix)} || s.code || '.description', 'uk', 'Група «' || s.name_uk || '».' FROM part_group_parent_seed AS s "
                + $"UNION ALL SELECT {NeutralCodeSql.Quote(keyPrefix)} || s.code || '.description', 'en', 'Group ' || s.name_en || '.' FROM part_group_parent_seed AS s"
                + ") AS t ON CONFLICT (key, language) DO NOTHING;");

            // The order of the tabs; a group whose order an administrator changed keeps it.
            migrationBuilder.Sql(
                "UPDATE part_group AS t SET sort_order = m.sort_order FROM (VALUES "
                + string.Join(", ", StartingGroupOrders.Select(entry => $"({NeutralCodeSql.Quote(entry.Code)}, {entry.OldSortOrder}, {entry.SortOrder})"))
                + ") AS m(group_code, old_sort_order, sort_order) "
                + "WHERE t.code = m.group_code AND t.sort_order = m.old_sort_order "
                + "AND EXISTS (SELECT 1 FROM part_group_parent_seed);");

            // The name of the other ship modules; a name an administrator changed is kept.
            foreach ((string language, string oldText, string text) in OtherModulesNames)
            {
                migrationBuilder.Sql(
                    $"UPDATE localization_text SET text = {NeutralCodeSql.Quote(text)}, version = version + 1, updated_at = now() "
                    + $"WHERE key = {NeutralCodeSql.Quote(ProductionTextKeys.GroupName("part_group_10"))} AND language = {NeutralCodeSql.Quote(language)} "
                    + $"AND text = {NeutralCodeSql.Quote(oldText)};");
                migrationBuilder.Sql(
                    $"UPDATE localization_text SET text = {NeutralCodeSql.Quote(language == "uk" ? "Група «" + text + "»." : "Group " + text + ".")}, version = version + 1, updated_at = now() "
                    + $"WHERE key = {NeutralCodeSql.Quote(ProductionTextKeys.GroupDescription("part_group_10"))} AND language = {NeutralCodeSql.Quote(language)} "
                    + $"AND text = {NeutralCodeSql.Quote(language == "uk" ? "Група «" + oldText + "»." : "Group " + oldText + ".")};");
            }

            migrationBuilder.Sql("DROP TABLE IF EXISTS part_group_parent_seed;");
            migrationBuilder.Sql(NeutralCodeSql.Raise("part_group", group));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The groups that hold other groups stay as rows, so a part or a link that an
            // administrator gave them is never lost; only the links between the groups go.
            migrationBuilder.DropForeignKey(
                name: "fk_part_group_part_group_parent_group_id",
                table: "part_group");

            migrationBuilder.DropIndex(
                name: "ix_part_group_parent_group_id_sort_order",
                table: "part_group");

            migrationBuilder.DropColumn(
                name: "parent_group_id",
                table: "part_group");
        }
    }
}
