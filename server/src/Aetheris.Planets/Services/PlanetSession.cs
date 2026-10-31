using Aetheris.Core.Errors;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;

namespace Aetheris.Planets.Services;

/// <summary>
/// A body loaded for one change or one read: its rows and the catalog they are read with.
/// </summary>
/// <remarks>
/// For a change the rows are tracked and the state row is locked; every method changes the
/// rows, and the unit of work saves them. For a read the rows are not tracked and only the
/// answer is built.
/// </remarks>
public sealed class PlanetSession
{
    private readonly PlanetsDbContext _dbContext;
    private readonly List<PlanetBuildingEntity> _buildings;
    private readonly List<PlanetResourceEntity> _resources;
    private readonly IReadOnlyDictionary<int, decimal> _deposits;
    private readonly IReadOnlyDictionary<int, int> _technologyLevels;
    private readonly PlanetBonuses _bonuses;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetSession"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="settings">The parameters.</param>
    /// <param name="state">The state row.</param>
    /// <param name="buildings">The buildings.</param>
    /// <param name="queue">The queue in its order.</param>
    /// <param name="resources">The balance rows.</param>
    /// <param name="deposits">The richness coefficients by resource.</param>
    /// <param name="technologyLevels">The technologies of the owner.</param>
    /// <param name="bonuses">The bonuses of the technologies.</param>
    public PlanetSession(
        PlanetsDbContext dbContext,
        BuildingCatalog catalog,
        PlanetsSettings settings,
        PlanetStateEntity state,
        List<PlanetBuildingEntity> buildings,
        List<ConstructionQueueItemEntity> queue,
        List<PlanetResourceEntity> resources,
        IReadOnlyDictionary<int, decimal> deposits,
        IReadOnlyDictionary<int, int> technologyLevels,
        PlanetBonuses bonuses)
    {
        _dbContext = dbContext;
        Catalog = catalog;
        Settings = settings;
        State = state;
        _buildings = buildings;
        Queue = queue;
        _resources = resources;
        _deposits = deposits;
        _technologyLevels = technologyLevels;
        _bonuses = bonuses;
    }

    /// <summary>Gets the building catalog.</summary>
    public BuildingCatalog Catalog { get; }

    /// <summary>Gets the parameters.</summary>
    public PlanetsSettings Settings { get; }

    /// <summary>Gets the state row.</summary>
    public PlanetStateEntity State { get; }

    /// <summary>Gets the queue in its order.</summary>
    public List<ConstructionQueueItemEntity> Queue { get; }

    /// <summary>
    /// Gets or sets the balances of the currencies of the civilization of the owner of the body, by
    /// currency. The costs in a currency are checked against them (task 00015).
    /// </summary>
    public IReadOnlyDictionary<int, long> Currencies { get; set; } = new Dictionary<int, long>();

    /// <summary>
    /// Gets the levels of the buildings by type.
    /// </summary>
    /// <returns>The levels.</returns>
    public Dictionary<int, int> Levels() => _buildings.ToDictionary(building => building.BuildingTypeId, building => building.Level);

    /// <summary>
    /// Brings the balance to a moment with the rates of the buildings as they are, and writes it.
    /// A moment before the last fix changes no amount.
    /// </summary>
    /// <param name="moment">The moment of game time.</param>
    public void FixTo(DateTimeOffset moment)
    {
        DateTimeOffset from = State.ResourcesUpdatedAt;
        DateTimeOffset to = moment > from ? moment : from;
        AccumulationResult result = Accumulate(from, to);

        foreach ((int resourceTypeId, long amount) in result.Amounts)
        {
            Row(resourceTypeId).Amount = amount;
        }

        State.ResourcesUpdatedAt = to;
        WriteFullAt(FullSince(result.FullAt, from));
    }

    /// <summary>
    /// Writes the moment every resource is full, from the amounts and the rates as they
    /// are now. It is called after every change of the amounts or of the buildings.
    /// </summary>
    /// <param name="moment">The moment of the amounts.</param>
    public void RefreshStorage(DateTimeOffset moment) =>
        WriteFullAt(FullSince(Accumulate(moment, moment).FullAt, moment));

    private void WriteFullAt(IReadOnlyDictionary<int, DateTimeOffset?> fullAtByResource)
    {
        foreach ((int resourceTypeId, DateTimeOffset? fullAt) in fullAtByResource)
        {
            PlanetResourceEntity? row = _resources.FirstOrDefault(entry => entry.ResourceTypeId == resourceTypeId);

            // A resource without a row is empty, so it has no moment to keep until it is produced.
            if (row is null && fullAt is null)
            {
                continue;
            }

            (row ?? Row(resourceTypeId)).FullAt = fullAt;
        }
    }

