using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartAccuracy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "default_power_priority",
                table: "part_group",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "part_accuracy",
                columns: table => new
                {
                    part_type_id = table.Column<int>(type: "integer", nullable: false),
                    target_part_group_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_part_accuracy", x => new { x.part_type_id, x.target_part_group_id });
                    table.CheckConstraint("ck_part_accuracy_value", "value >= 0 AND value <= 1");
                    table.ForeignKey(
                        name: "fk_part_accuracy_part_group_target_part_group_id",
                        column: x => x.target_part_group_id,
                        principalTable: "part_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_part_accuracy_part_type_part_type_id",
                        column: x => x.part_type_id,
                        principalTable: "part_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_part_accuracy_target",
                table: "part_accuracy",
                column: "target_part_group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "part_accuracy");

            migrationBuilder.DropColumn(
                name: "default_power_priority",
                table: "part_group");
        }
    }
}
