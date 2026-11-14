namespace Aetheris.Combat.Engine;

/// <summary>One stack in the shared hold of a fleet.</summary>
/// <param name="PartTypeId">The part.</param>
/// <param name="Count">How many.</param>
/// <param name="MassEach">The mass of one. Zero means the stack does not fill the hold.</param>
/// <param name="Kind">One of <see cref="HoldKinds"/>.</param>
public sealed record HoldStack(int PartTypeId, int Count, double MassEach, string Kind);

/// <summary>The shared hold at the start of an engagement.</summary>
/// <param name="Stacks">The stacks.</param>
public sealed record FleetHold(IReadOnlyList<HoldStack> Stacks);

/// <summary>How many of a part one ship wants.</summary>
/// <param name="PartTypeId">The part.</param>
/// <param name="Count">How many the ship can use. For rounds this is the magazine.</param>
/// <param name="Kind">One of <see cref="HoldKinds"/>.</param>
public sealed record HoldNeed(int PartTypeId, int Count, string Kind);

/// <summary>The hold of one ship and what it wants.</summary>
/// <param name="ShipId">The ship.</param>
/// <param name="Capacity">How much mass the hold takes.</param>
/// <param name="Needs">What the ship wants.</param>
public sealed record ShipHoldSpec(long ShipId, double Capacity, IReadOnlyList<HoldNeed> Needs);

/// <summary>One stack given to one ship.</summary>
/// <param name="ShipId">The ship.</param>
/// <param name="PartTypeId">The part.</param>
/// <param name="Count">How many.</param>
/// <param name="Kind">One of <see cref="HoldKinds"/>.</param>
public sealed record HoldAssignment(long ShipId, int PartTypeId, int Count, string Kind);

/// <summary>Counts that replace the split for the parts they name.</summary>
/// <param name="Assignments">The counts. A ship that is absent gets nothing of that part.</param>
public sealed record HoldPreference(IReadOnlyList<HoldAssignment> Assignments);

/// <summary>What one ship received, and how much mass it uses.</summary>
/// <param name="ShipId">The ship.</param>
/// <param name="Assignments">The stacks.</param>
/// <param name="MassUsed">The mass of the stacks that have a mass.</param>
public sealed record ShipHoldShare(long ShipId, IReadOnlyList<HoldAssignment> Assignments, double MassUsed);

/// <summary>The split of a shared hold. Dropping a ship removes its share and does not hand it on.</summary>
/// <param name="Shares">One share per ship, in the order of the ship identifiers.</param>
public sealed record HoldSplit(IReadOnlyList<ShipHoldShare> Shares)
{
    /// <summary>Removes the share of a ship. The other shares stay as they are.</summary>
    /// <param name="shipId">The ship.</param>
    /// <returns>The split without that ship.</returns>
    public HoldSplit Without(long shipId)
    {
        List<ShipHoldShare> kept = [];

        for (int i = 0; i < Shares.Count; i++)
        {
            if (Shares[i].ShipId != shipId)
            {
                kept.Add(Shares[i]);
            }
        }

        return new HoldSplit(kept);
    }
}

