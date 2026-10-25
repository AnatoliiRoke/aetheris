using Aetheris.World.Extraction;
using Aetheris.World.Generation;
using Aetheris.World.Geometry;
using FsCheck;
using FsCheck.Fluent;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the geometry (acceptance criterion 12), the layout of the sectors and the pure
/// processing rule of a field (task 00004, decisions 14 and 18).
/// </summary>
public sealed class GeometryAndExtractionTests
{
    private static Config TestConfig => Config.QuickThrowOnFailure;

    /// <summary>
    /// The distance is three-dimensional and equals the flat one when z is zero.
    /// </summary>
    [Fact]
    public void The_distance_is_three_dimensional()
    {
        Assert.Equal(5d, WorldGeometry.Distance(new Point3(0, 0, 0), new Point3(3, 4, 0)), 9);
        Assert.Equal(Math.Sqrt(50d), WorldGeometry.Distance(new Point3(0, 0, 0), new Point3(3, 4, 5)), 9);
        Assert.Equal(new Point3(1.5, 2, 2.5), WorldGeometry.PointOnRoute(new Point3(0, 0, 0), new Point3(3, 4, 5), 0.5));
    }

    /// <summary>
    /// A new sector always lies on the outermost ring of the layout.
    /// </summary>
    [Fact]
    public void A_new_sector_lies_at_the_edge()
    {
        HashSet<(int X, int Y)> cells = [];
        int outermost = 0;

        for (int index = 0; index < 200; index++)
        {
            int ring = SectorLayout.Ring(index);

            Assert.True(ring >= outermost, $"Sector {index} lies inside ring {outermost}.");
            Assert.True(cells.Add(SectorLayout.GridCell(index)), $"Sector {index} reuses a cell.");

            outermost = ring;
        }
    }

    /// <summary>
    /// The example of the author: the yield follows the shares, the rest is rock, and the
    /// field loses the whole processed mass.
    /// </summary>
    [Fact]
    public void The_example_of_the_author_gives_the_expected_yield()
    {
        ResourceShare[] composition =
        [
            new(1, 0.30m), new(2, 0.15m), new(3, 0.17m), new(4, 0.13m), new(5, 0.03m), new(6, 0.04m),
        ];

        ExtractionResult result = FieldExtraction.Extract(1_000_000, composition, 20_000, 1_000_000);

        Assert.Equal([6000L, 3000L, 3400L, 2600L, 600L, 800L], result.YieldIds.Select(entry => entry.Amount));
        Assert.Equal(3600, result.RockMass);
        Assert.Equal(20_000, result.ProcessedMass);
        Assert.Equal(980_000, result.RemainingMass);
        Assert.False(result.Depleted);
    }

    /// <summary>
    /// A full hold stops the cycle: only what fits is taken, and the field loses only the
    /// matching share of the processed mass.
    /// </summary>
    [Fact]
    public void A_full_hold_takes_only_what_fits()
    {
        ResourceShare[] composition = [new(1, 0.50m), new(2, 0.30m)];

        ExtractionResult result = FieldExtraction.Extract(1_000_000, composition, 20_000, 8_000);

        Assert.True(result.YieldIds.Sum(entry => entry.Amount) <= 8_000);
        Assert.Equal(10_000, result.ProcessedMass);
        Assert.Equal(990_000, result.RemainingMass);
    }

    /// <summary>
    /// First come, first served: the last cycle gets what is left and the field is depleted.
    /// </summary>
    [Fact]
    public void The_last_cycle_gets_the_rest_and_depletes_the_field()
    {
        ResourceShare[] composition = [new(1, 0.50m)];

        ExtractionResult first = FieldExtraction.Extract(30_000, composition, 20_000, 1_000_000);
        ExtractionResult second = FieldExtraction.Extract(first.RemainingMass, composition, 20_000, 1_000_000);
        ExtractionResult third = FieldExtraction.Extract(second.RemainingMass, composition, 20_000, 1_000_000);

        Assert.Equal(10_000, second.ProcessedMass);
        Assert.True(second.Depleted);
        Assert.Equal(0, third.ProcessedMass);
        Assert.Empty(third.YieldIds);
    }

    /// <summary>
    /// For any field and any cycle the mass is kept: yield plus rock equals the processed mass,
    /// nothing is negative, the hold is never overfilled.
    /// </summary>
    [Fact]
    public void The_mass_is_kept_for_any_cycle()
    {
        Property property = Prop.ForAll(
            Arb.From(Gen.Choose(0, 2_000_000)),
            fieldMass => Prop.ForAll(
                Arb.From(Gen.Choose(0, 100_000)),
                processing => Prop.ForAll(
                    Arb.From(Gen.Choose(0, 100_000)),
                    hold => Prop.ForAll(
                        Arb.From(Gen.Choose(0, 300)),
                        share =>
                        {
                            ResourceShare[] composition = [new(1, share / 1000m), new(2, share / 2000m)];
                            ExtractionResult result = FieldExtraction.Extract(fieldMass, composition, processing, hold);
                            long yields = result.YieldIds.Sum(entry => entry.Amount);

                            return yields + result.RockMass == result.ProcessedMass
                                && result.RockMass >= 0
                                && result.RemainingMass == fieldMass - result.ProcessedMass
                                && result.RemainingMass >= 0
                                && yields <= hold
                                && result.ProcessedMass <= processing;
                        }))));

        property.Check(TestConfig);
    }
}
