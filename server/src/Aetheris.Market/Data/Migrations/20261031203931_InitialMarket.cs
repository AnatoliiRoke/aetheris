using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aetheris.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "market_deal",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lot_id = table.Column<long>(type: "bigint", nullable: false),
                    seller_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    buyer_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    price_per_unit = table.Column<long>(type: "bigint", nullable: false),
                    sum = table.Column<long>(type: "bigint", nullable: false),
                    commission = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_deal", x => x.id);
                    table.CheckConstraint("ck_market_deal_commission", "commission >= 0");
                    table.CheckConstraint("ck_market_deal_quantity", "quantity > 0");
                    table.CheckConstraint("ck_market_deal_sum", "sum >= 0");
                });

            migrationBuilder.CreateTable(
                name: "market_delivery",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deal_id = table.Column<long>(type: "bigint", nullable: false),
                    buyer_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    destination_planet_id = table.Column<long>(type: "bigint", nullable: false),
                    destination_sector_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    energy_rod_amount = table.Column<long>(type: "bigint", nullable: false),
                    arrives_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_delivery", x => x.id);
                    table.CheckConstraint("ck_market_delivery_energy_rods", "energy_rod_amount >= 0");
                    table.CheckConstraint("ck_market_delivery_quantity", "quantity > 0");
                });

            migrationBuilder.CreateTable(
                name: "market_exchange",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    from_resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    to_resource_type_id = table.Column<int>(type: "integer", nullable: false),
                    from_amount = table.Column<long>(type: "bigint", nullable: false),
                    to_amount = table.Column<long>(type: "bigint", nullable: false),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    commission_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_exchange", x => x.id);
                    table.CheckConstraint("ck_market_exchange_from_amount", "from_amount > 0");
                    table.CheckConstraint("ck_market_exchange_rate", "rate > 0");
                    table.CheckConstraint("ck_market_exchange_to_amount", "to_amount > 0");
                });

            migrationBuilder.CreateTable(
                name: "market_level",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    visibility = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commission_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    name_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_level", x => x.id);
                    table.CheckConstraint("ck_market_level_commission", "commission_percent >= 0");
                });

            migrationBuilder.CreateTable(
                name: "market_lot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seller_player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    seller_planet_id = table.Column<long>(type: "bigint", nullable: false),
                    seller_sector_id = table.Column<int>(type: "integer", nullable: false),
                    market_level_id = table.Column<int>(type: "integer", nullable: false),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    price_per_unit = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    part_mark_level = table.Column<int>(type: "integer", nullable: true),
                    part_has_race_bonus = table.Column<bool>(type: "boolean", nullable: true),
                    part_condition_percent = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_lot", x => x.id);
                    table.CheckConstraint("ck_market_lot_price", "price_per_unit >= 0");
                    table.CheckConstraint("ck_market_lot_quantity", "quantity > 0");
                });

            migrationBuilder.CreateTable(
                name: "market_rate",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_rate", x => x.id);
                    table.CheckConstraint("ck_market_rate_rate", "rate > 0");
                });

            migrationBuilder.CreateTable(
                name: "market_reference_price",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resource_type_id = table.Column<int>(type: "integer", nullable: true),
                    part_type_id = table.Column<int>(type: "integer", nullable: true),
                    price = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_reference_price", x => x.id);
                    table.CheckConstraint("ck_market_reference_price_price", "price >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_market_deal_buyer_player_id",
                table: "market_deal",
                column: "buyer_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_deal_created_at",
                table: "market_deal",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_deal_lot_id",
                table: "market_deal",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_deal_seller_player_id",
                table: "market_deal",
                column: "seller_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_delivery_arrives_at",
                table: "market_delivery",
                column: "arrives_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_delivery_buyer_player_id_state",
                table: "market_delivery",
                columns: new[] { "buyer_player_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_market_delivery_deal_id",
                table: "market_delivery",
                column: "deal_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_exchange_created_at",
                table: "market_exchange",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_exchange_player_id",
                table: "market_exchange",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_level_code",
                table: "market_level",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_market_lot_part_type_id",
                table: "market_lot",
                column: "part_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_lot_resource_type_id",
                table: "market_lot",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_lot_seller_player_id",
                table: "market_lot",
                column: "seller_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_lot_state_market_level_id",
                table: "market_lot",
                columns: new[] { "state", "market_level_id" });

            migrationBuilder.CreateIndex(
                name: "ix_market_rate_recorded_at",
                table: "market_rate",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_reference_price_part_type_id",
                table: "market_reference_price",
                column: "part_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_reference_price_resource_type_id",
                table: "market_reference_price",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_reference_price_resource_type_id_part_type_id",
                table: "market_reference_price",
                columns: new[] { "resource_type_id", "part_type_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_deal");

            migrationBuilder.DropTable(
                name: "market_delivery");

            migrationBuilder.DropTable(
                name: "market_exchange");

            migrationBuilder.DropTable(
                name: "market_level");

            migrationBuilder.DropTable(
                name: "market_lot");

            migrationBuilder.DropTable(
                name: "market_rate");

            migrationBuilder.DropTable(
                name: "market_reference_price");
        }
    }
}
