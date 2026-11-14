using Aetheris.Core.Errors;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Research;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The holds of the ships of a fleet and what is in them (task 00050, task 00053).
/// </summary>
/// <remarks>
/// The hold of a ship is the room of its hull plus the room of its container modules, as the catalog of
/// the hulls and the modules of the project of the ship gives them; a stack of modules counts as many
/// times as it holds parts. What a hold carries is counted in tonnes: a resource as its amount times the
/// weight of the resource (<c>resource_type.weight</c>), a module as its number times the weight of its
/// kind. The holds are read in the order the ships joined the fleet, because the yield of the extraction
/// fills them in that order.
/// </remarks>
public sealed class FleetHoldService
{
    private readonly FleetDbContext _dbContext;
    private readonly IProductionModule _production;
    private readonly IPartStock _parts;
    private readonly FleetOrdersService _orders;
    private readonly IWorldModule _world;
    private readonly IResearchModule _research;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetHoldService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="production">The catalog of the hulls and the modules.</param>
    /// <param name="parts">The facts of the parts, for the mass of the parts in the holds.</param>
    /// <param name="orders">The orders the modules of the fleet open (task 00073).</param>
    /// <param name="world">The world, for the bodies of the player (task 00073).</param>
    /// <param name="research">The research, for the limit of the bodies (task 00073).</param>
    public FleetHoldService(
        FleetDbContext dbContext,
        IProductionModule production,
        IPartStock parts,
        FleetOrdersService orders,
        IWorldModule world,
        IResearchModule research)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(research);

