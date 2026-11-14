using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>The end of a cycle restores the shield and, from the second cycle, the repair drones (task 00063).</summary>
public sealed class CycleEndTests
{
    [Fact]
    public void The_shield_gains_the_configured_share()
    {
        ShipState ship = Engagement.Ship(
            1, 1, 1, 100, 100,
            [Engagement.Module(1, ModuleKinds.Armor, Engagement.Facts(), mass: 0)],
            shieldNow: 50,
            shieldCapacity: 100);
        BattleState state = Engagement.Solo(ship);
        BattleState after = CycleEnd.Apply(state, Engagement.Settings(shieldRestoreShare: 0.30));

        Assert.Equal(80, after.Ships[0].ShieldNow);
    }

    [Fact]
    public void Repair_drones_stay_in_the_hold_on_the_first_cycle()
    {
        BattleState state = RepairState(cycle: 0);
        BattleState after = CycleEnd.Apply(state, Engagement.Settings());

        Assert.Empty(after.Drones);
        Assert.Equal(1, after.Ships[0].HoldDrones[9]);
        Assert.Equal(10, after.Ships[0].Armor);
    }

    [Fact]
    public void Repair_drones_move_onto_the_armor_from_the_second_cycle()
    {
        BattleState state = RepairState(cycle: 1);
        BattleState after = CycleEnd.Apply(state, Engagement.Settings());

        Assert.Single(after.Drones);
        Assert.True(after.Drones[0].OnArmor);
        Assert.Equal(0, after.Ships[0].HoldDrones[9]);
        Assert.Equal(17, after.Ships[0].Armor);
    }

    private static BattleState RepairState(int cycle)
    {
        DronePattern pattern = new(
            9, 1, 10, 0, 1, 0, 7, true, 20, 1, Engagement.Facts(), []);
        ShipState ship = Engagement.Ship(
            1, 1, 1, 100, 100,
            [Engagement.Module(1, ModuleKinds.Armor, Engagement.Facts(), mass: 0, cooldownSeconds: 4)],
            armor: 10,
            armorMax: 100,
            hold: new Dictionary<int, int> { [9] = 1 },
            patterns: [pattern]);

        return Engagement.Solo(ship, cycle: cycle);
    }
}
