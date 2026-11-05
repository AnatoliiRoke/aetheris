using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacteristicKindsAndDamageKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The kind of the damage of a part was written under a key of its own in the starting
            // data, and the program reads the key of the attribute, so the rows of the starting
            // data take the key the program reads.
            migrationBuilder.Sql("UPDATE part_attribute SET attribute_key = 'damage_kind' WHERE attribute_key = 'damage_type';");

            // The characteristics the game rules read take their kind: the thrust of the drives,
            // the damage of the weapons and the capacity of the shields. The rows of the
            // starting data take their numbers in the order of the seed file.
            migrationBuilder.Sql(
                """
                UPDATE characteristic SET kind = 'thrust' WHERE id = 1;
                UPDATE characteristic SET kind = 'damage' WHERE id = 5;
                UPDATE characteristic SET kind = 'shield_capacity' WHERE id = 10;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE part_attribute SET attribute_key = 'damage_type' WHERE attribute_key = 'damage_kind';");

            migrationBuilder.Sql(
                """
                UPDATE characteristic SET kind = NULL WHERE kind IN ('thrust', 'damage', 'shield_capacity');
                """);
        }
    }
}
