using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>A planet blocks a segment, and the bypass keeps the upper tangent (task 00063).</summary>
public sealed class LineOfSightTests
{
    [Fact]
    public void A_circle_blocks_a_segment_through_it_and_an_endpoint_inside_it()
    {
        Vec3 center = new(0, 0, 0);

        Assert.True(LineOfSight.Blocks(new Vec3(-20, 0, 0), new Vec3(20, 0, 0), center, 5));
        Assert.False(LineOfSight.Blocks(new Vec3(-20, 20, 0), new Vec3(20, 20, 0), center, 5));
        Assert.True(LineOfSight.Blocks(new Vec3(-20, 0, 0), new Vec3(0, 0, 0), center, 5));
    }

    [Fact]
    public void The_bypass_keeps_the_upper_tangent()
    {
        Vec3 stepped = Pathing.AvoidCircle(
            new Vec3(-20, 2, 0),
            new Vec3(20, 0, 0),
            new Vec3(0, 0, 0),
            8,
            40);

        Assert.True(stepped.Y > 0);
        Assert.Equal(0, stepped.Z);
    }

    [Fact]
    public void A_planet_between_two_ships_stops_the_shot()
    {
        ShipState left = Engagement.Ship(
            1, 1, 1, -20, 0,
            [
                Engagement.Module(1, ModuleKinds.Radar, Engagement.Facts(detection: 1000), mass: 0),
                Engagement.Module(2, ModuleKinds.Weapon, Engagement.Facts(damage: 100, rate: 1, range: 100, damageKind: DamageKinds.Energy), mass: 0),
            ]);
        ShipState right = Engagement.Ship(
            2, 2, 2, 20, 0,
            [Engagement.Module(3, ModuleKinds.Armor, Engagement.Facts(), mass: 0)]);
        BattleState state = Engagement.Duel(
            left,
            right,
            circles: [new FieldObject(FieldObjectKinds.Planet, new Vec3(0, 0, 0), 5)]);
        SegmentResult finished = Engagement.Run(state);

        Assert.Equal(1000, Engagement.Find(finished, 2).Hull);
        Assert.Equal(0, Engagement.Shots(finished));
    }
}
