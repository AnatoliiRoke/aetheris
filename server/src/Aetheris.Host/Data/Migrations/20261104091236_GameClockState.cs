using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class GameClockState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_clock_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    real_anchor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    game_anchor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    multiplier = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_clock_states", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_clock_state");
        }
    }
}
