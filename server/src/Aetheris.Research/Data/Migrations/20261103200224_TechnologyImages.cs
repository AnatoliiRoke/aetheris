using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Research.Data.Migrations
{
    /// <summary>
    /// Gives the technologies the pictures of the starting data (task 00035): the key of the
    /// picture of the technology, which is the code of the technology with the extension of the
    /// file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pictures of the technologies come with the art of the game and lie in the folder of the
    /// pictures of the server under their readable keys (<c>tech_1.webp</c>…). A database that
    /// already holds technologies gets the key of its own code; a fresh database gets no row
    /// here: the seed of the module writes the rows with their key.
    /// </para>
    /// <para>
    /// A technology that already has a picture keeps it: the choice of the administrator wins
    /// over the starting data.
    /// </para>
    /// </remarks>
    public partial class TechnologyImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A database with technologies gives every technology the picture of its own code; an
            // empty table gets nothing, the seed writes the rows with their key then.
            migrationBuilder.Sql(
                "UPDATE technology SET image_key = code || '.webp' "
                + "WHERE image_key IS NULL AND code ~ '^tech_[0-9]+$' "
                + "AND EXISTS (SELECT 1 FROM technology);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The pictures of the starting data are not taken back, so a picture that an
            // administrator uploaded later is never lost.
            migrationBuilder.Sql(
                "UPDATE technology SET image_key = NULL "
                + "WHERE image_key IS NOT NULL AND code ~ '^tech_[0-9]+$' "
                + "AND image_key = code || '.webp'");
        }
    }
}
