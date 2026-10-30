using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResourceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every resource that exists lies on a body, so the rows that are already in the
            // table get the scope of a body (task 00015).
            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "resource_type",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "planet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scope",
                table: "resource_type");
        }
    }
}
