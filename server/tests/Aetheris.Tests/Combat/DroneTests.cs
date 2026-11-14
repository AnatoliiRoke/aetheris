using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>A drone stays on the leash and dies with the carrier (task 00063).</summary>
public sealed class DroneTests
{
    [Fact]
    public void A_drone_outside_the_leash_is_pulled_back()
    {
        ShipState carrier = Engagement.Ship(
            1, 1, 1, 100, 100,
            [Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0)]);
        DroneState drone = new(
            1, 1, 1, 9, 1,
            new Vec3(1000, 100, 0),
            default,
            10, 10, 0, 1, 0, 0, 0, 50,
            false, false, false, 0,
            Engagement.Facts(),
            []);
        SegmentResult finished = Engagement.Run(Engagement.Solo(carrier, drones: [drone]));
        DroneState after = finished.NewState.Drones[0];
        double dx = after.Position.X - 100;
        double dy = after.Position.Y - 100;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));

        Assert.True(distance <= 50.000001);
        Assert.True(distance > 49);
    }

    [Fact]
    public void A_drone_dies_with_the_carrier()
    {
        DronePattern pattern = new(
            9, 1, 10, 20, 1, 0, 0, false, 40, 1, Engagement.Facts(), []);
        ShipState carrier = Engagement.Ship(
            1, 1, 1, 100, 100,
            [Engagement.Module(1, ModuleKinds.Armor, Engagement.Facts(), mass: 0)],
            hull: 10,
            hold: new Dictionary<int, int> { [9] = 1 },
            patterns: [pattern]);
        ShipState killer = Engagement.Ship(
            2, 2, 2, 110, 100,
            [
                Engagement.Module(2, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(3, ModuleKinds.Weapon, Engagement.Facts(damage: 1000, rate: 1, range: 200, damageKind: DamageKinds.Energy), mass: 0),
            ]);
        BattleState state = Engagement.Duel(
            carrier,
            killer,
            leftDrones: new DroneCommand(DroneCommandLogics.Launch, null));
        SegmentResult finished = Engagement.Run(state);

        Assert.True(Engagement.Find(finished, 1).Destroyed);
        Assert.Empty(finished.NewState.Drones);
        Assert.True(Engagement.Logged(finished, LogTypes.DroneDestroyed));
    }
}
