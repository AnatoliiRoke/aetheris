using Aetheris.Economy.Api;
using Aetheris.Economy.Services;

namespace Aetheris.Economy;

/// <summary>
/// The economy module: the population of the bodies and the balances of the civilizations
/// (task 00015).
/// </summary>
/// <remarks>
/// The module reads the buildings of the bodies module and the levels of the technologies of
/// the research module through the contracts those modules implement for it, so the
/// dependencies go one way only.
/// </remarks>
public sealed class EconomyModule : IEconomyModule
{
    private readonly EconomyService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyModule"/> class.
    /// </summary>
    /// <param name="service">The service of the population and the balances.</param>
    public EconomyModule(EconomyService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    public string ModuleName => "Economy";

    /// <inheritdoc />
    public Task EnsureAsync(CancellationToken cancellationToken = default) =>
        _service.EnsureAsync(cancellationToken);

    /// <inheritdoc />
    public Task<PopulationFacts?> GetPopulationAsync(string playerId, long planetId, CancellationToken cancellationToken = default) =>
        _service.GetPopulationAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PopulationFacts>> GetPopulationsAsync(string playerId, CancellationToken cancellationToken = default) =>
        _service.GetPopulationsAsync(playerId, cancellationToken);

    /// <inheritdoc />
    public Task<CurrencyBalanceFacts> GetBalanceAsync(string playerId, int resourceTypeId, CancellationToken cancellationToken = default) =>
        _service.GetBalanceAsync(playerId, resourceTypeId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, long>> GetBalancesAsync(string playerId, CancellationToken cancellationToken = default) =>
        _service.GetBalancesAsync(playerId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryTakePopulationAsync(string playerId, long planetId, long amount, CancellationToken cancellationToken = default) =>
        _service.TryTakePopulationAsync(playerId, planetId, amount, cancellationToken);

    /// <inheritdoc />
    public Task GivePopulationAsync(string playerId, long planetId, long amount, CancellationToken cancellationToken = default) =>
        _service.GivePopulationAsync(playerId, planetId, amount, cancellationToken);

    /// <inheritdoc />
    public Task TakeAsync(string playerId, IReadOnlyDictionary<int, long> amounts, CancellationToken cancellationToken = default) =>
        _service.TakeAsync(playerId, amounts, cancellationToken);

    /// <inheritdoc />
    public Task GiveAsync(string playerId, IReadOnlyDictionary<int, long> amounts, CancellationToken cancellationToken = default) =>
        _service.GiveAsync(playerId, amounts, cancellationToken);

    /// <inheritdoc />
    public Task FixPlayerAsync(string playerId, DateTimeOffset moment, CancellationToken cancellationToken = default) =>
        _service.FixPlayerAsync(playerId, moment, cancellationToken);
}
