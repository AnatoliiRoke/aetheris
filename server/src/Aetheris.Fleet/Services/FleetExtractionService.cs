using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The extraction of a fleet (task 00050, task 00055).
/// </summary>
/// <remarks>
/// <para>
/// A fleet extracts with the modules of its ships. What a module takes and where it takes it is data
/// of the production, not a code of the fleet: a module of extraction has rows that name the kind of
/// the place (the class of the object of the map), the way it takes and, for some ways, the resource or
/// the group of the resources (task 00055). The fleet reads the rows through the catalog of the ships and
/// knows no module, no resource and no group of them. The ways are:
/// </para>
/// <list type="bullet">
/// <item>every resource of the composition of the place by its share (the drill rig of an asteroid
/// field, which also takes the mass it processes from the field);</item>
/// <item>a named resource: the yield of the period times its share in the composition, or the whole
/// yield at a place that has no composition (a gas giant);</item>
/// <item>the resources of a named group that the place holds, the yield split evenly between them.</item>
/// </list>
/// <para>
/// The yield of the characteristic of a module is the amount of an hour, and a cycle takes the part
/// of it that fits into the length of the cycle (the length comes from the configuration, task
/// 00053). Several modules of the same kind multiply the yield of the cycle.
/// </para>
/// </remarks>
public sealed class FleetExtractionService
{
    /// <summary>
    /// The seconds of an hour: the yield of a module is an amount of an hour, and a cycle takes the
    /// part of it that fits into the length of the cycle (task 00053).
    /// </summary>
    public const int SecondsPerHour = 3600;

    /// <summary>
    /// The modules of a ship that take from a place (task 00050, task 00055).
    /// </summary>
    /// <param name="PartTypeId">The module of the catalog.</param>
    /// <param name="YieldPerHour">The yield of the module per hour, in tonnes.</param>
    /// <param name="Count">How many such modules the fleet has.</param>
    /// <param name="Rules">What the module takes and where, from the catalog of the ships.</param>
    public sealed record ExtractionModules(
        int PartTypeId,
        decimal YieldPerHour,
        int Count,
        IReadOnlyList<ShipModuleExtractionDto> Rules);

    private readonly FleetDbContext _dbContext;
    private readonly IProductionModule _production;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetExtractionService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="production">The catalog of the modules.</param>
    public FleetExtractionService(FleetDbContext dbContext, IProductionModule production)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(production);

