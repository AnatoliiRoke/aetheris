using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Task 00006 lowers the starting password length to eight characters and adds the
            // digit and upper-case rules. A value that still has the old starting value of the
            // seed follows; a value set by an administrator stays.
            migrationBuilder.Sql(
                """
                UPDATE config_parameter
                SET value = '8', version = version + 1, updated_at = now(), updated_by = 'migration'
                WHERE key = 'security.password.min_length'
                  AND value = '12'
                  AND updated_by = 'seed';
                """);

            migrationBuilder.AddColumn<bool>(
                name: "is_persistent",
                table: "refresh_token",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "account_access_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_access_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blocked_email_domain",
                columns: table => new
                {
                    domain = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_email_domain", x => x.domain);
                });

            migrationBuilder.CreateTable(
                name: "player_profile",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nickname_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmation_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deletion_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deletion_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_profile", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_player_profile_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restricted_word",
                columns: table => new
                {
                    word = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restricted_word", x => x.word);
                });

            migrationBuilder.CreateTable(
                name: "sign_in_attempt",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    login_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sign_in_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unconfirmed_allowed_action",
                columns: table => new
                {
                    action_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unconfirmed_allowed_action", x => x.action_code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_access_log_ip_address_kind_at",
                table: "account_access_log",
                columns: new[] { "ip_address", "kind", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_account_access_log_user_id_at",
                table: "account_access_log",
                columns: new[] { "user_id", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_sign_in_attempt_ip_address_at",
                table: "sign_in_attempt",
                columns: new[] { "ip_address", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_sign_in_attempt_login_hash_at",
                table: "sign_in_attempt",
                columns: new[] { "login_hash", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_access_log");

            migrationBuilder.DropTable(
                name: "blocked_email_domain");

            migrationBuilder.DropTable(
                name: "player_profile");

            migrationBuilder.DropTable(
                name: "restricted_word");

            migrationBuilder.DropTable(
                name: "sign_in_attempt");

            migrationBuilder.DropTable(
                name: "unconfirmed_allowed_action");

            migrationBuilder.DropColumn(
                name: "is_persistent",
                table: "refresh_token");
        }
    }
}
