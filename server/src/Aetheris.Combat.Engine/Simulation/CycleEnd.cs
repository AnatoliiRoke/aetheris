namespace Aetheris.Combat.Engine;

/// <summary>
/// The end of a cycle. The shield is restored, repair drones that waited in the hold
/// move onto the armor from the second cycle on, and the cooldowns return to zero.
/// The cycle number itself is left for the caller.
/// </summary>
public static class CycleEnd
{
    /// <summary>Applies the end of the cycle.</summary>
    /// <param name="state">The state at the end of the segment.</param>
    /// <param name="settings">The combat parameters.</param>
    /// <returns>The state ready for the next cycle.</returns>
    public static BattleState Apply(BattleState state, CombatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();

        long nextDroneId = NextDroneId(state.Drones);
        List<DroneState> drones = [];

        for (int i = 0; i < state.Drones.Count; i++)
        {
            DroneState drone = state.Drones[i];
            drones.Add(drone with { CooldownSeconds = 0 });
        }

        ShipState[] ships = new ShipState[state.Ships.Count];

        for (int i = 0; i < state.Ships.Count; i++)
        {
            ships[i] = RestoreShip(state.Ships[i], state.Cycle, settings, drones, ref nextDroneId);
        }

        Array.Sort(ships, (left, right) => left.ShipId.CompareTo(right.ShipId));
        drones.Sort(static (left, right) => left.DroneId.CompareTo(right.DroneId));

        return state with
        {
            Ships = ships,
            Drones = drones,
        };
    }

    private static ShipState RestoreShip(
        ShipState ship,
        int cycle,
        CombatSettings settings,
        List<DroneState> drones,
        ref long nextDroneId)
    {
        ModuleState[] modules = new ModuleState[ship.Modules.Count];

        for (int i = 0; i < ship.Modules.Count; i++)
        {
            modules[i] = ship.Modules[i] with { CooldownSeconds = 0 };
        }

        Array.Sort(modules, static (left, right) => left.ModuleId.CompareTo(right.ModuleId));

        SortedDictionary<int, int> ammo = CopyMap(ship.Ammo);
        SortedDictionary<int, int> hold = CopyMap(ship.HoldDrones);

        if (ship.Destroyed || ship.Hull <= 0)
        {
            return ship with
            {
                Modules = modules,
                Ammo = ammo,
                HoldDrones = hold,
            };
        }

        double restore = 0;

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleState module = modules[i];

            if (module.On && module.Integrity > 0)
            {
                restore += module.Facts.ShieldRestore;
            }
        }

        double shield = ship.ShieldNow + (ship.ShieldCapacity * settings.ShieldRestoreShare) + restore;

        if (shield > ship.ShieldCapacity)
        {
            shield = ship.ShieldCapacity;
        }

        if (shield < 0)
        {
            shield = 0;
        }

        double armor = ship.Armor;

        if (cycle >= 1)
        {
            DeployRepairDrones(ship, hold, drones, ref nextDroneId);
            armor += RepairFromArmor(drones, ship.ShipId);

            if (armor > ship.ArmorMax)
            {
                armor = ship.ArmorMax;
            }

            if (armor < 0)
            {
                armor = 0;
            }
        }

        return ship with
        {
            ShieldNow = shield,
            Armor = armor,
            Modules = modules,
            Ammo = ammo,
            HoldDrones = hold,
        };
    }

    private static void DeployRepairDrones(
        ShipState ship,
        SortedDictionary<int, int> hold,
        List<DroneState> drones,
        ref long nextDroneId)
    {
        IReadOnlyList<DronePattern> patterns = ship.DronePatterns ?? [];

        for (int i = 0; i < patterns.Count; i++)
        {
            DronePattern pattern = patterns[i];

            if (!pattern.RepairDrone || pattern.Count <= 0)
            {
                continue;
            }

            if (AlreadyOut(drones, ship.ShipId, pattern.PartTypeId))
            {
                continue;
            }

            int stored = hold.TryGetValue(pattern.PartTypeId, out int storedCount) ? storedCount : 0;
            int launched = stored < pattern.Count ? stored : pattern.Count;

            if (launched <= 0)
            {
                continue;
            }

            hold[pattern.PartTypeId] = stored - launched;

            for (int copy = 0; copy < launched; copy++)
            {
                drones.Add(new DroneState(
                    nextDroneId,
                    ship.ShipId,
                    ship.SideId,
                    pattern.PartTypeId,
                    pattern.HullGroupId,
                    ship.Position,
                    default,
                    pattern.Hull,
                    pattern.Hull,
                    pattern.Speed,
                    pattern.Signature,
                    pattern.EwMalus,
                    pattern.EwMalus > 0 && pattern.LeashRadius > 0 ? pattern.LeashRadius : 0,
                    pattern.Repair,
                    pattern.LeashRadius,
                    true,
                    true,
                    false,
                    0,
                    pattern.Weapon,
                    pattern.Accuracy));
                nextDroneId++;
            }
        }
    }

    private static bool AlreadyOut(List<DroneState> drones, long carrierId, int partTypeId)
    {
        for (int i = 0; i < drones.Count; i++)
        {
            DroneState drone = drones[i];

            if (!drone.Destroyed && drone.CarrierShipId == carrierId && drone.PartTypeId == partTypeId)
            {
                return true;
            }
        }

        return false;
    }

    private static double RepairFromArmor(List<DroneState> drones, long carrierId)
    {
        double repair = 0;

        for (int i = 0; i < drones.Count; i++)
        {
            DroneState drone = drones[i];

            if (!drone.Destroyed && drone.OnArmor && drone.CarrierShipId == carrierId)
            {
                repair += drone.Repair;
            }
        }

        return repair;
    }

    private static long NextDroneId(IReadOnlyList<DroneState> drones)
    {
        long next = 1;

        for (int i = 0; i < drones.Count; i++)
        {
            if (drones[i].DroneId >= next)
            {
                next = drones[i].DroneId + 1;
            }
        }

        return next;
    }

    private static SortedDictionary<int, int> CopyMap(IReadOnlyDictionary<int, int> source)
    {
        SortedDictionary<int, int> copy = [];

        foreach (KeyValuePair<int, int> pair in source)
        {
            copy[pair.Key] = pair.Value;
        }

        return copy;
    }
}
