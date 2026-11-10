using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The extraction of a fleet (task 00050).
/// </summary>
/// <remarks>
/// <para>
/// A fleet extracts with the modules of its ships. The modules of the group of extraction are of
/// five kinds, and every kind takes from its own place:
/// </para>
/// <list type="bullet">
/// <item>the drill rig takes from an asteroid field by the shares of the composition of the field;</item>
/// <item>the crystal extractor takes only the crystals of the field, and its yield is split evenly
/// between them;</item>
/// <item>the helium extractor takes from a gas giant, which has no shares;</item>
/// <item>the ether extractor and the matter extractor take from a nebula by the shares of the
/// composition of the nebula.</item>
/// </list>
/// <para>
/// The yield of the characteristic of a module is the amount of an hour and a cycle lasts a quarter
/// of an hour, so a cycle takes a fourth of it. Several modules of the same kind multiply the yield
/// of the cycle.
/// </para>
/// </remarks>
public sealed class FleetExtractionService
{
    /// <summary>
    /// The group of the modules of extraction (task 00050).
    /// </summary>
    public const string ExtractionGroupCode = "pg_8";

    /// <summary>
    /// The module that takes from an asteroid field.
    /// </summary>
    public const string DrillRigCode = "part_m_54";

    /// <summary>
    /// The module that takes only the crystals of a field.
    /// </summary>
    public const string CrystalExtractorCode = "part_m_55";

    /// <summary>
    /// The module that takes the helium of a gas giant.
    /// </summary>
    public const string HeliumExtractorCode = "part_m_53";

    /// <summary>
    /// The module that takes the ether of a nebula.
    /// </summary>
    public const string EtherExtractorCode = "part_m_56";

    /// <summary>
    /// The module that takes the matter of a nebula.
    /// </summary>
    public const string MatterExtractorCode = "part_m_57";

    /// <summary>
    /// The number of cycles of an hour: the yield of a module is an amount of an hour.
    /// </summary>
    public const int CyclesPerHour = 4;

    /// <summary>
    /// The kinds of a place a fleet takes from (task 00050).
    /// </summary>
    public enum ExtractionSource
    {
        /// <summary>An asteroid field with a composition.</summary>
        Field,

        /// <summary>A gas giant without a composition.</summary>
        GasGiant,

        /// <summary>A nebula with a composition.</summary>
        Nebula,
    }

    /// <summary>
    /// The modules of a ship that take from a place (task 00050).
    /// </summary>
    /// <param name="Code">The code of the module.</param>
    /// <param name="YieldPerHour">The yield of the module per hour, in tonnes.</param>
    /// <param name="Count">How many such modules the ship has.</param>
    public sealed record ExtractionModules(string Code, decimal YieldPerHour, int Count);

    /// <summary>
    /// What the extraction of a fleet at a place will take (task 00050).
    /// </summary>
    /// <remarks>
    /// The three times answer three different questions: how long the player asked the fleet to
    /// work, how long the holds of the fleet will hold the yield, and how long the place itself
    /// will last.
    /// </remarks>
    /// <param name="CycleSeconds">The length of a cycle of extraction, in game seconds.</param>
    /// <param name="ResourceTonsPerCycle">The yield of the fleet in one cycle, in tonnes.</param>
    /// <param name="ProcessedTonsPerCycle">The mass the fleet takes from the place in one cycle, in tonnes.</param>
    /// <param name="FreeHoldTons">The room that is left in the holds of the fleet, in tonnes.</param>
    /// <param name="PlaceMassTons">The mass that is left in the place, in tonnes.</param>
    /// <param name="HoldFullSeconds">How long the holds of the fleet will hold the yield, in game seconds.</param>
    /// <param name="PlaceEmptySeconds">How long the place will last, in game seconds.</param>
    public sealed record ExtractionEstimate(
        int CycleSeconds,
        long ResourceTonsPerCycle,
        long ProcessedTonsPerCycle,
        long FreeHoldTons,
        long PlaceMassTons,
        long HoldFullSeconds,
        long PlaceEmptySeconds);

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
    /// The modules of extraction of the ships of a fleet, by the code of the module.
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
        Dictionary<string, (decimal Yield, int Count)> byCode = new(StringComparer.Ordinal);

        foreach (ShipModuleEntity module in modules)
        {
            if (!byType.TryGetValue(module.PartTypeId, out ShipModuleDto? known) || known.PartGroupCode != ExtractionGroupCode)
            {
                continue;
            }

            decimal yield = known.Facts?
                .Where(fact => fact.Kind == CharacteristicKinds.Yield)
                .Select(fact => fact.Amount)
                .FirstOrDefault() ?? 0m;

            (decimal Yield, int Count) current = byCode.GetValueOrDefault(known.Code);
            byCode[known.Code] = (current.Yield, current.Count + 1);
        }

