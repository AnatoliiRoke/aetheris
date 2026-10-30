using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// The mark of a terraformed body: a body is terraformed once (task 00014, section 9). A body
    /// whose deposits a device already made rich, as the journal of the deposits shows, is
    /// marked at once, so it is not terraformed a second time.
    /// </summary>
    public partial class Terraformed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_terraformed",
                table: "planet",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE planet SET is_terraformed = TRUE, version = version + 1
                WHERE id IN (SELECT DISTINCT planet_id FROM deposit_change WHERE reason = 'terraforming');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_terraformed",
                table: "planet");
        }
    }
}
