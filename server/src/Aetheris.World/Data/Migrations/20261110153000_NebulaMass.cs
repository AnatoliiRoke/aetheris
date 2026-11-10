using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.World.Data.Migrations
{
    /// <summary>
    /// The mass of a nebula (task 00050): the extractors of ether and of matter take a part of it.
    /// </summary>
    public partial class NebulaMass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "initial_mass",
                table: "nebula",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "mass",
                table: "nebula",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_nebula_mass",
                table: "nebula",
                sql: "mass >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_nebula_mass", table: "nebula");

            migrationBuilder.DropColumn(
                name: "mass",
                table: "nebula");

            migrationBuilder.DropColumn(
                name: "initial_mass",
                table: "nebula");
        }
    }
}
