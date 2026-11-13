using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Fleet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightLegs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "command",
                table: "flight_plan_order",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_key",
                table: "flight_plan_order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "leg",
                table: "flight_plan_order",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_json",
                table: "fleet_flight",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "leg_kind",
                table: "fleet_flight",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "prep_ends_at",
                table: "fleet_flight",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_flight_plan_order_group_key",
                table: "flight_plan_order",
                column: "group_key");

            migrationBuilder.AddCheckConstraint(
                name: "ck_flight_plan_order_leg",
                table: "flight_plan_order",
                sql: "leg IS NULL OR leg IN ('normal','to_edge','ftl','from_edge')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fleet_flight_leg_kind",
                table: "fleet_flight",
                sql: "leg_kind IN ('normal','hyper','jump')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flight_plan_order_group_key",
                table: "flight_plan_order");

            migrationBuilder.DropCheckConstraint(
                name: "ck_flight_plan_order_leg",
                table: "flight_plan_order");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fleet_flight_leg_kind",
                table: "fleet_flight");

            migrationBuilder.DropColumn(
                name: "command",
                table: "flight_plan_order");

            migrationBuilder.DropColumn(
                name: "group_key",
                table: "flight_plan_order");

            migrationBuilder.DropColumn(
                name: "leg",
                table: "flight_plan_order");

            migrationBuilder.DropColumn(
                name: "fuel_json",
                table: "fleet_flight");

            migrationBuilder.DropColumn(
                name: "leg_kind",
                table: "fleet_flight");

            migrationBuilder.DropColumn(
                name: "prep_ends_at",
                table: "fleet_flight");
        }
    }
}
