using Aetheris.Combat.Engine;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Combat.Services;

/// <summary>
/// One fitted stack read from a ship. The mapper tests build these without the database.
/// </summary>
/// <param name="ModuleId">The stack. The first copy keeps this identifier.</param>
/// <param name="PartTypeId">The part.</param>
/// <param name="MarkLevel">The mark.</param>
/// <param name="ConditionPercent">The condition in percent. One hundred is whole.</param>
/// <param name="ConditionCode">One of <see cref="ShipModuleConditions"/>.</param>
/// <param name="Quantity">How many copies the stack holds. One or fewer stays one copy.</param>
/// <param name="IsHull">Whether the stack is the hull.</param>
/// <param name="SlotPriority">The shutdown order saved on the slot, or none.</param>
public sealed record FittedModule(
    long ModuleId,
    int PartTypeId,
    int MarkLevel,
    int ConditionPercent,
    string ConditionCode,
    long Quantity,
    bool IsHull,
    short? SlotPriority);

/// <summary>
/// One ship the mapper turns into a state. The position is not stored on the ship, so the
/// state stands at the origin until the caller places it.
/// </summary>
/// <param name="ShipId">The ship.</param>
/// <param name="DesignId">The project the ship was built from.</param>
/// <param name="Cost">The sum of the resources the assembly paid.</param>
/// <param name="Modules">The stacks, hull included.</param>
public sealed record FittedShip(
    long ShipId,
    long DesignId,
    decimal Cost,
    IReadOnlyList<FittedModule> Modules);

/// <summary>
/// Builds the state of a ship from the fleet rows and the production catalog (task 00063).
/// </summary>
/// <remarks>
/// Two queries read every requested ship: the ships with the sum of their cost, and the
/// stacks with the shutdown order of the matching slot. The catalog is the process cache.
/// Ammunition and drones in the hold are not loaded here. The caller places the ship.
/// </remarks>
public sealed class ShipStateBuilder
{
    private const int Percent = 100;
    private const long CopyStride = 1_000_000_000_000L;
    private const int SingleCopy = 1;
    private const double FullArmorShare = 1;
    private const string DriveComponent = "drive";
    private const string ReactorComponent = "reactor";

    private readonly FleetDbContext _fleet;
    private readonly IProductionModule _production;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipStateBuilder"/> class.
    /// </summary>
    /// <param name="fleet">The fleet rows.</param>
    /// <param name="production">The production catalog.</param>
    public ShipStateBuilder(FleetDbContext fleet, IProductionModule production)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(production);

