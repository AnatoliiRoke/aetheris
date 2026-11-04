using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Gives the resource types the pictures of the starting data (task 00035): the key of the
    /// picture of the resource, which is the code of the resource with the extension of the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pictures of the resources come with the art of the game and lie in the folder of the
    /// pictures of the server under their readable keys (<c>resource_1.webp</c>…). A database that
    /// already holds resources gets the key of its own code; a fresh database gets no row here:
    /// the seed of the module writes the rows with their key.
    /// </para>
    /// <para>
    /// A resource that already has a picture keeps it: the choice of the administrator wins over
    /// the starting data.
    /// </para>
    /// </remarks>
    public partial class ResourceImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A database with resources gives every resource the picture of its own code; an empty
            // table gets nothing, the seed writes the rows with their key then.
            migrationBuilder.Sql(
                "UPDATE resource_type SET image_key = code || '.webp' "
                + "WHERE image_key IS NULL "
                + "AND code ~ '^resource_[0-9]+$' "
                + "AND EXISTS (SELECT 1 FROM resource_type);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The pictures of the starting data are not taken back, so a picture that an
            // administrator uploaded later is never lost.
            migrationBuilder.Sql(
                "UPDATE resource_type SET image_key = NULL "
                + "WHERE image_key IS NOT NULL "
                + "AND code ~ '^resource_[0-9]+$' "
                + "AND image_key = code || '.webp'");
        }
    }
}
