using System.Globalization;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Economy.Api;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Data;
using Aetheris.Economy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Economy.Services;

/// <summary>
/// The population of the bodies and the balances of the civilizations (task 00015).
/// </summary>
/// <remarks>
/// <para>
/// The population of a body grows to the largest population the body has and dies down above
/// it. Both the population and the credits are counted lazily: the amount and the moment it
/// was counted at are kept, and the value at a later moment is counted when it is read
/// (documentation/tech/09, section 5.2).
/// </para>
/// <para>
/// The credits belong to the whole civilization and are the integral of the population of its
/// bodies over the time, so they are counted when they are read and written back at the same
/// moment as the population they come from.
/// </para>
/// </remarks>
public sealed class EconomyService
{
    private readonly EconomyDbContext _dbContext;
    private readonly EconomyUnitOfWork _unitOfWork;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private readonly IEconomyBuildingEffects _buildingEffects;
    private readonly IEconomyTechnologyBonuses _technologyBonuses;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="buildingEffects">The effects of the buildings of the bodies.</param>
    /// <param name="technologyBonuses">The bonuses the technologies give the economy.</param>
    public EconomyService(
        EconomyDbContext dbContext,
        EconomyUnitOfWork unitOfWork,
        IGameConfig config,
        IGameClock clock,
        IEconomyBuildingEffects buildingEffects,
        IEconomyTechnologyBonuses technologyBonuses)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(buildingEffects);
        ArgumentNullException.ThrowIfNull(technologyBonuses);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _config = config;
        _clock = clock;
        _buildingEffects = buildingEffects;
        _technologyBonuses = technologyBonuses;
    }

    /// <summary>
    /// Fills the tables of the module with the starting data when they are empty.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the data is written.</returns>
    /// <remarks>
    /// The module has no starting data of its own: the population of a body starts empty and
    /// grows, and the balance of a currency starts at zero. The method exists so the host
    /// starts the modules the same way and a future starting data has one place to go.
    /// </remarks>
    public Task EnsureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Gets the population of a body of a player at a moment of game time.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The population, or <see langword="null"/> when the body is not a colony of the player.</returns>
    public async Task<PopulationFacts?> GetPopulationAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        DateTimeOffset now = _clock.GameNow;

        // The population of a body is read by its owner only, so the body of another player
        // is not known to the caller (documentation/tech/09, section 7.2).
        PlanetPopulationEntity? row = await _dbContext.PlanetPopulations
            .AsNoTracking()
            .Where(entry => entry.PlanetId == planetId && entry.OwnerPlayerId == playerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        PopulationSnapshot snapshot = await SnapshotAsync(row, now, cancellationToken).ConfigureAwait(false);

        return snapshot.Facts;
    }

    /// <summary>
    /// Gets the populations of the bodies of a player at a moment of game time.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The populations of the bodies.</returns>
    public async Task<IReadOnlyList<PopulationFacts>> GetPopulationsAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        DateTimeOffset now = _clock.GameNow;

        List<PlanetPopulationEntity> rows = await _dbContext.PlanetPopulations
            .AsNoTracking()
            .Where(entry => entry.OwnerPlayerId == playerId)
            .OrderBy(entry => entry.PlanetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PopulationFacts> facts = [];

        foreach (PlanetPopulationEntity row in rows)
        {
            PopulationSnapshot snapshot = await SnapshotAsync(row, now, cancellationToken).ConfigureAwait(false);
            facts.Add(snapshot.Facts);
        }

        return facts;
    }

    /// <summary>
    /// Gets the balance of a currency of a civilization at a moment of game time.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="resourceTypeId">The currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The balance and the income it gets in an hour.</returns>
    public async Task<CurrencyBalanceFacts> GetBalanceAsync(
        string playerId,
        int resourceTypeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        DateTimeOffset now = _clock.GameNow;

        (long amount, decimal income) = await BalanceAsync(playerId, resourceTypeId, now, cancellationToken).ConfigureAwait(false);

        return new CurrencyBalanceFacts(playerId, resourceTypeId, amount, income);
    }

    /// <summary>
    /// Reads the balances of the currencies of a civilization, for the admin panel.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The balances.</returns>
    public async Task<IReadOnlyList<CurrencyBalanceFacts>> ReadBalancesAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        DateTimeOffset now = _clock.GameNow;

        List<int> currencyIds = await _dbContext.CivilizationBalances.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .Select(entry => entry.ResourceTypeId)
            .Distinct()
            .Order()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CurrencyBalanceFacts> balances = [];

        foreach (int currencyId in currencyIds)
        {
            balances.Add(await GetBalanceAsync(playerId, currencyId, cancellationToken).ConfigureAwait(false));
        }

        _ = now;

        return balances;
    }

    /// <summary>
    /// Changes the balance of a currency of a civilization, as an administrator does.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="request">The change.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The change that was applied.</returns>
    /// <remarks>
    /// The balance never goes below zero, so an administrator takes less than the balance has.
    /// The change is in the transaction of the request, so the journal of the admin panel and
    /// the balance are committed together.
    /// </remarks>
    public async Task<AdminBalanceChange> ChangeBalanceAsync(
        string playerId,
        AdminBalanceUpdate request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount == 0)
        {
            throw new EconomyAdminException(EconomyErrorCodes.NegativeAmount, "The amount of a currency cannot be zero.");
        }

        return await _unitOfWork.RunAsync(
            async () =>
            {
                CivilizationBalanceEntity row = await BalanceRowAsync(playerId, request.ResourceTypeId, cancellationToken).ConfigureAwait(false);

                long before = row.Amount;
                long after = before + request.Amount;

                if (after < 0)
                {
                    throw new EconomyAdminException(EconomyErrorCodes.CurrencyShort, "The civilization lacks the currency.");
                }

                row.Amount = after;

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return new AdminBalanceChange(
                    row.Id,
                    $"economy_balance:{playerId}:{request.ResourceTypeId}",
                    before.ToString(CultureInfo.InvariantCulture),
                    after.ToString(CultureInfo.InvariantCulture),
                    row.Version);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the amounts from the balance of a currency of a civilization.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="amounts">The amounts by currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are taken.</returns>
    public Task TakeAsync(
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(playerId, amounts, give: false, cancellationToken);

    /// <summary>
    /// Gives the amounts back to the balance of a currency of a civilization.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="amounts">The amounts by currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are given back.</returns>
    public Task GiveAsync(
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(playerId, amounts, give: true, cancellationToken);

    /// <summary>
    /// Brings the balances and the populations of a civilization to a moment with the rates as
    /// they are.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="moment">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the balances are fixed.</returns>
    public Task FixPlayerAsync(string playerId, DateTimeOffset moment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                DateTimeOffset to = moment;

                List<PlanetPopulationEntity> rows = await _dbContext.PlanetPopulations
                    .Where(entry => entry.OwnerPlayerId == playerId)
                    .OrderBy(entry => entry.PlanetId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (PlanetPopulationEntity row in rows)
                {
                    await FixPopulationAsync(row, to, cancellationToken).ConfigureAwait(false);
                }

                List<int> currencyIds = await CurrenciesAsync(cancellationToken).ConfigureAwait(false);

                foreach (int currencyId in currencyIds)
                {
                    await FixBalanceAsync(playerId, currencyId, to, cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            cancellationToken);
    }

    private async Task ChangeAsync(
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

        DateTimeOffset now = _clock.GameNow;

        await _unitOfWork.RunAsync(
            async () =>
            {
                foreach ((int resourceTypeId, long amount) in amounts.OrderBy(entry => entry.Key))
                {
                    if (amount < 0)
                    {
                        throw new GameException(EconomyErrorCodes.NegativeAmount, "The amount of a currency cannot be negative.");
                    }

                    if (amount == 0)
                    {
                        continue;
                    }

                    CivilizationBalanceEntity row = await BalanceRowAsync(playerId, resourceTypeId, cancellationToken).ConfigureAwait(false);

                    // The balance is counted to the moment of the change before it changes, so
                    // the income the player earned since the last change is in the balance.
                    await FixBalanceAsync(row, now, cancellationToken).ConfigureAwait(false);

                    if (give)
                    {
                        row.Amount += amount;
                    }
                    else
                    {
                        if (row.Amount < amount)
                        {
                            throw new GameException(EconomyErrorCodes.CurrencyShort, "The civilization lacks the currency for the cost.");
                        }

                        row.Amount -= amount;
                    }
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(long Amount, decimal Income)> BalanceAsync(
        string playerId,
        int resourceTypeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CivilizationBalanceEntity row = await BalanceRowAsync(playerId, resourceTypeId, cancellationToken).ConfigureAwait(false);

        decimal income = await IncomeAsync(playerId, now, cancellationToken).ConfigureAwait(false);

        long amount = row.Amount;

        // The balance is counted without writing it, so a read does not change the database.
        foreach (PlanetPopulationEntity population in await PopulationsAsync(playerId, cancellationToken).ConfigureAwait(false))
        {
            PopulationSnapshot snapshot = await SnapshotAsync(population, now, cancellationToken).ConfigureAwait(false);
            amount = CreditRules.Accumulate(
                amount,
                snapshot.Amount,
                snapshot.Maximum,
                snapshot.GrowthPerHour,
                snapshot.CreditsPerInhabitant,
                population.UpdatedAt,
                now);
        }

        return (amount, income);
    }

    private async Task FixBalanceAsync(string playerId, int resourceTypeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        CivilizationBalanceEntity row = await BalanceRowAsync(playerId, resourceTypeId, cancellationToken).ConfigureAwait(false);

        await FixBalanceAsync(row, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task FixBalanceAsync(CivilizationBalanceEntity row, DateTimeOffset now, CancellationToken cancellationToken)
    {
        DateTimeOffset from = row.UpdatedAt;
        DateTimeOffset to = now > from ? now : from;

        long amount = row.Amount;

        foreach (PlanetPopulationEntity population in await PopulationsAsync(row.PlayerId, cancellationToken).ConfigureAwait(false))
        {
            PopulationSnapshot snapshot = await SnapshotAsync(population, from, cancellationToken).ConfigureAwait(false);

            amount = CreditRules.Accumulate(
                amount,
                snapshot.Amount,
                snapshot.Maximum,
                snapshot.GrowthPerHour,
                snapshot.CreditsPerInhabitant,
                population.UpdatedAt,
                to);
        }

        row.Amount = amount;
        row.UpdatedAt = to;
    }

    private async Task FixPopulationAsync(PlanetPopulationEntity row, DateTimeOffset now, CancellationToken cancellationToken)
    {
        PopulationSnapshot snapshot = await SnapshotAsync(row, now, cancellationToken).ConfigureAwait(false);

        row.Amount = snapshot.Amount;
        row.UpdatedAt = now;
    }

    private async Task<PopulationSnapshot> SnapshotAsync(
        PlanetPopulationEntity row,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        EconomySettings settings = EconomySettings.From(_config);
        EconomyBonuses bonuses = await _technologyBonuses.GetBonusesAsync(row.OwnerPlayerId, cancellationToken).ConfigureAwait(false);

        long maximum = await MaximumAsync(row, bonuses, cancellationToken).ConfigureAwait(false);

        // The buildings of the body may raise the speed the population grows with, so the
        // bonus of the buildings and the bonus of the technologies are multiplied (task 00015).
        decimal buildingBonus = await BuildingGrowthMultiplierAsync(row.PlanetId, cancellationToken).ConfigureAwait(false);

        decimal growth = PopulationRules.GrowthPerHour(
            maximum,
            settings.PopulationGrowthPercent,
            bonuses.PopulationGrowthMultiplier * buildingBonus,
            settings.PopulationDeclinePerHour);

        decimal decline = PopulationRules.DeclinePerHour(settings.PopulationDeclinePerHour);

        // The population grows until the largest population and dies down above it, so the
        // speed depends on where the population is.
        bool overLimitAtStart = row.Amount > maximum;
        decimal speedAtStart = overLimitAtStart ? decline : growth;

        long amount = PopulationRules.Accumulate(row.Amount, maximum, speedAtStart, row.UpdatedAt, now);

        // The speed and the warning are the ones of the moment now, and not of the moment of
        // the last count, so a population that died down under the limit grows again.
        bool overLimit = amount > maximum;
        decimal speed = overLimit ? decline : growth;

        decimal creditsPerInhabitant = settings.CreditsPerInhabitant * bonuses.CreditIncomeMultiplier;

        return new PopulationSnapshot(
            amount,
            maximum,
            speed,
            overLimit,
            creditsPerInhabitant,
            new PopulationFacts(
                row.PlanetId,
                amount,
                maximum,
                speed,
                overLimit,
                PopulationRules.TimeToMaximum(amount, maximum, speed)));
    }

    private async Task<decimal> BuildingGrowthMultiplierAsync(long planetId, CancellationToken cancellationToken)
    {
        long effect = await _buildingEffects.GetEffectTotalAsync(planetId, EconomyCodes.PopulationGrowthBonusEffect, cancellationToken).ConfigureAwait(false);

        return effect > 0 ? effect : 1m;
    }

    private async Task<long> MaximumAsync(
        PlanetPopulationEntity row,
        EconomyBonuses bonuses,
        CancellationToken cancellationToken)
    {
        long effect = await _buildingEffects.GetEffectTotalAsync(row.PlanetId, EconomyCodes.PopulationMaxEffect, cancellationToken).ConfigureAwait(false);

        return (long)decimal.Floor(effect * bonuses.PopulationMaxMultiplier);
    }

    private async Task<decimal> IncomeAsync(string playerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        EconomySettings settings = EconomySettings.From(_config);
        EconomyBonuses bonuses = await _technologyBonuses.GetBonusesAsync(playerId, cancellationToken).ConfigureAwait(false);

        decimal income = 0m;

        foreach (PlanetPopulationEntity population in await PopulationsAsync(playerId, cancellationToken).ConfigureAwait(false))
        {
            PopulationSnapshot snapshot = await SnapshotAsync(population, now, cancellationToken).ConfigureAwait(false);

            income += CreditRules.IncomePerHour(
                snapshot.Amount,
                snapshot.CreditsPerInhabitant,
                bonuses.CreditIncomeMultiplier);
        }

        _ = settings;

        return income;
    }

    private Task<List<PlanetPopulationEntity>> PopulationsAsync(string playerId, CancellationToken cancellationToken) =>
        _dbContext.PlanetPopulations
            .Where(entry => entry.OwnerPlayerId == playerId)
            .OrderBy(entry => entry.PlanetId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Gets the row of the balance of a currency and locks it, so two commands that change the
    /// same balance wait for each other and never spend the same currency twice
    /// (documentation/tech/09, section 5.3).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="resourceTypeId">The currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The row of the balance; a new row when the balance does not exist yet.</returns>
    private async Task<CivilizationBalanceEntity> BalanceRowAsync(string playerId, int resourceTypeId, CancellationToken cancellationToken)
    {
        CivilizationBalanceEntity? row = (await _dbContext.CivilizationBalances
            .FromSql($"SELECT * FROM civilization_balance WHERE player_id = {playerId} AND resource_type_id = {resourceTypeId} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (row is not null)
        {
            return row;
        }

        row = new CivilizationBalanceEntity
        {
            PlayerId = playerId,
            ResourceTypeId = resourceTypeId,
            Amount = 0,
            UpdatedAt = _clock.GameNow,
        };

        _dbContext.CivilizationBalances.Add(row);

        return row;
    }

    private async Task<List<int>> CurrenciesAsync(CancellationToken cancellationToken) =>
        await _dbContext.CivilizationBalances
            .AsNoTracking()
            .Select(entry => entry.ResourceTypeId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