    /// <summary>
    /// Takes an amount from the balance. The rules have checked it before, so a shortage here
    /// is a broken invariant and stops the change.
    /// </summary>
    /// <param name="resourceTypeId">The resource.</param>
    /// <param name="amount">The amount.</param>
    public void Take(int resourceTypeId, long amount)
    {
        PlanetResourceEntity row = Row(resourceTypeId);

        if (amount < 0 || row.Amount < amount)
        {
            throw new GameException(PlanetsErrorCodes.ResourcesShort, "The body lacks resources for the cost.");
        }

        row.Amount -= amount;
    }

    /// <summary>
    /// Gives an amount back to the balance.
    /// </summary>
    /// <param name="resourceTypeId">The resource.</param>
    /// <param name="amount">The amount.</param>
    public void Give(int resourceTypeId, long amount) => Row(resourceTypeId).Amount += amount;

    /// <summary>
    /// Sets the level of a building, creating the building at its first level.
    /// </summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="level">The level.</param>
    public void SetLevel(int buildingTypeId, int level)
    {
        PlanetBuildingEntity? building = _buildings.FirstOrDefault(entry => entry.BuildingTypeId == buildingTypeId);

        if (building is null)
        {
            building = new PlanetBuildingEntity { PlanetId = State.PlanetId, SectorId = State.SectorId, BuildingTypeId = buildingTypeId };
            _buildings.Add(building);
            _dbContext.PlanetBuildings.Add(building);
        }

        building.Level = level;
    }

    /// <summary>
    /// Numbers the queue again from 1 in its order.
    /// </summary>
    public void Renumber()
    {
        for (int index = 0; index < Queue.Count; index++)
        {
            Queue[index].Position = index + 1;
        }
    }

    /// <summary>
    /// Gathers the figures of the body at a moment; the amounts are brought to it without a write.
    /// </summary>
    /// <param name="now">The moment of game time.</param>
    /// <returns>The figures.</returns>
    public PlanetFigures Figures(DateTimeOffset now)
    {
        AccumulationResult result = Accumulate(State.ResourcesUpdatedAt, now);

        return new PlanetFigures(
            State.PlanetId,
            State.ExtraQueueSlots,
            Levels(),
            [
                .. Queue.Select(item => new QueueItemFigure(
                    item.Id,
                    item.BuildingTypeId,
                    item.TargetLevel,
                    item.Position,
                    item.DurationSeconds,
                    item.StartedAt,
                    item.FinishesAt,
                    item.CostIds.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount))),
            ],
            result.Amounts,
            FullSince(result.FullAt, State.ResourcesUpdatedAt),
            _deposits,
            _technologyLevels,
            _bonuses,
            now,
            Currencies);
    }

    /// <summary>
    /// Builds the answer about the body at a moment.
    /// </summary>
    /// <param name="now">The moment of game time.</param>
    /// <param name="timeMultiplier">The game seconds per real second.</param>
    /// <returns>The answer.</returns>
    public PlanetOverviewDto Overview(DateTimeOffset now, double timeMultiplier) =>
        PlanetOverviewBuilder.Build(Catalog, Settings, Figures(now), timeMultiplier);

    /// <summary>
    /// Keeps the earlier moment of a resource that was already full at the start of a computation,
    /// so the alarm says since when the resource is full, not when it was last computed.
    /// </summary>
    /// <param name="computed">The moments computed from <paramref name="from"/>.</param>
    /// <param name="from">The start of the computation.</param>
    /// <returns>The moments by resource.</returns>
    private Dictionary<int, DateTimeOffset?> FullSince(IReadOnlyDictionary<int, DateTimeOffset?> computed, DateTimeOffset from)
    {
        Dictionary<int, DateTimeOffset?> result = new(computed);

        foreach (PlanetResourceEntity row in _resources)
        {
            if (result.GetValueOrDefault(row.ResourceTypeId) is { } fullAt && fullAt <= from && row.FullAt is { } since && since < fullAt)
            {
                result[row.ResourceTypeId] = since;
            }
        }

        return result;
    }

    private AccumulationResult Accumulate(DateTimeOffset from, DateTimeOffset to)
    {
        Dictionary<int, int> levels = Levels();

        return PlanetEconomy.Accumulate(
            Catalog,
            _resources.ToDictionary(row => row.ResourceTypeId, row => row.Amount),
            PlanetEconomy.ProductionRates(Catalog, levels, _deposits, _bonuses),
            PlanetEconomy.Capacities(Catalog, levels, _bonuses),
            PlanetEconomy.Weights(Catalog),
            from,
            to);
    }

    private PlanetResourceEntity Row(int resourceTypeId)
    {
        PlanetResourceEntity? row = _resources.FirstOrDefault(entry => entry.ResourceTypeId == resourceTypeId);

        if (row is null)
        {
            row = new PlanetResourceEntity { PlanetId = State.PlanetId, SectorId = State.SectorId, ResourceTypeId = resourceTypeId };
            _resources.Add(row);
            _dbContext.PlanetResources.Add(row);
        }

        return row;
    }
}
