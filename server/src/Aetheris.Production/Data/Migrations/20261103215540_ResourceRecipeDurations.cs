using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <summary>
    /// Gives the recipes of the resources the time of the starting data (task 00035): the recipes
    /// of the resources were all one flat value of 600 seconds, and every kind of the game data
    /// has its own time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The time of a recipe grows with what it makes: a recipe of a simple resource is quick, a
    /// recipe of a resource that needs another finished product takes longer, and the recipe of
    /// the energy strands is the slowest of them all.
    /// </para>
    /// <para>
    /// The recipes are found by the code of the resource they make, because the codes of the
    /// recipes come from the sequence of the module and follow the order of the starting data,
    /// while the codes of the resources do not change. Every recipe of a resource is written, so
    /// the time of the starting data is the same in every database.
    /// </para>
    /// </remarks>
    public partial class ResourceRecipeDurations : Migration
    {
        /// <summary>
        /// The time of every recipe of a resource, by the code of the resource it makes.
        /// </summary>
        private static readonly Dictionary<string, int> Durations = new(StringComparer.Ordinal)
        {
            ["resource_20"] = 110,
            ["resource_21"] = 120,
            ["resource_22"] = 130,
            ["resource_23"] = 130,
            ["resource_24"] = 300,
            ["resource_25"] = 140,
            ["resource_26"] = 120,
            ["resource_16"] = 110,
        };

        /// <summary>
        /// The flat time the recipes of the resources had in the starting data.
        /// </summary>
        private const int FlatDuration = 600;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The resource of the recipe is the stable name of the row, so the recipe is found
            // by what it makes. Every recipe of a resource is written, so the time of the starting
            // data is the same in every database.
            migrationBuilder.Sql(
                "UPDATE recipe SET duration_seconds = m.duration_seconds "
                + "FROM (VALUES "
                + string.Join(", ", Durations.Select(entry => $"({NeutralCodeSql.Quote(entry.Key)}, {entry.Value})"))
                + ") AS m(resource_code, duration_seconds) "
                + "JOIN resource_type AS rt ON rt.code = m.resource_code "
                + "WHERE recipe.resource_type_id = rt.id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The time of the starting data is not taken back, so a time that an administrator
            // changed is never lost.
            migrationBuilder.Sql(
                "UPDATE recipe SET duration_seconds = " + FlatDuration + " "
                + "WHERE duration_seconds IN (110, 120, 130, 140, 300) "
                + "AND resource_type_id IS NOT NULL;");
        }
    }
}
