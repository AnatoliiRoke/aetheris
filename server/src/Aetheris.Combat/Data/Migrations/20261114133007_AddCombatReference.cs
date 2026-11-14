using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Combat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCombatReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "behavior",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    logic_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_behavior", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "target_rule",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    logic_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_target_rule", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_behavior_code",
                table: "behavior",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_behavior_logic_code",
                table: "behavior",
                column: "logic_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_target_rule_code",
                table: "target_rule",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_target_rule_logic_code",
                table: "target_rule",
                column: "logic_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "behavior");

            migrationBuilder.DropTable(
                name: "target_rule");
        }
    }
}
