using System.Collections.Concurrent;

namespace Aetheris.Processes.Services;

/// <summary>
/// The ship of a player on the orbit of a body, for what needs one: the device of the
/// terraforming by a free body, the repair of a ship and the replacement of a module (task
/// 00014, decision 5 and section 9).
/// </summary>
/// <remarks>
/// The ships and the flights do not exist yet, so the processes ask this interface and never
/// a module of the ships. The task of the flights replaces the stub with the real ships.
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
/// A ship on the orbit of a body with its hull and its modules.
/// </summary>
/// <param name="Parts">The hull and the modules of the ship.</param>
public sealed record ShipOnOrbit(IReadOnlyList<ShipPart> Parts);

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
/// ship on the orbit of every body, and a test places a ship with parts on a body (task 00014,
/// decision 5).
/// </summary>
public sealed class ShipsOnOrbitStub : IShipsOnOrbit
{
    private readonly ConcurrentDictionary<(string PlayerId, long PlanetId), ShipOnOrbit?> _ships = new();

    /// <summary>
    /// Places a ship on the orbit of a body, or takes it away with <see langword="null"/>.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="ship">The ship.</param>
    public void Place(string playerId, long planetId, ShipOnOrbit? ship) => _ships[(playerId, planetId)] = ship;

    /// <inheritdoc />
    public Task<ShipOnOrbit?> FindAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return Task.FromResult(_ships.TryGetValue((playerId, planetId), out ShipOnOrbit? ship) ? ship : new ShipOnOrbit([]));
    }

    /// <inheritdoc />
    public Task RepairAsync(string playerId, long planetId, int fullConditionPercent, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (_ships.TryGetValue((playerId, planetId), out ShipOnOrbit? ship) && ship is not null)
        {
            _ships[(playerId, planetId)] = new ShipOnOrbit([.. ship.Parts.Select(part => part with { ConditionPercent = fullConditionPercent })]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReplaceAsync(string playerId, long planetId, ShipPart removed, ShipPart installed, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (_ships.TryGetValue((playerId, planetId), out ShipOnOrbit? ship) && ship is not null)
        {
            List<ShipPart> parts = [.. ship.Parts];
            int index = parts.IndexOf(removed);

            if (index >= 0)
            {
                parts[index] = installed;
                _ships[(playerId, planetId)] = new ShipOnOrbit(parts);
            }
        }

        return Task.CompletedTask;
    }
}
