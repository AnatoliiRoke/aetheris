using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <summary>
    /// Turns the test stub of the radar off (code audit of 04.11.2026, decision of the author).
    /// </summary>
    /// <remarks>
    /// While the stub was on, every body counted as having a strong radar and every player saw
    /// the whole sector. The real radar of a body decides now; the stub can still be turned on in
    /// the admin panel for a check. Only the value the seed wrote is changed, so a choice of the
    /// administrator stays.
    /// </remarks>
    public partial class RadarStubOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = 'false', version = version + 1, updated_at = NOW(), updated_by = 'migration'
                WHERE key = 'world.radar.stub_enabled' AND value = 'true' AND updated_by = 'seed';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = 'true', version = version + 1, updated_at = NOW(), updated_by = 'migration'
                WHERE key = 'world.radar.stub_enabled' AND value = 'false' AND updated_by = 'migration';
                """);
        }
    }
}
