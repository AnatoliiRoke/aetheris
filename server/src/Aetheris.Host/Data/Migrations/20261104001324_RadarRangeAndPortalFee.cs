using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <summary>
    /// Gives the radar the range of the author's answer and takes the parameters of the delivery
    /// fee by the distance away: the fee and the time of a delivery come from the portal now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The range of a radar is its level times the range of a level, and the share of the radar
    /// is taken off that range. The highest level of the radar has a share of one, so the range of
    /// the highest level is twice the range of the level times ten. The range of a level is
    /// therefore fifteen, so the highest level covers thirty per cent of the sector.
    /// </para>
    /// <para>
    /// The test stub gives every body a radar of one level, so its level is a hundred: the area
    /// the stub opens around a body stays the same as it was. Only the values the seed wrote and
    /// nobody changed since are moved, so a value set by an administrator is never lost.
    /// </para>
    /// </remarks>
    public partial class RadarRangeAndPortalFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The range of a level of the radar and the level of the test stub.
            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = '15', version = version + 1, updated_at = NOW(), updated_by = 'migration'
                WHERE key = 'world.radar.range_per_level' AND value = '150' AND version = 1 AND updated_by = 'seed';
                """);

            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = '100', version = version + 1, updated_at = NOW(), updated_by = 'migration'
                WHERE key = 'world.radar.stub_level' AND value = '10' AND version = 1 AND updated_by = 'seed';
                """);

            // The parameters of the delivery by the distance are not read any more: the fee and
            // the time of a delivery come from the portal of the body that receives the goods.
            migrationBuilder.Sql(
                """
                DELETE FROM config_parameter
                WHERE key IN (
                          'market.delivery.base_seconds',
                          'market.delivery.seconds_per_distance',
                          'market.delivery.energy_per_tonne_distance')
                  AND version = 1
                  AND updated_by = 'seed';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The range of a level of the radar and the level of the test stub.
            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = '150', version = version + 1, updated_at = NOW(), updated_by = 'migration'
                WHERE key = 'world.radar.range_per_level' AND value = '15' AND version = 1 AND updated_by = 'migration';
                """);

            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = '10', version = version + 1, updated_at = NOW(), updated_by = 'migration'
                WHERE key = 'world.radar.stub_level' AND value = '100' AND version = 1 AND updated_by = 'migration';
                """);

            // The parameters of the delivery by the distance come back with the values they had.
            migrationBuilder.Sql(
                """
                INSERT INTO config_parameter (key, value, version, updated_at, updated_by)
                VALUES
                    ('market.delivery.base_seconds', '60', 1, NOW(), 'migration'),
                    ('market.delivery.seconds_per_distance', '5', 1, NOW(), 'migration'),
                    ('market.delivery.energy_per_tonne_distance', '0.01', 1, NOW(), 'migration')
                ON CONFLICT (key) DO NOTHING;
                """);
        }
    }
}
