using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "admin_audit_log",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admin_permission",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_role",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_session",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    admin_account_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    second_factor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_session_admin_account_admin_account_id",
                        column: x => x.admin_account_id,
                        principalTable: "admin_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "config_parameter_change",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    old_value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    new_value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rolled_back_change_id = table.Column<long>(type: "bigint", nullable: true),
                    admin_account_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_config_parameter_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_config_parameter_change_config_parameter_change_rolled_back~",
                        column: x => x.rolled_back_change_id,
                        principalTable: "config_parameter_change",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "config_parameter_read_only",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_parameter_read_only", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "admin_account_role",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_account_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    admin_role_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_account_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_account_role_admin_account_admin_account_id",
                        column: x => x.admin_account_id,
                        principalTable: "admin_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_account_role_admin_role_admin_role_id",
                        column: x => x.admin_role_id,
                        principalTable: "admin_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_role_permission",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_role_id = table.Column<int>(type: "integer", nullable: false),
                    admin_permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_role_permission_admin_permission_admin_permission_id",
                        column: x => x.admin_permission_id,
                        principalTable: "admin_permission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_role_permission_admin_role_admin_role_id",
                        column: x => x.admin_role_id,
                        principalTable: "admin_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_account_role_admin_account_id_admin_role_id",
                table: "admin_account_role",
                columns: new[] { "admin_account_id", "admin_role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_account_role_admin_role_id",
                table: "admin_account_role",
                column: "admin_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_admin_permission_code",
                table: "admin_permission",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_role_code",
                table: "admin_role",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_role_permission_admin_permission_id",
                table: "admin_role_permission",
                column: "admin_permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_admin_role_permission_admin_role_id_admin_permission_id",
                table: "admin_role_permission",
                columns: new[] { "admin_role_id", "admin_permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_session_admin_account_id",
                table: "admin_session",
                column: "admin_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_config_parameter_change_key_id",
                table: "config_parameter_change",
                columns: new[] { "key", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_config_parameter_change_rolled_back_change_id",
                table: "config_parameter_change",
                column: "rolled_back_change_id");

            // The roles of the existing administrators move from the comma separated column to
            // the new tables (task 00007, decision 4). Their permissions come with the seed at
            // the next start: every permission is new then, so the roles get what the seed lists.
            migrationBuilder.Sql(
                """
                INSERT INTO admin_role (code, name_key, sort_order)
                SELECT DISTINCT trim(role_code), 'admin.role.' || trim(role_code), 0
                FROM admin_account, unnest(string_to_array(roles, ',')) AS role_code
                WHERE trim(role_code) <> ''
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO admin_account_role (admin_account_id, admin_role_id)
                SELECT DISTINCT account.id, role.id
                FROM admin_account AS account, unnest(string_to_array(account.roles, ',')) AS role_code
                JOIN admin_role AS role ON role.code = trim(role_code);
                """);

            migrationBuilder.DropColumn(
                name: "roles",
                table: "admin_account");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "roles",
                table: "admin_account",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE admin_account AS account
                SET roles = coalesce((
                    SELECT string_agg(role.code, ',' ORDER BY role.sort_order)
                    FROM admin_account_role AS account_role
                    JOIN admin_role AS role ON role.id = account_role.admin_role_id
                    WHERE account_role.admin_account_id = account.id), '');
                """);

            migrationBuilder.DropTable(
                name: "admin_account_role");

            migrationBuilder.DropTable(
                name: "admin_role_permission");

            migrationBuilder.DropTable(
                name: "admin_session");

            migrationBuilder.DropTable(
                name: "config_parameter_change");

            migrationBuilder.DropTable(
                name: "config_parameter_read_only");

            migrationBuilder.DropTable(
                name: "admin_permission");

            migrationBuilder.DropTable(
                name: "admin_role");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "admin_audit_log");
        }
    }
}
