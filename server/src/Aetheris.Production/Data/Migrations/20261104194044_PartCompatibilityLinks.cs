using Aetheris.World.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartCompatibilityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "part_hull_compatibility",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    hull_part_type_id = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_hull_compatibility", x => x.id);
                    table.ForeignKey(
                        name: "fk_part_hull_compatibility_part_type_hull_part_type_id",
                        column: x => x.hull_part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_part_hull_compatibility_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "part_race_compatibility",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    race_id = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_race_compatibility", x => x.id);
                    table.ForeignKey(
                        name: "fk_part_race_compatibility_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_part_hull_compatibility_hull_part_type_id",
                table: "part_hull_compatibility",
                column: "hull_part_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_hull_compatibility_part_type_id_hull_part_type_id",
                table: "part_hull_compatibility",
                columns: new[] { "part_type_id", "hull_part_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_race_compatibility_part_type_id_race_id",
                table: "part_race_compatibility",
                columns: new[] { "part_type_id", "race_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_race_compatibility_race_id",
                table: "part_race_compatibility",
                column: "race_id");

            // The links of the starting data go into the databases the seed has already filled
            // (task 00042). A part or a hull that the database does not have leaves the link out,
            // and a link that is already there is kept, so a change of the admin panel stays.
            migrationBuilder.Sql(
                "INSERT INTO part_hull_compatibility (part_type_id, hull_part_type_id, is_archived, version) "
                + "SELECT p.id, h.id, false, 1 FROM (VALUES "
                + string.Join(", ", PartCompatibilitySeed.Hulls.Select(link => $"({NeutralCodeSql.Quote(link.Part)}, {NeutralCodeSql.Quote(link.Hull)})"))
                + ") AS m(part_code, hull_code) "
                + "JOIN part_type AS p ON p.code = m.part_code "
                + "JOIN part_type AS h ON h.code = m.hull_code "
                + "ON CONFLICT (part_type_id, hull_part_type_id) DO NOTHING;");

            migrationBuilder.Sql(
                "INSERT INTO part_race_compatibility (part_type_id, race_id, is_archived, version) "
                + "SELECT p.id, r.id, false, 1 FROM (VALUES "
                + string.Join(", ", PartCompatibilitySeed.Races.Select(link => $"({NeutralCodeSql.Quote(link.Part)}, {NeutralCodeSql.Quote(link.Race)})"))
                + ") AS m(part_code, race_code) "
                + "JOIN part_type AS p ON p.code = m.part_code "
                + "JOIN race AS r ON r.code = m.race_code "
                + "ON CONFLICT (part_type_id, race_id) DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_hull_compatibility");

            migrationBuilder.DropTable(
                name: "part_race_compatibility");
        }
    }
}
