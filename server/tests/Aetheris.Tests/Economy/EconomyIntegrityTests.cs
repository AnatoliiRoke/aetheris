using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Data;
using Aetheris.Economy.Data.Entities;
using Aetheris.Economy.Services;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Tests.Admin;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the rows and the counts of the economy after the second check of task 00015: the row
/// of the population of a colony, the inhabitants of a crew, the first balance row under parallel
/// calls, a count that is made often, and the changes of the balance of the administrator. The
/// expected values are counted by hand from the parameters of the starting data: the growth of 5
/// per cent of the limit in an hour and one credit for an inhabitant in an hour.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EconomyIntegrityTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyIntegrityTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public EconomyIntegrityTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A civilization that is founded has the row of the population of its home body from the same
    /// moment, with the starting population of the configuration.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_founded_civilization_has_the_row_of_the_population_of_its_home_body()
    {
        await EconomyTestData.ResetAsync(_factory);

        using (IServiceScope configuration = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(configuration.ServiceProvider, EconomyConfigKeys.PopulationInitial, "7");
        }

        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        PlanetPopulationEntity row = await scope.ServiceProvider.GetRequiredService<EconomyDbContext>().PlanetPopulations.AsNoTracking()
            .SingleAsync(entry => entry.PlanetId == player.PlanetId, Token);

        Assert.Equal(player.Account.UserId, row.OwnerPlayerId);
        Assert.Equal(7, row.Amount);

        PopulationFacts? facts = await scope.ServiceProvider.GetRequiredService<IEconomyModule>().GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(facts);
        Assert.Equal(7, facts!.Amount);
    }

    /// <summary>
    /// A colony that has no row of the population (a body of a world older than the module) gets
    /// it the first time its population or the overview of its owner is read, once, or at the start
    /// of the server.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_colony_without_the_row_gets_it_when_it_is_first_read()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        EconomyDbContext dbContext = scope.ServiceProvider.GetRequiredService<EconomyDbContext>();
        IEconomyModule economy = scope.ServiceProvider.GetRequiredService<IEconomyModule>();

        await dbContext.PlanetPopulations.Where(entry => entry.PlanetId == player.PlanetId).ExecuteDeleteAsync(Token);

        Assert.NotNull(await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token));
        Assert.Equal(1, await dbContext.PlanetPopulations.AsNoTracking().CountAsync(entry => entry.PlanetId == player.PlanetId, Token));

        await dbContext.PlanetPopulations.Where(entry => entry.PlanetId == player.PlanetId).ExecuteDeleteAsync(Token);

        Assert.Single(await economy.GetPopulationsAsync(player.Account.UserId, Token));
        Assert.Equal(1, await dbContext.PlanetPopulations.AsNoTracking().CountAsync(entry => entry.PlanetId == player.PlanetId, Token));

        // The start of the server writes the rows of the home bodies of the civilizations made before the module.
        await dbContext.PlanetPopulations.Where(entry => entry.PlanetId == player.PlanetId).ExecuteDeleteAsync(Token);
        await economy.EnsureAsync(Token);

        Assert.Equal(1, await dbContext.PlanetPopulations.AsNoTracking().CountAsync(entry => entry.PlanetId == player.PlanetId, Token));
    }

    /// <summary>
    /// The inhabitants of a crew are taken all or none: more than the body has is refused and
    /// changes nothing, a body of another player is not known, and a crew that returns is given
    /// back even above the limit.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_inhabitants_of_a_crew_are_taken_all_or_none()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();

        // The body has no housing, so its limit is 100 (the administrative centre of the first level),
        // and the 110 inhabitants are above it: they die down by one in an hour, so they do not change
        // during the test, and they do not grow, so no fraction of a newborn inhabitant is carried.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 110, services.GetRequiredService<IGameClock>().GameNow);

        Assert.True(await economy.TryTakePopulationAsync(player.Account.UserId, player.PlanetId, 4, Token));
        Assert.Equal(106, (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount);

        // More than the body has is refused and nothing is taken.
        Assert.False(await economy.TryTakePopulationAsync(player.Account.UserId, player.PlanetId, 107, Token));
        Assert.Equal(106, (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount);

        GameException foreign = await Assert.ThrowsAsync<GameException>(
            () => economy.TryTakePopulationAsync(stranger.Account.UserId, player.PlanetId, 1, Token));

        Assert.Equal(EconomyErrorCodes.PlanetUnknown, foreign.Code);

        GameException negative = await Assert.ThrowsAsync<GameException>(
            () => economy.TryTakePopulationAsync(player.Account.UserId, player.PlanetId, -1, Token));

        Assert.Equal(EconomyErrorCodes.NegativeAmount, negative.Code);

        // The crew comes back above the limit and is not refused: 106 + 5 = 111 inhabitants, and the
        // population dies down later.
        await economy.GivePopulationAsync(player.Account.UserId, player.PlanetId, 5, Token);

        PopulationFacts? back = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(111, back!.Amount);
        Assert.True(back.OverLimit);
    }

    /// <summary>
    /// Twenty parallel crews take exactly as many inhabitants as the body has, and never the same
    /// inhabitant twice.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_crews_never_take_the_same_inhabitants()
    {
        const int parallelCount = 20;
        const long inhabitants = 10;

        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, inhabitants, scope.ServiceProvider.GetRequiredService<IGameClock>().GameNow);
        }

        bool[] taken = await Task.WhenAll(Enumerable.Range(0, parallelCount).Select(async _ =>
        {
            using IServiceScope call = _factory.Services.CreateScope();

            return await call.ServiceProvider.GetRequiredService<IEconomyModule>().TryTakePopulationAsync(player.Account.UserId, player.PlanetId, 1, Token);
        }));

        using IServiceScope check = _factory.Services.CreateScope();
        PopulationFacts? left = await check.ServiceProvider.GetRequiredService<IEconomyModule>().GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(inhabitants, taken.Count(entry => entry));
        Assert.Equal(0, left!.Amount);
    }

    /// <summary>
    /// The first changes of a balance that does not exist yet, made at once, write one row and all
    /// of them succeed: the unique index never fails a request.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_first_changes_write_one_row_of_the_balance()
    {
        const int parallelCount = 20;
        const long amount = 5;

        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        int creditsId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            creditsId = await EconomyTestData.CreditsIdAsync(scope.ServiceProvider);

            Assert.False(await scope.ServiceProvider.GetRequiredService<EconomyDbContext>().CivilizationBalances.AsNoTracking().AnyAsync(Token));
        }

        await Task.WhenAll(Enumerable.Range(0, parallelCount).Select(async _ =>
        {
            using IServiceScope call = _factory.Services.CreateScope();

            await call.ServiceProvider.GetRequiredService<IEconomyModule>().GiveAsync(player.Account.UserId, new Dictionary<int, long> { [creditsId] = amount }, Token);
        }));

        using IServiceScope check = _factory.Services.CreateScope();
        EconomyDbContext dbContext = check.ServiceProvider.GetRequiredService<EconomyDbContext>();

        Assert.Equal(1, await dbContext.CivilizationBalances.AsNoTracking().CountAsync(entry => entry.PlayerId == player.Account.UserId && entry.ResourceTypeId == creditsId, Token));
        Assert.Equal(parallelCount * amount, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
    }

    /// <summary>
    /// A population and a balance that are counted often lose nothing: ten counts six minutes apart
    /// give the inhabitants and the credits of one count of the hour. The growth of 5 an hour is a
    /// half of an inhabitant in six minutes, which a count that keeps whole inhabitants only never adds.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_count_that_is_made_often_loses_no_fraction()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // The body has no residential complex: its limit is 100, the one of the administrative centre
        // of the first level, so the growth of 5 per cent of it is 5 in an hour.
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset start = ConstructionService.Normalize(services.GetRequiredService<IGameClock>().GameNow.AddHours(-1));

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, start);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, start);

        for (int i = 1; i <= 10; i++)
        {
            await economy.FixPlayerAsync(player.Account.UserId, start.AddMinutes(6 * i), Token);
        }

        // The limit is 100, so the population grows by 5 an hour: 5 inhabitants in the hour, and the
        // credits are the area under the line, 0.5 * 5 * 1 * 1 = 2.5, of which 2 are whole.
        using IServiceScope check = _factory.Services.CreateScope();
        EconomyDbContext dbContext = check.ServiceProvider.GetRequiredService<EconomyDbContext>();
        PlanetPopulationEntity population = await dbContext.PlanetPopulations.AsNoTracking().SingleAsync(entry => entry.PlanetId == player.PlanetId, Token);
        CivilizationBalanceEntity balance = await dbContext.CivilizationBalances.AsNoTracking().SingleAsync(entry => entry.ResourceTypeId == creditsId, Token);

        Assert.Equal(5, population.Amount);
        Assert.Equal(2, balance.Amount);
        Assert.Equal(0.5m, balance.AmountFraction);
        Assert.Equal(start.AddHours(1), population.UpdatedAt);
    }

    /// <summary>
    /// A count that is repeated at several moments counts the income once: the balance after the
    /// counts is the integral of the whole interval, and a count at a moment before the last one
    /// changes nothing.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_repeated_count_never_counts_the_income_twice()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset start = ConstructionService.Normalize(services.GetRequiredService<IGameClock>().GameNow.AddHours(-10));

        // The population is at the limit of 200 (100 of the administrative centre and 100 of the
        // residential complex) and the balance is empty ten hours ago.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 200, start);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, start);

        await economy.FixPlayerAsync(player.Account.UserId, start.AddHours(6), Token);
        await economy.FixPlayerAsync(player.Account.UserId, start.AddHours(3), Token);
        await economy.FixPlayerAsync(player.Account.UserId, start.AddHours(8), Token);

        // Eight hours at 200 credits in an hour.
        Assert.Equal(1600, await EconomyTestData.BalanceAsync(services, player.Account.UserId, creditsId));

        // Every read of the balance counts the same ten hours once, whatever the number of the reads.
        CurrencyBalanceFacts first = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);
        CurrencyBalanceFacts second = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(2000, first.Amount);
        Assert.Equal(2000, second.Amount);
    }

    /// <summary>
    /// The change of the balance of the administrator counts the income first, so the journal keeps
    /// the whole balance as the old amount; a change made from an old version, a resource that is not
    /// a currency, a player without a civilization and a change by nothing are refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_change_of_the_administrator_keeps_the_whole_balance_and_checks_the_version()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        int creditsId;
        int ironId;
        DateTimeOffset start;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;

            await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

            creditsId = await EconomyTestData.CreditsIdAsync(services);
            ironId = await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron);
            start = ConstructionService.Normalize(services.GetRequiredService<IGameClock>().GameNow.AddHours(-10));

            // The population is at the limit of 200 (100 of the administrative centre and 100 of the
            // residential complex) ten hours ago, so the balance has 2000 credits the administrator
            // sees and the row has none yet.
            await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 200, start);
            await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, start);
        }

        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        string route = AdminEndpoints.Prefix + "/economy/balances/" + player.Account.UserId;

        HttpResponseMessage read = await admin.GetAsync(route, Token);
        IReadOnlyList<AdminBalanceFacts>? balances = await read.Content.ReadFromJsonAsync<IReadOnlyList<AdminBalanceFacts>>(EconomyJson.Options, Token);

        AdminBalanceFacts credits = balances!.Single(entry => entry.ResourceTypeId == creditsId);

        Assert.Equal(2000, credits.Amount);

        HttpResponseMessage given = await admin.PutAsJsonAsync(route, new { resource_type_id = creditsId, amount = 5, reason = "Подарунок", version = credits.Version }, Token);

        Assert.Equal(HttpStatusCode.OK, given.StatusCode);

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            AdminAuditLogEntity audit = await check.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
                .Where(record => record.AdminId == owner.AdminId && record.Action == "admin.economy_balance_updated")
                .OrderByDescending(record => record.Id)
                .FirstAsync(Token);

            // The old amount is the whole balance with the income, and not the amount of the row.
            Assert.Equal("2000", audit.OldValue);
            Assert.Equal("2005", audit.NewValue);
        }

        // The version of the row went up, so the old one is refused with 409 and nothing is written.
        HttpResponseMessage stale = await admin.PutAsJsonAsync(route, new { resource_type_id = creditsId, amount = 5, reason = "Ще раз", version = credits.Version }, Token);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        // A resource that is not a currency, a change by nothing and a player without a civilization.
        HttpResponseMessage notACurrency = await admin.PutAsJsonAsync(route, new { resource_type_id = ironId, amount = 5, reason = "Залізо" }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, notACurrency.StatusCode);
        Assert.Equal(EconomyErrorCodes.NotACurrency, await TestAccounts.ReadErrorAsync(notACurrency));

        HttpResponseMessage nothing = await admin.PutAsJsonAsync(route, new { resource_type_id = creditsId, amount = 0, reason = "Нічого" }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
        Assert.Equal(EconomyErrorCodes.ZeroAmount, await TestAccounts.ReadErrorAsync(nothing));

        HttpResponseMessage unknown = await admin.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/economy/balances/nobody",
            new { resource_type_id = creditsId, amount = 5, reason = "Нікому" },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        using IServiceScope after = _factory.Services.CreateScope();

        Assert.Equal(2005, await EconomyTestData.BalanceAsync(after.ServiceProvider, player.Account.UserId, creditsId));
    }

    /// <summary>
    /// The scope of a resource that has a balance does not change, and a currency of a civilization
    /// has no group (task 00015, the rules of the reference of the resources).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_scope_of_a_resource_in_use_does_not_change_and_a_currency_has_no_group()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

        ResourceTypeEntity credits;
        int metals;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            credits = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
                .SingleAsync(resource => resource.Code == EconomyTestData.CreditsCode, Token);
            metals = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceGroups.AsNoTracking()
                .Where(group => group.Code == SeedCodes.MetalGroup)
                .Select(group => group.Id)
                .SingleAsync(Token);

            // The civilization holds credits, so the scope of the resource is not free to change.
            await EconomyTestData.SetBalanceAsync(scope.ServiceProvider, player.Account.UserId, credits.Id, 10, scope.ServiceProvider.GetRequiredService<IGameClock>().GameNow);
        }

        HttpResponseMessage refused = await ReferenceAdmin.UpdateAsync(admin, "/world/resources", credits.Id, credits.Version, Resource(ResourceScopes.Planet, metals));

        await ReferenceAdmin.AssertFieldAsync(refused, HttpStatusCode.UnprocessableEntity, "scope", AdminReferenceErrors.ScopeInUse);

        // A currency of a civilization with a group would reach the input of the generation of the world.
        HttpResponseMessage grouped = await ReferenceAdmin.UpdateAsync(admin, "/world/resources", credits.Id, credits.Version, Resource(ResourceScopes.Civilization, metals));

        await ReferenceAdmin.AssertFieldAsync(grouped, HttpStatusCode.BadRequest, "resource_group_id", AdminReferenceErrors.CurrencyHasNoGroup);
    }

    /// <summary>
    /// A database that was made before the credits joined the starting data gets the credits when the
    /// world starts, and a resource that is there is not touched.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_world_adds_the_credits_to_a_database_that_has_none()
    {
        await EconomyTestData.ResetAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        int resourcesBefore = await world.ResourceTypes.AsNoTracking().CountAsync(Token);

        await world.ResourceTypes.Where(resource => resource.Code == EconomyTestData.CreditsCode).ExecuteDeleteAsync(Token);

        Assert.Equal(resourcesBefore - 1, await world.ResourceTypes.AsNoTracking().CountAsync(Token));

        await scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);

        ResourceTypeEntity credits = await world.ResourceTypes.AsNoTracking().SingleAsync(resource => resource.Code == EconomyTestData.CreditsCode, Token);

        Assert.Equal(ResourceScopes.Civilization, credits.Scope);
        Assert.Null(credits.ResourceGroupId);
        Assert.Equal(resourcesBefore, await world.ResourceTypes.AsNoTracking().CountAsync(Token));
    }

    private static object Resource(string scope, int? groupId) => new
    {
        image_key = (string?)null,
        resource_group_id = groupId,
        scope,
        name = ReferenceAdmin.Texts("Кредити", "Credits"),
        description = ReferenceAdmin.Texts("Кредити опис", "Credits description"),
    };
}
