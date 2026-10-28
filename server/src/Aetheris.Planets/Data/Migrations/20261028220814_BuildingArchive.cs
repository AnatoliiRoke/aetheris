using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Gives the building types a version against parallel changes, the archive instead of the
    /// delete and a picture (task 00023, section 4.4).
    /// </summary>
    /// <remarks>
    /// Every existing type starts at version 1, stays active and has no picture.
    /// </remarks>
    public partial class BuildingArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "building_type",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "building_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "building_type",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_key",
                table: "building_type");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "building_type");

            migrationBuilder.DropColumn(
                name: "version",
                table: "building_type");
        }
    }
}
