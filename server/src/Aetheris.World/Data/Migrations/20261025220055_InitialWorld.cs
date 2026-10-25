using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "celestial_object_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_class = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_colonizable = table.Column<bool>(type: "boolean", nullable: false),
                    generation_weight = table.Column<int>(type: "integer", nullable: false),
                    mass_min = table.Column<long>(type: "bigint", nullable: true),
                    mass_max = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_celestial_object_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "galaxy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    seed = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_galaxy", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planet_size_class",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    generation_weight = table.Column<int>(type: "integer", nullable: false),
                    size_min = table.Column<int>(type: "integer", nullable: false),
                    size_max = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planet_size_class", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resource_group",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    allowed_on_planet = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_group", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "world_generation_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    galaxy_id = table.Column<int>(type: "integer", nullable: true),
                    sector_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    seed = table.Column<long>(type: "bigint", nullable: true),
                    actor_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    detail = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_world_generation_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generation_rule_group",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    object_type_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    min_picks = table.Column<int>(type: "integer", nullable: false),
                    max_picks = table.Column<int>(type: "integer", nullable: false),
                    extra_pick_probability = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_rule_group", x => x.id);
                    table.ForeignKey(
                        name: "fk_generation_rule_group_celestial_object_type_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "celestial_object_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sector",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    galaxy_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    seed = table.Column<long>(type: "bigint", nullable: false),
                    layout_index = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    z = table.Column<double>(type: "double precision", nullable: false),
                    is_newbie = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sector", x => x.id);
                    table.ForeignKey(
                        name: "fk_sector_galaxy_galaxy_id",
                        column: x => x.galaxy_id,
                        principalTable: "galaxy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_group_id = table.Column<int>(type: "integer", nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_type", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_type_resource_group_resource_group_id",
                        column: x => x.resource_group_id,
                        principalTable: "resource_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "star_system",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    z = table.Column<double>(type: "double precision", nullable: false),
                    in_nebula = table.Column<bool>(type: "boolean", nullable: false),
                    newbie_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_star_system", x => x.id);
                    table.ForeignKey(
                        name: "fk_star_system_sector_sector_id",
                        column: x => x.sector_id,
                        principalTable: "sector",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generation_rule",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rule_group_id = table.Column<int>(type: "integer", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    coefficient_min = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    coefficient_max = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generation_rule", x => x.id);
                    table.ForeignKey(
                        name: "fk_generation_rule_generation_rule_group_rule_group_id",
                        column: x => x.rule_group_id,
                        principalTable: "generation_rule_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_generation_rule_resource_type_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "celestial_object",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    system_id = table.Column<long>(type: "bigint", nullable: true),
                    object_type_id = table.Column<int>(type: "integer", nullable: false),
                    orbit = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    z = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_celestial_object", x => x.id);
                    table.ForeignKey(
                        name: "fk_celestial_object_celestial_object_type_object_type_id",
                        column: x => x.object_type_id,
                        principalTable: "celestial_object_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_celestial_object_sector_sector_id",
                        column: x => x.sector_id,
                        principalTable: "sector",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_celestial_object_star_system_system_id",
                        column: x => x.system_id,
                        principalTable: "star_system",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asteroid_field",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    system_id = table.Column<long>(type: "bigint", nullable: true),
                    celestial_object_id = table.Column<long>(type: "bigint", nullable: false),
                    initial_mass = table.Column<long>(type: "bigint", nullable: false),
                    mass = table.Column<long>(type: "bigint", nullable: false),
                    depleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asteroid_field", x => x.id);
                    table.ForeignKey(
                        name: "fk_asteroid_field_celestial_object_celestial_object_id",
                        column: x => x.celestial_object_id,
                        principalTable: "celestial_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asteroid_field_star_system_system_id",
                        column: x => x.system_id,
                        principalTable: "star_system",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nebula",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    celestial_object_id = table.Column<long>(type: "bigint", nullable: false),
                    radius = table.Column<double>(type: "double precision", nullable: false),
                    blocks_jump_drive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nebula", x => x.id);
                    table.ForeignKey(
                        name: "fk_nebula_celestial_object_celestial_object_id",
                        column: x => x.celestial_object_id,
                        principalTable: "celestial_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planet",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    system_id = table.Column<long>(type: "bigint", nullable: false),
                    celestial_object_id = table.Column<long>(type: "bigint", nullable: false),
                    size_class_id = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<int>(type: "integer", nullable: false),
                    owner_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planet", x => x.id);
                    table.ForeignKey(
                        name: "fk_planet_celestial_object_celestial_object_id",
                        column: x => x.celestial_object_id,
                        principalTable: "celestial_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_planet_planet_size_class_size_class_id",
                        column: x => x.size_class_id,
                        principalTable: "planet_size_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planet_star_system_system_id",
                        column: x => x.system_id,
                        principalTable: "star_system",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "field_composition",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    asteroid_field_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    coefficient = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_composition", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_composition_asteroid_field_asteroid_field_id",
                        column: x => x.asteroid_field_id,
                        principalTable: "asteroid_field",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_field_composition_resource_type_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nebula_composition",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    nebula_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    coefficient = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nebula_composition", x => x.id);
                    table.ForeignKey(
                        name: "fk_nebula_composition_nebula_nebula_id",
                        column: x => x.nebula_id,
                        principalTable: "nebula",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nebula_composition_resource_type_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deposit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    coefficient = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deposit", x => x.id);
                    table.ForeignKey(
                        name: "fk_deposit_planet_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_deposit_resource_type_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resource_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "start_planet_allocation",
                columns: table => new
                {
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    planet_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    allocated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_start_planet_allocation", x => x.player_id);
                    table.ForeignKey(
                        name: "fk_start_planet_allocation_planet_planet_id",
                        column: x => x.planet_id,
                        principalTable: "planet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asteroid_field_celestial_object_id",
                table: "asteroid_field",
                column: "celestial_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asteroid_field_sector_id_depleted_at",
                table: "asteroid_field",
                columns: new[] { "sector_id", "depleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_asteroid_field_system_id",
                table: "asteroid_field",
                column: "system_id");

            migrationBuilder.CreateIndex(
                name: "ix_celestial_object_object_type_id",
                table: "celestial_object",
                column: "object_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_celestial_object_sector_id_object_type_id",
                table: "celestial_object",
                columns: new[] { "sector_id", "object_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_celestial_object_system_id_orbit",
                table: "celestial_object",
                columns: new[] { "system_id", "orbit" });

            migrationBuilder.CreateIndex(
                name: "ix_celestial_object_type_code",
                table: "celestial_object_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deposit_planet_id_resource_id",
                table: "deposit",
                columns: new[] { "planet_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deposit_resource_id",
                table: "deposit",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_deposit_sector_id",
                table: "deposit",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_composition_asteroid_field_id_resource_id",
                table: "field_composition",
                columns: new[] { "asteroid_field_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_composition_resource_id",
                table: "field_composition",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_composition_sector_id",
                table: "field_composition",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_rule_resource_id",
                table: "generation_rule",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_rule_rule_group_id_resource_id",
                table: "generation_rule",
                columns: new[] { "rule_group_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_generation_rule_group_object_type_id_code",
                table: "generation_rule_group",
                columns: new[] { "object_type_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nebula_celestial_object_id",
                table: "nebula",
                column: "celestial_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nebula_sector_id",
                table: "nebula",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_nebula_composition_nebula_id_resource_id",
                table: "nebula_composition",
                columns: new[] { "nebula_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nebula_composition_resource_id",
                table: "nebula_composition",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_nebula_composition_sector_id",
                table: "nebula_composition",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_celestial_object_id",
                table: "planet",
                column: "celestial_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_planet_owner_player_id",
                table: "planet",
                column: "owner_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_sector_id_owner_player_id",
                table: "planet",
                columns: new[] { "sector_id", "owner_player_id" });

            migrationBuilder.CreateIndex(
                name: "ix_planet_size_class_id",
                table: "planet",
                column: "size_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_system_id",
                table: "planet",
                column: "system_id");

            migrationBuilder.CreateIndex(
                name: "ix_planet_size_class_code",
                table: "planet_size_class",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_group_code",
                table: "resource_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_type_code",
                table: "resource_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_type_resource_group_id",
                table: "resource_type",
                column: "resource_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_sector_galaxy_id_layout_index",
                table: "sector",
                columns: new[] { "galaxy_id", "layout_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_star_system_sector_id_number",
                table: "star_system",
                columns: new[] { "sector_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_star_system_sector_id_x_y",
                table: "star_system",
                columns: new[] { "sector_id", "x", "y" });

            migrationBuilder.CreateIndex(
                name: "ix_start_planet_allocation_planet_id",
                table: "start_planet_allocation",
                column: "planet_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_start_planet_allocation_sector_id",
                table: "start_planet_allocation",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_world_generation_log_sector_id_at",
                table: "world_generation_log",
                columns: new[] { "sector_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deposit");

            migrationBuilder.DropTable(
                name: "field_composition");

            migrationBuilder.DropTable(
                name: "generation_rule");

            migrationBuilder.DropTable(
                name: "nebula_composition");

            migrationBuilder.DropTable(
                name: "start_planet_allocation");

            migrationBuilder.DropTable(
                name: "world_generation_log");

            migrationBuilder.DropTable(
                name: "asteroid_field");

            migrationBuilder.DropTable(
                name: "generation_rule_group");

            migrationBuilder.DropTable(
                name: "nebula");

            migrationBuilder.DropTable(
                name: "resource_type");

            migrationBuilder.DropTable(
                name: "planet");

            migrationBuilder.DropTable(
                name: "resource_group");

            migrationBuilder.DropTable(
                name: "celestial_object");

            migrationBuilder.DropTable(
                name: "planet_size_class");

            migrationBuilder.DropTable(
                name: "celestial_object_type");

            migrationBuilder.DropTable(
                name: "star_system");

            migrationBuilder.DropTable(
                name: "sector");

            migrationBuilder.DropTable(
                name: "galaxy");
        }
    }
}
