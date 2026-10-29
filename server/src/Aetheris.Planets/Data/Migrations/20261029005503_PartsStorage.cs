using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Adds the storage of the parts (task 00013, decision 9): a building whose levels add room
    /// for the parts the factories make.
    /// </summary>
    /// <remarks>
    /// The building is data, so the migration adds it to a database that already has the
    /// starting data; a fresh database gets it from the seed of the module, which runs only while
    /// the table of the buildings is empty. The building takes the next neutral code of the
    /// counter, so a building the administrator added keeps its code. The numbers are the
    /// starting values of the seed.
    /// </remarks>
    public partial class PartsStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    next_number integer;
                    new_code text;
                    new_type_id integer;
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM building_type)
                        OR EXISTS (SELECT 1 FROM building_level_effect WHERE effect_type = 'part_storage_capacity') THEN
                        RETURN;
                    END IF;

                    SELECT GREATEST(
                            coalesce((SELECT last_number FROM code_sequence WHERE code_group = 'building'), 0),
                            coalesce((SELECT max(substring(code from '^building_([0-9]+)$')::int) FROM building_type), 0)) + 1
                    INTO next_number;

                    new_code := 'building_' || next_number;

                    INSERT INTO code_sequence (code_group, last_number) VALUES ('building', next_number)
                    ON CONFLICT (code_group) DO UPDATE SET last_number = GREATEST(code_sequence.last_number, EXCLUDED.last_number);

                    INSERT INTO building_type (code, name_key, description_key, sort_order, is_start_building, building_class, is_archived, version)
                    VALUES (
                        new_code,
                        'planets.building.' || new_code || '.name',
                        'planets.building.' || new_code || '.description',
                        (SELECT coalesce(max(sort_order), 0) + 1 FROM building_type),
                        false, '', false, 1)
                    RETURNING id INTO new_type_id;

                    INSERT INTO building_level (building_type_id, level, duration_seconds)
                    SELECT new_type_id, l, round(120 * power(1.5::numeric, l - 1))::bigint
                    FROM generate_series(1, (SELECT coalesce(max(level), 20) FROM building_level)) AS l;

                    INSERT INTO building_level_cost (building_level_id, resource_type_id, amount)
                    SELECT bl.id, r.id, round(c.base * power(1.6::numeric, bl.level - 1))::bigint
                    FROM building_level bl
                    CROSS JOIN (VALUES ('resource_1', 200), ('resource_3', 120)) AS c(code, base)
                    JOIN resource_type r ON r.code = c.code
                    WHERE bl.building_type_id = new_type_id;

                    INSERT INTO building_level_effect (building_level_id, effect_type, value)
                    SELECT bl.id, e.effect_type, round(e.base * power(e.growth, bl.level - 1))::bigint
                    FROM building_level bl
                    CROSS JOIN (VALUES ('part_storage_capacity', 200, 1.5::numeric), ('energy_consumption', 2, 1.2::numeric)) AS e(effect_type, base, growth)
                    WHERE bl.building_type_id = new_type_id;

                    INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
                    VALUES
                        ('planets.building.' || new_code || '.name', 'uk', 'Склад деталей', 1, now(), 'seed'),
                        ('planets.building.' || new_code || '.name', 'en', 'Parts storage', 1, now(), 'seed'),
                        ('planets.building.' || new_code || '.description', 'uk', 'Зберігає вироблені деталі: кожен рівень додає місця.', 1, now(), 'seed'),
                        ('planets.building.' || new_code || '.description', 'en', 'Keeps the produced parts: every level adds room.', 1, now(), 'seed')
                    ON CONFLICT (key, language) DO NOTHING;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM building_type
                WHERE id IN (
                    SELECT bl.building_type_id
                    FROM building_level bl
                    JOIN building_level_effect e ON e.building_level_id = bl.id
                    WHERE e.effect_type = 'part_storage_capacity')
                  AND NOT EXISTS (
                    SELECT 1 FROM planet_building pb
                    WHERE pb.building_type_id = building_type.id);
                """);

            migrationBuilder.Sql("DELETE FROM building_level_effect WHERE effect_type = 'part_storage_capacity';");
        }
    }
}
