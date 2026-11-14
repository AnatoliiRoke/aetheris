using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>A shared hold is split once and a lost share is not handed on (task 00063).</summary>
public sealed class HoldSplitterTests
{
    [Fact]
    public void An_even_shortage_splits_in_half()
    {
        HoldSplit split = HoldSplitter.Split(
            new FleetHold([new HoldStack(7, 10, 0, HoldKinds.Ammo)]),
            [
                new ShipHoldSpec(1, 100, [new HoldNeed(7, 10, HoldKinds.Ammo)]),
                new ShipHoldSpec(2, 100, [new HoldNeed(7, 10, HoldKinds.Ammo)]),
            ],
            null);

        Assert.Equal(5, Count(split, 1));
        Assert.Equal(5, Count(split, 2));
    }

    [Fact]
    public void The_largest_remainder_keeps_the_smaller_ship_ahead()
    {
        HoldSplit split = HoldSplitter.Split(
            new FleetHold([new HoldStack(7, 5, 0, HoldKinds.Ammo)]),
            [
                new ShipHoldSpec(1, 100, [new HoldNeed(7, 2, HoldKinds.Ammo)]),
                new ShipHoldSpec(2, 100, [new HoldNeed(7, 2, HoldKinds.Ammo)]),
                new ShipHoldSpec(3, 100, [new HoldNeed(7, 2, HoldKinds.Ammo)]),
            ],
            null);

        Assert.Equal(2, Count(split, 1));
        Assert.Equal(2, Count(split, 2));
        Assert.Equal(1, Count(split, 3));
    }

    [Fact]
    public void Capacity_trims_the_stack_and_a_dropped_share_stays_dropped()
    {
        HoldSplit split = HoldSplitter.Split(
            new FleetHold([new HoldStack(8, 2, 3, HoldKinds.Drone)]),
            [new ShipHoldSpec(1, 5, [new HoldNeed(8, 2, HoldKinds.Drone)])],
            null);

        Assert.Equal(1, Count(split, 1));

        HoldSplit dropped = split.Without(1);

        Assert.Empty(dropped.Shares);
    }

    private static int Count(HoldSplit split, long shipId)
    {
        for (int i = 0; i < split.Shares.Count; i++)
        {
            if (split.Shares[i].ShipId != shipId)
            {
                continue;
            }

            int total = 0;

            for (int assignmentIndex = 0; assignmentIndex < split.Shares[i].Assignments.Count; assignmentIndex++)
            {
                total += split.Shares[i].Assignments[assignmentIndex].Count;
            }

            return total;
        }

        return 0;
    }
}
