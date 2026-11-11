using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_extraction",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    place_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: true),
                    resource_group_id = table.Column<int>(type: "integer", nullable: true),
                    reduces_mass = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_extraction", x => x.id);
                    table.CheckConstraint("ck_part_extraction_target", "NOT (resource_id IS NOT NULL AND resource_group_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_part_extraction_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_part_extraction_part_type_id",
                table: "part_extraction",
                column: "part_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_extraction_resource_group_id",
                table: "part_extraction",
                column: "resource_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_extraction_resource_id",
                table: "part_extraction",
                column: "resource_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_extraction");
        }
    }
}
