using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Research.Data.Migrations
{
    /// <summary>
    /// Gives every row of the learned technologies the body its research ran on, so a body has
    /// its own level of a technology (task 00028).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A technology is researched from a body, so the level of a technology belongs to the body
    /// that researched it. The known levels of a player go to the body of the player with the
    /// smallest identifier; a player without a body has no technology.
    /// </para>
    /// <para>
    /// The level of the civilization is the largest level of its bodies, so the subjects a
    /// technology opens are open for the whole civilization once any of its bodies has researched
    /// it.
    /// </para>
    /// </remarks>
    public partial class PlanetTechnologyLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "planet_id",
                table: "player_technology",
                type: "bigint",
                nullable: true);

            // The knowledge of a player goes to the body he owns with the smallest identifier:
            // the research of a player who had bodies ran on one of them.
            migrationBuilder.Sql(
                """
                UPDATE player_technology pt
                SET planet_id = owned.planet_id
                FROM (
                    SELECT DISTINCT ON (owner_player_id) owner_player_id, id AS planet_id
                    FROM planet
                    WHERE owner_player_id IS NOT NULL
                    ORDER BY owner_player_id, id
                ) AS owned
                WHERE owned.owner_player_id = pt.player_id;
                """);

            // A technology is learned only by a research that runs on a body, and bodies are never
            // lost, so a player without a body has no technology. A row without a body could
            // only be a leftover, so it is removed instead of being tied to a body that does not
            // exist (code audit of 04.11.2026, decision of the author).
            migrationBuilder.Sql(
                """
                DELETE FROM player_technology
                WHERE planet_id IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "planet_id",
                table: "player_technology",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "ix_player_technology_player_id_technology_id",
                table: "player_technology");

            migrationBuilder.CreateIndex(
                name: "ix_player_technology_player_id_planet_id_technology_id",
                table: "player_technology",
                columns: new[] { "player_id", "planet_id", "technology_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_technology_player_id_technology_id",
                table: "player_technology",
                columns: new[] { "player_id", "technology_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_player_technology_player_id_planet_id_technology_id",
                table: "player_technology");

            migrationBuilder.DropIndex(
                name: "ix_player_technology_player_id_technology_id",
                table: "player_technology");

            // The knowledge of a body goes back to the civilization: the largest level of the
            // bodies of the player is the level of the civilization.
            migrationBuilder.Sql(
                """
                DELETE FROM player_technology pt
                USING player_technology best
                WHERE pt.player_id = best.player_id
                  AND pt.technology_id = best.technology_id
                  AND (pt.level < best.level OR (pt.level = best.level AND pt.id > best.id));
                """);

            migrationBuilder.DropColumn(
                name: "planet_id",
                table: "player_technology");

            migrationBuilder.CreateIndex(
                name: "ix_player_technology_player_id_technology_id",
                table: "player_technology",
                columns: new[] { "player_id", "technology_id" },
                unique: true);
        }
    }
}
