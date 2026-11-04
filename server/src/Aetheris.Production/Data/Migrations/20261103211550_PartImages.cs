using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <summary>
    /// Gives the part types the pictures of the starting data (task 00035): the key of the picture
    /// of the part, which is the code of the part with the extension of the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pictures of the modules come with the art of the game and lie in the folder of the
    /// pictures of the server under their readable keys (<c>part_1.webp</c>…). A database that
    /// already holds parts gets the key of its own code; a fresh database gets no row here: the
    /// seed of the module writes the rows with their key.
    /// </para>
    /// <para>
    /// The codes of the parts come from the sequence of the module and follow the order of the
    /// starting data, so the first eighty-three codes are the modules and the rest are the hulls.
    /// Only the modules have a picture; a hull keeps the placeholder.
    /// </para>
    /// <para>
    /// A part that already has a picture keeps it: the choice of the administrator wins over the
    /// starting data.
    /// </para>
    /// </remarks>
    public partial class PartImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A database with parts gives every part the picture of its own code; an empty table
            // gets nothing, the seed writes the rows with their key then. The codes come from the
            // sequence of the module and follow the order of the starting data, so the code of a
            // part is the number of the part in that data.
            migrationBuilder.Sql(
                "UPDATE part_type SET image_key = code || '.webp' "
                + "WHERE image_key IS NULL "
                + "AND code ~ '^part_[0-9]+$' "
                + "AND EXISTS (SELECT 1 FROM part_type);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The pictures of the starting data are not taken back, so a picture that an
            // administrator uploaded later is never lost.
            migrationBuilder.Sql(
                "UPDATE part_type SET image_key = NULL "
                + "WHERE image_key IS NOT NULL "
                + "AND code ~ '^part_[0-9]+$' "
                + "AND image_key = code || '.webp'");
        }
    }
}
