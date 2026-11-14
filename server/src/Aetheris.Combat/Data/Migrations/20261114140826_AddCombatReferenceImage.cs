using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Combat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCombatReferenceImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "target_rule",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                table: "behavior",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_key",
                table: "target_rule");

            migrationBuilder.DropColumn(
                name: "image_key",
                table: "behavior");
        }
    }
}
