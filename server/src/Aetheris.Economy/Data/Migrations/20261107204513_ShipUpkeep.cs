using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Economy.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShipUpkeep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ship_upkeep",
                columns: table => new
                {
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    per_hour = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ship_upkeep", x => x.player_id);
                    table.CheckConstraint("ck_ship_upkeep_per_hour", "per_hour >= 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ship_upkeep");
        }
    }
}
