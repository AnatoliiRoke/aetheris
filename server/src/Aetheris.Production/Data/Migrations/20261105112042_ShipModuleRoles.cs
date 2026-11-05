using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShipModuleRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The cargo holds and the module of transmission go on every hull, for the ships of war
            // carry cargo too, so they are of no role of their own (the layout of the author,
            // task 00042). The role of a module that is of the peace goes on the transport hulls,
            // because the transport hull is a peaceful hull (game-design/05, section 3.1б).
            migrationBuilder.Sql(
                """
                UPDATE part_attribute
                SET code_value = 'universal'
                WHERE attribute_key = 'role'
                  AND part_type_id IN (
                      SELECT id FROM part_type WHERE code IN ('part_66', 'part_67', 'part_69'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE part_attribute
                SET code_value = 'transport'
                WHERE attribute_key = 'role'
                  AND part_type_id IN (
                      SELECT id FROM part_type WHERE code IN ('part_66', 'part_67', 'part_69'));
                """);
        }
    }
}
