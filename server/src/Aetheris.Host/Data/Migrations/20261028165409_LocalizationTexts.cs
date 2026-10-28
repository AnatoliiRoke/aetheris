using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalizationTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "code_sequence",
                columns: table => new
                {
                    code_group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_sequence", x => x.code_group);
                    table.CheckConstraint("ck_code_sequence_last_number", "last_number >= 0");
                });

            migrationBuilder.CreateTable(
                name: "localization_text",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_localization_texts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_localization_text_key_language",
                table: "localization_text",
                columns: new[] { "key", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_localization_text_language",
                table: "localization_text",
                column: "language");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_sequence");

            migrationBuilder.DropTable(
                name: "localization_text");
        }
    }
}
