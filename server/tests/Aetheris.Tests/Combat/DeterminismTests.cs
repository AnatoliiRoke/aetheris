using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>Two runs and a restored state write the same text (task 00063).</summary>
public sealed class DeterminismTests
{
    [Fact]
    public void Two_runs_write_the_same_text()
    {
        BattleState state = Sample();
        CombatSettings settings = Engagement.Settings(distanceFalloff: 0.5, targetSpeedFalloff: 0.5);
        string first = BattleJson.Write(Engagement.Run(state, settings));
        string second = BattleJson.Write(Engagement.Run(state, settings));

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_restored_state_repeats_the_next_cycle()
    {
        CombatSettings settings = Engagement.Settings(distanceFalloff: 0.5, targetSpeedFalloff: 0.5);
        BattleState continued = Engagement.Run(Sample(), settings).NewState with { Cycle = 1 };
        string direct = BattleJson.Write(Engagement.Run(continued, settings));
        BattleState restored = BattleJson.ReadState(BattleJson.WriteState(continued));
        string repeated = BattleJson.Write(Engagement.Run(restored, settings));

        Assert.Equal(direct, repeated);
    }

    private static BattleState Sample()
    {
        ShipState left = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 40, rate: 1, range: 100, damageKind: DamageKinds.Energy), accuracy: 0.5),
            ]);
        ShipState right = Engagement.Ship(
            2, 2, 2, 130, 100,
            [
                Engagement.Module(3, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(4, ModuleKinds.Weapon, Engagement.Facts(damage: 15, rate: 1, range: 100, flightSpeed: 20, damageKind: DamageKinds.Kinetic), accuracy: 0.5),
            ],
            shieldNow: 30,
            shieldCapacity: 30,
            energyBlock: 0.4,
            kineticBlock: 0.4);

        return Engagement.Duel(left, right, seed: 99);
    }
}
