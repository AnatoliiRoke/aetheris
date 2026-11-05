using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacteristicKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "characteristic",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // The characteristics of the starting data take their rows in the order of the seed
            // file, so the kinds of the characteristics the game rules read are known by the
            // number of the row. The seed writes the kind of every characteristic it adds, so
            // only the rows of the starting data are filled here.
            migrationBuilder.Sql(
                """
                UPDATE characteristic SET kind = 'energy_use' WHERE id = 3;
                UPDATE characteristic SET kind = 'power' WHERE id = 4;
                UPDATE characteristic SET kind = 'integrity' WHERE id = 9;
                UPDATE characteristic SET kind = 'crew' WHERE id = 23;
                UPDATE characteristic SET kind = 'hull_capacity' WHERE id = 39;
                UPDATE characteristic SET kind = 'hull_slots' WHERE id = 40;
                UPDATE characteristic SET kind = 'hull_race_slot' WHERE id = 41;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "characteristic");
        }
    }
}
