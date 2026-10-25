using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <summary>
    /// Removes the seeded parameters that do not belong to the core.
    /// </summary>
    /// <remarks>
    /// The core seeds only neutral <c>test.*</c> parameters and the <c>security.*</c>
    /// parameters of its own mechanisms (task 00001, section 4). An earlier build seeded
    /// feature parameters as well. Only rows that the seed wrote and nobody changed since
    /// are removed, so a value set by an administrator is never lost.
    /// </remarks>
    public partial class RetireNonCoreSeedParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM config_parameter
                WHERE updated_by = 'seed'
                  AND version = 1
                  AND key NOT LIKE 'test.%'
                  AND key NOT LIKE 'security.%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed rows were seed values of features that are not part of the core.
            // They are not restored: the feature that needs them seeds them again.
        }
    }
}
