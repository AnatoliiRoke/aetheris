using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Drops the table of the requirements of the bodies module, because the table moved to the
    /// common requirements module (task 00012, decision 3).
    /// </summary>
    /// <remarks>
    /// The rows of the requirements were copied into the table of the common requirements
    /// module by the migration of that module, which runs before this one. The identifiers and
    /// every link between the rows stay as they were.
    /// </remarks>
    public partial class MoveRequirementsToCommonModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The table of this module was renamed by the migration of the common requirements
            // module, which copied its rows into the table of that module.
            migrationBuilder.DropTable(
                name: "requirement_bodies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The table of this module was renamed by the migration of the common requirements
            // module; it takes its name back.
            migrationBuilder.Sql(
                """
                ALTER TABLE requirement_bodies RENAME TO requirement;
                ALTER TABLE requirement RENAME CONSTRAINT pk_requirement_bodies TO pk_requirement;
                """);
        }
    }
}
