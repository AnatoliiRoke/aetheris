using Aetheris.Core.Errors;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Planets;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Research;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The colony module a fleet carries and the facts of it (task 00073).
/// </summary>
/// <param name="PartTypeId">The kind of the module.</param>
/// <param name="DeploySeconds">How long the deployment of the module takes, in game seconds.</param>
/// <param name="Colonists">How many colonists the module brings.</param>
public sealed record ColonyModule(int PartTypeId, long DeploySeconds, long Colonists);

/// <summary>
/// The colonization of a planet by a fleet (task 00073).
/// </summary>
/// <remarks>
/// <para>
/// A fleet that carries a colony module may be given the order of colonization. The order takes
/// no fuel and burns none: only the flight to the planet costs fuel. The deployment of the module
/// takes the time the module says, and the planet belongs to the player when the deployment is
/// over.
/// </para>
/// <para>
/// The checks are made twice: once when the plan is written and once when the order begins, so a
/// fleet that flies to a planet that was taken in the meantime is refused with the code of a
/// taken planet and keeps its module.
/// </para>
/// </remarks>
public sealed class FleetColonizeService
{
    /// <summary>The key of the details of a refusal for the limit of the planets (task 00073).</summary>
    public const string OwnedDetail = "owned";

    /// <summary>The key of the details of a refusal for the limit of the planets (task 00073).</summary>
    public const string AllowedDetail = "allowed";

    private readonly FleetDbContext _dbContext;
    private readonly IProductionModule _production;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly IResearchModule _research;
    private readonly FleetCargoService _cargo;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetColonizeService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="production">The catalog of the modules.</param>
    /// <param name="world">The world, for the bodies and the races.</param>
    /// <param name="planets">The bodies, for the colony.</param>
    /// <param name="research">The research, for the limit of the planets.</param>
    /// <param name="cargo">The holds of the fleet.</param>
    public FleetColonizeService(
        FleetDbContext dbContext,
        IProductionModule production,
        IWorldModule world,
        IPlanetsModule planets,
        IResearchModule research,
        FleetCargoService cargo)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(research);
        ArgumentNullException.ThrowIfNull(cargo);