        _dbContext = dbContext;
        _production = production;
        _parts = parts;
        _orders = orders;
        _world = world;
        _research = research;
    }

    /// <summary>
    /// Reads what the holds of a fleet of a player carry, for the window of the hold (task 00056).
    /// </summary>
    /// <param name="playerId">The player; the fleet is looked up by its owner, so a fleet of another player is refused.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The room and the load of the holds with the sum of the fleet by resource.</returns>
    /// <exception cref="GameException">The fleet does not exist or belongs to another player.</exception>
    public async Task<FleetHoldDto> ReadForPlayerAsync(string playerId, long fleetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        bool owned = await _dbContext.Fleets.AsNoTracking()
            .AnyAsync(fleet => fleet.Id == fleetId && fleet.PlayerId == playerId, cancellationToken).ConfigureAwait(false);

        if (!owned)
        {
            throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet does not exist.");
        }

        IReadOnlyList<ShipHold> holds = await ReadAsync(fleetId, cancellationToken).ConfigureAwait(false);

        // The limit of the bodies of the player (task 00073): the window shows how many of them the
        // player has and how many it may have, so the player sees whether a colony is possible.
        int planetLimit = await _research.GetPlanetLimitAsync(playerId, cancellationToken).ConfigureAwait(false);
        int ownedPlanets = (await _world.CountOwnedPlanetsAsync([playerId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(playerId);
        IReadOnlyList<string> availableOrders = await _orders.AvailableAsync(fleetId, cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<int, decimal> weights = await ReadResourceWeightsAsync(cancellationToken).ConfigureAwait(false);
        List<FleetResourceWeightDto> resourceWeights = [.. weights.OrderBy(entry => entry.Key).Select(entry => new FleetResourceWeightDto(entry.Key, entry.Value))];

        // A fleet without ships has no holds, so it carries nothing, whatever its rows of the sum still say.
        if (holds.Count == 0)
        {
            return new FleetHoldDto(fleetId, 0, 0, [], [], availableOrders, ownedPlanets, planetLimit, resourceWeights);
        }

        List<FleetHoldItemDto> items = await _dbContext.FleetCargos.AsNoTracking()
            .Where(cargo => cargo.FleetId == fleetId && cargo.Amount > 0)
            .OrderByDescending(cargo => cargo.Amount)
            .ThenBy(cargo => cargo.ResourceId)
            .Select(cargo => new FleetHoldItemDto(cargo.ResourceId, cargo.Amount))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The parts of the ships of the fleet, one row for a kind, a mark and a bonus (task 00057).
        long[] shipIds = [.. holds.Select(hold => hold.ShipId)];
        var partRows = await _dbContext.ShipPartCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Quantity > 0)
            .GroupBy(cargo => new { cargo.PartTypeId, cargo.MarkLevel, cargo.HasRaceBonus })
            .Select(group => new { group.Key.PartTypeId, group.Key.MarkLevel, group.Key.HasRaceBonus, Quantity = group.Sum(cargo => cargo.Quantity) })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<FleetHoldPartDto> parts =
        [
            .. partRows
                .OrderByDescending(row => row.Quantity)
                .ThenBy(row => row.PartTypeId)
                .Select(row => new FleetHoldPartDto(row.PartTypeId, row.MarkLevel, row.HasRaceBonus, row.Quantity)),
        ];

        return new FleetHoldDto(
            fleetId,
            holds.Sum(hold => hold.Capacity),
            holds.Sum(hold => hold.Carried),
            items,
            parts,
            availableOrders,
            ownedPlanets,
            planetLimit,
            resourceWeights);
    }

    /// <summary>
    /// Reads the weight of one unit of every resource, in tonnes, from the catalog of the resources.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The weights by resource.</returns>
    public async Task<IReadOnlyDictionary<int, decimal>> ReadResourceWeightsAsync(CancellationToken cancellationToken = default)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        return catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.Weight);
    }

    /// <summary>
    /// Reads the holds of the ships of a fleet with the room that is left in them.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The holds, in the order the ships joined the fleet.</returns>
    public async Task<IReadOnlyList<ShipHold>> ReadAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        List<FleetShipEntity> members = await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .OrderBy(entry => entry.Position)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (members.Count == 0)
        {
            return [];
        }

        long[] shipIds = [.. members.Select(entry => entry.ShipId)];
        List<ShipEntity> ships = await _dbContext.Ships.AsNoTracking()
            .Where(ship => shipIds.Contains(ship.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ShipModuleEntity> modules = await _dbContext.ShipModules.AsNoTracking()
            .Where(module => shipIds.Contains(module.ShipId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The resources in a hold take its room as much as they weigh: the amount times the weight of the resource.
        var resourceRows = await _dbContext.ShipCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Amount > 0)
            .Select(cargo => new { cargo.ShipId, cargo.ResourceId, cargo.Amount })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<long, decimal> mass = [];

        if (resourceRows.Count > 0)
        {
            IReadOnlyDictionary<int, decimal> weights = await ReadResourceWeightsAsync(cancellationToken).ConfigureAwait(false);

            foreach (var row in resourceRows)
            {
                mass[row.ShipId] = mass.GetValueOrDefault(row.ShipId) + (row.Amount * Math.Max(0m, weights.GetValueOrDefault(row.ResourceId)));
            }
        }

        // The parts in a hold take its room as much as they weigh (task 00057).
        List<ShipPartCargoEntity> partRows = await _dbContext.ShipPartCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Quantity > 0)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (partRows.Count > 0)
        {
            IReadOnlyDictionary<int, PartRecipeFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

            foreach (ShipPartCargoEntity row in partRows)
            {
                decimal weight = facts.TryGetValue(row.PartTypeId, out PartRecipeFacts? known) ? known.Weight : 0m;
                mass[row.ShipId] = mass.GetValueOrDefault(row.ShipId) + (row.Quantity * Math.Max(0m, weight));
            }
        }

        // A hold is counted in whole tonnes: a part of a tonne takes a whole one, so the room is never overstated.
        Dictionary<long, long> carried = mass.ToDictionary(entry => entry.Key, entry => (long)Math.Ceiling(entry.Value));

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ShipHullDto> hulls = catalog.Hulls.ToDictionary(hull => hull.Id);
        Dictionary<int, ShipModuleDto> byType = catalog.Modules.ToDictionary(module => module.Id);

        long[] designIds = [.. ships.Select(ship => ship.ShipDesignId).Distinct()];
        Dictionary<long, int> hullOfDesign = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => designIds.Contains(design.Id))
            .Select(design => new { design.Id, design.HullPartTypeId })
            .ToDictionaryAsync(row => row.Id, row => row.HullPartTypeId, cancellationToken).ConfigureAwait(false);

        Dictionary<long, List<ShipModuleEntity>> modulesOfShip = modules
            .GroupBy(module => module.ShipId)
            .ToDictionary(group => group.Key, group => group.ToList());
        Dictionary<long, ShipEntity> shipById = ships.ToDictionary(ship => ship.Id);

        List<ShipHold> holds = [];

        foreach (long shipId in shipIds)
        {
            if (!shipById.TryGetValue(shipId, out ShipEntity? ship))
            {
                continue;
            }

            int hullTypeId = hullOfDesign.GetValueOrDefault(ship.ShipDesignId);
            decimal capacity = hulls.TryGetValue(hullTypeId, out ShipHullDto? hull) ? hull.HoldCapacity : 0m;

            foreach (ShipModuleEntity module in modulesOfShip.GetValueOrDefault(shipId) ?? [])
            {
                if (byType.TryGetValue(module.PartTypeId, out ShipModuleDto? known))
                {
                    capacity += known.CargoCapacity * module.Quantity;
                }
            }

            holds.Add(new ShipHold(shipId, (long)Math.Floor(capacity), carried.GetValueOrDefault(shipId)));
        }

        return holds;
    }
}

