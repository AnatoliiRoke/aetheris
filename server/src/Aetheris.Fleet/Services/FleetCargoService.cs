using Aetheris.Core.Errors;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Planets;
using Aetheris.Production;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// What an order of a load or an unload did (task 00057).
/// </summary>
/// <param name="Code">The reason the order did not begin, or <see langword="null"/> when it was carried out.</param>
public sealed record CargoOutcome(string? Code)
{
    /// <summary>Gets whether the order was carried out.</summary>
    public bool Done => Code is null;

    /// <summary>
    /// Gets the items of a load that were loaded short of what the order named: a load takes what the
    /// stock has and what the holds take, and the rest is listed here (author's rule, 14.11.2026).
    /// </summary>
    public IReadOnlyList<CargoShortfall> Shortfalls { get; init; } = [];

    /// <summary>Gets whether a load put anything into the holds.</summary>
    public bool LoadedAny { get; init; }
}

/// <summary>
/// The reasons an item of a load was loaded short. They are logical codes, not numbers of the balance.
/// </summary>
public static class CargoShortfallReasons
{
    /// <summary>The stock of the body has less than the order named.</summary>
    public const string Stock = "stock";

    /// <summary>The holds of the fleet have no room for the rest.</summary>
    public const string Room = "room";
}

/// <summary>
/// An item of a load that was loaded short of what the order named (author's rule, 14.11.2026).
/// </summary>
/// <param name="Kind">The kind of the item, one of <see cref="CargoItemKinds"/>.</param>
/// <param name="Id">The resource or the kind of the part.</param>
/// <param name="MarkLevel">The mark level of a part; zero for a resource.</param>
/// <param name="HasRaceBonus">Whether a part carries the bonus of its race.</param>
/// <param name="NameKey">The key of the name of a part, or <see langword="null"/> for a resource.</param>
/// <param name="Requested">How much the order named.</param>
/// <param name="Loaded">How much was loaded.</param>
/// <param name="ReasonCode">Why the rest was not loaded, one of <see cref="CargoShortfallReasons"/>.</param>
public sealed record CargoShortfall(
    string Kind,
    int Id,
    int MarkLevel,
    bool HasRaceBonus,
    string? NameKey,
    long Requested,
    long Loaded,
    string ReasonCode);

/// <summary>
/// The load and the unload of a fleet on a body of its owner (task 00057).
/// </summary>
/// <remarks>
/// <para>
/// A load takes resources and modules from the stock of the body and puts them into the holds of
/// the ships, in the order the ships joined the fleet. It never stops for want of goods or room
/// (author's rule, 14.11.2026): of every item it loads what the stock has and what the holds still
/// take, a module whole into the hold of one ship, and lists the rest as a shortfall. No module of
/// the ships is needed for a load: the holds are enough.
/// </para>
/// <para>
/// An unload puts what the holds carry back into the stock of the body. It never fails for want of
/// cargo: when the holds carry less than the order names, what is there is put out, and an item the
/// holds do not carry at all is skipped.
/// </para>
/// <para>
/// A resource weighs one tonne a unit, as the data says now (<c>world.resource.default_weight</c>); the
/// weight of a module is the weight of its kind. The orders take no time.
/// </para>
/// </remarks>
public sealed class FleetCargoService
{
    private readonly FleetDbContext _dbContext;
    private readonly FleetHoldService _holds;
    private readonly IPlanetResources _resources;
    private readonly IPartStock _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetCargoService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="holds">The holds of the ships.</param>
    /// <param name="resources">The balance of the bodies.</param>
    /// <param name="parts">The stock of the parts of the bodies.</param>
    public FleetCargoService(FleetDbContext dbContext, FleetHoldService holds, IPlanetResources resources, IPartStock parts)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(parts);

