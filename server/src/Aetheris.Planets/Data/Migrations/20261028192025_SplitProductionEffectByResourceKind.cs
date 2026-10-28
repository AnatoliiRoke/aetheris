using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aetheris.Planets.Data.Migrations
{
    /// <summary>
    /// Splits the production effect into the effect of the metals and the effect of the
    /// crystals, so the technologies can raise one of them (task 00012, decision 4).
    /// </summary>
    /// <remarks>
    /// The mines of the starting data produce metals, so their effect becomes the effect of
    /// the metals. The rows keep their identifiers and every link between them.
    /// </remarks>
    public partial class SplitProductionEffectByResourceKind : Migration
    {
        /// <summary>
        /// The effect of the production before the split.
        /// </summary>
        private const string ProductionEffect = "production";

        /// <summary>
        /// The effect of the production of the metals after the split.
        /// </summary>
        private const string MetalProductionEffect = "metal_production";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE building_level_effect
                SET effect_type = '{MetalProductionEffect}'
                WHERE effect_type = '{ProductionEffect}';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE building_level_effect
                SET effect_type = '{ProductionEffect}'
                WHERE effect_type = '{MetalProductionEffect}';
                """);
        }
    }
}