        return
        [
            .. byCode
                .Select(entry => new ExtractionModules(entry.Key, entry.Value.Yield, entry.Value.Count))
                .OrderBy(entry => entry.Code, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The mass that the modules of a fleet take from a place in one cycle (task 00050).
    /// </summary>
    /// <remarks>
    /// Only the drill rig and the extractors of a nebula take the mass of the place. The crystal
    /// extractor and the helium extractor take the yield out of what the place holds without
    /// emptying it, so they do not shorten the life of the place: a gas giant has no mass at all.
    /// </remarks>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="source">The kind of the place.</param>
    /// <returns>The mass of the cycle, in tonnes.</returns>
    public static long CycleMass(IReadOnlyList<ExtractionModules> modules, ExtractionSource source)
    {
        ArgumentNullException.ThrowIfNull(modules);

        long mass = 0;

        foreach (ExtractionModules module in modules)
        {
            bool takes = source switch
            {
                ExtractionSource.Field => module.Code == DrillRigCode,
                ExtractionSource.GasGiant => false,
                ExtractionSource.Nebula => module.Code is EtherExtractorCode or MatterExtractorCode,
                _ => false,
            };

            if (takes)
            {
                mass += (long)(module.YieldPerHour / CyclesPerHour) * module.Count;
            }
        }

        return mass;
    }

    /// <summary>
    /// The crystals that the crystal extractor takes from a field, split evenly between them
    /// (task 00050).
    /// </summary>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="groupResources">The resources of the group of the crystals among the shares of the field.</param>
    /// <returns>The amount of every crystal.</returns>
    public static IReadOnlyList<ResourceAmount> CrystalCycle(IReadOnlyList<ExtractionModules> modules, IReadOnlyList<int> groupResources)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(groupResources);

        List<ResourceAmount> yields = [];

        foreach (ExtractionModules module in modules)
        {
            if (module.Code != CrystalExtractorCode || groupResources.Count == 0)
            {
                continue;
            }

            // The yield of the module is an amount of an hour; a cycle takes a fourth of it, and the
            // crystals of the field share it evenly.
            long amount = (long)(module.YieldPerHour / CyclesPerHour) * module.Count;
            long each = amount / groupResources.Count;

            if (each <= 0)
            {
                continue;
            }

            foreach (int resource in groupResources)
            {
                yields.Add(new ResourceAmount(resource, each));
            }
        }

        return yields;
    }

    /// <summary>
    /// The yield of the helium extractor of a fleet in one cycle (task 00050): a gas giant has no
    /// shares, so the yield goes to the hold as it is.
    /// </summary>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="heliumResourceId">The resource of the helium.</param>
    /// <returns>The amount of the helium.</returns>
    public static ResourceAmount? HeliumCycle(IReadOnlyList<ExtractionModules> modules, int heliumResourceId)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (ExtractionModules module in modules)
        {
            if (module.Code == HeliumExtractorCode)
            {
                return new ResourceAmount(heliumResourceId, (long)(module.YieldPerHour / CyclesPerHour) * module.Count);
            }
        }

        return null;
    }

    /// <summary>
    /// The yield of the whole fleet in one cycle, in tonnes (task 00050).
    /// </summary>
    /// <remarks>
    /// The drill rig and the extractors of a nebula take a part of the mass of the place, and the
    /// crystal extractor adds the crystals of the field on top of it. The shares of the place are
    /// not counted here: they are counted when the cycle runs, and the estimate must not lie about
    /// the amount that goes to the hold.
    /// </remarks>
    /// <param name="modules">The modules of the fleet.</param>
    /// <returns>The yield of one cycle, in tonnes.</returns>
    public static long ResourceCycleTons(IReadOnlyList<ExtractionModules> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        long tons = 0;

        foreach (ExtractionModules module in modules)
        {
            tons += (long)(module.YieldPerHour / CyclesPerHour) * module.Count;
        }

        return tons;
    }

    /// <summary>
    /// Counts how long a thing lasts, in game seconds (task 00050).
    /// </summary>
    /// <param name="total">The whole amount that is spent or gathered.</param>
    /// <param name="perCycle">The amount that is spent or gathered in one cycle.</param>
    /// <param name="cycleSeconds">The length of a cycle, in game seconds.</param>
    /// <returns>The time in seconds, or zero when nothing is spent or gathered.</returns>
    public static long SecondsOfCycles(long total, long perCycle, int cycleSeconds)
    {
        if (total <= 0 || perCycle <= 0)
        {
            return 0;
        }

        long cycles = (long)Math.Ceiling((double)total / perCycle);

        return cycles * cycleSeconds;
    }
}
