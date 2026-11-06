using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Requirements.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "requirement",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subject_type = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<int>(type: "integer", nullable: false),
                    subject_level = table.Column<int>(type: "integer", nullable: false),
                    requirement_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    target_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requirement", x => x.id);
                    table.CheckConstraint("ck_requirement_subject_level", "subject_level >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_requirement_subject_type_subject_id_subject_level",
                table: "requirement",
                columns: new[] { "subject_type", "subject_id", "subject_level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "requirement");
        }
    }
}
