using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Research.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialResearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "research_queue_state",
                columns: table => new
                {
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    extra_queue_slots = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_research_queue_state", x => x.player_id);
                    table.CheckConstraint("ck_research_queue_state_extra_slots", "extra_queue_slots >= 0");
                });

            migrationBuilder.CreateTable(
                name: "technology",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    branch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    max_level = table.Column<int>(type: "integer", nullable: false),
                    research_center_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technology", x => x.id);
                    table.CheckConstraint("ck_technology_max_level", "max_level >= 1");
                    table.CheckConstraint("ck_technology_research_center_level", "research_center_level >= 1");
                });

            migrationBuilder.CreateTable(
                name: "player_technology",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    technology_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_technology", x => x.id);
                    table.CheckConstraint("ck_player_technology_level", "level >= 0");
                    table.ForeignKey(
                        name: "fk_player_technology_technology_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technology",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_queue_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    technology_id = table.Column<int>(type: "integer", nullable: false),
                    target_level = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finishes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_research_queue_item", x => x.id);
                    table.CheckConstraint("ck_research_queue_item_position", "position >= 1");
                    table.CheckConstraint("ck_research_queue_item_target_level", "target_level >= 1");
                    table.ForeignKey(
                        name: "fk_research_queue_item_technology_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technology",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technology_level",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    technology_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technology_level", x => x.id);
                    table.CheckConstraint("ck_technology_level_duration", "duration_seconds >= 0");
                    table.CheckConstraint("ck_technology_level_level", "level >= 1");
                    table.ForeignKey(
                        name: "fk_technology_level_technology_technology_id",
                        column: x => x.technology_id,
                        principalTable: "technology",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_queue_cost",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    research_queue_item_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_research_queue_cost", x => x.id);
                    table.CheckConstraint("ck_research_queue_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_research_queue_cost_research_queue_item_research_queue_item~",
                        column: x => x.research_queue_item_id,
                        principalTable: "research_queue_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technology_level_cost",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    technology_level_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technology_level_cost", x => x.id);
                    table.CheckConstraint("ck_technology_level_cost_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_technology_level_cost_technology_level_technology_level_id",
                        column: x => x.technology_level_id,
                        principalTable: "technology_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technology_level_effect",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    technology_level_id = table.Column<int>(type: "integer", nullable: false),
                    effect_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    unlock_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    unlock_target_id = table.Column<int>(type: "integer", nullable: true),
                    value = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technology_level_effect", x => x.id);
                    table.ForeignKey(
                        name: "fk_technology_level_effect_technology_level_technology_level_id",
                        column: x => x.technology_level_id,
                        principalTable: "technology_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_player_technology_player_id_technology_id",
                table: "player_technology",
                columns: new[] { "player_id", "technology_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_technology_technology_id",
                table: "player_technology",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_queue_cost_research_queue_item_id_resource_type_id",
                table: "research_queue_cost",
                columns: new[] { "research_queue_item_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_research_queue_item_planet_id",
                table: "research_queue_item",
                column: "planet_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_queue_item_player_id",
                table: "research_queue_item",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_queue_item_player_id_position",
                table: "research_queue_item",
                columns: new[] { "player_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_research_queue_item_technology_id",
                table: "research_queue_item",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "ix_technology_branch_sort_order",
                table: "technology",
                columns: new[] { "branch", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_technology_code",
                table: "technology",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technology_level_technology_id_level",
                table: "technology_level",
                columns: new[] { "technology_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technology_level_cost_technology_level_id_resource_type_id",
                table: "technology_level_cost",
                columns: new[] { "technology_level_id", "resource_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technology_level_effect_technology_level_id",
                table: "technology_level_effect",
                column: "technology_level_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_technology");

            migrationBuilder.DropTable(
                name: "research_queue_cost");

            migrationBuilder.DropTable(
                name: "research_queue_state");

            migrationBuilder.DropTable(
                name: "technology_level_cost");

            migrationBuilder.DropTable(
                name: "technology_level_effect");

            migrationBuilder.DropTable(
                name: "research_queue_item");

            migrationBuilder.DropTable(
                name: "technology_level");

            migrationBuilder.DropTable(
                name: "technology");
        }
    }
}
