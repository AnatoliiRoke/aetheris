using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Adds the class of a building type: what the building is for (task 00012, decision 1).
    /// </summary>
    /// <remarks>
    /// The class is a logical code, so it does not depend on the neutral code of the row and
    /// survives a change of the codes. The research center is the only building with a class
    /// for now; the migration finds it by the name of the type, because the neutral code of a
    /// row is given by the server and may differ between databases.
    /// </remarks>
    public partial class BuildingClass : Migration
    {
        /// <summary>
        /// The name of the research center, in the language of the texts of the database.
        /// </summary>
        private const string ResearchCenterName = "Науковий центр";

        /// <summary>
        /// The class of the research center.
        /// </summary>
        private const string ResearchCenterClass = "research_center";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "building_class",
                table: "building_type",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_building_type_building_class",
                table: "building_type",
                column: "building_class");

            // The research center of an existing database gets its class. The name is read from
            // the texts of the row, so the migration does not name a neutral code.
            migrationBuilder.Sql(
                """
                UPDATE building_type
                SET building_class = 'research_center'
                WHERE name_key IN (
                    SELECT key FROM localization_text
                    WHERE language = 'uk' AND text = 'Науковий центр'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_building_type_building_class",
                table: "building_type");

            migrationBuilder.DropColumn(
                name: "building_class",
                table: "building_type");
        }
    }
}
