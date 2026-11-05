using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class FuelPartsEnergy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The drives and the life support run on their own fuel, so the power of the
            // reactors pays for the modules of the ship and not for them (decision 9: "the
            // reactor limits the modules"). The rows of the starting data take their numbers in
            // the order of the seed file, so the parts that run on fuel are known by their
            // numbers: the six drives and the board computer.
            migrationBuilder.Sql(
                """
                UPDATE part_characteristic_value
                SET number_value = 0
                WHERE characteristic_id = (SELECT id FROM characteristic WHERE kind = 'energy_use')
                  AND part_type_id IN (1, 2, 3, 4, 5, 6, 64);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The starting values of the parts that run on fuel come back.
            migrationBuilder.Sql(
                """
                UPDATE part_characteristic_value
                SET number_value = CASE part_type_id
                    WHEN 1 THEN 100 WHEN 2 THEN 110 WHEN 3 THEN 121
                    WHEN 4 THEN 133.1 WHEN 5 THEN 146.41 WHEN 6 THEN 161.051
                    WHEN 64 THEN 110
                    ELSE number_value END
                WHERE characteristic_id = (SELECT id FROM characteristic WHERE kind = 'energy_use')
                  AND part_type_id IN (1, 2, 3, 4, 5, 6, 64);
                """);
        }
    }
}
