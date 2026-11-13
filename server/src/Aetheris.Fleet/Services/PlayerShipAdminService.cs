using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The finished ships of a player in the admin panel: one row per project, and the count of
/// the ships of that project. A fleet is not edited here.
/// </summary>
public sealed class PlayerShipAdminService
{
    /// <summary>
    /// How many ships one change may add or remove. A larger change is refused, so one request
    /// cannot write an unbounded number of rows.
    /// </summary>
    private const int MaximumShipsPerChange = 1000;

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly ShipyardQueueService _shipyard;
    private readonly IEconomyModule _economy;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerShipAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="shipyard">The quote of a project, used when the player has no ship of it yet.</param>
    /// <param name="economy">The upkeep ledger and the count of the credits up to the change.</param>
    /// <param name="clock">The clock of the game. A granted ship is created at the current moment.</param>
    public PlayerShipAdminService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        ShipyardQueueService shipyard,
        IEconomyModule economy,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(shipyard);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _shipyard = shipyard;
        _economy = economy;
        _clock = clock;
    }

    /// <summary>
    /// Lists every project of the player, archived ones included, with the count of its finished
    /// ships. A project with no ship is still listed, with a count of zero.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The projects and their counts.</returns>
    public async Task<IReadOnlyList<PlayerShipQuantityRow>> ReadAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        var designRows = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => design.OwnerPlayerId == playerId)
            .OrderBy(design => design.Name)
            .ThenBy(design => design.Id)
            .Select(design => new { design.Id, design.Name, design.PlanetId, design.IsArchived })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var shipCounts = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId)
            .GroupBy(ship => ship.ShipDesignId)
            .Select(group => new { DesignId = group.Key, Quantity = group.LongCount() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, long> quantityByDesignId = shipCounts.ToDictionary(count => count.DesignId, count => count.Quantity);

        return
        [
            .. designRows.Select(design => new PlayerShipQuantityRow(
                design.Id,
                design.Name,
                design.PlanetId,
                design.IsArchived,
                quantityByDesignId.GetValueOrDefault(design.Id))),
        ];
    }

    /// <summary>
    /// Sets how many finished ships of a project the player has. Ships already in a fleet stay
    /// there: the count cannot fall below their number, and no ship is taken out of a fleet.
    /// A new ship is not paid for. It copies a ship of the same project when one exists, and
    /// otherwise it is built from the project. The upkeep ledger is the sum of the ships afterwards.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="designId">The project.</param>
    /// <param name="quantity">The count after the change.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The count before the change and the count after it.</returns>
    public Task<(long Before, long After)> SetQuantityAsync(
        string playerId,
        long designId,
        long quantity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (quantity < 0)
        {
            throw new GameException(PlayerAdminCodes.AmountNegative, "The quantity of the ships cannot be negative.");
        }

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);

                long? designPlanetId = await _dbContext.ShipDesigns.AsNoTracking()
                    .Where(design => design.Id == designId && design.OwnerPlayerId == playerId)
                    .Select(design => (long?)design.PlanetId)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (designPlanetId is not long planetId)
                {
                    throw new GameException(GameErrorCodes.NotFound, "The project does not exist.");
                }

                long standingQuantity = await CountAsync(playerId, designId, inFleetOnly: false, cancellationToken).ConfigureAwait(false);

                if (standingQuantity == quantity)
                {
                    return (standingQuantity, quantity);
                }

                long fleetQuantity = await CountAsync(playerId, designId, inFleetOnly: true, cancellationToken).ConfigureAwait(false);

                if (quantity < fleetQuantity)
                {
                    throw new GameException(FleetErrorCodes.ShipInFleet, "Ships of the project stand in a fleet.");
                }

                long changedQuantity = quantity > standingQuantity ? quantity - standingQuantity : standingQuantity - quantity;

                if (changedQuantity > MaximumShipsPerChange)
                {
                    throw new GameException(FleetErrorCodes.QuantityInvalid, "The change of the quantity does not fit one request.");
                }

                int changed = (int)changedQuantity;

                // The quote of a project reads the catalog before the economy is locked. The economy
                // stays the last lock, the same order the shipyard uses.
                GrantedShip? granted = quantity > standingQuantity
                    ? await DescribeAsync(playerId, designId, planetId, standingQuantity, cancellationToken).ConfigureAwait(false)
                    : null;

                // The past interval is charged at the rate that still has the old ships.
                await _economy.FixPlayerAsync(playerId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                if (granted is not null)
                {
                    Add(playerId, designId, granted, changed);
                }
                else
                {
                    await RemoveAsync(playerId, designId, changed, cancellationToken).ConfigureAwait(false);
                }

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await PublishUpkeepAsync(playerId, cancellationToken).ConfigureAwait(false);

                return (standingQuantity, quantity);
            },
            cancellationToken);
    }

    private async Task<long> CountAsync(string playerId, long designId, bool inFleetOnly, CancellationToken cancellationToken)
    {
        IQueryable<ShipEntity> ships = _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId && ship.ShipDesignId == designId);

        if (inFleetOnly)
        {
            ships = ships.Where(ship => _dbContext.FleetShips.Any(membership => membership.ShipId == ship.Id));
        }

        return await ships.LongCountAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<GrantedShip> DescribeAsync(
        string playerId,
        long designId,
        long planetId,
        long standingQuantity,
        CancellationToken cancellationToken)
    {
        if (standingQuantity > 0)
        {
            ShipEntity sample = await _dbContext.Ships.AsNoTracking()
                .Include(ship => ship.ModuleIds)
                .Where(ship => ship.PlayerId == playerId && ship.ShipDesignId == designId)
                .OrderByDescending(ship => ship.Id)
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);

            return new GrantedShip(
                planetId,
                sample.Crew,
                sample.UpkeepPerHour,
                [
                    .. sample.ModuleIds.Select(module => new GrantedPart(
                        module.PartTypeId,
                        module.MarkLevel,
                        module.HasRaceBonus,
                        module.ConditionPercent,
                        module.ConditionCode,
                        module.Quantity,
                        module.IsHull)),
                ]);
        }

        PlayerShipGrant grant = await _shipyard.GrantOfAsync(playerId, designId, cancellationToken).ConfigureAwait(false);

        return new GrantedShip(
            grant.PlanetId,
            grant.Crew,
            grant.UpkeepPerHour,
            [
                .. grant.PartLines
                    .Where(line => line.Quantity > 0)
                    .Select(line => new GrantedPart(
                        line.PartTypeId,
                        line.MarkLevel,
                        line.HasRaceBonus,
                        PartConditions.Full,
                        ShipModuleConditions.Intact,
                        line.Quantity,
                        line.IsHull)),
            ]);
    }

    private void Add(string playerId, long designId, GrantedShip granted, int added)
    {
        DateTimeOffset createdAt = _clock.GameNow;

        for (int index = 0; index < added; index++)
        {
            _dbContext.Ships.Add(new ShipEntity
            {
                PlayerId = playerId,
                PlanetId = granted.PlanetId,
                ShipDesignId = designId,
                Crew = granted.Crew,
                UpkeepPerHour = granted.UpkeepPerHour,
                StateCode = ShipStates.Ready,
                CreatedAt = createdAt,
                ModuleIds = ModulesOf(granted.PartLines),
                CostIds = [],
            });
        }
    }

    private async Task RemoveAsync(string playerId, long designId, int removed, CancellationToken cancellationToken)
    {
        List<long> doomedShipIds = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId && ship.ShipDesignId == designId)
            .Where(ship => !_dbContext.FleetShips.Any(membership => membership.ShipId == ship.Id))
            .OrderByDescending(ship => ship.Id)
            .Select(ship => ship.Id)
            .Take(removed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (doomedShipIds.Count < removed)
        {
            throw new GameException(FleetErrorCodes.ShipInFleet, "Ships of the project stand in a fleet.");
        }

        await _dbContext.Ships
            .Where(ship => doomedShipIds.Contains(ship.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PublishUpkeepAsync(string playerId, CancellationToken cancellationToken)
    {
        decimal perHour = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId)
            .SumAsync(ship => (decimal?)ship.UpkeepPerHour, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        await _economy.SetShipUpkeepPerHourAsync(playerId, perHour, cancellationToken).ConfigureAwait(false);
    }

    private static List<ShipModuleEntity> ModulesOf(IReadOnlyList<GrantedPart> lines) =>
    [
        .. lines.Select(line => new ShipModuleEntity
        {
            PartTypeId = line.PartTypeId,
            MarkLevel = line.MarkLevel,
            HasRaceBonus = line.HasRaceBonus,
            ConditionPercent = line.ConditionPercent,
            ConditionCode = line.ConditionCode,
            Quantity = line.Quantity,
            IsHull = line.IsHull,
        }),
    ];

    private sealed record GrantedShip(long PlanetId, long Crew, decimal UpkeepPerHour, IReadOnlyList<GrantedPart> PartLines);

    private sealed record GrantedPart(
        int PartTypeId,
        int MarkLevel,
        bool HasRaceBonus,
        int ConditionPercent,
        string ConditionCode,
        long Quantity,
        bool IsHull);
}

/// <summary>The finished ships of one project.</summary>
/// <param name="DesignId">The project.</param>
/// <param name="Name">The name of the project.</param>
/// <param name="PlanetId">The body the project was made on. A granted ship stands there.</param>
/// <param name="IsArchived">Whether the project is archived.</param>
/// <param name="Quantity">How many finished ships of the project exist. Zero is a real count.</param>
public sealed record PlayerShipQuantityRow(long DesignId, string Name, long PlanetId, bool IsArchived, long Quantity);

/// <summary>One ship an administrator grants from a project.</summary>
/// <param name="PlanetId">The body the ship stands on.</param>
/// <param name="Crew">The crew of the ship.</param>
/// <param name="UpkeepPerHour">The credits the ship takes per hour.</param>
/// <param name="PartLines">The parts of the ship.</param>
public sealed record PlayerShipGrant(long PlanetId, long Crew, decimal UpkeepPerHour, IReadOnlyList<PlayerShipPartLine> PartLines);

/// <summary>A part of a granted ship.</summary>
/// <param name="PartTypeId">The part.</param>
/// <param name="MarkLevel">The mark of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="Quantity">How many parts.</param>
/// <param name="IsHull">Whether the stack is the hull.</param>
public sealed record PlayerShipPartLine(int PartTypeId, int MarkLevel, bool HasRaceBonus, long Quantity, bool IsHull);
