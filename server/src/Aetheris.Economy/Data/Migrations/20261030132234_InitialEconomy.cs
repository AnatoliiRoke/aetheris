using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Economy.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialEconomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "civilization_balance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "text", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_civilization_balance", x => x.Id);
                    table.CheckConstraint("ck_civilization_balance_amount", "amount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "planet_population",
                columns: table => new
                {
                    PlanetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    owner_player_id = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planet_population", x => x.PlanetId);
                    table.CheckConstraint("ck_planet_population_amount", "amount >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_civilization_balance_player_id_resource_type_id",
                table: "civilization_balance",
                columns: new[] { "player_id", "resource_type_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "civilization_balance");

            migrationBuilder.DropTable(
                name: "planet_population");
        }
    }
}
