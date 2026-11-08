using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class AmmunitionCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_ammunition_compatibility",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    ammunition_part_type_id = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_ammunition_compatibility", x => x.id);
                    table.ForeignKey(
                        name: "fk_part_ammunition_compatibility_part_type_ammunition_part_typ~",
                        column: x => x.ammunition_part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_part_ammunition_compatibility_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_part_ammunition_compatibility_ammunition_part_type_id",
                table: "part_ammunition_compatibility",
                column: "ammunition_part_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_ammunition_compatibility_part_type_id_ammunition_part_~",
                table: "part_ammunition_compatibility",
                columns: new[] { "part_type_id", "ammunition_part_type_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_ammunition_compatibility");
        }
    }
}
