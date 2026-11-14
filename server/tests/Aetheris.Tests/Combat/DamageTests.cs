using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>Shield, armor, hull and one module (task 00063).</summary>
public sealed class DamageTests
{
    /// <summary>
    /// Salvo 40, block 0.5, shield 100, armor 0, hull 1000, module 50, share 0.25.
    /// The shield spends 20, 20 reach the hull, and the module loses 5.
    /// </summary>
    [Fact]
    public void A_salvo_spends_the_shield_then_the_hull_and_one_module()
    {
        DamageHit hit = DamageResolution.Apply(40, 0.5, 100, 0, 1, 1000, 50, 0.25);

        Assert.Equal(20, hit.Spent);
        Assert.Equal(80, hit.ShieldNow);
        Assert.Equal(20, hit.Passed);
        Assert.Equal(0, hit.Armor);
        Assert.Equal(980, hit.Hull);
        Assert.Equal(45, hit.ModuleIntegrity);

        CombatSettings settings = Engagement.Settings();
        ShipState shooter = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 40, rate: 1, range: 100, damageKind: DamageKinds.Energy), integrity: 50),
            ]);
        ShipState target = Engagement.Ship(
            2, 2, 2, 110, 100,
            [Engagement.Module(3, ModuleKinds.Armor, Engagement.Facts(), integrity: 50)],
            shieldNow: 100,
            shieldCapacity: 100,
            energyBlock: 0.5);
        SegmentResult finished = Engagement.Run(Engagement.Duel(shooter, target), settings);
        ShipState after = Engagement.Find(finished, 2);

        Assert.Equal(80, after.ShieldNow);
        Assert.Equal(980, after.Hull);
        Assert.Equal(45, after.Modules[0].Integrity);
        Assert.Equal(1, Engagement.Hits(finished));
    }

    /// <summary>A kinetic block is the block passed for a kinetic shot. Flight speed zero still arrives in the tick.</summary>
    [Fact]
    public void A_kinetic_shot_uses_the_kinetic_block()
    {
        ShipState shooter = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 40, rate: 1, range: 100, damageKind: DamageKinds.Kinetic), integrity: 50),
            ]);
        ShipState target = Engagement.Ship(
            2, 2, 2, 110, 100,
            [Engagement.Module(3, ModuleKinds.Armor, Engagement.Facts(), integrity: 50)],
            shieldNow: 100,
            shieldCapacity: 100,
            kineticBlock: 0.5);
        SegmentResult finished = Engagement.Run(Engagement.Duel(shooter, target));
        ShipState after = Engagement.Find(finished, 2);

        Assert.Equal(80, after.ShieldNow);
        Assert.Equal(980, after.Hull);
        Assert.Equal(45, after.Modules[0].Integrity);
    }
}
