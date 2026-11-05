using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Planets;
using Aetheris.Production;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The projects of the ships: the builder of the ships writes them, reads them and keeps them
/// (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// <para>
/// The service is the only source of truth about the rules of the constructor: the number of
/// the slots of the hull, the mandatory components, the energy, the role and the belonging of
/// every module. The client shows the reasons of a refusal, it never decides them.
/// </para>
/// <para>
/// The catalog of the ships comes from the production module through its contract, so this
/// module never reaches into the tables of the other modules (documentation/tech/01, section 8).
/// </para>
/// </remarks>
public sealed class ShipDesignService
{
    /// <summary>
    /// The code of the component that gives the ship its movement.
    /// </summary>
    public const string DriveComponent = "drive";

    /// <summary>
    /// The code of the component that gives the ship its energy.
    /// </summary>
    public const string ReactorComponent = "reactor";

    /// <summary>
    /// The code of the component that gives the ship its crew.
    /// </summary>
    public const string LifeSupportComponent = "life_support";

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly IProductionModule _production;
    private readonly IPlanetsModule _planets;
    private readonly IGameClock _clock;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipDesignService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="production">The production module, for the catalog of the ships.</param>
    /// <param name="planets">The planets module, for the limit of the projects.</param>
    /// <param name="clock">The clock of the game.</param>
    /// <param name="config">The configuration of the game.</param>
    public ShipDesignService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        IProductionModule production,
        IPlanetsModule planets,
        IGameClock clock,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _production = production;
        _planets = planets;
        _clock = clock;
        _config = config;
    }

    /// <summary>
    /// Creates a project of a ship.
    /// </summary>
    /// <param name="playerId">The owner of the project.</param>
    /// <param name="planetId">The body the project is made on.</param>
    /// <param name="name">The name of the project.</param>
    /// <param name="hullPartTypeId">The hull of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project after the change.</returns>
    /// <exception cref="GameException">The player may not create the project.</exception>
    public Task<ShipDesignDto> CreateAsync(
        string playerId,
        long planetId,
        string name,
        int hullPartTypeId,
        bool hasRaceBonusSlot,
        CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                string checkedName = CheckedName(name);

                ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
                ShipHullDto hull = FindHull(catalog, hullPartTypeId);

                CheckRaceBonusSlot(hull, hasRaceBonusSlot);
                CheckHold(hull);

                int limit = await DesignLimitAsync(playerId, cancellationToken).ConfigureAwait(false);

                // Two parallel commands of one player must not read the same number of the
                // projects and both write one (task 00038, criterion 9). The lock is a
                // PostgreSQL advisory lock: it has no row of its own, so it works even when the
                // player has no projects yet, and it is released with the transaction.
                await LockProjectsAsync(playerId, cancellationToken).ConfigureAwait(false);

                int count = await _dbContext.ShipDesigns.AsNoTracking()
                    .CountAsync(design => design.OwnerPlayerId == playerId && !design.IsArchived, cancellationToken)
                    .ConfigureAwait(false);

                if (count >= limit)
                {
                    throw new GameException(FleetErrorCodes.DesignLimitReached, "The player has as many projects as the design bureau allows.");
                }

                DateTimeOffset now = _clock.GameNow;

                ShipDesignEntity design = new()
                {
                    OwnerPlayerId = playerId,
                    PlanetId = planetId,
                    Name = checkedName,
                    HullPartTypeId = hull.Id,
                    HasRaceBonusSlot = hasRaceBonusSlot,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsArchived = false,
                    Version = 1,
                };

                _dbContext.ShipDesigns.Add(design);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await BuildAsync(design, catalog, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Renames a project of a ship.
    /// </summary>
    /// <param name="playerId">The owner of the project.</param>
    /// <param name="designId">The project.</param>
    /// <param name="name">The new name of the project.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project after the change.</returns>
    /// <exception cref="GameException">The project does not belong to the player or is archived.</exception>
    public Task<ShipDesignDto> RenameAsync(
        string playerId,
        long designId,
        string name,
        CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
                ShipDesignEntity design = await OwnedDesignAsync(playerId, designId, cancellationToken).ConfigureAwait(false);

                design.Name = CheckedName(name);
                design.UpdatedAt = _clock.GameNow;
                design.Version += 1;

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await BuildAsync(design, catalog, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Saves the hull and the slots of a project of a ship, after every check of the
    /// constructor (task 00038, section 3.2).
    /// </summary>
    /// <param name="playerId">The owner of the project.</param>
    /// <param name="designId">The project.</param>
    /// <param name="hullPartTypeId">The hull of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <param name="slots">The slots of the project.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project after the change.</returns>
    /// <exception cref="GameException">The project does not belong to the player, is archived or the hull does not take the slots.</exception>
    public Task<ShipDesignDto> SaveAsync(
        string playerId,
        long designId,
        int hullPartTypeId,
        bool hasRaceBonusSlot,
        IReadOnlyList<ShipDesignSlotInput> slots,
        CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(slots);

                ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
                ShipDesignEntity design = await OwnedDesignAsync(playerId, designId, cancellationToken).ConfigureAwait(false);

                ShipHullDto hull = FindHull(catalog, hullPartTypeId);
                CheckRaceBonusSlot(hull, hasRaceBonusSlot);
                CheckHold(hull);

                // The hull of the project changed, so the slots that do not fit the new hull
                // are cleared (task 00038, section 6, question 7). The hull stayed, so every
                // slot of the request must fit it.
                bool hullChanged = design.HullPartTypeId != hull.Id;

                List<ShipDesignSlotEntity> rows = BuildSlots(catalog, hull, hasRaceBonusSlot, slots, hullChanged);
                CheckDesign(catalog, hull, hasRaceBonusSlot, rows);

                await ReplaceSlotsAsync(design, rows, cancellationToken).ConfigureAwait(false);

                design.HullPartTypeId = hull.Id;
                design.HasRaceBonusSlot = hasRaceBonusSlot;
                design.UpdatedAt = _clock.GameNow;
                design.Version += 1;

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await BuildAsync(design, catalog, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Archives a project of a ship: it is kept, so a ship already built of it does not lose
    /// its link (task 00023).
    /// </summary>
    /// <param name="playerId">The owner of the project.</param>
    /// <param name="designId">The project.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project after the change.</returns>
    /// <exception cref="GameException">The project does not belong to the player or is archived already.</exception>
    public Task<ShipDesignDto> ArchiveAsync(
        string playerId,
        long designId,
        CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
                ShipDesignEntity design = await OwnedDesignAsync(playerId, designId, cancellationToken).ConfigureAwait(false);

                if (design.IsArchived)
                {
                    throw new GameException(FleetErrorCodes.DesignArchived, "The project is archived already.");
                }

                design.IsArchived = true;
                design.UpdatedAt = _clock.GameNow;
                design.Version += 1;

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return await BuildAsync(design, catalog, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Reads the projects of a player.
    /// </summary>
    /// <param name="playerId">The owner of the projects.</param>
    /// <param name="planetId">The body whose projects are wanted, or <see langword="null"/> for the projects of every body.</param>
    /// <param name="includeArchived">Whether the archived projects are read too.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The projects, newest first.</returns>
    public async Task<IReadOnlyList<ShipDesignDto>> ReadAsync(
        string playerId,
        long? planetId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<ShipDesignEntity> query = _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => design.OwnerPlayerId == playerId);

        if (planetId is not null)
        {
            query = query.Where(design => design.PlanetId == planetId.Value);
        }

        if (!includeArchived)
        {
            query = query.Where(design => !design.IsArchived);
        }

        List<ShipDesignEntity> designs = await query
            .OrderByDescending(design => design.UpdatedAt)
            .ThenByDescending(design => design.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await BuildAllAsync(designs, catalog, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one project of a player.
    /// </summary>
    /// <param name="playerId">The owner of the project.</param>
    /// <param name="designId">The project.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project, or <see langword="null"/> when the player has no such project.</returns>
    public async Task<ShipDesignDto?> ReadOneAsync(
        string playerId,
        long designId,
        CancellationToken cancellationToken)
    {
        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);

        ShipDesignEntity? design = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(entry => entry.Id == designId && entry.OwnerPlayerId == playerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (design is null)
        {
            return null;
        }

        return await BuildAsync(design, catalog, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the catalog of the ships for the builder, with what the body has on its stock.
    /// </summary>
    /// <param name="playerId">The owner of the stock.</param>
    /// <param name="planetId">The body whose stock is read.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog of the ships.</returns>
    public async Task<ShipCatalogViewDto> ReadCatalogAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        ProductionStockDto stock = await _production.GetStockAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        Dictionary<int, long> stockByPart = [];
        foreach (ProductionStackDto stack in stock.Stacks)
        {
            stockByPart[stack.PartTypeId] = stockByPart.GetValueOrDefault(stack.PartTypeId) + stack.Quantity;
        }

        int limit = await DesignLimitAsync(playerId, cancellationToken).ConfigureAwait(false);
        int count = await _dbContext.ShipDesigns.AsNoTracking()
            .CountAsync(design => design.OwnerPlayerId == playerId && !design.IsArchived, cancellationToken)
            .ConfigureAwait(false);

        List<ShipHullViewDto> hulls =
        [
            .. catalog.Hulls
                .Where(hull => IsShipHull(hull.Role))
                .Select(hull => new ShipHullViewDto(
                    hull.Id,
                    hull.Code,
                    hull.NameKey,
                    hull.DescriptionKey,
                    hull.ImageKey,
                    hull.PartGroupCode,
                    hull.Role,
                    hull.Slots,
                    hull.HoldCapacity,
                    hull.Integrity,
                    hull.Weight,
                    hull.RaceBonusSlots > 0,
                    stockByPart.GetValueOrDefault(hull.Id)))
        ];

        List<ShipModuleViewDto> modules =
        [
            .. catalog.Modules
                .Where(module => IsShipModule(module.Role))
                .Select(module => new ShipModuleViewDto(
                    module.Id,
                    module.Code,
                    module.NameKey,
                    module.DescriptionKey,
                    module.ImageKey,
                    module.PartGroupCode,
                    module.Role,
                    module.Component,
                    module.CompatibleHullIds,
                    module.RaceIds,
                    module.EnergyUse,
                    module.Power,
                    module.Crew,
                    module.Weight,
                    module.MarkLevels,
                    stockByPart.GetValueOrDefault(module.Id)))
        ];

        return new ShipCatalogViewDto(hulls, modules, limit, count);
    }

    /// <summary>
    /// Locks the projects of the player for the rest of the transaction.
    /// </summary>
    /// <param name="playerId">The owner of the projects.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the lock is taken.</returns>
    private async Task LockProjectsAsync(string playerId, CancellationToken cancellationToken) =>
        await _dbContext.Database
            .ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))", [playerId], cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Reads the largest number of the projects the player may keep: the effect of the design
    /// bureau of the buildings of the bodies of the player (task 00038, section 6, question 1).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the projects.</returns>
    private async Task<int> DesignLimitAsync(string playerId, CancellationToken cancellationToken)
    {
        OwnerBuildingBonus bonus = await _planets.GetOwnerBuildingBonusAsync(playerId, cancellationToken).ConfigureAwait(false);

        return Convert.ToInt32(bonus.DesignQueueSlots, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the project of the player and refuses the one that is not his.
    /// </summary>
    /// <param name="playerId">The owner of the project.</param>
    /// <param name="designId">The project.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The project.</returns>
    /// <exception cref="GameException">The project does not exist, is not the player's or is archived.</exception>
    private async Task<ShipDesignEntity> OwnedDesignAsync(string playerId, long designId, CancellationToken cancellationToken)
    {
        ShipDesignEntity? design = await _dbContext.ShipDesigns
            .Where(entry => entry.Id == designId && entry.OwnerPlayerId == playerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (design is null)
        {
            throw new GameException(FleetErrorCodes.DesignUnknown, "The project does not exist or does not belong to the player.");
        }

        if (design.IsArchived)
        {
            throw new GameException(FleetErrorCodes.DesignArchived, "The project is archived.");
        }

        return design;
    }

    /// <summary>
    /// Checks the name of a project.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The checked name.</returns>
    /// <exception cref="GameException">The name is empty or too long.</exception>
    private string CheckedName(string name)
    {
        FleetSettings settings = FleetSettings.From(_config);
        string trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0 || trimmed.Length > settings.DesignNameMaxLength)
        {
            throw new GameException(FleetErrorCodes.NameInvalid, "The name of the project is empty or too long.");
        }

        return trimmed;
    }

    /// <summary>
    /// Finds the hull of the catalog and refuses the one that is not a hull of a ship.
    /// </summary>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="hullPartTypeId">The hull.</param>
    /// <returns>The hull.</returns>
    /// <exception cref="GameException">The hull does not exist, is a drone or is archived.</exception>
    private static ShipHullDto FindHull(ShipCatalogDto catalog, int hullPartTypeId)
    {
        ShipHullDto? hull = catalog.Hulls.FirstOrDefault(entry => entry.Id == hullPartTypeId);

        if (hull is null)
        {
            throw new GameException(FleetErrorCodes.HullUnknown, "The hull does not exist.");
        }

        if (!IsShipHull(hull.Role))
        {
            throw new GameException(FleetErrorCodes.HullIsDrone, "The hull is not a hull of a ship.");
        }

        if (hull.IsArchived)
        {
            throw new GameException(FleetErrorCodes.HullArchived, "The hull is archived.");
        }

        return hull;
    }

    /// <summary>
    /// Tells whether the hull is a drone: a drone is carried in the hold and is never fitted to
    /// a hull (task 00042, decision 3).
    /// </summary>
    /// <param name="role">The role of the hull.</param>
    /// <returns><see langword="true"/> when the hull is a drone.</returns>
    private static bool IsDrone(string role) => string.Equals(role, HullRoles.Drone, StringComparison.Ordinal);

    /// <summary>
    /// Tells whether a ship may be built of the hull: a combat hull and a transport hull. A
    /// drone is carried in the hold and a structure is not a ship (task 00042, decision 3).
    /// </summary>
    /// <param name="role">The role of the hull.</param>
    /// <returns><see langword="true"/> when a ship may be built of the hull.</returns>
    private static bool IsShipHull(string role) =>
        string.Equals(role, HullRoles.Combat, StringComparison.Ordinal)
        || string.Equals(role, HullRoles.Transport, StringComparison.Ordinal);

    /// <summary>
    /// Tells whether the module goes into a ship: the modules of the combat, of the transport
    /// and the ones that fit any hull. The modules of the civil go into the structures.
    /// </summary>
    /// <param name="role">The role of the module.</param>
    /// <returns><see langword="true"/> when the module goes into a ship.</returns>
    private static bool IsShipModule(string role) =>
        string.Equals(role, PartRoles.Combat, StringComparison.Ordinal)
        || string.Equals(role, PartRoles.Transport, StringComparison.Ordinal)
        || string.Equals(role, PartRoles.Universal, StringComparison.Ordinal);

    /// <summary>
    /// Checks that the hull may take the extra slot of the race.
    /// </summary>
    /// <param name="hull">The hull.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot.</param>
    /// <exception cref="GameException">The hull gives no extra slot to the race.</exception>
    private static void CheckRaceBonusSlot(ShipHullDto hull, bool hasRaceBonusSlot)
    {
        if (hasRaceBonusSlot && hull.RaceBonusSlots <= 0)
        {
            throw new GameException(FleetErrorCodes.RaceSlotUnavailable, "The hull of this class gives no extra slot to the race.");
        }
    }

    /// <summary>
    /// Checks that the basic hold of the hull takes the least set of ammunition (task 00038,
    /// section 3.2, check 6).
    /// </summary>
    /// <param name="hull">The hull of the project.</param>
    /// <exception cref="GameException">The hold of the hull is too small.</exception>
    /// <remarks>
    /// The ammunition of the player is not compared with the hold: the game does not know it
    /// yet. The number of the least set comes from the modelling of the ships (task 00041)
    /// and, until the modelling gives it, from the configuration.
    /// </remarks>
    private void CheckHold(ShipHullDto hull)
    {
        FleetSettings settings = FleetSettings.From(_config);

        if (hull.HoldCapacity < settings.MinimumAmmunition)
        {
            throw new GameException(FleetErrorCodes.HoldTooSmall, "The hold of the hull does not take the least set of ammunition.");
        }
    }

    /// <summary>
    /// Builds the rows of the slots of a project and refuses the ones the hull does not take.
    /// </summary>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="hull">The hull of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <param name="slots">The slots the client sent.</param>
    /// <param name="hullChanged">Whether the hull of the project changed.</param>
    /// <returns>The rows of the slots, in the order of their numbers.</returns>
    /// <exception cref="GameException">A module does not exist or the number of the slot is not a slot of the hull.</exception>
    private static List<ShipDesignSlotEntity> BuildSlots(
        ShipCatalogDto catalog,
        ShipHullDto hull,
        bool hasRaceBonusSlot,
        IReadOnlyList<ShipDesignSlotInput> slots,
        bool hullChanged)
    {
        int available = AvailableSlots(hull, hasRaceBonusSlot);
        List<ShipDesignSlotEntity> rows = [];

        // The hull of the project stayed, so the ship takes no more modules than it has slots.
        if (!hullChanged && slots.Count > available)
        {
            throw new GameException(FleetErrorCodes.SlotsFull, "The ship has no free slots left.");
        }

        foreach (ShipDesignSlotInput slot in slots.OrderBy(slot => slot.SlotIndex))
        {
            if (slot.SlotIndex < 0 || slot.SlotIndex >= available)
            {
                if (!hullChanged)
                {
                    throw new GameException(FleetErrorCodes.SlotUnknown, "The number of the slot is not a slot of the hull.");
                }

                // The hull changed and the slot does not fit the new one, so it is cleared.
                continue;
            }

            ShipModuleDto? module = catalog.Modules.FirstOrDefault(entry => entry.Id == slot.PartTypeId);

            if (module is null)
            {
                throw new GameException(FleetErrorCodes.ModuleUnknown, "The module does not exist.");
            }

            rows.Add(new ShipDesignSlotEntity
            {
                SlotIndex = slot.SlotIndex,
                PartTypeId = module.Id,
                MarkLevel = slot.MarkLevel,
                HasRaceBonus = slot.HasRaceBonus,
            });
        }

        return rows;
    }

    /// <summary>
    /// Replaces the slots of a project: the slots that stay keep their rows, the ones that
    /// went are removed and the new ones are added.
    /// </summary>
    /// <param name="design">The project.</param>
    /// <param name="rows">The new slots of the project.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the slots of the project are the new ones.</returns>
    private async Task ReplaceSlotsAsync(
        ShipDesignEntity design,
        IReadOnlyList<ShipDesignSlotEntity> rows,
        CancellationToken cancellationToken)
    {
        List<ShipDesignSlotEntity> existing = await _dbContext.ShipDesignSlots
            .Where(slot => slot.DesignId == design.Id)
            .OrderBy(slot => slot.SlotIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<int, ShipDesignSlotEntity> existingByIndex = existing.ToDictionary(slot => slot.SlotIndex);

        foreach (ShipDesignSlotEntity row in rows)
        {
            if (existingByIndex.Remove(row.SlotIndex, out ShipDesignSlotEntity? kept))
            {
                kept.PartTypeId = row.PartTypeId;
                kept.MarkLevel = row.MarkLevel;
                kept.HasRaceBonus = row.HasRaceBonus;
                continue;
            }

            row.DesignId = design.Id;
            _dbContext.ShipDesignSlots.Add(row);
        }

        foreach (ShipDesignSlotEntity gone in existingByIndex.Values)
        {
            _dbContext.ShipDesignSlots.Remove(gone);
        }
    }

    /// <summary>
    /// Checks the design of a ship against every rule of the constructor (task 00038, section
    /// 3.2).
    /// </summary>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="hull">The hull of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <param name="rows">The slots of the project.</param>
    /// <exception cref="GameException">The design breaks a rule.</exception>
    private static void CheckDesign(
        ShipCatalogDto catalog,
        ShipHullDto hull,
        bool hasRaceBonusSlot,
        IReadOnlyList<ShipDesignSlotEntity> rows)
    {
        int available = AvailableSlots(hull, hasRaceBonusSlot);

        if (rows.Count > available)
        {
            throw new GameException(FleetErrorCodes.SlotsFull, "The ship has no free slots left.");
        }

        bool hasDrive = false;
        bool hasReactor = false;
        bool hasLifeSupport = false;
        decimal energyUse = 0m;
        decimal power = 0m;

        foreach (ShipDesignSlotEntity row in rows)
        {
            ShipModuleDto? module = catalog.Modules.FirstOrDefault(entry => entry.Id == row.PartTypeId);

            if (module is null)
            {
                throw new GameException(FleetErrorCodes.ModuleUnknown, "The module does not exist.");
            }

            CheckRole(hull, module);
            CheckBelonging(hull, module);

            switch (module.Component)
            {
                case DriveComponent:
                    hasDrive = true;
                    break;
                case ReactorComponent:
                    hasReactor = true;
                    break;
                case LifeSupportComponent:
                    hasLifeSupport = true;
                    break;
            }

            energyUse += module.EnergyUse;
            power += module.Power;
        }

        if (!hasDrive)
        {
            throw new GameException(FleetErrorCodes.DriveMissing, "The ship has no drive.");
        }

        if (!hasReactor)
        {
            throw new GameException(FleetErrorCodes.ReactorMissing, "The ship has no reactor.");
        }

        if (!hasLifeSupport)
        {
            throw new GameException(FleetErrorCodes.LifeSupportMissing, "The ship has no life support.");
        }

        if (energyUse > power)
        {
            throw new GameException(FleetErrorCodes.EnergyShort, "The modules of the ship take more energy than the reactors give.");
        }
    }

    /// <summary>
    /// Checks that the module may be put on the hull: a combat module goes only on a combat
    /// hull, a transport module only on a transport hull, a civil module only on a civil hull
    /// (task 00038, section 3.2, check 4).
    /// </summary>
    /// <param name="hull">The hull of the project.</param>
    /// <param name="module">The module.</param>
    /// <exception cref="GameException">The module is of another role.</exception>
    private static void CheckRole(ShipHullDto hull, ShipModuleDto module)
    {
        if (string.Equals(module.Role, PartRoles.Universal, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(module.Role, hull.Role, StringComparison.Ordinal))
        {
            return;
        }

        throw new GameException(FleetErrorCodes.RoleMismatch, "The module does not go on this hull.");
    }

    /// <summary>
    /// Checks that the module is fit for this hull: the largest reactor does not go on a light
    /// transport (task 00038, section 3.2, check 5).
    /// </summary>
    /// <param name="hull">The hull of the project.</param>
    /// <param name="module">The module.</param>
    /// <exception cref="GameException">The module is not fit for the hull.</exception>
    private static void CheckBelonging(ShipHullDto hull, ShipModuleDto module)
    {
        if (module.CompatibleHullIds.Count == 0)
        {
            return;
        }

        if (!module.CompatibleHullIds.Contains(hull.Id))
        {
            throw new GameException(FleetErrorCodes.NotCompatible, "The module is not fit for this hull.");
        }
    }

    /// <summary>
    /// Counts the slots the hull gives: its own and the extra slot of the race when the project
    /// takes it.
    /// </summary>
    /// <param name="hull">The hull of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <returns>The number of the slots.</returns>
    private static int AvailableSlots(ShipHullDto hull, bool hasRaceBonusSlot) =>
        hull.Slots + (hasRaceBonusSlot ? hull.RaceBonusSlots : 0);

    /// <summary>
    /// Builds the answer of one project.
    /// </summary>
    /// <param name="design">The project.</param>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The answer.</returns>
    private async Task<ShipDesignDto> BuildAsync(
        ShipDesignEntity design,
        ShipCatalogDto catalog,
        CancellationToken cancellationToken)
    {
        List<ShipDesignSlotEntity> rows = await _dbContext.ShipDesignSlots.AsNoTracking()
            .Where(slot => slot.DesignId == design.Id)
            .OrderBy(slot => slot.SlotIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Build(design, rows, catalog);
    }

    /// <summary>
    /// Builds the answers of many projects, reading the slots of all of them at once.
    /// </summary>
    /// <param name="designs">The projects.</param>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The answers.</returns>
    private async Task<IReadOnlyList<ShipDesignDto>> BuildAllAsync(
        IReadOnlyList<ShipDesignEntity> designs,
        ShipCatalogDto catalog,
        CancellationToken cancellationToken)
    {
        if (designs.Count == 0)
        {
            return [];
        }

        long[] ids = [.. designs.Select(design => design.Id)];

        List<ShipDesignSlotEntity> rows = await _dbContext.ShipDesignSlots.AsNoTracking()
            .Where(slot => ids.Contains(slot.DesignId))
            .OrderBy(slot => slot.DesignId)
            .ThenBy(slot => slot.SlotIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, List<ShipDesignSlotEntity>> rowsByDesign = rows
            .GroupBy(row => row.DesignId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return
        [
            .. designs.Select(design => Build(design, rowsByDesign.GetValueOrDefault(design.Id) ?? [], catalog))
        ];
    }

    /// <summary>
    /// Builds the answer of one project with its slots.
    /// </summary>
    /// <param name="design">The project.</param>
    /// <param name="rows">The slots of the project.</param>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <returns>The answer.</returns>
    private static ShipDesignDto Build(
        ShipDesignEntity design,
        IReadOnlyList<ShipDesignSlotEntity> rows,
        ShipCatalogDto catalog)
    {
        ShipHullDto? hull = catalog.Hulls.FirstOrDefault(entry => entry.Id == design.HullPartTypeId);

        List<ShipDesignSlotDto> slots =
        [
            .. rows.Select(row => new ShipDesignSlotDto(row.SlotIndex, row.PartTypeId, row.MarkLevel, row.HasRaceBonus))
        ];

        decimal energyUse = 0m;
        decimal power = 0m;
        decimal crew = 0m;
        decimal weight = hull?.Weight ?? 0m;
        bool hasDrive = false;
        bool hasReactor = false;
        bool hasLifeSupport = false;

        foreach (ShipDesignSlotEntity row in rows)
        {
            ShipModuleDto? module = catalog.Modules.FirstOrDefault(entry => entry.Id == row.PartTypeId);

            if (module is null)
            {
                continue;
            }

            energyUse += module.EnergyUse;
            power += module.Power;
            crew += module.Crew;
            weight += module.Weight;

            hasDrive |= string.Equals(module.Component, DriveComponent, StringComparison.Ordinal);
            hasReactor |= string.Equals(module.Component, ReactorComponent, StringComparison.Ordinal);
            hasLifeSupport |= string.Equals(module.Component, LifeSupportComponent, StringComparison.Ordinal);
        }

        int total = hull is null ? 0 : AvailableSlots(hull, design.HasRaceBonusSlot);

        ShipDesignSummaryDto summary = new(
            total,
            slots.Count,
            Math.Max(0, total - slots.Count),
            weight,
            energyUse,
            power,
            crew,
            hull?.HoldCapacity ?? 0m,
            hull?.Integrity ?? 0m,
            hasDrive,
            hasReactor,
            hasLifeSupport);

        return new ShipDesignDto(
            design.Id,
            design.Name,
            design.PlanetId,
            design.HullPartTypeId,
            design.HasRaceBonusSlot,
            design.IsArchived,
            design.CreatedAt,
            design.UpdatedAt,
            design.Version,
            slots,
            summary);
    }
}
