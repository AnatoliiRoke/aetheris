using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Economy.Api;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Data;
using Aetheris.Economy.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
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
/// bodies over the time. A read counts <c>balance + the integral of every body from the moment
/// of its own population</c> and writes nothing. A change fixes the populations of all bodies and
/// the balances of the civilization to one moment first: it adds the integral of every body to the
/// balance and moves the population and the balance to that moment, so the income is counted once
/// and with the rates that held until the moment. The parts of an inhabitant and of a credit that
/// the count has not yet made whole are kept in the rows, so a count that is made often loses
/// nothing.
/// </para>
/// </remarks>
public sealed class EconomyService
{
    private readonly EconomyDbContext _dbContext;
    private readonly EconomyUnitOfWork _unitOfWork;
    private readonly ColonyRegistry _colonies;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private readonly IWorldModule _world;
    private readonly IEconomyBuildingEffects _buildingEffects;
    private readonly IEconomyTechnologyBonuses _technologyBonuses;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="colonies">The rows of the population of the colonies.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="world">The world, for the currencies, the bodies of a player and the civilizations.</param>
    /// <param name="buildingEffects">The effects of the buildings of the bodies.</param>
    /// <param name="technologyBonuses">The bonuses the technologies give the economy.</param>
    public EconomyService(
        EconomyDbContext dbContext,
        EconomyUnitOfWork unitOfWork,
        ColonyRegistry colonies,
        IGameConfig config,
        IGameClock clock,
        IWorldModule world,
        IEconomyBuildingEffects buildingEffects,
        IEconomyTechnologyBonuses technologyBonuses)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(colonies);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(buildingEffects);
        ArgumentNullException.ThrowIfNull(technologyBonuses);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _colonies = colonies;
        _config = config;
        _clock = clock;
        _world = world;
        _buildingEffects = buildingEffects;
        _technologyBonuses = technologyBonuses;
    }

    /// <summary>
    /// Writes the rows of the population of the colonies that have none: the home bodies of the
    /// civilizations made before the module.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    /// <remarks>
    /// Every colony that appears later gets its row from the module that makes it, and a colony
    /// that has none (a body that changed hands, say) gets it when it is first read or changed.
    /// </remarks>
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        HashSet<long> known = [.. await _dbContext.PlanetPopulations.AsNoTracking()
            .Select(row => row.PlanetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        foreach (CivilizationHome home in await _world.GetCivilizationHomesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (known.Contains(home.PlanetId))
            {
                continue;
            }

            await _colonies.FoundAsync(home.PlayerId, home.PlanetId, home.SectorId, cancellationToken).ConfigureAwait(false);
        }
    }

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

        DateTimeOffset now = EconomyTime.Normalize(_clock.GameNow);

        // The population of a body is read by its owner only, so the body of another player
        // is not known to the caller (documentation/tech/09, section 7.2).
        PlanetPopulationEntity? row = await FindRowAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

            if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
            {
                return null;
            }

            // The body is a colony of the player that has no row yet: it gets one now.
            await _unitOfWork.RunAsync(
                async () =>
                {
                    await _colonies.EnsureAsync(playerId, [new OwnedPlanet(planetId, facts.SectorId)], cancellationToken).ConfigureAwait(false);

                    return true;
                },
                cancellationToken).ConfigureAwait(false);

            row = await FindRowAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

            if (row is null)
            {
                return null;
            }
        }

        Rates rates = await RatesAsync(playerId, [planetId], cancellationToken).ConfigureAwait(false);

        return View(row, rates, now).Facts;
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

        return (await GetOverviewAsync(playerId, cancellationToken).ConfigureAwait(false)).Populations;
    }

    /// <summary>
    /// Gets the economy of a player at a moment of game time: the populations of its bodies and the
    /// balances of its currencies, counted in one pass.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The populations and the balances.</returns>
    public async Task<EconomyOverviewDto> GetOverviewAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        DateTimeOffset now = EconomyTime.Normalize(_clock.GameNow);

        // The read of a colony that has no row writes it: a new transaction when the call has
        // none, the one of the request otherwise.
        IReadOnlyList<OwnedPlanet> missing = await MissingColoniesAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (missing.Count > 0)
        {
            await _unitOfWork.RunAsync(
                async () =>
                {
                    await _colonies.EnsureAsync(playerId, missing, cancellationToken).ConfigureAwait(false);

                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }

        Snapshot snapshot = await SnapshotAsync(playerId, now, cancellationToken).ConfigureAwait(false);

        return new EconomyOverviewDto(
            [.. snapshot.Views.Select(view => view.Facts)],
            [.. snapshot.CurrencyIds.Select(currencyId => snapshot.Balance(playerId, currencyId))],
            now);
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

        Snapshot snapshot = await SnapshotAsync(playerId, EconomyTime.Normalize(_clock.GameNow), cancellationToken).ConfigureAwait(false);

        if (!snapshot.CurrencyIds.Contains(resourceTypeId))
        {
            throw new GameException(EconomyErrorCodes.NotACurrency, "The resource is not a currency of a civilization.");
        }

        return snapshot.Balance(playerId, resourceTypeId);
    }

    /// <summary>
    /// Gets the balances of all the currencies of a civilization, for the bodies module that checks
    /// a cost against them.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The amounts by currency; a currency the player has none of is there with zero.</returns>
    public async Task<IReadOnlyDictionary<int, long>> GetBalancesAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        Snapshot snapshot = await SnapshotAsync(playerId, EconomyTime.Normalize(_clock.GameNow), cancellationToken).ConfigureAwait(false);

        return snapshot.CurrencyIds.ToDictionary(currencyId => currencyId, currencyId => snapshot.Balance(playerId, currencyId).Amount);
    }

    /// <summary>
    /// Reads the balances of the currencies of a civilization, for the admin panel.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The balances with the versions of their rows.</returns>
    public async Task<IReadOnlyList<AdminBalanceFacts>> ReadBalancesAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        Snapshot snapshot = await SnapshotAsync(playerId, EconomyTime.Normalize(_clock.GameNow), cancellationToken).ConfigureAwait(false);

        return
        [
            .. snapshot.CurrencyIds.Select(currencyId =>
            {
                CurrencyBalanceFacts balance = snapshot.Balance(playerId, currencyId);

                // A balance that has no row yet is made with the first version.
                int version = snapshot.Rows.FirstOrDefault(row => row.ResourceTypeId == currencyId)?.Version ?? 1;

                return new AdminBalanceFacts(playerId, currencyId, balance.Amount, balance.IncomePerHour, version);
            }),
        ];
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
    /// The income of the civilization is fixed into the balance first, so the amount the journal
    /// keeps as the old one is the whole balance the administrator saw. The journal of the admin
    /// panel is written by its own filter of the request, which receives the old and the new
    /// amount from this answer; the balance and the journal are committed with the request.
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
            throw new EconomyAdminException(EconomyErrorCodes.ZeroAmount, "The amount of a currency cannot be zero.");
        }

        if (!(await CurrencyIdsAsync(cancellationToken).ConfigureAwait(false)).Contains(request.ResourceTypeId))
        {
            throw new EconomyAdminException(EconomyErrorCodes.NotACurrency, "The resource is not a currency of a civilization.");
        }

        // The balance of a civilization is a balance of a player who has one.
        if (await _world.GetCivilizationAsync(playerId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new EconomyAdminException(GameErrorCodes.NotFound, "The player has no civilization.");
        }

        return await _unitOfWork.RunAsync(
            async () =>
            {
                Locked locked = await LockAsync(playerId, await CurrencyIdsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

                await FixAsync(playerId, locked, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                CivilizationBalanceEntity row = locked.Balances.Single(entry => entry.ResourceTypeId == request.ResourceTypeId);

                if (request.Version is { } expectedVersion && expectedVersion != row.Version)
                {
                    throw new EconomyAdminException(AdminReferenceErrors.VersionConflict, "The balance changed since it was read.");
                }

                long before = row.Amount;
                long after = before + request.Amount;

                if (after < 0)
                {
                    throw new EconomyAdminException(EconomyErrorCodes.CurrencyShort, "The civilization lacks the currency.");
                }

                row.Amount = after;
                row.Version++;

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
    /// Takes inhabitants of a body, for a crew: all of them or none.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="amount">How many inhabitants to take.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the inhabitants are taken, <see langword="false"/> when the body has fewer of them and nothing is taken.</returns>
    /// <exception cref="GameException">The body is not a colony of the player, or the amount is negative.</exception>
    public async Task<bool> TryTakePopulationAsync(string playerId, long planetId, long amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (amount < 0)
        {
            throw new GameException(EconomyErrorCodes.NegativeAmount, "The number of the inhabitants cannot be negative.");
        }

        return await _unitOfWork.RunAsync(
            async () =>
            {
                // The count and the change are made under the lock of the population of the
                // player, so two crews never take the same inhabitants.
                PlanetPopulationEntity row = await LockedPopulationAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                if (row.Amount < amount)
                {
                    return false;
                }

                row.Amount -= amount;

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives inhabitants back to a body, for a crew that returns. The population above the largest
    /// population is not refused: it dies down.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="amount">How many inhabitants to give back.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the inhabitants are given back.</returns>
    /// <exception cref="GameException">The body is not a colony of the player, or the amount is negative.</exception>
    public async Task GivePopulationAsync(string playerId, long planetId, long amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (amount < 0)
        {
            throw new GameException(EconomyErrorCodes.NegativeAmount, "The number of the inhabitants cannot be negative.");
        }

        await _unitOfWork.RunAsync(
            async () =>
            {
                PlanetPopulationEntity row = await LockedPopulationAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                row.Amount += amount;

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Brings the balances and the populations of a civilization to a moment with the rates as
    /// they are.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="moment">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the balances are fixed.</returns>
    /// <remarks>
    /// The module that changes a rate (a building that raises the largest population, a technology
    /// that raises the income) calls this just before the change, so the change applies from that
    /// moment on. A moment before the last count of a body changes nothing: the rows never go back
    /// in time.
    /// </remarks>
    public Task FixPlayerAsync(string playerId, DateTimeOffset moment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                Locked locked = await LockAsync(playerId, await CurrencyIdsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

                await FixAsync(playerId, locked, moment, cancellationToken).ConfigureAwait(false);

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

        if (amounts.Any(entry => entry.Value < 0))
        {
            throw new GameException(EconomyErrorCodes.NegativeAmount, "The amount of a currency cannot be negative.");
        }

        List<KeyValuePair<int, long>> changes = [.. amounts.Where(entry => entry.Value > 0).OrderBy(entry => entry.Key)];

        if (changes.Count == 0)
        {
            return;
        }

        await _unitOfWork.RunAsync(
            async () =>
            {
                List<int> currencyIds = await CurrencyIdsAsync(cancellationToken).ConfigureAwait(false);

                if (changes.Any(entry => !currencyIds.Contains(entry.Key)))
                {
                    throw new GameException(EconomyErrorCodes.NotACurrency, "The resource is not a currency of a civilization.");
                }

                Locked locked = await LockAsync(playerId, currencyIds, cancellationToken).ConfigureAwait(false);

                // The balance is counted to the moment of the change before it changes, so the
                // income the player earned since the last change is in the balance.
                await FixAsync(playerId, locked, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                foreach ((int resourceTypeId, long amount) in changes)
                {
                    CivilizationBalanceEntity row = locked.Balances.Single(entry => entry.ResourceTypeId == resourceTypeId);

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

    private async Task<PlanetPopulationEntity> LockedPopulationAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        Locked locked = await LockAsync(playerId, await CurrencyIdsAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

        // The count is made first, so the credits the inhabitants earned until now stay with
        // them and the change of the number applies from now on.
        await FixAsync(playerId, locked, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        return locked.Populations.FirstOrDefault(entry => entry.PlanetId == planetId)
            ?? throw new GameException(EconomyErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
    }

    /// <summary>
    /// Locks the rows of a civilization in the one order every change takes them in: the
    /// populations by the identifier of the body, then the balances by the identifier of the
    /// currency (see <see cref="EconomyUnitOfWork"/>).
    /// </summary>
    private async Task<Locked> LockAsync(string playerId, IReadOnlyList<int> currencyIds, CancellationToken cancellationToken)
    {
        // A colony that has no row yet gets it before the rows are locked. The call is already
        // inside the transaction of the change, so the row is written in it.
        await _colonies.EnsureAsync(playerId, await MissingColoniesAsync(playerId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

        List<PlanetPopulationEntity> populations = await _dbContext.PlanetPopulations
            .FromSql($"SELECT * FROM planet_population WHERE owner_player_id = {playerId} ORDER BY planet_id FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A row that does not exist cannot be locked, so the missing balances are written first
        // with INSERT ... ON CONFLICT: two first changes of one civilization write one row and then
        // wait for it one after another, and never fail on the unique index.
        HashSet<int> known = [.. await _dbContext.CivilizationBalances.AsNoTracking()
            .Where(row => row.PlayerId == playerId)
            .Select(row => row.ResourceTypeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        DateTimeOffset now = EconomyTime.Normalize(_clock.GameNow);

        foreach (int currencyId in currencyIds.Where(currencyId => !known.Contains(currencyId)).Order())
        {
            await _dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO civilization_balance (player_id, resource_type_id, amount, amount_fraction, updated_at, version)
                VALUES ({playerId}, {currencyId}, 0, 0, {now}, 1)
                ON CONFLICT (player_id, resource_type_id) DO NOTHING
                """,
                cancellationToken).ConfigureAwait(false);
        }

        List<CivilizationBalanceEntity> balances = await _dbContext.CivilizationBalances
            .FromSql($"SELECT * FROM civilization_balance WHERE player_id = {playerId} ORDER BY resource_type_id FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new Locked(populations, balances);
    }

    /// <summary>
    /// Brings the locked populations and balances to one moment: the income of every body since its
    /// own count goes into the balances, and every row gets the moment.
    /// </summary>
    private async Task FixAsync(string playerId, Locked locked, DateTimeOffset moment, CancellationToken cancellationToken)
    {
        DateTimeOffset target = EconomyTime.Normalize(moment);

        // The rows never go back in time: a body counted later than the moment keeps its count
        // and the moment moves to it.
        foreach (PlanetPopulationEntity row in locked.Populations)
        {
            if (row.UpdatedAt > target)
            {
                target = row.UpdatedAt;
            }
        }

        Rates rates = await RatesAsync(playerId, [.. locked.Populations.Select(row => row.PlanetId)], cancellationToken).ConfigureAwait(false);
        decimal earned = 0m;

        foreach (PlanetPopulationEntity row in locked.Populations)
        {
            PopulationView view = View(row, rates, target);

            earned += view.Earned;
            row.Amount = view.Amount;
            row.AmountFraction = view.Fraction;
            row.UpdatedAt = target;
        }

        // Only a currency that is flagged in the reference table earns the income of the
        // population; the other currencies of the civilization are left as they are.
        HashSet<int> incomeCurrencyIds = [.. await IncomeCurrencyIdsAsync(cancellationToken).ConfigureAwait(false)];

        foreach (CivilizationBalanceEntity row in locked.Balances)
        {
            decimal exact = row.Amount + row.AmountFraction + (incomeCurrencyIds.Contains(row.ResourceTypeId) ? earned : 0m);
            long amount = (long)decimal.Floor(exact);

            row.Amount = amount;
            row.AmountFraction = exact - amount;
            row.UpdatedAt = row.UpdatedAt > target ? row.UpdatedAt : target;
        }
    }

    /// <summary>
    /// Gets the bodies a player owns in the world that have no row of the population yet.
    /// </summary>
    private async Task<IReadOnlyList<OwnedPlanet>> MissingColoniesAsync(string playerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OwnedPlanet> owned = await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (owned.Count == 0)
        {
            return owned;
        }

        HashSet<long> known = [.. await _dbContext.PlanetPopulations.AsNoTracking()
            .Where(row => row.OwnerPlayerId == playerId)
            .Select(row => row.PlanetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        return [.. owned.Where(planet => !known.Contains(planet.PlanetId))];
    }

    private Task<PlanetPopulationEntity?> FindRowAsync(string playerId, long planetId, CancellationToken cancellationToken) =>
        _dbContext.PlanetPopulations.AsNoTracking()
            .Where(entry => entry.PlanetId == planetId && entry.OwnerPlayerId == playerId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Reads the rows of a player and counts them to a moment, without locking or writing.
    /// </summary>
    private async Task<Snapshot> SnapshotAsync(string playerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<PlanetPopulationEntity> populations = await _dbContext.PlanetPopulations.AsNoTracking()
            .Where(entry => entry.OwnerPlayerId == playerId)
            .OrderBy(entry => entry.PlanetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CivilizationBalanceEntity> rows = await _dbContext.CivilizationBalances.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Rates rates = await RatesAsync(playerId, [.. populations.Select(row => row.PlanetId)], cancellationToken).ConfigureAwait(false);
        List<int> currencyIds = await CurrencyIdsAsync(cancellationToken).ConfigureAwait(false);

        List<int> incomeCurrencyIds = await IncomeCurrencyIdsAsync(cancellationToken).ConfigureAwait(false);

        return new Snapshot([.. populations.Select(row => View(row, rates, now))], rows, currencyIds, incomeCurrencyIds);
    }

    /// <summary>
    /// Reads what sets the speed of the population and of the income of a player, once for all its
    /// bodies: the parameters, the bonuses of the technologies and the effects of the buildings.
    /// </summary>
    private async Task<Rates> RatesAsync(string playerId, IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken)
    {
        EconomySettings settings = EconomySettings.From(_config);
        EconomyBonuses bonuses = await _technologyBonuses.GetBonusesAsync(playerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<long, PlanetEconomyEffects> effects = planetIds.Count == 0
            ? new Dictionary<long, PlanetEconomyEffects>()
            : await _buildingEffects.GetAsync(planetIds, cancellationToken).ConfigureAwait(false);

        return new Rates(settings, bonuses, effects);
    }

    private async Task<List<int>> CurrencyIdsAsync(CancellationToken cancellationToken) =>
        [.. (await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false)).Resources
            .Where(resource => resource.IsCivilizationScope)
            .Select(resource => resource.Id)
            .Order()];

    /// <summary>
    /// Gets the currencies the population of a civilization brings: the currencies of the scope of
    /// the civilization that carry the flag of the income in the reference table.
    /// </summary>
    private async Task<List<int>> IncomeCurrencyIdsAsync(CancellationToken cancellationToken) =>
        [.. (await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false)).Resources
            .Where(resource => resource.IsCivilizationScope && resource.EarnsPopulationIncome)
            .Select(resource => resource.Id)
            .Order()];

    /// <summary>
    /// Counts the population of a row to a moment, with the credits it earned meanwhile.
    /// </summary>
    private static PopulationView View(PlanetPopulationEntity row, Rates rates, DateTimeOffset moment)
    {
        PlanetEconomyEffects effects = rates.Effects.GetValueOrDefault(row.PlanetId) ?? new PlanetEconomyEffects(0, 0);
        long maximum = effects.PopulationMax;

        // The buildings of the body may raise the speed the population grows with, so the effect
        // of the buildings and the bonus of the technologies are multiplied (task 00015).
        decimal buildingMultiplier = effects.PopulationGrowthEffect > 0 ? effects.PopulationGrowthEffect : 1m;
        decimal growth = PopulationRules.GrowthPerHour(
            maximum,
            rates.Settings.PopulationGrowthPercent,
            rates.Bonuses.PopulationGrowthMultiplier * buildingMultiplier);
        decimal decline = PopulationRules.DeclinePerHour(rates.Settings.PopulationDeclinePerHour);

        // The population grows until the largest population and dies down above it, so the
        // speed of the interval is the one of the population at its start.
        decimal start = row.Amount + row.AmountFraction;
        decimal speedAtStart = PopulationRules.SpeedAt(start, maximum, growth, decline);
        DateTimeOffset to = moment > row.UpdatedAt ? moment : row.UpdatedAt;

        decimal exact = PopulationRules.AccumulateExact(start, maximum, speedAtStart, row.UpdatedAt, to);
        (long amount, decimal fraction) = PopulationRules.Settle(exact, speedAtStart < 0m);
        decimal earned = CreditRules.Earned(start, maximum, speedAtStart, rates.CreditsPerInhabitant, row.UpdatedAt, to);

        // The speed and the warning are the ones of the moment, and not of the last count, so a
        // population that died down under the limit grows again.
        bool overLimit = amount > maximum;
        decimal speed = overLimit ? decline : growth;

        return new PopulationView(
            amount,
            fraction,
            earned,
            new PopulationFacts(row.PlanetId, amount, maximum, speed, overLimit, PopulationRules.TimeToMaximum(amount, maximum, speed)),
            CreditRules.IncomePerHour(amount, rates.CreditsPerInhabitant));
    }

    /// <summary>
    /// What sets the speed of the population and of the income of a player.
    /// </summary>
    private sealed record Rates(
        EconomySettings Settings,
        EconomyBonuses Bonuses,
        IReadOnlyDictionary<long, PlanetEconomyEffects> Effects)
    {
        /// <summary>Gets how many credits an inhabitant brings in an hour: the parameter with the bonus of the technologies.</summary>
        public decimal CreditsPerInhabitant => Settings.CreditsPerInhabitant * Bonuses.CreditIncomeMultiplier;
    }

    /// <summary>
    /// The population of a body counted to a moment.
    /// </summary>
    private sealed record PopulationView(long Amount, decimal Fraction, decimal Earned, PopulationFacts Facts, decimal Income);

    /// <summary>
    /// The rows of a civilization, locked.
    /// </summary>
    private sealed record Locked(List<PlanetPopulationEntity> Populations, List<CivilizationBalanceEntity> Balances);

    /// <summary>
    /// The rows of a civilization counted to a moment, for a read.
    /// </summary>
    private sealed record Snapshot(
        IReadOnlyList<PopulationView> Views,
        IReadOnlyList<CivilizationBalanceEntity> Rows,
        IReadOnlyList<int> CurrencyIds,
        IReadOnlyList<int> IncomeCurrencyIds)
    {
        /// <summary>
        /// Gets the balance of a currency: the stored amount and the credits every body earned since its
        /// own count.
        /// </summary>
        /// <param name="playerId">The player.</param>
        /// <param name="currencyId">The currency.</param>
        /// <returns>The balance.</returns>
        public CurrencyBalanceFacts Balance(string playerId, int currencyId)
        {
            CivilizationBalanceEntity? row = Rows.FirstOrDefault(entry => entry.ResourceTypeId == currencyId);
            bool earns = IncomeCurrencyIds.Contains(currencyId);
            decimal exact = (row?.Amount ?? 0) + (row?.AmountFraction ?? 0m) + (earns ? Views.Sum(view => view.Earned) : 0m);

            return new CurrencyBalanceFacts(playerId, currencyId, (long)decimal.Floor(exact), earns ? Views.Sum(view => view.Income) : 0m);
        }
    }
}
