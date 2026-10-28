using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// Gives the resources and the resource groups a version against parallel changes and the
    /// archive instead of the delete (task 00023, decision 5).
    /// </summary>
    /// <remarks>
    /// Every existing row starts at version 1 and stays active.
    /// </remarks>
    public partial class ReferenceArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "resource_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "resource_type",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "resource_group",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "resource_group",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "resource_type");

            migrationBuilder.DropColumn(
                name: "version",
                table: "resource_type");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "resource_group");

            migrationBuilder.DropColumn(
                name: "version",
                table: "resource_group");
        }
    }
}