        _dbContext = dbContext;
        _holds = holds;
        _resources = resources;
        _parts = parts;
    }

    /// <summary>
    /// Loads the items from the body the fleet stands on into the holds of the fleet: of every item
    /// what the stock has and what the holds take.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="items">What to load.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// The outcome: done with the items loaded short, or the reason nothing could be loaded because the
    /// request itself is wrong (the fleet is not on a body of its own, or a part is unknown).
    /// </returns>
    public async Task<CargoOutcome> LoadAsync(FleetEntity fleet, IReadOnlyList<CargoItem> items, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(items);

        if (!await StandsOnOwnBodyAsync(fleet, cancellationToken).ConfigureAwait(false))
        {
            return new CargoOutcome(FleetErrorCodes.CargoNoPlanet);
        }

        IReadOnlyDictionary<int, PartRecipeFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

        foreach (CargoItem item in items.Where(entry => entry.Kind == CargoItemKinds.Part))
        {
            if (!facts.TryGetValue(item.Id, out PartRecipeFacts? known) || known.IsArchived)
            {
                return new CargoOutcome(FleetErrorCodes.CargoItemsInvalid);
            }
        }

        // What the body has: the resources by the balance brought to the moment, the modules by the
        // stacks of new parts.
        Dictionary<int, long> resourcesLeft;

        try
        {
            resourcesLeft = await ReadResourcesAsync(fleet, items, cancellationToken).ConfigureAwait(false);
        }
        catch (GameException)
        {
            return new CargoOutcome(FleetErrorCodes.CargoNoPlanet);
        }

        Dictionary<PartKey, long> partsLeft = await ReadPartsAsync(fleet, items, cancellationToken).ConfigureAwait(false);

        // The holds are filled on paper first, item by item in the order of the list: of every item what
        // the stock has, as far as the room goes. The room is counted on a copy.
        IReadOnlyList<ShipHold> holds = await _holds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<long, long> room = holds.ToDictionary(hold => hold.ShipId, hold => hold.Free);
        List<Placement> placements = [];
        List<CargoShortfall> shortfalls = [];
        Dictionary<int, long> resourcesToTake = [];
        Dictionary<PartKey, long> partsToTake = [];

        foreach (CargoItem item in items)
        {
            bool isResource = item.Kind == CargoItemKinds.Resource;
            PartKey partKey = new(item.Id, item.MarkLevel, item.HasRaceBonus);
            long inStock = isResource ? resourcesLeft.GetValueOrDefault(item.Id) : partsLeft.GetValueOrDefault(partKey);
            long wanted = Math.Min(item.Amount, Math.Max(0, inStock));
            decimal weight = isResource ? ResourceWeight : facts[item.Id].Weight;
            long placed = wanted > 0 ? Place(item, wanted, weight, holds, room, placements) : 0;

            if (placed > 0 && isResource)
            {
                resourcesLeft[item.Id] = inStock - placed;
                resourcesToTake[item.Id] = resourcesToTake.GetValueOrDefault(item.Id) + placed;
            }
            else if (placed > 0)
            {
                partsLeft[partKey] = inStock - placed;
                partsToTake[partKey] = partsToTake.GetValueOrDefault(partKey) + placed;
            }

            if (placed < item.Amount)
            {
                shortfalls.Add(new CargoShortfall(
                    item.Kind,
                    item.Id,
                    item.MarkLevel,
                    item.HasRaceBonus,
                    isResource ? null : facts[item.Id].NameKey,
                    item.Amount,
                    placed,
                    placed < wanted ? CargoShortfallReasons.Room : CargoShortfallReasons.Stock));
            }
        }

        if (placements.Count == 0)
        {
            return new CargoOutcome(null) { Shortfalls = shortfalls, LoadedAny = false };
        }

        // The amounts were counted against the balance at the moment and the stacks under their lock, so
        // the taking does not fail; if it did, the error goes up and the whole step is undone with the
        // transaction, not left half done.
        if (resourcesToTake.Count > 0)
        {
            await _resources.TakeAsync(fleet.PlanetId, fleet.PlayerId, resourcesToTake, cancellationToken).ConfigureAwait(false);
        }

        if (partsToTake.Count > 0)
        {
            await _parts.TakeMarkedAsync(
                fleet.PlanetId,
                [.. partsToTake.Select(entry => new ShipPartRequest(entry.Key.PartTypeId, entry.Key.MarkLevel, entry.Key.HasRaceBonus, entry.Value))],
                cancellationToken).ConfigureAwait(false);
        }

        foreach (Placement placement in placements)
        {
            if (placement.Item.Kind == CargoItemKinds.Resource)
            {
                await AddResourceAsync(placement.ShipId, placement.Item.Id, placement.Count, now, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await AddPartAsync(placement.ShipId, placement.Item, placement.Count, now, cancellationToken).ConfigureAwait(false);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await RecountAsync(fleet.Id, now, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CargoOutcome(null) { Shortfalls = shortfalls, LoadedAny = true };
    }

    /// <summary>
    /// Unloads the items from the holds of the fleet into the stock of the body the fleet stands on.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="items">What to unload.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome: done, or the reason nothing was unloaded.</returns>
    public async Task<CargoOutcome> UnloadAsync(FleetEntity fleet, IReadOnlyList<CargoItem> items, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(items);

        if (!await StandsOnOwnBodyAsync(fleet, cancellationToken).ConfigureAwait(false))
        {
            return new CargoOutcome(FleetErrorCodes.CargoNoPlanet);
        }

        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleet.Id)
            .OrderBy(entry => entry.Position)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return new CargoOutcome(null);
        }

        List<ShipCargoEntity> resourceRows = await _dbContext.ShipCargos
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Amount > 0)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<ShipPartCargoEntity> partRows = await _dbContext.ShipPartCargos
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Quantity > 0)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<int, long> resourcesBack = [];
        List<ShipPartStack> partsBack = [];

        foreach (CargoItem item in items)
        {
            long left = item.Amount;

            if (item.Kind == CargoItemKinds.Resource)
            {
                foreach (ShipCargoEntity row in resourceRows.Where(row => row.ResourceId == item.Id).OrderBy(row => Array.IndexOf(shipIds, row.ShipId)))
                {
                    long taken = Math.Min(left, row.Amount);

                    if (taken <= 0)
                    {
                        continue;
                    }

                    row.Amount -= taken;
                    row.UpdatedAt = now;
                    left -= taken;
                    resourcesBack[item.Id] = resourcesBack.GetValueOrDefault(item.Id) + taken;
                }

                continue;
            }

            long partsTaken = 0;

            foreach (ShipPartCargoEntity row in partRows
                .Where(row => row.PartTypeId == item.Id && row.MarkLevel == item.MarkLevel && row.HasRaceBonus == item.HasRaceBonus)
                .OrderBy(row => Array.IndexOf(shipIds, row.ShipId)))
            {
                long taken = Math.Min(left, row.Quantity);

                if (taken <= 0)
                {
                    continue;
                }

                row.Quantity -= taken;
                row.UpdatedAt = now;
                left -= taken;
                partsTaken += taken;
            }

            if (partsTaken > 0)
            {
                partsBack.Add(new ShipPartStack(item.Id, item.MarkLevel, item.HasRaceBonus, PartConditions.Full, partsTaken));
            }
        }

        if (resourcesBack.Count == 0 && partsBack.Count == 0)
        {
            return new CargoOutcome(null);
        }

        if (resourcesBack.Count > 0)
        {
            await _resources.GiveAsync(fleet.PlanetId, fleet.PlayerId, resourcesBack, cancellationToken).ConfigureAwait(false);
        }

        if (partsBack.Count > 0)
        {
            await _parts.LockAsync(fleet.PlanetId, fleet.SectorId, cancellationToken).ConfigureAwait(false);
            await _parts.AddAsync(fleet.PlanetId, partsBack, cancellationToken).ConfigureAwait(false);
        }

        // A stack that is gone leaves no row: the hold lists only what it carries.
        _dbContext.ShipCargos.RemoveRange(resourceRows.Where(row => row.Amount <= 0));
        _dbContext.ShipPartCargos.RemoveRange(partRows.Where(row => row.Quantity <= 0));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await RecountAsync(fleet.Id, now, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CargoOutcome(null);
    }

    /// <summary>
    /// Takes one unit of a module out of the holds of the ships of a fleet (task 00073).
    /// </summary>
    /// <remarks>
    /// The unit is taken from the first ship of the fleet that carries it, in the order the ships
    /// joined the fleet. A module of a hold is never divided: a unit of it is a whole module, so
    /// the hold of the ship that carries it loses one module.
    /// </remarks>
    /// <param name="fleet">The fleet.</param>
    /// <param name="partTypeId">The kind of the module.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a module was taken.</returns>
    public async Task<bool> TakePartAsync(
        FleetEntity fleet,
        int partTypeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleet.Id)
            .OrderBy(entry => entry.Position)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return false;
        }

        List<ShipPartCargoEntity> rows = await _dbContext.ShipPartCargos
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.PartTypeId == partTypeId && cargo.Quantity > 0)
            .OrderBy(cargo => Array.IndexOf(shipIds, cargo.ShipId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        ShipPartCargoEntity? taken = rows.FirstOrDefault();

        if (taken is null)
        {
            return false;
        }

        taken.Quantity -= 1;
        taken.UpdatedAt = now;

        // A stack that is gone leaves no row: the hold lists only what it carries.
        if (taken.Quantity <= 0)
        {
            _dbContext.ShipPartCargos.Remove(taken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await RecountAsync(fleet.Id, now, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Fills the holds on paper with as much of one item as there is room for: which ship takes how much.
    /// The ships are taken in the order they joined the fleet. A resource goes where there is room for it,
    /// a module goes whole into the hold of one ship.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="wanted">How much of the item to place at most.</param>
    /// <param name="weight">The weight of a unit of the item.</param>
    /// <param name="holds">The holds, in the order the ships joined the fleet.</param>
    /// <param name="room">The room that is left in each hold; changed.</param>
    /// <param name="placements">The placements; the new ones are added.</param>
    /// <returns>How much of the item was placed.</returns>
    private static long Place(
        CargoItem item,
        long wanted,
        decimal weight,
        IReadOnlyList<ShipHold> holds,
        Dictionary<long, long> room,
        List<Placement> placements)
    {
        long left = wanted;

        foreach (ShipHold hold in holds)
        {
            if (left <= 0)
            {
                break;
            }

            long free = room[hold.ShipId];
            long fits = weight <= 0m ? left : (long)Math.Floor(free / weight);
            long count = Math.Min(left, fits);

            if (count <= 0)
            {
                continue;
            }

            room[hold.ShipId] = free - (long)Math.Ceiling(count * weight);
            left -= count;
            placements.Add(new Placement(hold.ShipId, item, count));
        }

        return wanted - left;
    }

    /// <summary>
    /// Reads how much of each resource of the load the body has. The balance is brought to the moment
    /// first, so the load sees what the body has produced since the last change and never takes more
    /// than the body has now.
    /// </summary>
    private async Task<Dictionary<int, long>> ReadResourcesAsync(FleetEntity fleet, IReadOnlyList<CargoItem> items, CancellationToken cancellationToken)
    {
        int[] resourceIds = [.. items.Where(item => item.Kind == CargoItemKinds.Resource).Select(item => item.Id).Distinct()];

        if (resourceIds.Length == 0)
        {
            return [];
        }

        IReadOnlyDictionary<int, long> stored = await _resources.GetBalanceAsync(fleet.PlanetId, fleet.PlayerId, cancellationToken).ConfigureAwait(false);

        // A gift of nothing opens the body like a payment does and brings its balance to the moment. Only the
        // resources the body keeps are named, so no row of a resource the body has never had is written.
        Dictionary<int, long> nothing = resourceIds.Where(stored.ContainsKey).ToDictionary(id => id, _ => 0L);

        if (nothing.Count == 0)
        {
            return [];
        }

        await _resources.GiveAsync(fleet.PlanetId, fleet.PlayerId, nothing, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, long> balance = await _resources.GetBalanceAsync(fleet.PlanetId, fleet.PlayerId, cancellationToken).ConfigureAwait(false);

        return resourceIds.ToDictionary(id => id, id => balance.GetValueOrDefault(id));
    }

    /// <summary>
    /// Reads how many whole new modules of each kind, mark and bonus of the load the body has. The
    /// stock is locked first, so the count holds until the modules are taken.
    /// </summary>
    private async Task<Dictionary<PartKey, long>> ReadPartsAsync(FleetEntity fleet, IReadOnlyList<CargoItem> items, CancellationToken cancellationToken)
    {
        int[] partTypeIds = [.. items.Where(item => item.Kind == CargoItemKinds.Part).Select(item => item.Id).Distinct()];

        if (partTypeIds.Length == 0)
        {
            return [];
        }

        await _parts.LockAsync(fleet.PlanetId, fleet.SectorId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ShipPartStack> stock = await _parts.ReadAsync(fleet.PlanetId, partTypeIds, cancellationToken).ConfigureAwait(false);

        return stock
            .Where(stack => stack.ConditionPercent >= PartConditions.Full)
            .GroupBy(stack => new PartKey(stack.PartTypeId, stack.MarkLevel, stack.HasRaceBonus))
            .ToDictionary(group => group.Key, group => group.Sum(stack => stack.Quantity));
    }

    /// <summary>
    /// Whether the fleet stands on a body of its owner, so it may load and unload.
    /// </summary>
    private async Task<bool> StandsOnOwnBodyAsync(FleetEntity fleet, CancellationToken cancellationToken)
    {
        if (fleet.PlanetId <= 0 || fleet.StateCode == FleetStates.Flying)
        {
            return false;
        }

        try
        {
            _ = await _resources.GetBalanceAsync(fleet.PlanetId, fleet.PlayerId, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (GameException)
        {
            return false;
        }
    }

    /// <summary>
    /// Adds an amount of a resource to the hold of a ship.
    /// </summary>
    private async Task AddResourceAsync(long shipId, int resourceId, long amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ShipCargoEntity? cargo = await _dbContext.ShipCargos
            .FirstOrDefaultAsync(entry => entry.ShipId == shipId && entry.ResourceId == resourceId, cancellationToken).ConfigureAwait(false);

        if (cargo is null)
        {
            cargo = new ShipCargoEntity { ShipId = shipId, ResourceId = resourceId, Amount = 0 };
            _dbContext.ShipCargos.Add(cargo);
        }

        cargo.Amount += amount;
        cargo.UpdatedAt = now;
    }

    /// <summary>
    /// Adds modules to the hold of a ship, onto the stack of their kind, mark and bonus.
    /// </summary>
    private async Task AddPartAsync(long shipId, CargoItem item, long quantity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ShipPartCargoEntity? cargo = await _dbContext.ShipPartCargos
            .FirstOrDefaultAsync(
                entry => entry.ShipId == shipId && entry.PartTypeId == item.Id && entry.MarkLevel == item.MarkLevel && entry.HasRaceBonus == item.HasRaceBonus,
                cancellationToken).ConfigureAwait(false);

        if (cargo is null)
        {
            cargo = new ShipPartCargoEntity { ShipId = shipId, PartTypeId = item.Id, MarkLevel = item.MarkLevel, HasRaceBonus = item.HasRaceBonus, Quantity = 0 };
            _dbContext.ShipPartCargos.Add(cargo);
        }

        cargo.Quantity += quantity;
        cargo.UpdatedAt = now;
    }

    /// <summary>
    /// Counts the cargo of the fleet again from the holds of its ships: the fleet carries what its ships carry.
    /// </summary>
    private async Task RecountAsync(long fleetId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return;
        }

        Dictionary<int, long> carried = await _dbContext.ShipCargos
            .Where(cargo => shipIds.Contains(cargo.ShipId))
            .GroupBy(cargo => cargo.ResourceId)
            .Select(group => new { ResourceId = group.Key, Amount = group.Sum(cargo => cargo.Amount) })
            .ToDictionaryAsync(row => row.ResourceId, row => row.Amount, cancellationToken).ConfigureAwait(false);

        List<FleetCargoEntity> rows = await _dbContext.FleetCargos
            .Where(cargo => cargo.FleetId == fleetId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (FleetCargoEntity row in rows)
        {
            row.Amount = carried.GetValueOrDefault(row.ResourceId);
            row.UpdatedAt = now;
        }

        foreach ((int resourceId, long amount) in carried)
        {
            if (rows.Any(row => row.ResourceId == resourceId))
            {
                continue;
            }

            _dbContext.FleetCargos.Add(new FleetCargoEntity
            {
                FleetId = fleetId,
                ResourceId = resourceId,
                Amount = amount,
                UpdatedAt = now,
            });
        }
    }

    /// <summary>The weight of a unit of a resource, in tonnes: the data says one for every resource now.</summary>
    private const decimal ResourceWeight = 1m;

    private sealed record Placement(long ShipId, CargoItem Item, long Count);

    private readonly record struct PartKey(int PartTypeId, int MarkLevel, bool HasRaceBonus);
}
