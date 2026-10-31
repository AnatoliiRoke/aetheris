using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResourceAllowsDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allows_debt",
                table: "resource_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allows_debt",
                table: "resource_type");
        }
    }
}
