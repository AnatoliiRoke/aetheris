using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Gives the resource types the flag of the resource the delivery through a portal is paid
    /// with (code audit of 04.11.2026).
    /// </summary>
    /// <remarks>
    /// The market looked the resource up by a code in the configuration, and the code went stale
    /// when the resources were numbered anew. The flag replaces the code: a database that already
    /// holds the energy rods of the starting data gets the flag on them; a fresh database gets it
    /// from the seed.
    /// </remarks>
    public partial class ResourcePaysPortalDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "pays_portal_delivery",
                table: "resource_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The energy rods of the starting data pay the delivery; a database where no resource
            // has the flag yet gets it on them.
            migrationBuilder.Sql(
                """
                UPDATE resource_type
                SET pays_portal_delivery = TRUE
                WHERE code = 'resource_24'
                  AND NOT EXISTS (SELECT 1 FROM resource_type WHERE pays_portal_delivery);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pays_portal_delivery",
                table: "resource_type");
        }
    }
}
