using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScanSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scan_snapshot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    z = table.Column<double>(type: "double precision", nullable: false),
                    radius = table.Column<double>(type: "double precision", nullable: false),
                    scanned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_snapshot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scan_snapshot_object",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scan_snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    celestial_object_id = table.Column<long>(type: "bigint", nullable: false),
                    system_id = table.Column<long>(type: "bigint", nullable: true),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    object_type_id = table.Column<int>(type: "integer", nullable: false),
                    orbit = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    z = table.Column<double>(type: "double precision", nullable: false),
                    owner_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    planet_id = table.Column<long>(type: "bigint", nullable: true),
                    size_class_id = table.Column<int>(type: "integer", nullable: true),
                    size = table.Column<int>(type: "integer", nullable: true),
                    is_terraformed = table.Column<bool>(type: "boolean", nullable: false),
                    asteroid_field_id = table.Column<long>(type: "bigint", nullable: true),
                    nebula_id = table.Column<long>(type: "bigint", nullable: true),
                    nebula_radius = table.Column<double>(type: "double precision", nullable: true),
                    nebula_blocks_jump_drive = table.Column<bool>(type: "boolean", nullable: false),
                    field_mass = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_snapshot_object", x => x.id);
                    table.ForeignKey(
                        name: "fk_scan_snapshot_object_scan_snapshot_scan_snapshot_id",
                        column: x => x.scan_snapshot_id,
                        principalTable: "scan_snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scan_snapshot_system",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scan_snapshot_id = table.Column<long>(type: "bigint", nullable: false),
                    system_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    in_nebula = table.Column<bool>(type: "boolean", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    z = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_snapshot_system", x => x.id);
                    table.ForeignKey(
                        name: "fk_scan_snapshot_system_scan_snapshot_scan_snapshot_id",
                        column: x => x.scan_snapshot_id,
                        principalTable: "scan_snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scan_snapshot_deposit",
                columns: table => new
                {
                    scan_snapshot_object_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    coefficient = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_snapshot_deposit", x => new { x.scan_snapshot_object_id, x.resource_id });
                    table.ForeignKey(
                        name: "fk_scan_snapshot_deposit_scan_snapshot_object_scan_snapshot_ob~",
                        column: x => x.scan_snapshot_object_id,
                        principalTable: "scan_snapshot_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scan_snapshot_player_id_expires_at",
                table: "scan_snapshot",
                columns: new[] { "player_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scan_snapshot_object_scan_snapshot_id_celestial_object_id",
                table: "scan_snapshot_object",
                columns: new[] { "scan_snapshot_id", "celestial_object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scan_snapshot_object_sector_id_system_id",
                table: "scan_snapshot_object",
                columns: new[] { "sector_id", "system_id" });

            migrationBuilder.CreateIndex(
                name: "ix_scan_snapshot_system_scan_snapshot_id_system_id",
                table: "scan_snapshot_system",
                columns: new[] { "scan_snapshot_id", "system_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scan_snapshot_system_sector_id_system_id",
                table: "scan_snapshot_system",
                columns: new[] { "sector_id", "system_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scan_snapshot_deposit");

            migrationBuilder.DropTable(
                name: "scan_snapshot_system");

            migrationBuilder.DropTable(
                name: "scan_snapshot_object");

            migrationBuilder.DropTable(
                name: "scan_snapshot");
        }
    }
}