/// <summary>
/// The hold of a ship of a fleet (task 00050).
/// </summary>
/// <param name="shipId">The ship.</param>
/// <param name="capacity">The room of the hold, in tonnes.</param>
/// <param name="carried">What the hold carries now, in tonnes.</param>
public sealed class ShipHold(long shipId, long capacity, long carried)
{
    /// <summary>
    /// Gets the ship the hold belongs to.
    /// </summary>
    public long ShipId { get; } = shipId;

    /// <summary>
    /// Gets the room of the hold, in tonnes.
    /// </summary>
    public long Capacity { get; } = capacity;

    /// <summary>
    /// Gets what the hold carries now, in tonnes.
    /// </summary>
    public long Carried { get; } = carried;

    /// <summary>
    /// Gets the room that is left in the hold, in tonnes.
    /// </summary>
    public long Free { get; private set; } = Math.Max(0, capacity - carried);

    /// <summary>
    /// Puts units of a thing into the hold, as many as fit, and takes the room they took.
    /// </summary>
    /// <param name="amount">The number of units.</param>
    /// <param name="weight">The weight of one unit, in tonnes; above zero.</param>
    /// <returns>How many of the units fitted into the hold.</returns>
    /// <exception cref="GameException">The weight is not above zero.</exception>
    public long Take(long amount, decimal weight)
    {
        long taken = Math.Min(Math.Max(0, amount), UnitsThatFit(Free, weight));
        Free -= RoomOf(taken, weight);

        return taken;
    }

    /// <summary>
    /// Counts how many units of a weight fit into a room: the room divided by the weight, rounded down.
    /// </summary>
    /// <param name="free">The room, in tonnes.</param>
    /// <param name="weight">The weight of one unit, in tonnes; above zero.</param>
    /// <returns>The number of the units.</returns>
    /// <exception cref="GameException">The weight is not above zero: the data of the thing is wrong.</exception>
    public static long UnitsThatFit(long free, decimal weight)
    {
        if (weight <= 0m)
        {
            throw new GameException(FleetErrorCodes.CargoWeightInvalid, "The weight of a thing to carry is not above zero.");
        }

        return free <= 0 ? 0 : (long)decimal.Floor(free / weight);
    }

    /// <summary>
    /// Counts the room units of a weight take, in whole tonnes: a part of a tonne takes a whole one.
    /// </summary>
    /// <param name="units">The number of the units.</param>
    /// <param name="weight">The weight of one unit, in tonnes.</param>
    /// <returns>The room, in tonnes.</returns>
    public static long RoomOf(long units, decimal weight) => (long)decimal.Ceiling(units * weight);
}
