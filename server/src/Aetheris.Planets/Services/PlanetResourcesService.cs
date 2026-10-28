using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Planets.Data;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The balance of a body for the modules that pay for something on a body (task 00012).
/// </summary>
/// <remarks>
/// The change joins the transaction of the request, so the payment of a research and the
/// record of its queue are committed together.
/// </remarks>
public sealed class PlanetResourcesService : IPlanetResources
{
    private readonly PlanetsDbContext _dbContext;
    private readonly ConstructionService _construction;
    private readonly IWorldModule _world;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetResourcesService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="construction">The changes of a body.</param>
    /// <param name="world">The world, for the owner of a body.</param>
    /// <param name="clock">The game clock.</param>
    public PlanetResourcesService(
        PlanetsDbContext dbContext,
        ConstructionService construction,
        IWorldModule world,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _construction = construction;
        _world = world;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task TakeAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(planetId, playerId, amounts, give: false, cancellationToken);

    /// <inheritdoc />
    public Task GiveAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(planetId, playerId, amounts, give: true, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, long>> GetBalanceAsync(
        long planetId,
        string playerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await EnsureOwnedAsync(planetId, playerId, cancellationToken).ConfigureAwait(false);

        return await _dbContext.PlanetResources.AsNoTracking()
            .Where(row => row.PlanetId == planetId)
            .ToDictionaryAsync(row => row.ResourceTypeId, row => row.Amount, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task FixPlayerAsync(string playerId, DateTimeOffset moment, CancellationToken cancellationToken = default) =>
        _construction.FixPlayerAsync(playerId, moment, cancellationToken);

    private async Task ChangeAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        bool give,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(amounts);

        if (amounts.Count == 0)
        {
            return;
        }

        await EnsureOwnedAsync(planetId, playerId, cancellationToken).ConfigureAwait(false);

        // The body is opened like for a construction: the row of the body is locked, the due
        // constructions are completed and the balance is brought to the moment before the
        // amounts change, so the payment never spends the same resources twice and sees what
        // the body has produced since the last change.
        await _construction.ChangeBalanceAsync(planetId, amounts, give, _clock.GameNow, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureOwnedAsync(long planetId, string playerId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(PlanetsErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }
    }
}
