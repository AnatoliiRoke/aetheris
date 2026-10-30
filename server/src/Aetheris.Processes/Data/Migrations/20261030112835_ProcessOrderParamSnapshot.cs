using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Processes.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcessOrderParamSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_param_snapshot",
                table: "process_order",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "process_order_param",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    process_order_id = table.Column<long>(type: "bigint", nullable: false),
                    process_param_id = table.Column<int>(type: "integer", nullable: false),
                    param_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    level_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    level_target_id = table.Column<int>(type: "integer", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    value_max = table.Column<decimal>(type: "numeric", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_process_order_param", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_order_param_process_order_process_order_id",
                        column: x => x.process_order_id,
                        principalTable: "process_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_process_order_param_process_order_id",
                table: "process_order_param",
                column: "process_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_order_param");

            migrationBuilder.DropColumn(
                name: "has_param_snapshot",
                table: "process_order");
        }
    }
}
