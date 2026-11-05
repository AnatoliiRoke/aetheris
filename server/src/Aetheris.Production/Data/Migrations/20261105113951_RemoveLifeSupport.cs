using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLifeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The life support of the ship does not exist: there is no such rule in the game, so
            // the board computer is an ordinary module that lowers the crew the ship needs and
            // gives a bonus to the push, the shields and the scan speed (the decision of the
            // author, 05.11.2026).
            migrationBuilder.Sql("DELETE FROM part_attribute WHERE attribute_key = 'component' AND code_value = 'life_support';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO part_attribute (part_type_id, attribute_key, code_value)
                SELECT id, 'component', 'life_support' FROM part_type WHERE code = 'part_m_64';
                """);
        }
    }
}