        _fleet = fleet;
        _production = production;
    }

    /// <summary>
    /// Builds the state of each ship that exists. A missing identifier is skipped.
    /// </summary>
    /// <param name="shipIds">The ships. An empty list reads nothing.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The states, in the order of the ship identifiers.</returns>
    public async Task<IReadOnlyList<ShipState>> BuildAsync(
        IReadOnlyCollection<long> shipIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shipIds);

        long[] ids = shipIds.Distinct().ToArray();
        Array.Sort(ids);

        if (ids.Length == 0)
        {
            return [];
        }

        List<LoadedShip> loadedShips = await _fleet.Ships
            .AsNoTracking()
            .Where(ship => ids.Contains(ship.Id))
            .Select(ship => new LoadedShip(
                ship.Id,
                ship.ShipDesignId,
                _fleet.ShipCosts.Where(cost => cost.ShipId == ship.Id).Sum(cost => (decimal)cost.Amount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<LoadedModule> loadedModules = await (
            from module in _fleet.ShipModules.AsNoTracking()
            join ship in _fleet.Ships.AsNoTracking() on module.ShipId equals ship.Id
            where ids.Contains(module.ShipId)
            select new LoadedModule(
                module.Id,
                module.ShipId,
                module.PartTypeId,
                module.MarkLevel,
                module.ConditionPercent,
                module.ConditionCode,
                module.Quantity,
                module.IsHull,
                _fleet.ShipDesignSlots
                    .Where(slot => slot.DesignId == ship.ShipDesignId
                        && slot.PartTypeId == module.PartTypeId
                        && slot.MarkLevel == module.MarkLevel)
                    .OrderBy(slot => slot.SlotIndex)
                    .Select(slot => (short?)slot.PowerPriority)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<long, List<FittedModule>> byShip = GroupModules(loadedModules);
        FittedShip[] fitted = new FittedShip[loadedShips.Count];

        for (int i = 0; i < loadedShips.Count; i++)
        {
            LoadedShip loaded = loadedShips[i];
            IReadOnlyList<FittedModule> stacks = byShip.TryGetValue(loaded.ShipId, out List<FittedModule>? stacksOfShip)
                ? stacksOfShip
                : [];
            fitted[i] = new FittedShip(loaded.ShipId, loaded.DesignId, loaded.Cost, stacks);
        }

        return Map(fitted, catalog);
    }

    /// <summary>
    /// Maps fitted stacks and a catalog into states. Tests call this without the database.
    /// </summary>
    /// <param name="fitted">The ships.</param>
    /// <param name="catalog">The catalog.</param>
    /// <returns>The states, in the order of the ship identifiers.</returns>
    /// <exception cref="InvalidOperationException">A part or a shutdown order is missing.</exception>
    public static IReadOnlyList<ShipState> Map(IReadOnlyList<FittedShip> fitted, ShipCatalogDto catalog)
    {
        ArgumentNullException.ThrowIfNull(fitted);
        ArgumentNullException.ThrowIfNull(catalog);

        Dictionary<int, ShipHullDto> hulls = Index(catalog.Hulls, static hull => hull.Id);
        Dictionary<int, ShipModuleDto> modules = Index(catalog.Modules, static module => module.Id);
        Dictionary<int, ShipAmmunitionDto> ammunition = Index(catalog.Ammunition, static round => round.Id);
        int[] order = new int[fitted.Count];

        for (int i = 0; i < fitted.Count; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => fitted[left].ShipId.CompareTo(fitted[right].ShipId));
        ShipState[] ships = new ShipState[fitted.Count];

        for (int i = 0; i < order.Length; i++)
        {
            ships[i] = MapShip(fitted[order[i]], hulls, modules, ammunition);
        }

        return ships;
    }

    private static Dictionary<long, List<FittedModule>> GroupModules(List<LoadedModule> loadedModules)
    {
        Dictionary<long, List<FittedModule>> byShip = [];

        for (int i = 0; i < loadedModules.Count; i++)
        {
            LoadedModule loaded = loadedModules[i];

            if (!byShip.TryGetValue(loaded.ShipId, out List<FittedModule>? stacks))
            {
                stacks = [];
                byShip[loaded.ShipId] = stacks;
            }

            stacks.Add(new FittedModule(
                loaded.ModuleId,
                loaded.PartTypeId,
                loaded.MarkLevel,
                loaded.ConditionPercent,
                loaded.ConditionCode,
                loaded.Quantity,
                loaded.IsHull,
                loaded.SlotPriority));
        }

        return byShip;
    }

    private static ShipState MapShip(
        FittedShip fitted,
        Dictionary<int, ShipHullDto> hulls,
        Dictionary<int, ShipModuleDto> modules,
        Dictionary<int, ShipAmmunitionDto> ammunition)
    {
        List<ModuleState> built = [];
        bool haveHull = false;
        int hullGroupId = 0;
        double signature = 0;
        double hullMass = 0;
        double fullMass = 0;
        double hull = 0;
        double hullMax = 0;
        double armor = 0;
        double armorMax = 0;
        double shieldCapacity = 0;
        double signatureCut = 0;
        double energyWeight = 0;
        double energySum = 0;
        double kineticSum = 0;

        IReadOnlyList<FittedModule> stacks = fitted.Modules;
        int[] stackOrder = new int[stacks.Count];

        for (int i = 0; i < stacks.Count; i++)
        {
            stackOrder[i] = i;
        }

        Array.Sort(stackOrder, (left, right) => stacks[left].ModuleId.CompareTo(stacks[right].ModuleId));

        for (int orderIndex = 0; orderIndex < stackOrder.Length; orderIndex++)
        {
            FittedModule stack = stacks[stackOrder[orderIndex]];
            bool destroyed = string.Equals(stack.ConditionCode, ShipModuleConditions.Destroyed, StringComparison.Ordinal);
            int copies = CopyCount(stack.Quantity);

            if (stack.IsHull)
            {
                if (!hulls.TryGetValue(stack.PartTypeId, out ShipHullDto? hullPart))
                {
                    throw new InvalidOperationException($"The part {stack.PartTypeId} is not in the catalog.");
                }

                double scaled = Scaled(hullPart.Integrity, stack.ConditionPercent, destroyed);

                for (int copyIndex = 0; copyIndex < copies; copyIndex++)
                {
                    if (!haveHull)
                    {
                        haveHull = true;
                        hullGroupId = hullPart.PartGroupId;
                        signature = Share(hullPart.Signature);
                        hullMass = (double)hullPart.Weight;
                    }

                    hull += scaled;
                    hullMax += (double)hullPart.Integrity;
                    fullMass += (double)hullPart.Weight;
                }

                continue;
            }

            if (!modules.TryGetValue(stack.PartTypeId, out ShipModuleDto? part))
            {
                throw new InvalidOperationException($"The part {stack.PartTypeId} is not in the catalog.");
            }

            string kind = Classify(part);
            int priority = PriorityOf(stack, part);
            double integrityMax = (double)part.Integrity;
            double integrity = Scaled(part.Integrity, stack.ConditionPercent, destroyed);
            bool on = !destroyed && integrity > 0;
            ModuleFacts facts = FactsOf(part, kind, ammunition);
            IReadOnlyList<AccuracyEntry> accuracy = AccuracyOf(part);
            signatureCut += Share(part.SignatureCut) * copies;

            if (on && string.Equals(kind, ModuleKinds.Armor, StringComparison.Ordinal))
            {
                armor += integrity * copies;
                armorMax += integrityMax * copies;
            }

            if (on && string.Equals(kind, ModuleKinds.Shield, StringComparison.Ordinal))
            {
                double capacity = (double)part.ShieldCapacity;
                shieldCapacity += capacity * copies;
                double energyBlock = Share(part.EnergyBlock);
                double kineticBlock = Share(part.KineticBlock);
                double weight = capacity > 0 ? capacity : integrity;

                if (weight > 0)
                {
                    energyWeight += weight * copies;
                    energySum += weight * copies * energyBlock;
                    kineticSum += weight * copies * kineticBlock;
                }
            }

            for (int copyIndex = 0; copyIndex < copies; copyIndex++)
            {
                long moduleId = copyIndex == 0
                    ? stack.ModuleId
                    : stack.ModuleId + (copyIndex * CopyStride);
                built.Add(new ModuleState(
                    moduleId,
                    stack.PartTypeId,
                    kind,
                    integrity,
                    integrityMax,
                    priority,
                    (double)part.Weight,
                    on,
                    0,
                    facts,
                    accuracy));
                fullMass += (double)part.Weight;
            }
        }

        if (!haveHull)
        {
            throw new InvalidOperationException($"The ship {fitted.ShipId} has no hull.");
        }

        double loadFactor = hullMass > 0 ? fullMass / hullMass : 1;
        double energyBlockOfShip = energyWeight > 0 ? energySum / energyWeight : 0;
        double kineticBlockOfShip = energyWeight > 0 ? kineticSum / energyWeight : 0;

        return new ShipState(
            fitted.ShipId,
            0,
            0,
            hullGroupId,
            default,
            default,
            0,
            hull > 0 ? hull : 0,
            hullMax > 0 ? hullMax : 0,
            armor > 0 ? armor : 0,
            armorMax > 0 ? armorMax : 0,
            shieldCapacity > 0 ? shieldCapacity : 0,
            shieldCapacity > 0 ? shieldCapacity : 0,
            signature,
            signatureCut,
            built,
            new Dictionary<int, int>(),
            new Dictionary<int, int>(),
            false,
            hull <= 0,
            false,
            default,
            loadFactor,
            0,
            energyBlockOfShip,
            kineticBlockOfShip,
            FullArmorShare,
            (double)fitted.Cost,
            false,
            null);
    }

    private static int CopyCount(long quantity)
    {
        if (quantity <= SingleCopy)
        {
            return SingleCopy;
        }

        if (quantity > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)quantity;
    }

    private static int PriorityOf(FittedModule stack, ShipModuleDto part)
    {
        if (stack.SlotPriority is short slotPriority)
        {
            return slotPriority;
        }

        if (part.DefaultPowerPriority is short catalogPriority)
        {
            return catalogPriority;
        }

        throw new InvalidOperationException($"The part {stack.PartTypeId} has no shutdown order.");
    }

    private static string Classify(ShipModuleDto part)
    {
        if (string.Equals(part.Component, DriveComponent, StringComparison.Ordinal))
        {
            return ModuleKinds.Engine;
        }

        if (string.Equals(part.Component, ReactorComponent, StringComparison.Ordinal)
            || (part.Power > 0m && part.Damage == 0m))
        {
            return ModuleKinds.Reactor;
        }

        if (part.Damage > 0m
            || part.Rate > 0m
            || part.LauncherSize > 0m
            || part.Magazine > 0m
            || part.Accuracy is { Count: > 0 })
        {
            return ModuleKinds.Weapon;
        }

        if (part.ShieldCapacity > 0m)
        {
            return ModuleKinds.Shield;
        }

        if (Fact(part, CharacteristicKinds.Missiles) > 0m)
        {
            return ModuleKinds.AntiMissile;
        }

        if (part.ShieldRestore > 0m)
        {
            return ModuleKinds.ShieldRestorer;
        }

        if (Fact(part, CharacteristicKinds.Detection) > 0m)
        {
            return ModuleKinds.Radar;
        }

        if (part.CargoCapacity > 0m)
        {
            return ModuleKinds.Cargo;
        }

        if (Fact(part, CharacteristicKinds.RepairSpeed) > 0m)
        {
            return ModuleKinds.Other;
        }

        if (part.Integrity > 0m && IsBarePlate(part))
        {
            return ModuleKinds.Armor;
        }

        return ModuleKinds.Other;
    }

    private static bool IsBarePlate(ShipModuleDto part) =>
        part.Component is null
        && string.IsNullOrEmpty(part.DamageKind)
        && part.Accuracy is not { Count: > 0 }
        && part.Damage == 0m
        && part.Rate == 0m
        && part.LauncherSize == 0m
        && part.Magazine == 0m
        && part.ShieldCapacity == 0m
        && part.ShieldRestore == 0m
        && part.Power == 0m
        && part.EnergyUse == 0m
        && part.CargoCapacity == 0m
        && Fact(part, CharacteristicKinds.Detection) == 0m
        && Fact(part, CharacteristicKinds.Speed) == 0m
        && Fact(part, CharacteristicKinds.Missiles) == 0m
        && Fact(part, CharacteristicKinds.RepairSpeed) == 0m;

    private static ModuleFacts FactsOf(
        ShipModuleDto part,
        string kind,
        Dictionary<int, ShipAmmunitionDto> ammunition)
    {
        ShipAmmunitionDto? round = LowestRound(part, ammunition);
        double damage = (double)part.Damage;
        double flightSpeed = (double)part.FlightSpeed;
        string damageKind = part.DamageKind ?? string.Empty;
        int ammoPartTypeId = 0;

        if (round is not null)
        {
            ammoPartTypeId = round.Id;

            if (flightSpeed == 0 && round.FlightSpeed != 0m)
            {
                flightSpeed = (double)round.FlightSpeed;
            }

            if (damage == 0 && round.Damage != 0m)
            {
                damage = (double)round.Damage;
            }

            if (string.IsNullOrEmpty(damageKind) && !string.IsNullOrEmpty(round.DamageKind))
            {
                damageKind = round.DamageKind;
            }
        }

        double launcherSize = (double)part.LauncherSize;

        if (launcherSize > 0
            && (string.IsNullOrEmpty(damageKind)
                || string.Equals(damageKind, DamageKinds.Kinetic, StringComparison.Ordinal)))
        {
            damageKind = DamageKinds.Missile;
        }

        if (!string.Equals(kind, ModuleKinds.Weapon, StringComparison.Ordinal))
        {
            ammoPartTypeId = 0;
        }

        return new ModuleFacts(
            (double)part.EnergyUse,
            (double)part.Power,
            (double)Fact(part, CharacteristicKinds.Speed),
            (double)Fact(part, CharacteristicKinds.Detection),
            damage,
            (double)part.Rate,
            (double)part.Range,
            flightSpeed,
            damageKind,
            ammoPartTypeId,
            Share(part.EnergyBlock),
            Share(part.KineticBlock),
            (double)part.ShieldCapacity,
            (double)part.ShieldRestore,
            (double)Fact(part, CharacteristicKinds.RepairSpeed),
            0,
            (double)Fact(part, CharacteristicKinds.Missiles),
            launcherSize,
            (double)part.Magazine);
    }

    private static ShipAmmunitionDto? LowestRound(ShipModuleDto part, Dictionary<int, ShipAmmunitionDto> ammunition)
    {
        IReadOnlyList<int>? identifiers = part.AmmunitionIds;

        if (identifiers is null || identifiers.Count == 0)
        {
            return null;
        }

        ShipAmmunitionDto? chosen = null;

        for (int i = 0; i < identifiers.Count; i++)
        {
            if (!ammunition.TryGetValue(identifiers[i], out ShipAmmunitionDto? round))
            {
                continue;
            }

            if (chosen is null || round.Id < chosen.Id)
            {
                chosen = round;
            }
        }

        return chosen;
    }

    private static IReadOnlyList<AccuracyEntry> AccuracyOf(ShipModuleDto part)
    {
        if (part.Accuracy is not { Count: > 0 } rows)
        {
            return [];
        }

        int[] groups = new int[rows.Count];
        int cursor = 0;

        foreach (int groupId in rows.Keys)
        {
            groups[cursor++] = groupId;
        }

        Array.Sort(groups);
        AccuracyEntry[] accuracy = new AccuracyEntry[cursor];

        for (int i = 0; i < cursor; i++)
        {
            accuracy[i] = new AccuracyEntry(groups[i], rows[groups[i]]);
        }

        return accuracy;
    }

    private static decimal Fact(ShipModuleDto part, string kind)
    {
        IReadOnlyList<ShipModuleFactDto>? facts = part.Facts;

        if (facts is null)
        {
            return 0m;
        }

        for (int i = 0; i < facts.Count; i++)
        {
            if (string.Equals(facts[i].Kind, kind, StringComparison.Ordinal))
            {
                return facts[i].Amount;
            }
        }

        return 0m;
    }

    private static double Scaled(decimal whole, int conditionPercent, bool destroyed)
    {
        if (destroyed)
        {
            return 0;
        }

        return (double)whole * conditionPercent / Percent;
    }

    private static double Share(decimal number)
    {
        double raw = (double)number;

        if (raw > 1)
        {
            return raw / Percent;
        }

        if (raw < 0)
        {
            return 0;
        }

        return raw;
    }

    private static Dictionary<int, TPart> Index<TPart>(IReadOnlyList<TPart> parts, Func<TPart, int> identifier)
    {
        Dictionary<int, TPart> index = [];

        for (int i = 0; i < parts.Count; i++)
        {
            index.Add(identifier(parts[i]), parts[i]);
        }

        return index;
    }

    private sealed record LoadedShip(long ShipId, long DesignId, decimal Cost);

    private sealed record LoadedModule(
        long ModuleId,
        long ShipId,
        int PartTypeId,
        int MarkLevel,
        int ConditionPercent,
        string ConditionCode,
        long Quantity,
        bool IsHull,
        short? SlotPriority);
}
