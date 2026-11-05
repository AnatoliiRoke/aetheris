using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacteristicKindDrivePush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The kind of the characteristic of the drive is a code of the program, and a code that
            // spells the name of the characteristic does not belong in the code (task 00027, criterion
            // 5), so the rows that carry the old kind take the new one.
            migrationBuilder.Sql("UPDATE characteristic SET kind = 'drive_push' WHERE kind = 'thrust';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE characteristic SET kind = 'thrust' WHERE kind = 'drive_push';");
        }
    }
}
