using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Gives the ten buildings that had no numeric effect the effects of their levels (task
    /// 00028, point "Buildings without a numeric effect"): the classes of the buildings and the
    /// values of their effects at every level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seed writes the starting data only into an empty table, so a database that already
    /// holds the buildings keeps the effects it has. This migration gives such a database the
    /// effects of the starting data: the classes of the buildings and the values of the effects
    /// at every level.
    /// </para>
    /// <para>
    /// A class that an administrator has chosen is not taken back: only a building without a
    /// class gets the class of the starting data. An effect that is already there is left as it
    /// is, so a change made in the admin panel stays.
    /// </para>
    /// </remarks>
    public partial class BuildingEffectsTwelve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The classes of the buildings: only a building without a class gets the class of
            // the starting data, so a class chosen in the admin panel stays.
            migrationBuilder.Sql(
                """
                UPDATE building_type bt
                SET building_class = v.building_class
                FROM (VALUES
                    ('building_4', 'bank'),
                    ('building_5', 'construction_plant'),
                    ('building_6', 'spaceport'),
                    ('building_8', 'bunker'),
                    ('building_9', 'research_center'),
                    ('building_10', 'design_bureau'),
                    ('building_39', 'converter'),
                    ('building_50', 'recycling'),
                    ('building_52', 'sensor'),
                    ('building_56', 'portal')
                ) AS v(code, building_class)
                WHERE bt.code = v.code AND bt.building_class = '';
                """);

            // The effects of the levels: a row is written for every level of every building that
            // has the effect, and a row that is already there is left as it is.
            migrationBuilder.Sql(
                """
                INSERT INTO building_level_effect (building_level_id, effect_type, value)
                SELECT bl.id, v.effect_type, v.value
                FROM (VALUES
                    ('building_4', 'credit_income_bonus', 1, 0.1),
                    ('building_4', 'credit_income_bonus', 2, 0.3053),
                    ('building_4', 'credit_income_bonus', 3, 0.5105),
                    ('building_4', 'credit_income_bonus', 4, 0.7158),
                    ('building_4', 'credit_income_bonus', 5, 0.9211),
                    ('building_4', 'credit_income_bonus', 6, 1.1263),
                    ('building_4', 'credit_income_bonus', 7, 1.3316),
                    ('building_4', 'credit_income_bonus', 8, 1.5368),
                    ('building_4', 'credit_income_bonus', 9, 1.7421),
                    ('building_4', 'credit_income_bonus', 10, 1.9474),
                    ('building_4', 'credit_income_bonus', 11, 2.1526),
                    ('building_4', 'credit_income_bonus', 12, 2.3579),
                    ('building_4', 'credit_income_bonus', 13, 2.5632),
                    ('building_4', 'credit_income_bonus', 14, 2.7684),
                    ('building_4', 'credit_income_bonus', 15, 2.9737),
                    ('building_4', 'credit_income_bonus', 16, 3.1789),
                    ('building_4', 'credit_income_bonus', 17, 3.3842),
                    ('building_4', 'credit_income_bonus', 18, 3.5895),
                    ('building_4', 'credit_income_bonus', 19, 3.7947),
                    ('building_4', 'credit_income_bonus', 20, 4),
                    ('building_5', 'construction_queue_slots', 1, 0),
                    ('building_5', 'construction_queue_slots', 2, 0),
                    ('building_5', 'construction_queue_slots', 3, 0),
                    ('building_5', 'construction_queue_slots', 4, 0),
                    ('building_5', 'construction_queue_slots', 5, 0),
                    ('building_5', 'construction_queue_slots', 6, 0),
                    ('building_5', 'construction_queue_slots', 7, 1),
                    ('building_5', 'construction_queue_slots', 8, 1),
                    ('building_5', 'construction_queue_slots', 9, 1),
                    ('building_5', 'construction_queue_slots', 10, 1),
                    ('building_5', 'construction_queue_slots', 11, 1),
                    ('building_5', 'construction_queue_slots', 12, 1),
                    ('building_5', 'construction_queue_slots', 13, 1),
                    ('building_5', 'construction_queue_slots', 14, 2),
                    ('building_5', 'construction_queue_slots', 15, 2),
                    ('building_5', 'construction_queue_slots', 16, 2),
                    ('building_5', 'construction_queue_slots', 17, 2),
                    ('building_5', 'construction_queue_slots', 18, 2),
                    ('building_5', 'construction_queue_slots', 19, 2),
                    ('building_5', 'construction_queue_slots', 20, 3),
                    ('building_5', 'construction_speed_bonus', 1, 0),
                    ('building_5', 'construction_speed_bonus', 2, 0.0526),
                    ('building_5', 'construction_speed_bonus', 3, 0.1053),
                    ('building_5', 'construction_speed_bonus', 4, 0.1579),
                    ('building_5', 'construction_speed_bonus', 5, 0.2105),
                    ('building_5', 'construction_speed_bonus', 6, 0.2632),
                    ('building_5', 'construction_speed_bonus', 7, 0.3158),
                    ('building_5', 'construction_speed_bonus', 8, 0.3684),
                    ('building_5', 'construction_speed_bonus', 9, 0.4211),
                    ('building_5', 'construction_speed_bonus', 10, 0.4737),
                    ('building_5', 'construction_speed_bonus', 11, 0.5263),
                    ('building_5', 'construction_speed_bonus', 12, 0.5789),
                    ('building_5', 'construction_speed_bonus', 13, 0.6316),
                    ('building_5', 'construction_speed_bonus', 14, 0.6842),
                    ('building_5', 'construction_speed_bonus', 15, 0.7368),
                    ('building_5', 'construction_speed_bonus', 16, 0.7895),
                    ('building_5', 'construction_speed_bonus', 17, 0.8421),
                    ('building_5', 'construction_speed_bonus', 18, 0.8947),
                    ('building_5', 'construction_speed_bonus', 19, 0.9474),
                    ('building_5', 'construction_speed_bonus', 20, 1),
                    ('building_6', 'fleet_limit', 1, 1),
                    ('building_6', 'fleet_limit', 2, 2),
                    ('building_6', 'fleet_limit', 3, 3),
                    ('building_6', 'fleet_limit', 4, 4),
                    ('building_6', 'fleet_limit', 5, 5),
                    ('building_6', 'fleet_limit', 6, 6),
                    ('building_6', 'fleet_limit', 7, 7),
                    ('building_6', 'fleet_limit', 8, 8),
                    ('building_6', 'fleet_limit', 9, 9),
                    ('building_6', 'fleet_limit', 10, 10),
                    ('building_6', 'fleet_limit', 11, 11),
                    ('building_6', 'fleet_limit', 12, 12),
                    ('building_8', 'protected_storage_capacity', 1, 10000),
                    ('building_8', 'protected_storage_capacity', 2, 15000),
                    ('building_8', 'protected_storage_capacity', 3, 22500),
                    ('building_8', 'protected_storage_capacity', 4, 33750),
                    ('building_8', 'protected_storage_capacity', 5, 50625),
                    ('building_8', 'protected_storage_capacity', 6, 75937.5),
                    ('building_8', 'protected_storage_capacity', 7, 113906.25),
                    ('building_8', 'protected_storage_capacity', 8, 170859.38),
                    ('building_8', 'protected_storage_capacity', 9, 256289.06),
                    ('building_8', 'protected_storage_capacity', 10, 384433.59),
                    ('building_8', 'protected_storage_capacity', 11, 576650.39),
                    ('building_8', 'protected_storage_capacity', 12, 864975.59),
                    ('building_8', 'protected_storage_capacity', 13, 1297463.38),
                    ('building_8', 'protected_storage_capacity', 14, 1946195.07),
                    ('building_8', 'protected_storage_capacity', 15, 2919292.6),
                    ('building_8', 'protected_storage_capacity', 16, 4378938.9),
                    ('building_8', 'protected_storage_capacity', 17, 6568408.36),
                    ('building_8', 'protected_storage_capacity', 18, 9852612.53),
                    ('building_8', 'protected_storage_capacity', 19, 14778918.8),
                    ('building_8', 'protected_storage_capacity', 20, 22168378.2),
                    ('building_9', 'research_queue_slots', 1, 0),
                    ('building_9', 'research_queue_slots', 2, 0),
                    ('building_9', 'research_queue_slots', 3, 0),
                    ('building_9', 'research_queue_slots', 4, 0),
                    ('building_9', 'research_queue_slots', 5, 0),
                    ('building_9', 'research_queue_slots', 6, 0),
                    ('building_9', 'research_queue_slots', 7, 1),
                    ('building_9', 'research_queue_slots', 8, 1),
                    ('building_9', 'research_queue_slots', 9, 1),
                    ('building_9', 'research_queue_slots', 10, 1),
                    ('building_9', 'research_queue_slots', 11, 1),
                    ('building_9', 'research_queue_slots', 12, 1),
                    ('building_9', 'research_queue_slots', 13, 1),
                    ('building_9', 'research_queue_slots', 14, 2),
                    ('building_9', 'research_queue_slots', 15, 2),
                    ('building_9', 'research_queue_slots', 16, 2),
                    ('building_9', 'research_queue_slots', 17, 2),
                    ('building_9', 'research_queue_slots', 18, 2),
                    ('building_9', 'research_queue_slots', 19, 2),
                    ('building_9', 'research_queue_slots', 20, 3),
                    ('building_9', 'research_speed_bonus', 1, 0),
                    ('building_9', 'research_speed_bonus', 2, 0.0526),
                    ('building_9', 'research_speed_bonus', 3, 0.1053),
                    ('building_9', 'research_speed_bonus', 4, 0.1579),
                    ('building_9', 'research_speed_bonus', 5, 0.2105),
                    ('building_9', 'research_speed_bonus', 6, 0.2632),
                    ('building_9', 'research_speed_bonus', 7, 0.3158),
                    ('building_9', 'research_speed_bonus', 8, 0.3684),
                    ('building_9', 'research_speed_bonus', 9, 0.4211),
                    ('building_9', 'research_speed_bonus', 10, 0.4737),
                    ('building_9', 'research_speed_bonus', 11, 0.5263),
                    ('building_9', 'research_speed_bonus', 12, 0.5789),
                    ('building_9', 'research_speed_bonus', 13, 0.6316),
                    ('building_9', 'research_speed_bonus', 14, 0.6842),
                    ('building_9', 'research_speed_bonus', 15, 0.7368),
                    ('building_9', 'research_speed_bonus', 16, 0.7895),
                    ('building_9', 'research_speed_bonus', 17, 0.8421),
                    ('building_9', 'research_speed_bonus', 18, 0.8947),
                    ('building_9', 'research_speed_bonus', 19, 0.9474),
                    ('building_9', 'research_speed_bonus', 20, 1),
                    ('building_10', 'design_queue_slots', 1, 0),
                    ('building_10', 'design_queue_slots', 2, 2),
                    ('building_10', 'design_queue_slots', 3, 4),
                    ('building_10', 'design_queue_slots', 4, 6),
                    ('building_10', 'design_queue_slots', 5, 8),
                    ('building_10', 'design_queue_slots', 6, 10),
                    ('building_10', 'design_queue_slots', 7, 12),
                    ('building_10', 'design_queue_slots', 8, 14),
                    ('building_10', 'design_queue_slots', 9, 16),
                    ('building_10', 'design_queue_slots', 10, 18),
                    ('building_10', 'design_queue_slots', 11, 20),
                    ('building_10', 'design_queue_slots', 12, 22),
                    ('building_10', 'design_queue_slots', 13, 23),
                    ('building_10', 'design_queue_slots', 14, 24),
                    ('building_10', 'design_queue_slots', 15, 25),
                    ('building_10', 'design_queue_slots', 16, 26),
                    ('building_10', 'design_queue_slots', 17, 27),
                    ('building_10', 'design_queue_slots', 18, 28),
                    ('building_10', 'design_queue_slots', 19, 29),
                    ('building_10', 'design_queue_slots', 20, 30),
                    ('building_10', 'hull_class_limit', 1, 0),
                    ('building_10', 'hull_class_limit', 2, 2),
                    ('building_10', 'hull_class_limit', 3, 4),
                    ('building_10', 'hull_class_limit', 4, 6),
                    ('building_10', 'hull_class_limit', 5, 8),
                    ('building_10', 'hull_class_limit', 6, 10),
                    ('building_10', 'hull_class_limit', 7, 12),
                    ('building_10', 'hull_class_limit', 8, 14),
                    ('building_10', 'hull_class_limit', 9, 16),
                    ('building_10', 'hull_class_limit', 10, 17),
                    ('building_10', 'hull_class_limit', 11, 18),
                    ('building_10', 'hull_class_limit', 12, 19),
                    ('building_10', 'hull_class_limit', 13, 20),
                    ('building_10', 'hull_class_limit', 14, 21),
                    ('building_10', 'hull_class_limit', 15, 22),
                    ('building_10', 'hull_class_limit', 16, 23),
                    ('building_10', 'hull_class_limit', 17, 24),
                    ('building_10', 'hull_class_limit', 18, 25),
                    ('building_10', 'hull_class_limit', 19, 26),
                    ('building_10', 'hull_class_limit', 20, 27),
                    ('building_39', 'conversion_coefficient', 1, 0.7),
                    ('building_39', 'conversion_coefficient', 2, 0.7132),
                    ('building_39', 'conversion_coefficient', 3, 0.7263),
                    ('building_39', 'conversion_coefficient', 4, 0.7395),
                    ('building_39', 'conversion_coefficient', 5, 0.7526),
                    ('building_39', 'conversion_coefficient', 6, 0.7658),
                    ('building_39', 'conversion_coefficient', 7, 0.7789),
                    ('building_39', 'conversion_coefficient', 8, 0.7921),
                    ('building_39', 'conversion_coefficient', 9, 0.8053),
                    ('building_39', 'conversion_coefficient', 10, 0.8184),
                    ('building_39', 'conversion_coefficient', 11, 0.8316),
                    ('building_39', 'conversion_coefficient', 12, 0.8447),
                    ('building_39', 'conversion_coefficient', 13, 0.8579),
                    ('building_39', 'conversion_coefficient', 14, 0.8711),
                    ('building_39', 'conversion_coefficient', 15, 0.8842),
                    ('building_39', 'conversion_coefficient', 16, 0.8974),
                    ('building_39', 'conversion_coefficient', 17, 0.9105),
                    ('building_39', 'conversion_coefficient', 18, 0.9237),
                    ('building_39', 'conversion_coefficient', 19, 0.9368),
                    ('building_39', 'conversion_coefficient', 20, 0.95),
                    ('building_50', 'recycling_coefficient', 1, 0.7),
                    ('building_50', 'recycling_coefficient', 2, 0.7132),
                    ('building_50', 'recycling_coefficient', 3, 0.7263),
                    ('building_50', 'recycling_coefficient', 4, 0.7395),
                    ('building_50', 'recycling_coefficient', 5, 0.7526),
                    ('building_50', 'recycling_coefficient', 6, 0.7658),
                    ('building_50', 'recycling_coefficient', 7, 0.7789),
                    ('building_50', 'recycling_coefficient', 8, 0.7921),
                    ('building_50', 'recycling_coefficient', 9, 0.8053),
                    ('building_50', 'recycling_coefficient', 10, 0.8184),
                    ('building_50', 'recycling_coefficient', 11, 0.8316),
                    ('building_50', 'recycling_coefficient', 12, 0.8447),
                    ('building_50', 'recycling_coefficient', 13, 0.8579),
                    ('building_50', 'recycling_coefficient', 14, 0.8711),
                    ('building_50', 'recycling_coefficient', 15, 0.8842),
                    ('building_50', 'recycling_coefficient', 16, 0.8974),
                    ('building_50', 'recycling_coefficient', 17, 0.9105),
                    ('building_50', 'recycling_coefficient', 18, 0.9237),
                    ('building_50', 'recycling_coefficient', 19, 0.9368),
                    ('building_50', 'recycling_coefficient', 20, 0.95),
                    ('building_52', 'radar_range_bonus', 1, 0),
                    ('building_52', 'radar_range_bonus', 2, 0.1111),
                    ('building_52', 'radar_range_bonus', 3, 0.2222),
                    ('building_52', 'radar_range_bonus', 4, 0.3333),
                    ('building_52', 'radar_range_bonus', 5, 0.4444),
                    ('building_52', 'radar_range_bonus', 6, 0.5556),
                    ('building_52', 'radar_range_bonus', 7, 0.6667),
                    ('building_52', 'radar_range_bonus', 8, 0.7778),
                    ('building_52', 'radar_range_bonus', 9, 0.8889),
                    ('building_52', 'radar_range_bonus', 10, 1),
                    ('building_56', 'portal_mass_per_energy_rod', 1, 1000),
                    ('building_56', 'portal_mass_per_energy_rod', 2, 1181.8182),
                    ('building_56', 'portal_mass_per_energy_rod', 3, 1363.6364),
                    ('building_56', 'portal_mass_per_energy_rod', 4, 1545.4545),
                    ('building_56', 'portal_mass_per_energy_rod', 5, 1727.2727),
                    ('building_56', 'portal_mass_per_energy_rod', 6, 1909.0909),
                    ('building_56', 'portal_mass_per_energy_rod', 7, 2090.9091),
                    ('building_56', 'portal_mass_per_energy_rod', 8, 2272.7273),
                    ('building_56', 'portal_mass_per_energy_rod', 9, 2454.5455),
                    ('building_56', 'portal_mass_per_energy_rod', 10, 2636.3636),
                    ('building_56', 'portal_mass_per_energy_rod', 11, 2818.1818),
                    ('building_56', 'portal_mass_per_energy_rod', 12, 3000),
                    ('building_56', 'portal_mass_per_hour', 1, 200000),
                    ('building_56', 'portal_mass_per_hour', 2, 218181.8182),
                    ('building_56', 'portal_mass_per_hour', 3, 236363.6364),
                    ('building_56', 'portal_mass_per_hour', 4, 254545.4545),
                    ('building_56', 'portal_mass_per_hour', 5, 272727.2727),
                    ('building_56', 'portal_mass_per_hour', 6, 290909.0909),
                    ('building_56', 'portal_mass_per_hour', 7, 309090.9091),
                    ('building_56', 'portal_mass_per_hour', 8, 327272.7273),
                    ('building_56', 'portal_mass_per_hour', 9, 345454.5455),
                    ('building_56', 'portal_mass_per_hour', 10, 363636.3636),
                    ('building_56', 'portal_mass_per_hour', 11, 381818.1818),
                    ('building_56', 'portal_mass_per_hour', 12, 400000)
                ) AS v(code, effect_type, level, value)
                JOIN building_type bt ON bt.code = v.code
                JOIN building_level bl ON bl.building_type_id = bt.id AND bl.level = v.level
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM building_level_effect e
                    WHERE e.building_level_id = bl.id AND e.effect_type = v.effect_type);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The effects of the starting data are taken back, and only them: an effect another
            // source has written is not touched.
            migrationBuilder.Sql(
                """
                DELETE FROM building_level_effect e
                USING building_level bl
                JOIN building_type bt ON bt.id = bl.building_type_id
                WHERE e.building_level_id = bl.id
                  AND bt.code IN (
                      'building_4',
                      'building_5',
                      'building_6',
                      'building_8',
                      'building_9',
                      'building_10',
                      'building_39',
                      'building_50',
                      'building_52',
                      'building_56'
                  )
                  AND e.effect_type IN (
                      'credit_income_bonus',
                      'construction_queue_slots',
                      'construction_speed_bonus',
                      'research_queue_slots',
                      'research_speed_bonus',
                      'fleet_limit',
                      'protected_storage_capacity',
                      'conversion_coefficient',
                      'recycling_coefficient',
                      'radar_range_bonus',
                      'portal_mass_per_energy_rod',
                      'portal_mass_per_hour',
                      'hull_class_limit',
                      'design_queue_slots'
                  );
                """);

            // The classes of the starting data are taken back, and only them: a class another
            // source has written is not touched.
            migrationBuilder.Sql(
                """
                UPDATE building_type
                SET building_class = ''
                WHERE code IN (
                      'building_4',
                      'building_5',
                      'building_6',
                      'building_8',
                      'building_9',
                      'building_10',
                      'building_39',
                      'building_50',
                      'building_52',
                      'building_56'
                  )
                  AND building_class IN (
                      'bank',
                      'construction_plant',
                      'spaceport',
                      'bunker',
                      'research_center',
                      'design_bureau',
                      'converter',
                      'recycling',
                      'sensor',
                      'portal'
                  );
                """);
        }
    }
}
