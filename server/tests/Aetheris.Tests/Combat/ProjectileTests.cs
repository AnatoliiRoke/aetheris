using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>A shot that arrives after the target is gone misses, and a missile can be stopped (task 00063).</summary>
public sealed class ProjectileTests
{
    [Fact]
    public void A_shot_at_a_dead_target_misses()
    {
        ShipState shooter = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 5000, rate: 1, range: 200, damageKind: DamageKinds.Energy), mass: 0),
                Engagement.Module(3, ModuleKinds.Weapon, Engagement.Facts(damage: 10, rate: 1, range: 200, flightSpeed: 1, damageKind: DamageKinds.Kinetic), mass: 0),
            ]);
        ShipState target = Engagement.Ship(
            2, 2, 2, 110, 100,
            [Engagement.Module(4, ModuleKinds.Armor, Engagement.Facts(), mass: 0)],
            hull: 100);
        SegmentResult finished = Engagement.Run(Engagement.Duel(shooter, target));

        Assert.True(Engagement.Find(finished, 2).Destroyed);
        Assert.Equal(2, Engagement.Shots(finished));
        Assert.Equal(1, Engagement.Hits(finished));
    }

    [Fact]
    public void An_anti_missile_system_stops_a_missile()
    {
        ShipState shooter = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 100, rate: 1, range: 200, damageKind: DamageKinds.Missile), mass: 0),
            ]);
        ShipState target = Engagement.Ship(
            2, 2, 2, 110, 100,
            [Engagement.Module(3, ModuleKinds.AntiMissile, Engagement.Facts(antiMissileChance: 1), mass: 0)]);
        SegmentResult finished = Engagement.Run(Engagement.Duel(shooter, target));

        Assert.Equal(1000, Engagement.Find(finished, 2).Hull);
        Assert.Equal(1, Engagement.Shots(finished));
        Assert.Equal(0, Engagement.Hits(finished));
    }
}
