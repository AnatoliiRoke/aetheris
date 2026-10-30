namespace Aetheris.Processes.Services;

/// <summary>
/// The ship of a player on the orbit of a body, for what needs one: the device of the
/// terraforming, which the ship takes from its own hold, the repair of a ship and the
/// replacement of a module (task 00014, decision 5 and sections 9 and 11).
/// </summary>
/// <remarks>
/// The ships and the flights do not exist yet, so the processes ask this interface and never
/// a module of the ships. The task of the flights replaces the stub with the real ships. The
/// hold of a ship carries whatever the ship loaded where it stood (task 00010, the general rule
/// of the cargo); the terraforming takes its device from the hold of one named ship and never
/// from the storage of a colony.
/// </remarks>
public interface IShipsOnOrbit
{
    /// <summary>
    /// Finds the ship of a player on the orbit of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The ship, or <see langword="null"/> when no ship of the player stands there.</returns>
    Task<ShipOnOrbit?> FindAsync(string playerId, long planetId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the ships of a player on the orbits of bodies with the parts in their holds.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The ships of the player by the body they stand by; a body without a ship of the player is absent.</returns>
    Task<IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>>> ListAsync(
        string playerId,
        IReadOnlyCollection<long> planetIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists every ship of a player that stands by a body, with the parts in its hold.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The ships of the player by the body they stand by; a body without a ship of the player is absent.</returns>
    Task<IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>>> ListAllAsync(
        string playerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes parts out of the hold of a ship of the player that stands by a body, all or
    /// nothing.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the ship stands by.</param>
    /// <param name="shipId">The ship.</param>
    /// <param name="partTypeId">The kind of the part.</param>
    /// <param name="quantity">How many parts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether the hold held the parts and they were taken; nothing is taken otherwise.</returns>
    Task<bool> TakeCargoAsync(
        string playerId,
        long planetId,
        long shipId,
        int partTypeId,
        long quantity,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives the hull and every module of the ship on the orbit of a body their full condition.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="fullConditionPercent">The condition of a new part, in per cent.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the ship is repaired.</returns>
    Task RepairAsync(string playerId, long planetId, int fullConditionPercent, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a module off the ship on the orbit of a body and puts a new one in its place.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="removed">The module that comes off.</param>
    /// <param name="installed">The module that takes its place.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the module is replaced.</returns>
    Task ReplaceAsync(string playerId, long planetId, ShipPart removed, ShipPart installed, CancellationToken cancellationToken);
}

/// <summary>
/// A ship on the orbit of a body with its hull, its modules and its hold.
/// </summary>
/// <param name="Parts">The hull and the modules of the ship.</param>
public sealed record ShipOnOrbit(IReadOnlyList<ShipPart> Parts)
{
    /// <summary>Gets the identifier of the ship.</summary>
    public long ShipId { get; init; }

    /// <summary>Gets the parts in the hold of the ship, the one-shot devices among them.</summary>
    public IReadOnlyList<ShipCargoPart> Cargo { get; init; } = [];
}

/// <summary>
/// A kind of part in the hold of a ship.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="Quantity">How many parts of the kind the hold carries.</param>
public sealed record ShipCargoPart(int PartTypeId, long Quantity);

/// <summary>
/// A hull or a module of a ship.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="ConditionPercent">The condition of the part, in per cent.</param>
public sealed record ShipPart(int PartTypeId, int MarkLevel, bool HasRaceBonus, int ConditionPercent);

/// <summary>
/// The stub of the ships on the orbit until the task of the flights: every player has an empty
/// ship with an empty hold on the orbit of every body, and a test places ships with parts and
/// cargo on a body (task 00014, decision 5 and section 11).
/// </summary>
public sealed class ShipsOnOrbitStub : IShipsOnOrbit
{
    private readonly object _lock = new();
    private readonly Dictionary<(string PlayerId, long PlanetId), List<ShipOnOrbit>> _ships = [];

    /// <summary>
    /// Places one ship on the orbit of a body in place of the ones there, or takes every ship
    /// away with <see langword="null"/>.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="ship">The ship.</param>
    public void Place(string playerId, long planetId, ShipOnOrbit? ship)
    {
        lock (_lock)
        {
            _ships[(playerId, planetId)] = ship is null ? [] : [ship];
        }
    }

    /// <summary>
    /// Adds a ship to the ones on the orbit of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="ship">The ship.</param>
    public void Add(string playerId, long planetId, ShipOnOrbit ship)
    {
        lock (_lock)
        {
            if (!_ships.TryGetValue((playerId, planetId), out List<ShipOnOrbit>? ships))
            {
                ships = [];
                _ships[(playerId, planetId)] = ships;
            }

            ships.Add(ship);
        }
    }

    /// <summary>
    /// Reads how many parts of a kind the hold of a ship carries.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="shipId">The ship.</param>
    /// <param name="partTypeId">The kind of the part.</param>
    /// <returns>The quantity; zero when the ship is not there.</returns>
    public long CargoOf(string playerId, long planetId, long shipId, int partTypeId)
    {
        lock (_lock)
        {
            return _ships.GetValueOrDefault((playerId, planetId))?
                .Where(ship => ship.ShipId == shipId)
                .SelectMany(ship => ship.Cargo)
                .Where(part => part.PartTypeId == partTypeId)
                .Sum(part => part.Quantity) ?? 0;
        }
    }

    /// <inheritdoc />
    public Task<ShipOnOrbit?> FindAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        lock (_lock)
        {
            return Task.FromResult(ShipsOf(playerId, planetId).FirstOrDefault());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>>> ListAsync(
        string playerId,
        IReadOnlyCollection<long> planetIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planetIds);
        _ = cancellationToken;

        lock (_lock)
        {
            return Task.FromResult<IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>>>(planetIds
                .Distinct()
                .Select(planetId => (PlanetId: planetId, Ships: ShipsOf(playerId, planetId)))
                .Where(entry => entry.Ships.Count > 0)
                .ToDictionary(entry => entry.PlanetId, entry => (IReadOnlyList<ShipOnOrbit>)entry.Ships));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>>> ListAllAsync(string playerId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        // The empty ship every player has by every body is not a ship that stands anywhere in
        // particular, so only the ships a test placed are listed.
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>>>(_ships
                .Where(entry => string.Equals(entry.Key.PlayerId, playerId, StringComparison.Ordinal) && entry.Value.Count > 0)
                .ToDictionary(entry => entry.Key.PlanetId, entry => (IReadOnlyList<ShipOnOrbit>)[.. entry.Value]));
        }
    }

    /// <inheritdoc />
    public Task<bool> TakeCargoAsync(
        string playerId,
        long planetId,
        long shipId,
        int partTypeId,
        long quantity,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        lock (_lock)
        {
            if (quantity <= 0 || !_ships.TryGetValue((playerId, planetId), out List<ShipOnOrbit>? ships))
            {
                return Task.FromResult(false);
            }

            int index = ships.FindIndex(ship => ship.ShipId == shipId);

            if (index < 0)
            {
                return Task.FromResult(false);
            }

            ShipOnOrbit ship = ships[index];
            long held = ship.Cargo.Where(part => part.PartTypeId == partTypeId).Sum(part => part.Quantity);

            if (held < quantity)
            {
                return Task.FromResult(false);
            }

            List<ShipCargoPart> cargo = [.. ship.Cargo.Where(part => part.PartTypeId != partTypeId)];

            if (held - quantity > 0)
            {
                cargo.Add(new ShipCargoPart(partTypeId, held - quantity));
            }

            ships[index] = ship with { Cargo = cargo };

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task RepairAsync(string playerId, long planetId, int fullConditionPercent, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        lock (_lock)
        {
            if (_ships.TryGetValue((playerId, planetId), out List<ShipOnOrbit>? ships) && ships.Count > 0)
            {
                ships[0] = ships[0] with { Parts = [.. ships[0].Parts.Select(part => part with { ConditionPercent = fullConditionPercent })] };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReplaceAsync(string playerId, long planetId, ShipPart removed, ShipPart installed, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        lock (_lock)
        {
            if (_ships.TryGetValue((playerId, planetId), out List<ShipOnOrbit>? ships) && ships.Count > 0)
            {
                List<ShipPart> parts = [.. ships[0].Parts];
                int index = parts.IndexOf(removed);

                if (index >= 0)
                {
                    parts[index] = installed;
                    ships[0] = ships[0] with { Parts = parts };
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The ships of a player by a body: the ones a test placed, or the empty ship every player
    /// has there until a test places something else.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <returns>The ships.</returns>
    private List<ShipOnOrbit> ShipsOf(string playerId, long planetId) =>
        _ships.TryGetValue((playerId, planetId), out List<ShipOnOrbit>? ships) ? ships : [new ShipOnOrbit([])];
}
