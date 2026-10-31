using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeightAndCharacteristics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "weight",
                table: "part_type",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateTable(
                name: "characteristic",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    unit_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characteristic", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "part_characteristic_value",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    characteristic_id = table.Column<int>(type: "integer", nullable: false),
                    number_value = table.Column<decimal>(type: "numeric", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_characteristic_value", x => x.id);
                    table.ForeignKey(
                        name: "fk_part_characteristic_value_characteristic_characteristic_id",
                        column: x => x.characteristic_id,
                        principalTable: "characteristic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_part_characteristic_value_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "part_group_characteristic",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_group_id = table.Column<int>(type: "integer", nullable: false),
                    characteristic_id = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_group_characteristic", x => x.id);
                    table.ForeignKey(
                        name: "fk_part_group_characteristic_characteristic_characteristic_id",
                        column: x => x.characteristic_id,
                        principalTable: "characteristic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_part_group_characteristic_part_group_part_group_id",
                        column: x => x.part_group_id,
                        principalTable: "part_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "part_characteristic_level_value",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    part_characteristic_value_id = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    number_value = table.Column<decimal>(type: "numeric", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_characteristic_level_value", x => x.id);
                    table.CheckConstraint("ck_part_characteristic_level_value_level", "level >= 1");
                    table.ForeignKey(
                        name: "fk_part_characteristic_level_value_part_characteristic_value_p~",
                        column: x => x.part_characteristic_value_id,
                        principalTable: "part_characteristic_value",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_part_type_weight",
                table: "part_type",
                sql: "weight >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_characteristic_code",
                table: "characteristic",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_characteristic_level_value_part_characteristic_value_i~",
                table: "part_characteristic_level_value",
                columns: new[] { "part_characteristic_value_id", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_characteristic_value_characteristic_id",
                table: "part_characteristic_value",
                column: "characteristic_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_characteristic_value_part_type_id_characteristic_id",
                table: "part_characteristic_value",
                columns: new[] { "part_type_id", "characteristic_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_group_characteristic_characteristic_id",
                table: "part_group_characteristic",
                column: "characteristic_id");

            migrationBuilder.CreateIndex(
                name: "ix_part_group_characteristic_part_group_id_characteristic_id",
                table: "part_group_characteristic",
                columns: new[] { "part_group_id", "characteristic_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_part_group_characteristic_part_group_id_sort_order",
                table: "part_group_characteristic",
                columns: new[] { "part_group_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_characteristic_level_value");

            migrationBuilder.DropTable(
                name: "part_group_characteristic");

            migrationBuilder.DropTable(
                name: "part_characteristic_value");

            migrationBuilder.DropTable(
                name: "characteristic");

            migrationBuilder.DropCheckConstraint(
                name: "ck_part_type_weight",
                table: "part_type");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "part_type");
        }
    }
}
