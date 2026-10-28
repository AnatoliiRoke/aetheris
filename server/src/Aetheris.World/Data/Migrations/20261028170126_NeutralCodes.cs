using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Moves the reference rows of the world to neutral codes and their texts into the table of
    /// texts (task 00022).
    /// </summary>
    /// <remarks>
    /// The identifiers of the rows and every link between them stay as they were; only the codes
    /// and the keys change, by the table of old and new codes kept with the migrations
    /// (<see cref="NeutralCodeMap"/>). The texts of the client move into <c>localization_text</c>.
    /// The group that was named «Кристали» keeps its row and gets the text «Мінерали»; the new
    /// group «Кристали» with its resource and the hidden share of the fields is added as data.
    /// The start body type of the configuration becomes a flag of the type. On a fresh database
    /// the tables are empty and the statements change nothing: the starting data brings the same
    /// codes and texts.
    /// </remarks>
    public partial class NeutralCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description_key",
                table: "resource_type",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "resource_type",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_key",
                table: "resource_group",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "color_key",
                table: "race",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_key",
                table: "race",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "race",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mood_key",
                table: "race",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<List<string>>(
                name: "trait_keys",
                table: "race",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "description_key",
                table: "planet_size_class",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description_key",
                table: "celestial_object_type",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "hidden_resource_type_id",
                table: "celestial_object_type",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "hidden_share_max",
                table: "celestial_object_type",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "hidden_share_min",
                table: "celestial_object_type",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "celestial_object_type",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_start_default",
                table: "celestial_object_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_celestial_object_type_hidden_resource_type_id",
                table: "celestial_object_type",
                column: "hidden_resource_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_celestial_object_type_resource_type_hidden_resource_type_id",
                table: "celestial_object_type",
                column: "hidden_resource_type_id",
                principalTable: "resource_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> map = NeutralCodeMap.Load();

            migrationBuilder.Sql(NeutralCodeSql.Rename("resource_group", WorldCodeGroups.ResourceGroup, map[WorldCodeGroups.ResourceGroup]));
            migrationBuilder.Sql(NeutralCodeSql.Rename("resource_type", WorldCodeGroups.Resource, map[WorldCodeGroups.Resource]));
            migrationBuilder.Sql(NeutralCodeSql.Rename("celestial_object_type", WorldCodeGroups.ObjectType, map[WorldCodeGroups.ObjectType]));
            migrationBuilder.Sql(NeutralCodeSql.Rename("planet_size_class", WorldCodeGroups.SizeClass, map[WorldCodeGroups.SizeClass]));
            migrationBuilder.Sql(NeutralCodeSql.Rename("race", WorldCodeGroups.Race, map[WorldCodeGroups.Race]));
            migrationBuilder.Sql(NeutralCodeSql.Renumber("generation_rule_group", WorldCodeGroups.RuleGroup));

            migrationBuilder.Sql(NeutralCodeSql.Keys("resource_group", WorldTextKeys.ResourceGroup));
            migrationBuilder.Sql(NeutralCodeSql.Keys("resource_type", WorldTextKeys.Resource));
            migrationBuilder.Sql(NeutralCodeSql.Keys("celestial_object_type", WorldTextKeys.ObjectType));
            migrationBuilder.Sql(NeutralCodeSql.Keys("planet_size_class", WorldTextKeys.SizeClass));
            migrationBuilder.Sql(NeutralCodeSql.Keys("race", WorldTextKeys.Race));
            migrationBuilder.Sql(
                """
                UPDATE race
                SET mood_key = 'world.race.' || code || '.mood',
                    trait_keys = ARRAY['world.race.' || code || '.trait_1', 'world.race.' || code || '.trait_2']
                """
                + $" WHERE code IN ({string.Join(", ", map[WorldCodeGroups.Race].Values.Select(NeutralCodeSql.Quote))});");

            Dictionary<string, string> raceNumbers = map[WorldCodeGroups.Race].Values.ToDictionary(
                code => code,
                code => code[(WorldCodeGroups.Race.Length + 1)..],
                StringComparer.Ordinal);

            migrationBuilder.Sql(NeutralCodeSql.SetByCode("race", "image_key", raceNumbers.ToDictionary(entry => entry.Key, entry => RaceImages[entry.Value])));
            migrationBuilder.Sql(NeutralCodeSql.SetByCode("race", "color_key", raceNumbers.ToDictionary(entry => entry.Key, entry => "race-" + entry.Value)));
            migrationBuilder.Sql(NeutralCodeSql.SetByCode(
                "celestial_object_type",
                "image_key",
                BodyImages.ToDictionary(entry => map[WorldCodeGroups.ObjectType][entry.Key], entry => entry.Value)));

            // The start type of the configuration becomes a flag of the type.
            migrationBuilder.Sql(
                "UPDATE celestial_object_type AS t SET is_start_default = true FROM (VALUES "
                + string.Join(", ", map[WorldCodeGroups.ObjectType].Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {NeutralCodeSql.Quote(entry.Value)})"))
                + """
                ) AS m(old_code, new_code)
                WHERE t.code = m.new_code
                  AND m.old_code = coalesce((SELECT value FROM config_parameter WHERE key = 'world.start.planet_type_code'), 'terrestrial');
                DELETE FROM config_parameter WHERE key = 'world.start.planet_type_code';
                """);

            migrationBuilder.Sql(NeutralCodeSql.Texts(MovedTexts, UsedKeys));

            // The new group «Кристали» with its one resource and the hidden share of the fields,
            // only on a database that already has its reference rows.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    new_group_code text;
                    new_resource_code text;
                    new_group_id integer;
                    new_resource_id integer;
                BEGIN
                    IF EXISTS (SELECT 1 FROM resource_group) THEN
                        SELECT 'resource_group_' || (coalesce(max(substring(code from '^resource_group_([0-9]+)$')::int), 0) + 1)
                        INTO new_group_code FROM resource_group;
                        SELECT 'resource_' || (coalesce(max(substring(code from '^resource_([0-9]+)$')::int), 0) + 1)
                        INTO new_resource_code FROM resource_type;

                        INSERT INTO resource_group (code, name_key, description_key, allowed_on_planet)
                        VALUES (new_group_code, 'world.resource_group.' || new_group_code || '.name', 'world.resource_group.' || new_group_code || '.description', false)
                        RETURNING id INTO new_group_id;

                        INSERT INTO resource_type (code, resource_group_id, name_key, description_key)
                        VALUES (new_resource_code, new_group_id, 'world.resource.' || new_resource_code || '.name', 'world.resource.' || new_resource_code || '.description')
                        RETURNING id INTO new_resource_id;

                        UPDATE celestial_object_type
                        SET hidden_resource_type_id = new_resource_id, hidden_share_min = 0.04, hidden_share_max = 0.1
                        WHERE object_class = 'asteroid_field';

                        INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
                        VALUES
                            ('world.resource_group.' || new_group_code || '.name', 'uk', 'Кристали', 1, now(), 'seed'),
                            ('world.resource_group.' || new_group_code || '.name', 'en', 'Crystals', 1, now(), 'seed'),
                            ('world.resource_group.' || new_group_code || '.description', 'uk', 'Унікальні кристали, які трапляються лише в астероїдних полях.', 1, now(), 'seed'),
                            ('world.resource_group.' || new_group_code || '.description', 'en', 'Unique crystals found only in asteroid fields.', 1, now(), 'seed'),
                            ('world.resource.' || new_resource_code || '.name', 'uk', 'Кристал', 1, now(), 'seed'),
                            ('world.resource.' || new_resource_code || '.name', 'en', 'Crystal', 1, now(), 'seed'),
                            ('world.resource.' || new_resource_code || '.description', 'uk', 'Унікальний ресурс астероїдних полів.', 1, now(), 'seed'),
                            ('world.resource.' || new_resource_code || '.description', 'en', 'A unique resource of asteroid fields.', 1, now(), 'seed')
                        ON CONFLICT (key, language) DO NOTHING;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(NeutralCodeSql.Raise("resource_group", WorldCodeGroups.ResourceGroup));
            migrationBuilder.Sql(NeutralCodeSql.Raise("resource_type", WorldCodeGroups.Resource));
            migrationBuilder.Sql(NeutralCodeSql.Raise("celestial_object_type", WorldCodeGroups.ObjectType));
            migrationBuilder.Sql(NeutralCodeSql.Raise("planet_size_class", WorldCodeGroups.SizeClass));
            migrationBuilder.Sql(NeutralCodeSql.Raise("race", WorldCodeGroups.Race));
            migrationBuilder.Sql(NeutralCodeSql.Raise("generation_rule_group", WorldCodeGroups.RuleGroup));
        }

        /// <inheritdoc />
        /// <remarks>
        /// The columns are removed, but the codes and the texts stay as they are: the old codes
        /// are not restored, and the table of texts belongs to the host.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_celestial_object_type_resource_type_hidden_resource_type_id",
                table: "celestial_object_type");

            migrationBuilder.DropIndex(
                name: "ix_celestial_object_type_hidden_resource_type_id",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "description_key",
                table: "resource_type");

            migrationBuilder.DropColumn(
                name: "image_key",
                table: "resource_type");

            migrationBuilder.DropColumn(
                name: "description_key",
                table: "resource_group");

            migrationBuilder.DropColumn(
                name: "color_key",
                table: "race");

            migrationBuilder.DropColumn(
                name: "description_key",
                table: "race");

            migrationBuilder.DropColumn(
                name: "image_key",
                table: "race");

            migrationBuilder.DropColumn(
                name: "mood_key",
                table: "race");

            migrationBuilder.DropColumn(
                name: "trait_keys",
                table: "race");

            migrationBuilder.DropColumn(
                name: "description_key",
                table: "planet_size_class");

            migrationBuilder.DropColumn(
                name: "description_key",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "hidden_resource_type_id",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "hidden_share_max",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "hidden_share_min",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "image_key",
                table: "celestial_object_type");

            migrationBuilder.DropColumn(
                name: "is_start_default",
                table: "celestial_object_type");
        }
    }
}