/// <summary>
/// Splits a shared hold once, at the start of an engagement. Rounds follow the magazines
/// and, when there are not enough, the largest remainder (a tie goes to the smaller ship).
/// Drones and other cargo follow the capacity the same way. A preference replaces that
/// split for the parts it names and is still cut to the fleet total and the capacity.
/// </summary>
public static class HoldSplitter
{
    private const int CargoReductionRank = 3;
    private const int DroneReductionRank = 2;
    private const int AmmoReductionRank = 1;
    /// <summary>Splits the hold.</summary>
    /// <param name="hold">The shared hold.</param>
    /// <param name="ships">The ships, with their capacity and their needs.</param>
    /// <param name="preference">Counts that replace the split, or none.</param>
    /// <returns>The shares.</returns>
    public static HoldSplit Split(FleetHold hold, IReadOnlyList<ShipHoldSpec> ships, HoldPreference? preference)
    {
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(ships);

        int shipCount = ships.Count;
        int[] order = new int[shipCount];

        for (int i = 0; i < shipCount; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => ships[left].ShipId.CompareTo(ships[right].ShipId));

        var rows = new List<HoldAssignment>[shipCount];

        for (int i = 0; i < shipCount; i++)
        {
            rows[i] = [];
        }

        for (int stackIndex = 0; stackIndex < hold.Stacks.Count; stackIndex++)
        {
            HoldStack stack = hold.Stacks[stackIndex];

            if (stack.Count <= 0)
            {
                continue;
            }

            int[] counts = new int[shipCount];

            if (PreferenceCovers(preference, stack))
            {
                AssignPreference(preference!, ships, order, stack, counts);
            }
            else if (string.Equals(stack.Kind, HoldKinds.Ammo, StringComparison.Ordinal))
            {
                AssignByNeed(ships, order, stack, counts);
            }
            else
            {
                AssignByCapacity(ships, order, stack, counts);
            }

            for (int i = 0; i < shipCount; i++)
            {
                if (counts[i] > 0)
                {
                    rows[i].Add(new HoldAssignment(ships[i].ShipId, stack.PartTypeId, counts[i], stack.Kind));
                }
            }
        }

        ShipHoldShare[] shares = new ShipHoldShare[shipCount];

        for (int i = 0; i < shipCount; i++)
        {
            TrimToCapacity(rows[i], ships[i].Capacity, hold);
            double mass = 0;

            for (int assignmentIndex = 0; assignmentIndex < rows[i].Count; assignmentIndex++)
            {
                HoldAssignment assignment = rows[i][assignmentIndex];
                mass += assignment.Count * MassOf(hold, assignment);
            }

            shares[i] = new ShipHoldShare(ships[i].ShipId, rows[i], mass);
        }

        Array.Sort(shares, (left, right) => left.ShipId.CompareTo(right.ShipId));

        return new HoldSplit(shares);
    }

