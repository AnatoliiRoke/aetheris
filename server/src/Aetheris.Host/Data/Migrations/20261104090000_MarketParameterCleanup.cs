using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <summary>
    /// Removes a parameter of the market that is not read any more (code audit of 04.11.2026).
    /// </summary>
    /// <remarks>
    /// The market module named an entry of its reference data by a code in this parameter, and
    /// the code went stale when the reference data was numbered anew. The entry is marked by a
    /// flag of the reference data instead, so the parameter is removed.
    /// </remarks>
    public partial class MarketParameterCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The only parameter of the delivery whose key ends with "code": the code of the entry
            // of the fee. The core names no game entity, so the key is matched by its shape.
            migrationBuilder.Sql(
                """
                DELETE FROM config_parameter
                WHERE key LIKE 'market.delivery.energy\_%\_code';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The parameter is not read by any version that has the flag of the reference data, so
            // there is nothing to bring back.
        }
    }
}