        _dbContext = dbContext;
        _production = production;
    }

    /// <summary>
    /// The modules of extraction of the ships of a fleet, by the module of the catalog (task 00055): a
    /// module is of extraction when the catalog gives it rows of what it takes.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The modules of the fleet with their yield and their number.</returns>
    public async Task<IReadOnlyList<ExtractionModules>> ModulesAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return [];
        }

        List<ShipModuleEntity> modules = await _dbContext.ShipModules.AsNoTracking()
            .Where(module => shipIds.Contains(module.ShipId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (modules.Count == 0)
        {
            return [];
        }

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ShipModuleDto> byType = catalog.Modules.ToDictionary(module => module.Id);
        Dictionary<int, (ShipModuleDto Module, int Count)> byPart = [];

        foreach (ShipModuleEntity module in modules)
        {
            // A destroyed module does not work (the battles of a later stage set the condition).
            if (module.IsHull
                || module.ConditionCode == ShipModuleConditions.Destroyed
                || !byType.TryGetValue(module.PartTypeId, out ShipModuleDto? known)
                || known.Extraction is not { Count: > 0 })
            {
                continue;
            }

            // A stack of modules counts as many modules as it holds parts.
            (ShipModuleDto Module, int Count) current = byPart.GetValueOrDefault(known.Id);
            byPart[known.Id] = (known, current.Count + (int)Math.Min(int.MaxValue, Math.Max(0L, module.Quantity)));
        }

        return
        [
            .. byPart
                .OrderBy(entry => entry.Value.Module.Code, StringComparer.Ordinal)
                .Select(entry => new ExtractionModules(
                    entry.Key,
                    entry.Value.Module.Facts?
                        .Where(fact => fact.Kind == CharacteristicKinds.Yield)
                        .Select(fact => fact.Amount)
                        .FirstOrDefault() ?? 0m,
                    entry.Value.Count,
                    entry.Value.Module.Extraction!)),
        ];
    }

    /// <summary>
    /// The amount a number of modules of one kind gives in a period (task 00053): the yield of an hour
    /// times the number of modules and the part of the hour the period takes, rounded down.
    /// </summary>
    /// <param name="yieldPerHour">The yield of one module per hour, in tonnes.</param>
    /// <param name="count">How many such modules the fleet has.</param>
    /// <param name="periodSeconds">The length of the period, in game seconds.</param>
    /// <returns>The amount, in whole tonnes.</returns>
    public static long YieldOfPeriod(decimal yieldPerHour, int count, int periodSeconds)
    {
        if (yieldPerHour <= 0m || count <= 0 || periodSeconds <= 0)
        {
            return 0;
        }

        return (long)decimal.Floor(yieldPerHour * count * periodSeconds / SecondsPerHour);
    }

    /// <summary>
    /// The mass that the modules of a fleet process at a place in a period (task 00050, task 00055).
    /// </summary>
    /// <remarks>
    /// Only a module whose row says that it takes the mass processes a mass of the place, and the mass
    /// is taken away from the place; the other modules take their yield by the rules of their rows and
    /// do not touch the mass. A gas giant and a nebula have no mass at all.
    /// </remarks>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="placeKind">The kind of the place, the class of its object.</param>
    /// <param name="periodSeconds">The length of the period, in game seconds.</param>
    /// <returns>The mass of the period, in tonnes.</returns>
    public static long CycleMass(IReadOnlyList<ExtractionModules> modules, string placeKind, int periodSeconds)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(placeKind);

        return modules
            .Where(module => module.Rules.Any(rule => IsMassRule(rule, placeKind)))
            .Sum(module => YieldOfPeriod(module.YieldPerHour, module.Count, periodSeconds));
    }

    /// <summary>
    /// Tells whether the modules of a fleet take anything from a kind of place (task 00053, task 00055).
    /// </summary>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="placeKind">The kind of the place, the class of its object.</param>
    /// <returns><see langword="true"/> when at least one module has a row for such a place.</returns>
    public static bool CanExtract(IReadOnlyList<ExtractionModules> modules, string placeKind)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(placeKind);

        return modules.Any(module => module.Rules.Any(rule => rule.PlaceKind == placeKind));
    }

    /// <summary>
    /// The kinds of the places the modules of a fleet take from (task 00055).
    /// </summary>
    /// <param name="modules">The modules of the fleet.</param>
    /// <returns>The kinds of the places, the classes of their objects.</returns>
    public static IReadOnlySet<string> PlaceKinds(IReadOnlyList<ExtractionModules> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        return modules
            .SelectMany(module => module.Rules)
            .Select(rule => rule.PlaceKind)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The resources the modules of a fleet take from a place in a period, by the rows of the modules
    /// (task 00055), without the mass the drilling processes: that part is taken from the place by the
    /// world and comes back as the yield of the mass.
    /// </summary>
    /// <remarks>
    /// Every module gives the yield of the period times the number of its modules, rounded down, and
    /// every row of the module turns it into resources: the way «every resource» gives the share of every
    /// resource of the composition, the way «named resource» the share of that resource (the whole yield
    /// at a place that has no composition), and the way «group evenly» an even part of the yield for every
    /// resource of the group that the place holds. The amounts of the modules that take the same resource
    /// add up.
    /// </remarks>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="placeKind">The kind of the place, the class of its object.</param>
    /// <param name="shares">The shares of the composition of the place, or <see langword="null"/> when the place has no composition.</param>
    /// <param name="groupResourceIds">The resources of every group of resources the rows name, by the group.</param>
    /// <param name="periodSeconds">The length of the period, in game seconds.</param>
    /// <returns>The amount of every resource the modules take.</returns>
    public static IReadOnlyList<ResourceAmount> RuleYields(
        IReadOnlyList<ExtractionModules> modules,
        string placeKind,
        IReadOnlyList<CompositionDto>? shares,
        IReadOnlyDictionary<int, IReadOnlyList<int>> groupResourceIds,
        int periodSeconds)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(placeKind);
        ArgumentNullException.ThrowIfNull(groupResourceIds);

        List<ResourceAmount> yields = [];

        foreach (ExtractionModules module in modules)
        {
            long amount = YieldOfPeriod(module.YieldPerHour, module.Count, periodSeconds);

            if (amount <= 0)
            {
                continue;
            }

            foreach (ShipModuleExtractionDto rule in module.Rules.Where(rule => rule.PlaceKind == placeKind && !IsMassRule(rule, placeKind)))
            {
                switch (rule.Mode)
                {
                    case ExtractionModes.AllShares when shares is not null:
                        foreach (CompositionDto share in shares)
                        {
                            Add(yields, share.ResourceId, (long)decimal.Floor(amount * share.Coefficient));
                        }

                        break;

                    case ExtractionModes.Listed when rule.ResourceId is { } resourceId:
                        // A place without a composition gives the whole yield; a place with one gives the
                        // share of the resource, which is nothing when the place does not hold it.
                        Add(
                            yields,
                            resourceId,
                            shares is null
                                ? amount
                                : (long)decimal.Floor(amount * shares.Where(entry => entry.ResourceId == resourceId).Sum(entry => entry.Coefficient)));
                        break;

                    case ExtractionModes.GroupEven when rule.ResourceGroupId is { } groupId && shares is not null:
                        HashSet<int> members = [.. groupResourceIds.GetValueOrDefault(groupId, [])];
                        int[] held = [.. shares.Select(entry => entry.ResourceId).Where(members.Contains).Distinct().OrderBy(id => id)];

                        if (held.Length > 0)
                        {
                            long each = amount / held.Length;

                            foreach (int resourceInGroup in held)
                            {
                                Add(yields, resourceInGroup, each);
                            }
                        }

                        break;
                }
            }
        }

        return yields;
    }

    /// <summary>
    /// Tells whether a row of a module is the drilling of the mass of a place: the way «every
    /// resource», taking the mass it processes from the place (task 00055).
    /// </summary>
    private static bool IsMassRule(ShipModuleExtractionDto rule, string placeKind) =>
        rule.PlaceKind == placeKind && rule.Mode == ExtractionModes.AllShares && rule.ReducesMass;

    /// <summary>
    /// Adds an amount to the yield of a resource, keeping the order of the first appearance; a zero
    /// amount is left out.
    /// </summary>
    private static void Add(List<ResourceAmount> yields, int resourceId, long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int index = yields.FindIndex(entry => entry.ResourceId == resourceId);

        if (index < 0)
        {
            yields.Add(new ResourceAmount(resourceId, amount));
        }
        else
        {
            yields[index] = new ResourceAmount(resourceId, yields[index].Amount + amount);
        }
    }
}
