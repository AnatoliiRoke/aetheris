using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>Accuracy, distance, speed and the asteroid multiplier (task 00063).</summary>
public sealed class AccuracyTests
{
    [Fact]
    public void The_chance_falls_with_distance_speed_and_the_field()
    {
        Assert.Equal(1, HitChance.Compute(1, 0, 100, 1, 0, 100, 1, 0, 0, 1));
        Assert.Equal(0, HitChance.Compute(1, 100, 100, 1, 0, 100, 0, 0, 0, 1));
        Assert.Equal(0, HitChance.Compute(1, 0, 100, 0, 100, 100, 1, 0, 0, 1));
        Assert.Equal(0.7, HitChance.Compute(1, 0, 100, 0, 0, 100, 0, 0, 0, 0.7));
    }

    [Fact]
    public void A_zero_row_never_hits_and_a_full_row_always_hits()
    {
        ShipState shooter = Shooter(0);
        ShipState target = Engagement.Ship(2, 2, 2, 110, 100, [Engagement.Module(3, ModuleKinds.Armor, Engagement.Facts(), mass: 0)]);
        SegmentResult missed = Engagement.Run(Engagement.Duel(shooter, target));

        Assert.Equal(1000, Engagement.Find(missed, 2).Hull);
        Assert.Equal(1, Engagement.Shots(missed));
        Assert.Equal(0, Engagement.Hits(missed));

        SegmentResult landed = Engagement.Run(Engagement.Duel(Shooter(1), target));

        Assert.Equal(900, Engagement.Find(landed, 2).Hull);
        Assert.Equal(1, Engagement.Hits(landed));
    }

    private static ShipState Shooter(double accuracy)
    {
        return Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(
                    2,
                    ModuleKinds.Weapon,
                    Engagement.Facts(damage: 100, rate: 1, range: 100, damageKind: DamageKinds.Energy),
                    mass: 0,
                    accuracy: accuracy),
            ]);
    }
}
