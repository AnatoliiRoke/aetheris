using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Mail.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mail_category",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mail_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mail_draft",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    receiver_player_id = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mail_draft", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mail_message",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sender_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    receiver_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    text_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    text_parameters = table.Column<string>(type: "text", nullable: true),
                    kind_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    system_kind_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mail_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_mail_message_mail_category_category_id",
                        column: x => x.category_id,
                        principalTable: "mail_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mail_box",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    message_id = table.Column<long>(type: "bigint", nullable: false),
                    folder_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    is_saved = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mail_box", x => x.id);
                    table.ForeignKey(
                        name: "fk_mail_box_mail_message_message_id",
                        column: x => x.message_id,
                        principalTable: "mail_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mail_complaint",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    message_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_reviewed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mail_complaint", x => x.id);
                    table.ForeignKey(
                        name: "fk_mail_complaint_mail_message_message_id",
                        column: x => x.message_id,
                        principalTable: "mail_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mail_box_message_id",
                table: "mail_box",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_mail_box_player_id_folder_code_created_at",
                table: "mail_box",
                columns: new[] { "player_id", "folder_code", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mail_box_player_id_is_deleted_deleted_at",
                table: "mail_box",
                columns: new[] { "player_id", "is_deleted", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mail_box_player_id_is_read",
                table: "mail_box",
                columns: new[] { "player_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_mail_category_code",
                table: "mail_category",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mail_category_sort_order_id",
                table: "mail_category",
                columns: new[] { "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_mail_complaint_is_reviewed_created_at",
                table: "mail_complaint",
                columns: new[] { "is_reviewed", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mail_complaint_message_id",
                table: "mail_complaint",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_mail_draft_player_id",
                table: "mail_draft",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_mail_draft_player_id_updated_at",
                table: "mail_draft",
                columns: new[] { "player_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mail_message_category_id",
                table: "mail_message",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_mail_message_created_at",
                table: "mail_message",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_mail_message_receiver_player_id",
                table: "mail_message",
                column: "receiver_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_mail_message_sender_player_id",
                table: "mail_message",
                column: "sender_player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mail_box");

            migrationBuilder.DropTable(
                name: "mail_complaint");

            migrationBuilder.DropTable(
                name: "mail_draft");

            migrationBuilder.DropTable(
                name: "mail_message");

            migrationBuilder.DropTable(
                name: "mail_category");
        }
    }
}