    private static bool PreferenceCovers(HoldPreference? preference, HoldStack stack)
    {
        if (preference is null)
        {
            return false;
        }

        for (int i = 0; i < preference.Assignments.Count; i++)
        {
            HoldAssignment assignment = preference.Assignments[i];

            if (assignment.PartTypeId == stack.PartTypeId
                && string.Equals(assignment.Kind, stack.Kind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void AssignPreference(
        HoldPreference preference,
        IReadOnlyList<ShipHoldSpec> ships,
        int[] order,
        HoldStack stack,
        int[] counts)
    {
        int remaining = stack.Count;

        for (int orderIndex = 0; orderIndex < order.Length; orderIndex++)
        {
            int shipIndex = order[orderIndex];
            int asked = 0;

            for (int i = 0; i < preference.Assignments.Count; i++)
            {
                HoldAssignment assignment = preference.Assignments[i];

                if (assignment.ShipId == ships[shipIndex].ShipId
                    && assignment.PartTypeId == stack.PartTypeId
                    && string.Equals(assignment.Kind, stack.Kind, StringComparison.Ordinal)
                    && assignment.Count > asked)
                {
                    asked = assignment.Count;
                }
            }

            int given = asked < remaining ? asked : remaining;

            if (given < 0)
            {
                given = 0;
            }

            counts[shipIndex] = given;
            remaining -= given;
        }
    }

    private static void AssignByNeed(
        IReadOnlyList<ShipHoldSpec> ships,
        int[] order,
        HoldStack stack,
        int[] counts)
    {
        int[] weights = new int[ships.Count];
        int weightSum = 0;

        for (int i = 0; i < ships.Count; i++)
        {
            weights[i] = NeedOf(ships[i], stack);
            weightSum += weights[i];
        }

        if (weightSum <= 0)
        {
            return;
        }

        if (weightSum <= stack.Count)
        {
            for (int i = 0; i < ships.Count; i++)
            {
                counts[i] = weights[i];
            }

            return;
        }

        LargestRemainder(ships, order, weights, weightSum, stack.Count, counts, weights);
    }

    private static void AssignByCapacity(
        IReadOnlyList<ShipHoldSpec> ships,
        int[] order,
        HoldStack stack,
        int[] counts)
    {
        int[] weights = new int[ships.Count];
        int[] caps = new int[ships.Count];
        int weightSum = 0;

        for (int i = 0; i < ships.Count; i++)
        {
            int need = NeedOf(ships[i], stack);

            weights[i] = CapacityWeight(ships[i].Capacity);
            caps[i] = need > 0 ? need : int.MaxValue;
            weightSum += weights[i];
        }

        if (weightSum <= 0)
        {
            return;
        }

        LargestRemainder(ships, order, weights, weightSum, stack.Count, counts, caps);
    }

    private static int CapacityWeight(double capacity)
    {
        if (capacity <= 0)
        {
            return 0;
        }

        double whole = Math.Floor(capacity);

        if (whole >= int.MaxValue)
        {
            return int.MaxValue;
        }

        int weight = (int)whole;

        return weight > 0 ? weight : 1;
    }

    private static void LargestRemainder(
        IReadOnlyList<ShipHoldSpec> ships,
        int[] order,
        int[] weights,
        int weightSum,
        int fleetCount,
        int[] counts,
        int[] caps)
    {
        double[] remainder = new double[ships.Count];
        int assigned = 0;

        for (int i = 0; i < ships.Count; i++)
        {
            if (weights[i] <= 0 || caps[i] <= 0)
            {
                continue;
            }

            double exact = (double)fleetCount * weights[i] / weightSum;
            int whole = (int)Math.Floor(exact);

            if (whole > caps[i])
            {
                whole = caps[i];
            }

            counts[i] = whole;
            remainder[i] = exact - whole;
            assigned += whole;
        }

        int left = fleetCount - assigned;

        while (left > 0)
        {
            int best = -1;

            for (int orderIndex = 0; orderIndex < order.Length; orderIndex++)
            {
                int shipIndex = order[orderIndex];

                if (counts[shipIndex] >= caps[shipIndex] || weights[shipIndex] <= 0)
                {
                    continue;
                }

                if (best < 0 || remainder[shipIndex] > remainder[best])
                {
                    best = shipIndex;
                }
            }

            if (best < 0)
            {
                break;
            }

            counts[best]++;
            remainder[best] = -1;
            left--;
        }
    }

    private static int NeedOf(ShipHoldSpec ship, HoldStack stack)
    {
        int need = 0;

        for (int i = 0; i < ship.Needs.Count; i++)
        {
            HoldNeed candidate = ship.Needs[i];

            if (candidate.PartTypeId == stack.PartTypeId
                && string.Equals(candidate.Kind, stack.Kind, StringComparison.Ordinal)
                && candidate.Count > need)
            {
                need = candidate.Count;
            }
        }

        return need;
    }

    private static void TrimToCapacity(List<HoldAssignment> rows, double capacity, FleetHold hold)
    {
        while (MassOf(rows, hold) > capacity)
        {
            int index = IndexToReduce(rows);

            if (index < 0)
            {
                break;
            }

            HoldAssignment current = rows[index];

            if (current.Count <= 1)
            {
                rows.RemoveAt(index);
            }
            else
            {
                rows[index] = current with { Count = current.Count - 1 };
            }
        }
    }

    private static int IndexToReduce(List<HoldAssignment> rows)
    {
        int best = -1;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Count <= 0)
            {
                continue;
            }

            if (best < 0 || ReductionRank(rows[i]) > ReductionRank(rows[best])
                || (ReductionRank(rows[i]) == ReductionRank(rows[best]) && rows[i].PartTypeId > rows[best].PartTypeId))
            {
                best = i;
            }
        }

        return best;
    }

    private static int ReductionRank(HoldAssignment assignment)
    {
        if (string.Equals(assignment.Kind, HoldKinds.Cargo, StringComparison.Ordinal))
        {
            return CargoReductionRank;
        }

        if (string.Equals(assignment.Kind, HoldKinds.Drone, StringComparison.Ordinal))
        {
            return DroneReductionRank;
        }

        return AmmoReductionRank;
    }

    private static double MassOf(List<HoldAssignment> rows, FleetHold hold)
    {
        double mass = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            mass += rows[i].Count * MassOf(hold, rows[i]);
        }

        return mass;
    }

    private static double MassOf(FleetHold hold, HoldAssignment assignment)
    {
        for (int i = 0; i < hold.Stacks.Count; i++)
        {
            HoldStack stack = hold.Stacks[i];

            if (stack.PartTypeId == assignment.PartTypeId
                && string.Equals(stack.Kind, assignment.Kind, StringComparison.Ordinal))
            {
                return stack.MassEach > 0 ? stack.MassEach : 0;
            }
        }

        return 0;
    }

}
