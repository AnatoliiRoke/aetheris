using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Processes.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcessOrderLevelsAndNoCrewState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_population_state");

            migrationBuilder.AddColumn<int>(
                name: "building_level",
                table: "process_order",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_order_technology",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    process_order_id = table.Column<long>(type: "bigint", nullable: false),
                    technology_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_order_technology", x => x.id);
                    table.CheckConstraint("ck_process_order_technology_level", "level >= 0");
                    table.ForeignKey(
                        name: "fk_process_order_technology_process_order_process_order_id",
                        column: x => x.process_order_id,
                        principalTable: "process_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_process_order_technology_process_order_id",
                table: "process_order_technology",
                column: "process_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_order_technology");

            migrationBuilder.DropColumn(
                name: "building_level",
                table: "process_order");

            migrationBuilder.CreateTable(
                name: "process_population_state",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    crew_pending = table.Column<long>(type: "bigint", nullable: false),
                    is_over_limit = table.Column<bool>(type: "boolean", nullable: false),
                    over_limit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    planet_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_population_state", x => x.id);
                    table.CheckConstraint("ck_process_population_state_crew", "crew_pending >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_process_population_state_planet_id",
                table: "process_population_state",
                column: "planet_id",
                unique: true);
        }
    }
}
