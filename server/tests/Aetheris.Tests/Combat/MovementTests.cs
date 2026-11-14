using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>Approach closes on a visible target. A ship that sees nothing does not fire (task 00063).</summary>
public sealed class MovementTests
{
    [Fact]
    public void Approach_moves_toward_the_visible_target()
    {
        ShipState left = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Engine, Engagement.Facts(speed: 10), mass: 0),
                Engagement.Module(3, ModuleKinds.Weapon, Engagement.Facts(range: 50), mass: 0, integrity: 10),
            ]);
        ShipState right = Engagement.Ship(
            2, 2, 2, 180, 100,
            [Engagement.Module(4, ModuleKinds.Armor, Engagement.Facts(), mass: 0)]);
        SegmentResult finished = Engagement.Run(Engagement.Duel(left, right, BehaviorLogics.Approach));
        ShipState after = Engagement.Find(finished, 1);

        Assert.True(after.Position.X > 109);
        Assert.True(after.Position.X < 112);
    }

    [Fact]
    public void An_unseen_target_is_not_fired_at()
    {
        ShipState left = Engagement.Ship(
            1, 1, 1, 100, 100,
            [Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 100, rate: 1, range: 100, damageKind: DamageKinds.Energy), mass: 0)]);
        ShipState right = Engagement.Ship(
            2, 2, 2, 110, 100,
            [Engagement.Module(3, ModuleKinds.Armor, Engagement.Facts(), mass: 0)]);
        SegmentResult finished = Engagement.Run(Engagement.Duel(left, right));

        Assert.Equal(1000, Engagement.Find(finished, 2).Hull);
        Assert.Equal(0, Engagement.Shots(finished));
    }
}
