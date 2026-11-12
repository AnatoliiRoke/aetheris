using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Planets;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The fleets of a player: forming, changing the composition, renaming, disbanding and
/// losing a fleet (task 00040).
/// </summary>
/// <remarks>
/// <para>
/// A fleet stands on the orbit of one body, its home body, and holds finished ships that stand
/// on that body. The size of a fleet is not limited, a fleet keeps at least one ship, and a ship
/// is in one fleet at most. The number of the fleets is limited by the spaceports: the effect
/// of the building gives the number, so the code carries no limit of its own.
/// </para>
/// <para>
/// Every change locks the row of the fleets of the player first, so twenty requests at once do
/// not pass the limit and a ship is not taken apart while it joins a fleet. There is no
/// commander yet: the game has no way to get one, so the fleet has none.
/// </para>
/// </remarks>
public sealed class FleetFormationService
{
    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly IPlanetsModule _planets;
    private readonly IWorldModule _world;
    private readonly IEconomyModule _economy;
    private readonly IProductionModule _production;
    private readonly ShipDesignService _designs;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetFormationService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="planets">The bodies and their buildings.</param>
    /// <param name="world">The world.</param>
    /// <param name="economy">The economy.</param>
    /// <param name="production">The production, for the catalog of the hulls.</param>
    /// <param name="designs">The projects of the ships, for what a fleet carries and deals.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The game clock.</param>
    public FleetFormationService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        IPlanetsModule planets,
        IWorldModule world,
        IEconomyModule economy,
        IProductionModule production,
        ShipDesignService designs,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(designs);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _planets = planets;
        _world = world;
        _economy = economy;
        _production = production;
        _designs = designs;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Reads the fleets of a player and the ships that are in no fleet.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets.</returns>
    public Task<FleetsViewDto> ReadAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return ViewAsync(playerId, cancellationToken);
    }

    /// <summary>
    /// Forms a fleet of free ships that stand on one body of the player. The body is the home
    /// body of the fleet.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the ships stand on.</param>
    /// <param name="name">The name of the fleet.</param>
    /// <param name="shipIds">The ships. The fleet needs at least one.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets after the change.</returns>
    public Task<FleetsViewDto> CreateAsync(
        string playerId,
        long planetId,
        string name,
        IReadOnlyCollection<long> shipIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(shipIds);

        string cleanName = CleanName(name);
        List<long> ships = DistinctShips(shipIds);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetFacts facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false)
                    ?? throw new GameException(FleetErrorCodes.PlanetUnknown, "The body does not exist.");

                if (!string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
                {
                    throw new GameException(FleetErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
                }

                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);

                int limit = await LimitAsync(playerId, cancellationToken).ConfigureAwait(false);
                int used = await _dbContext.Fleets.CountAsync(fleet => fleet.PlayerId == playerId, cancellationToken).ConfigureAwait(false);

                if (used >= limit)
                {
                    throw new GameException(FleetErrorCodes.FleetLimitReached, "The player has as many fleets as the spaceports allow.");
                }

                await CheckFreeShipsAsync(playerId, planetId, ships, cancellationToken).ConfigureAwait(false);

                FleetEntity fleet = new()
                {
                    PlayerId = playerId,
                    Name = cleanName,
                    HomePlanetId = planetId,
                    PlanetId = planetId,
                    SectorId = facts.SectorId,
                    StateCode = FleetStates.Stationed,
                    CreatedAt = _clock.GameNow,
                    ShipIds = [.. ships.Select((shipId, index) => new FleetShipEntity { ShipId = shipId, Position = index + 1 })],
                };

                _dbContext.Fleets.Add(fleet);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, cancellationToken, limit).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Puts free ships into a fleet. The ships must stand on the body of the fleet.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="shipIds">The ships.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets after the change.</returns>
    public Task<FleetsViewDto> AddShipsAsync(
        string playerId,
        long fleetId,
        IReadOnlyCollection<long> shipIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(shipIds);

        List<long> ships = DistinctShips(shipIds);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                await EnsureDockedAsync(fleet, cancellationToken).ConfigureAwait(false);
                await CheckFreeShipsAsync(playerId, fleet.PlanetId, ships, cancellationToken).ConfigureAwait(false);

                Append(fleet, ships);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Takes ships out of a fleet: they stay on the body as free ships. The last ship of a
    /// fleet cannot be taken out, the fleet is disbanded instead.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="shipIds">The ships.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets after the change.</returns>
    public Task<FleetsViewDto> RemoveShipsAsync(
        string playerId,
        long fleetId,
        IReadOnlyCollection<long> shipIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(shipIds);

        List<long> ships = DistinctShips(shipIds);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                await EnsureDockedAsync(fleet, cancellationToken).ConfigureAwait(false);

                Detach(fleet, ships);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Moves ships from one fleet to another fleet that stands on the same body. The first fleet
    /// keeps at least one ship.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet the ships leave.</param>
    /// <param name="targetFleetId">The fleet the ships join.</param>
    /// <param name="shipIds">The ships.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets after the change.</returns>
    public Task<FleetsViewDto> TransferShipsAsync(
        string playerId,
        long fleetId,
        long targetFleetId,
        IReadOnlyCollection<long> shipIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(shipIds);

        List<long> ships = DistinctShips(shipIds);

        if (fleetId == targetFleetId)
        {
            throw new GameException(FleetErrorCodes.FleetsApart, "A ship cannot be moved into the fleet it is in.");
        }

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FleetEntity source = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                FleetEntity target = await LoadFleetAsync(playerId, targetFleetId, cancellationToken).ConfigureAwait(false);

                if (source.PlanetId != target.PlanetId)
                {
                    throw new GameException(FleetErrorCodes.FleetsApart, "The fleets do not stand on the same body.");
                }

                await EnsureDockedAsync(source, cancellationToken).ConfigureAwait(false);
                await EnsureDockedAsync(target, cancellationToken).ConfigureAwait(false);

                Detach(source, ships);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // The ship is unique in the table, so it leaves the first fleet before it joins the second.
                Append(target, ships);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Renames a fleet.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets after the change.</returns>
    public Task<FleetsViewDto> RenameAsync(string playerId, long fleetId, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        string cleanName = CleanName(name);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                fleet.Name = cleanName;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Disbands a fleet: its ships stay on the body as free ships.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets after the change.</returns>
    public Task<FleetsViewDto> DissolveAsync(string playerId, long fleetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                await EnsureDockedAsync(fleet, cancellationToken).ConfigureAwait(false);

                _dbContext.Fleets.Remove(fleet);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Loses a fleet: the fleet and every ship in it are gone for good, with the crew. The battles
    /// of a later stage call this; the player does not.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the fleet is lost.</returns>
    public Task LoseAsync(long fleetId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                string playerId = await _dbContext.Fleets.AsNoTracking()
                    .Where(fleet => fleet.Id == fleetId)
                    .Select(fleet => fleet.PlayerId)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet does not exist.");

                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);

                // The fleet is read again under the lock: a command that held it may have disbanded the fleet.
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                List<long> shipIds = [.. fleet.ShipIds.Select(entry => entry.ShipId)];
                List<ShipEntity> ships = await _dbContext.Ships.Where(ship => shipIds.Contains(ship.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

                // The past interval is charged at the rate that still includes these ships.
                await _economy.FixPlayerAsync(playerId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                _dbContext.Fleets.Remove(fleet);
                _dbContext.Ships.RemoveRange(ships);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await PublishUpkeepAsync(playerId, cancellationToken).ConfigureAwait(false);

                return 0;
            },
            cancellationToken);

    /// <summary>
    /// Reads what the other modules need to know about the fleets of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts.</returns>
    public async Task<FleetFacts> GetFactsAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        int fleets = await _dbContext.Fleets.AsNoTracking().CountAsync(fleet => fleet.PlayerId == playerId, cancellationToken).ConfigureAwait(false);
        int ships = await _dbContext.Ships.AsNoTracking().CountAsync(ship => ship.PlayerId == playerId, cancellationToken).ConfigureAwait(false);
        int inFleets = await _dbContext.FleetShips.AsNoTracking()
            .CountAsync(entry => _dbContext.Fleets.Any(fleet => fleet.Id == entry.FleetId && fleet.PlayerId == playerId), cancellationToken)
            .ConfigureAwait(false);
        Dictionary<int, long> costs = await _dbContext.ShipCosts.AsNoTracking()
            .Where(cost => _dbContext.Ships.Any(ship => ship.Id == cost.ShipId && ship.PlayerId == playerId))
            .GroupBy(cost => cost.ResourceTypeId)
            .Select(group => new { ResourceTypeId = group.Key, Amount = group.Sum(cost => cost.Amount) })
            .ToDictionaryAsync(entry => entry.ResourceTypeId, entry => entry.Amount, cancellationToken)
            .ConfigureAwait(false);

        return new FleetFacts(fleets, ships, inFleets, costs);
    }

    private static List<long> DistinctShips(IReadOnlyCollection<long> shipIds)
    {
        List<long> ships = [.. shipIds.Distinct()];

        if (ships.Count == 0)
        {
            throw new GameException(FleetErrorCodes.FleetNeedsShip, "The request names no ship.");
        }

        return ships;
    }

    private static void Append(FleetEntity fleet, IReadOnlyList<long> shipIds)
    {
        int position = fleet.ShipIds.Count == 0 ? 0 : fleet.ShipIds.Max(entry => entry.Position);

        foreach (long shipId in shipIds)
        {
            position++;
            fleet.ShipIds.Add(new FleetShipEntity { ShipId = shipId, Position = position });
        }
    }

    private static void Detach(FleetEntity fleet, IReadOnlyList<long> shipIds)
    {
        List<FleetShipEntity> leaving = [.. fleet.ShipIds.Where(entry => shipIds.Contains(entry.ShipId))];

        if (leaving.Count != shipIds.Count)
        {
            throw new GameException(FleetErrorCodes.ShipNotInFleet, "A ship is not in the fleet.");
        }

        if (leaving.Count == fleet.ShipIds.Count)
        {
            throw new GameException(FleetErrorCodes.FleetLastShip, "A fleet keeps at least one ship.");
        }

        foreach (FleetShipEntity entry in leaving)
        {
            fleet.ShipIds.Remove(entry);
        }
    }

    private string CleanName(string? name)
    {
        string clean = name?.Trim() ?? string.Empty;
        int maxLength = FleetSettings.From(_config).FleetNameMaxLength;

        if (clean.Length == 0 || clean.Length > maxLength)
        {
            throw new GameException(FleetErrorCodes.NameInvalid, "The name of the fleet is empty or too long.");
        }

        return clean;
    }

    private async Task<FleetEntity> LoadFleetAsync(string playerId, long fleetId, CancellationToken cancellationToken) =>
        await _dbContext.Fleets
            .Include(fleet => fleet.ShipIds)
            .SingleOrDefaultAsync(fleet => fleet.Id == fleetId && fleet.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet does not exist.");

    /// <summary>
    /// Checks that the fleet stands on a body together with its ships and has no plan under way
    /// (task 00053): a fleet that flies, that works at a field or that stands at a body that is not
    /// the body of its ships does not give its ships back, so a ship cannot be moved by taking it out
    /// of a fleet that is far away.
    /// </summary>
    /// <param name="fleet">The fleet with its ships.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the fleet is docked.</returns>
    /// <exception cref="GameException">The fleet is away.</exception>
    private async Task EnsureDockedAsync(FleetEntity fleet, CancellationToken cancellationToken)
    {
        bool away = !string.Equals(fleet.StateCode, FleetStates.Stationed, StringComparison.Ordinal) || fleet.PlanetId <= 0;

        if (!away)
        {
            long[] shipIds = [.. fleet.ShipIds.Select(entry => entry.ShipId)];
            away = await _dbContext.Ships.AsNoTracking()
                .AnyAsync(ship => shipIds.Contains(ship.Id) && ship.PlanetId != fleet.PlanetId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!away)
        {
            away = await _dbContext.FlightPlans.AsNoTracking()
                .AnyAsync(plan => plan.FleetId == fleet.Id && plan.StateCode == FlightPlanStates.Active, cancellationToken)
                .ConfigureAwait(false);
        }

        if (away)
        {
            throw new GameException(FleetErrorCodes.FleetAway, "The fleet is away from the body of its ships or its plan is under way.");
        }
    }

    /// <summary>
    /// Checks that every ship is the player's, stands on the body and is in no fleet.
    /// </summary>
    private async Task CheckFreeShipsAsync(string playerId, long planetId, IReadOnlyList<long> shipIds, CancellationToken cancellationToken)
    {
        List<long> stands = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId && shipIds.Contains(ship.Id))
            .Select(ship => ship.PlanetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stands.Count != shipIds.Count)
        {
            throw new GameException(FleetErrorCodes.ShipUnknown, "A ship does not exist.");
        }

        if (stands.Any(standsOn => standsOn != planetId))
        {
            throw new GameException(FleetErrorCodes.ShipElsewhere, "A ship does not stand on the body of the fleet.");
        }

        if (await _dbContext.FleetShips.AsNoTracking().AnyAsync(entry => shipIds.Contains(entry.ShipId), cancellationToken).ConfigureAwait(false))
        {
            throw new GameException(FleetErrorCodes.ShipInFleet, "A ship is in a fleet already.");
        }
    }

    private async Task<int> LimitAsync(string playerId, CancellationToken cancellationToken)
    {
        OwnerBuildingBonus bonus = await _planets.GetOwnerBuildingBonusAsync(playerId, cancellationToken).ConfigureAwait(false);

        return bonus.FleetLimit <= 0m ? 0 : (int)decimal.Floor(bonus.FleetLimit);
    }

    private async Task PublishUpkeepAsync(string playerId, CancellationToken cancellationToken)
    {
        decimal perHour = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId)
            .SumAsync(ship => (decimal?)ship.UpkeepPerHour, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        await _economy.SetShipUpkeepPerHourAsync(playerId, perHour, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FleetsViewDto> ViewAsync(string playerId, CancellationToken cancellationToken, int? knownLimit = null)
    {
        // A command that already read the limit passes it on, so the answer does not read it twice.
        int limit = knownLimit ?? await LimitAsync(playerId, cancellationToken).ConfigureAwait(false);
        List<FleetEntity> fleets = await _dbContext.Fleets.AsNoTracking()
            .Include(fleet => fleet.ShipIds)
            .Where(fleet => fleet.PlayerId == playerId)
            .OrderBy(fleet => fleet.CreatedAt)
            .ThenBy(fleet => fleet.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The hull and the condition are summed in the query, so the parts of the ships are not loaded.
        var ships = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId)
            .OrderBy(ship => ship.PlanetId)
            .ThenBy(ship => ship.Id)
            .Select(ship => new
            {
                ship.Id,
                ship.PlanetId,
                ship.ShipDesignId,
                ship.Crew,
                ship.UpkeepPerHour,
                HullPartTypeId = ship.ModuleIds.Where(module => module.IsHull).Select(module => module.PartTypeId).FirstOrDefault(),
                Condition = ship.ModuleIds.Min(module => (int?)module.ConditionPercent),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, string> designNames = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => design.OwnerPlayerId == playerId)
            .ToDictionaryAsync(design => design.Id, design => design.Name, cancellationToken)
            .ConfigureAwait(false);
        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);

        // What a fleet carries and deals is the sum of the summaries of the projects of its ships.
        Dictionary<long, ShipDesignSummaryDto> summaries = (await _designs.ReadAsync(playerId, null, true, cancellationToken).ConfigureAwait(false))
            .ToDictionary(design => design.Id, design => design.Summary);
        Dictionary<int, ShipHullDto> hulls = catalog.Hulls.ToDictionary(hull => hull.Id);
        long[] fleetIds = [.. fleets.Select(fleet => fleet.Id)];
        Dictionary<long, FleetFlightEntity> flights = fleetIds.Length == 0
            ? []
            : await _dbContext.FleetFlights.AsNoTracking()
                .Where(flight => fleetIds.Contains(flight.FleetId))
                .ToDictionaryAsync(flight => flight.FleetId, cancellationToken)
                .ConfigureAwait(false);
        Dictionary<long, long> fleetOfShip = fleets
            .SelectMany(fleet => fleet.ShipIds.Select(entry => (entry.ShipId, FleetId: fleet.Id)))
            .ToDictionary(entry => entry.ShipId, entry => entry.FleetId);

        Dictionary<long, FleetShipDto> cards = ships.ToDictionary(
            ship => ship.Id,
            ship =>
            {
                hulls.TryGetValue(ship.HullPartTypeId, out ShipHullDto? hull);

                return new FleetShipDto(
                    ship.Id,
                    ship.PlanetId,
                    ship.ShipDesignId,
                    designNames.GetValueOrDefault(ship.ShipDesignId) ?? string.Empty,
                    hull?.NameKey ?? string.Empty,
                    hull?.PartGroupNameKey ?? string.Empty,
                    ship.Condition ?? 0,
                    ship.Crew,
                    ship.UpkeepPerHour,
                    fleetOfShip.TryGetValue(ship.Id, out long fleetId) ? fleetId : null);
            });

        List<FleetDto> fleetCards =
        [
            .. fleets.Select(fleet =>
            {
                List<FleetShipDto> members =
                [
                    .. fleet.ShipIds
                        .OrderBy(entry => entry.Position)
                        .Where(entry => cards.ContainsKey(entry.ShipId))
                        .Select(entry => cards[entry.ShipId]),
                ];

                List<ShipDesignSummaryDto> carried = [.. members.Where(member => summaries.ContainsKey(member.DesignId)).Select(member => summaries[member.DesignId])];

                return new FleetDto(
                    fleet.Id,
                    fleet.Name,
                    fleet.HomePlanetId,
                    fleet.PlanetId,
                    fleet.SectorId,
                    fleet.StateCode,
                    members.Sum(member => member.Crew),
                    members.Sum(member => member.UpkeepPerHour),
                    fleet.CreatedAt,
                    members,
                    carried.Sum(summary => summary.HoldCapacity),
                    carried.Sum(summary => summary.Integrity),
                    carried.Sum(summary => summary.DamageMin),
                    carried.Sum(summary => summary.DamageMax),
                    carried.Sum(summary => summary.ShieldCapacity),
                    fleet.ObjectId,
                    fleet.SystemId,
                    fleet.PositionX,
                    fleet.PositionY,
                    fleet.PositionZ,
                    flights.TryGetValue(fleet.Id, out FleetFlightEntity? flight)
                        ? new FleetFlightDto(
                            flight.FlightPlanOrderId,
                            flight.StartedAt,
                            flight.ArrivesAt,
                            flight.Distance,
                            flight.Speed,
                            flight.FromSectorId,
                            flight.FromSystemId,
                            flight.FromOrbit,
                            flight.FromX,
                            flight.FromY,
                            flight.FromZ,
                            flight.ToSectorId,
                            flight.ToSystemId,
                            flight.ToObjectId,
                            flight.ToPlanetId,
                            flight.ToOrbit,
                            flight.ToX,
                            flight.ToY,
                            flight.ToZ)
                        : null);
            }),
        ];

        List<FleetShipDto> free = [.. cards.Values.Where(card => card.FleetId is null).OrderBy(card => card.PlanetId).ThenBy(card => card.Id)];
        bool hasCredits = await HasCreditsAsync(playerId, cancellationToken).ConfigureAwait(false);

        return new FleetsViewDto(
            limit,
            fleets.Count,
            hasCredits,
            ships.Sum(ship => ship.UpkeepPerHour),
            fleetCards,
            free,
            _clock.GameNow,
            _clock.Multiplier);
    }

    /// <summary>
    /// Tells whether the civilization has credits. The credit is the currency that the population
    /// earns and that never goes into debt.
    /// </summary>
    private async Task<bool> HasCreditsAsync(string playerId, CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        int? creditId = catalog.Resources
            .Where(resource => resource.IsCivilizationScope && resource.EarnsPopulationIncome && !resource.AllowsDebt)
            .OrderBy(resource => resource.Id)
            .Select(resource => (int?)resource.Id)
            .FirstOrDefault();

        if (creditId is not int id)
        {
            return false;
        }

        CurrencyBalanceFacts balance = await _economy.GetBalanceAsync(playerId, id, cancellationToken).ConfigureAwait(false);

        return balance.Amount > 0;
    }
}
