using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Production.Data.Migrations
{
    /// <inheritdoc />
    public partial class StorageCapacityPerPartKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_part_storage_quantity",
                table: "part_storage");

            migrationBuilder.DropColumn(
                name: "full_at",
                table: "part_storage");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "part_storage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "full_at",
                table: "part_storage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "quantity",
                table: "part_storage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_part_storage_quantity",
                table: "part_storage",
                sql: "quantity >= 0");
        }
    }
}
