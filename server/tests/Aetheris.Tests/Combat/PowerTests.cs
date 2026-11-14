using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>Modules turn off from the highest shutdown order (task 00063).</summary>
public sealed class PowerTests
{
    [Fact]
    public void The_higher_shutdown_order_turns_off_first()
    {
        ShipState ship = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Reactor, Engagement.Facts(power: 10), priority: 0),
                Engagement.Module(2, ModuleKinds.Other, Engagement.Facts(energyUse: 8), priority: 10),
                Engagement.Module(3, ModuleKinds.Other, Engagement.Facts(energyUse: 8), priority: 20),
            ]);
        SegmentResult finished = Engagement.Run(Engagement.Solo(ship));
        ShipState after = Engagement.Find(finished, 1);

        Assert.True(ModuleOn(after, 1));
        Assert.True(ModuleOn(after, 2));
        Assert.False(ModuleOn(after, 3));
    }

    [Fact]
    public void The_same_order_turns_the_larger_module_off_first()
    {
        ShipState ship = Engagement.Ship(
            1, 1, 1, 100, 100,
            [
                Engagement.Module(1, ModuleKinds.Reactor, Engagement.Facts(power: 10), priority: 0),
                Engagement.Module(4, ModuleKinds.Other, Engagement.Facts(energyUse: 8), priority: 20),
                Engagement.Module(5, ModuleKinds.Other, Engagement.Facts(energyUse: 8), priority: 20),
            ]);
        SegmentResult finished = Engagement.Run(Engagement.Solo(ship));
        ShipState after = Engagement.Find(finished, 1);

        Assert.True(ModuleOn(after, 4));
        Assert.False(ModuleOn(after, 5));
    }

    private static bool ModuleOn(ShipState ship, long moduleId)
    {
        for (int i = 0; i < ship.Modules.Count; i++)
        {
            if (ship.Modules[i].ModuleId == moduleId)
            {
                return ship.Modules[i].On;
            }
        }

        return false;
    }
}