        _dbContext = dbContext;
        _production = production;
        _world = world;
        _planets = planets;
        _research = research;
        _cargo = cargo;
    }

    /// <summary>
    /// Finds the colony module the ships of the fleet carry in their holds.
    /// </summary>
    /// <remarks>
    /// The ships are read in the order they joined the fleet, and the module of the first ship
    /// that carries one is the module of the fleet. The number of the queries does not depend on
    /// the number of the ships.
    /// </remarks>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The module, or <see langword="null"/> when the fleet carries none.</returns>
    public async Task<ColonyModule?> FindModuleAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .OrderBy(entry => entry.Position)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return null;
        }

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);

        // Only the modules that open the order of the colonization are of interest, so the
        // catalog is read once and the modules of it are looked up by their kind.
        Dictionary<int, ShipModuleDto> colonies = catalog.Modules
            .Where(module => module.CargoOrder == FlightOrderTypes.Colonize)
            .ToDictionary(module => module.Id);

        if (colonies.Count == 0)
        {
            return null;
        }

        int[] kinds = [.. colonies.Keys];

        List<int> carried = await _dbContext.ShipPartCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Quantity > 0 && kinds.Contains(cargo.PartTypeId))
            .Select(cargo => cargo.PartTypeId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        int? typeId = carried.OrderBy(id => id).FirstOrDefault();

        if (typeId is null || !colonies.TryGetValue(typeId.Value, out ShipModuleDto? module))
        {
            return null;
        }

        return new ColonyModule(
            typeId.Value,
            FactOf(module, CharacteristicKinds.DeployTime),
            FactOf(module, CharacteristicKinds.Colonists));
    }

    /// <summary>
    /// Checks that the player has room for one more body (task 00073).
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the player has room.</returns>
    /// <exception cref="GameException">The player has as many bodies as the limit allows.</exception>
    public async Task CheckLimitAsync(FleetEntity fleet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        int allowed = await _research.GetPlanetLimitAsync(fleet.PlayerId, cancellationToken).ConfigureAwait(false);
        int owned = await CountOwnedPlanetsAsync(fleet.PlayerId, cancellationToken).ConfigureAwait(false);

        if (owned >= allowed)
        {
            throw new GameException(
                FleetErrorCodes.ColonizeLimit,
                "The player has as many planets as the limit allows.",
                new Dictionary<string, object?>
                {
                    [OwnedDetail] = owned,
                    [AllowedDetail] = allowed,
                });
        }
    }

    /// <summary>
    /// Checks that a planet may be colonized by the player (task 00073): it is free, and its type
    /// suits the race of the player.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="planetId">The planet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the planet may be colonized.</returns>
    /// <exception cref="GameException">The planet is taken or does not suit the race of the player.</exception>
    public async Task CheckPlanetAsync(
        FleetEntity fleet,
        long planetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        WorldPlaceDto place = await _world.GetPlanetPlaceAsync(planetId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(FleetErrorCodes.ColonizeNotOnPlanet, "The planet does not exist.");

        if (place.OwnerPlayerId is not null)
        {
            throw new GameException(FleetErrorCodes.ColonizeTaken, "The planet belongs to a player already.");
        }

        int raceId = await RaceIdAsync(fleet.PlayerId, cancellationToken).ConfigureAwait(false);

        if (!await _world.PlanetSuitsRaceAsync(planetId, raceId, cancellationToken).ConfigureAwait(false))
        {
            throw new GameException(FleetErrorCodes.ColonizeUnsuitable, "The planet does not suit the race of the player.");
        }
    }

    /// <summary>
    /// Colonizes a planet: the body belongs to the player, the colony is founded on it with the
    /// start buildings of the registration and the people of the module, and the module is gone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body is taken first, under the lock of its row, so two fleets that fly to the same body
    /// cannot take it twice. The second one is refused with the code of a taken body and keeps its
    /// module.
    /// </para>
    /// <para>
    /// The colony is founded without the start resources of the race: the colony starts with its
    /// buildings and its people only.
    /// </para>
    /// </remarks>
    /// <param name="fleet">The fleet.</param>
    /// <param name="planetId">The planet.</param>
    /// <param name="module">The module of the colonization.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The name of the planet, for the report.</returns>
    /// <exception cref="GameException">The planet was taken, does not suit the race, or the limit is reached.</exception>
    public async Task<string> ColonizeAsync(
        FleetEntity fleet,
        long planetId,
        ColonyModule module,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(module);

        // The colonizations of one player wait for each other on the row of the player, so the limit
        // read here is the limit no other fleet of the player changes meanwhile.
        await FleetLocks.LockPlayerAsync(_dbContext, fleet.PlayerId, cancellationToken).ConfigureAwait(false);
        await CheckLimitAsync(fleet, cancellationToken).ConfigureAwait(false);

        int raceId = await RaceIdAsync(fleet.PlayerId, cancellationToken).ConfigureAwait(false);
        PlanetClaimResult claim;

        // The planet is checked with the codes of the colonization, and then claimed under the lock of its
        // row. Every refusal comes before anything is written; a player who took the planet between the
        // check and the lock makes it a taken planet.
        await CheckPlanetAsync(fleet, planetId, cancellationToken).ConfigureAwait(false);

        try
        {
            claim = await _world.ClaimPlanetAsync(planetId, fleet.PlayerId, raceId, cancellationToken).ConfigureAwait(false);
        }
        catch (GameException exception)
        {
            throw new GameException(FleetErrorCodes.ColonizeTaken, exception.Message, exception.Details, exception);
        }

        // The module is taken out of the hold of a ship of the fleet: the colonization costs the fleet
        // its module, and only one of them. The module was found under the lock of the player, so its
        // absence here is a broken invariant: the whole step is undone, not left with a planet without
        // its colony.
        if (!await _cargo.TakePartAsync(fleet, module.PartTypeId, now, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The colony module disappeared from the hold during the colonization.");
        }

        RaceDto? race = (await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(entry => entry.Id == raceId);

        int raceBuildingTypeId = race?.StartBuildingTypeId ?? 0;
        int raceBuildingLevel = race?.StartBuildingLevel ?? 0;

        await _planets.FoundColonyAsync(
            fleet.PlayerId,
            planetId,
            claim.SectorId,
            raceBuildingTypeId,
            raceBuildingLevel,
            module.Colonists,
            cancellationToken).ConfigureAwait(false);

        return await PlanetNameAsync(fleet.PlayerId, planetId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the bodies of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the bodies of the player.</returns>
    private async Task<int> CountOwnedPlanetsAsync(string playerId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, int> counts = await _world.CountOwnedPlanetsAsync([playerId], cancellationToken).ConfigureAwait(false);

        return counts.GetValueOrDefault(playerId);
    }

    /// <summary>
    /// Gets the race of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier of the race.</returns>
    /// <exception cref="GameException">The player has no civilization.</exception>
    private async Task<int> RaceIdAsync(string playerId, CancellationToken cancellationToken)
    {
        CivilizationSummary? civilization = await _world.GetCivilizationAsync(playerId, cancellationToken).ConfigureAwait(false);

        return civilization?.RaceId
            ?? throw new GameException(FleetErrorCodes.ColonizeUnsuitable, "The player has no civilization.");
    }

    /// <summary>
    /// Gets the name a player gave to a body.
    /// </summary>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The name of the body, or an empty string when it has none.</returns>
    private async Task<string> PlanetNameAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OwnedPlanet> owned = await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false);

        return owned.FirstOrDefault(planet => planet.PlanetId == planetId)?.Name ?? string.Empty;
    }

    /// <summary>
    /// Reads the amount of a characteristic of a module.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <param name="kind">The kind of the characteristic.</param>
    /// <returns>The amount; zero when the module does not have the characteristic.</returns>
    private static long FactOf(ShipModuleDto module, string kind)
    {
        ShipModuleFactDto? fact = module.Facts?.FirstOrDefault(entry => entry.Kind == kind);

        return fact is null ? 0L : decimal.ToInt64(decimal.Truncate(fact.Amount));
    }
}
