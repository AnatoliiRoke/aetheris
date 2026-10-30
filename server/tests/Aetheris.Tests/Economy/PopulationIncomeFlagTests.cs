using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the flag "income from the population" of a resource (task 00015, the answers of the
/// author of 30.10.2026): only a currency with the flag earns the income of the inhabitants, and
/// the flag is changed in the admin panel like the scope of the resource.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PopulationIncomeFlagTests
{
    private const string Resources = "/world/resources";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopulationIncomeFlagTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public PopulationIncomeFlagTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The credits of the starting data carry the flag, and a currency of the civilization without
    /// the flag gets no income from the population: neither the balance nor the income in an hour
    /// grows, and a change of its balance writes exactly the amount that was given.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_currency_without_the_flag_gets_no_income_from_the_population()
    {
        await EconomyTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();

        (int plainId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource("Талери", "Thalers", earnsIncome: false)));

        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        ResourceCatalog catalog = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.True(catalog.Resources.Single(resource => resource.Id == creditsId).EarnsPopulationIncome);
        Assert.False(catalog.Resources.Single(resource => resource.Id == plainId).EarnsPopulationIncome);

        // The population stood for ten hours: the credits earned, the other currency did not.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 100, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, plainId, 0, now.AddHours(-10));

        CurrencyBalanceFacts credits = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);
        CurrencyBalanceFacts plain = await economy.GetBalanceAsync(player.Account.UserId, plainId, Token);

        Assert.True(credits.Amount > 0);
        Assert.True(credits.IncomePerHour > 0m);
        Assert.Equal(0, plain.Amount);
        Assert.Equal(0m, plain.IncomePerHour);

        // A change fixes the income into the balances: the credits keep it, the other currency
        // holds only what was given.
        await economy.GiveAsync(player.Account.UserId, new Dictionary<int, long> { [plainId] = 5 }, Token);

        Assert.Equal(5, await EconomyTestData.BalanceAsync(services, player.Account.UserId, plainId));
        Assert.True(await EconomyTestData.BalanceAsync(services, player.Account.UserId, creditsId) > 0);
    }

    /// <summary>
    /// The flag of the income is changed in the admin panel: the change needs a reason, the journal
    /// keeps the old and the new value, a change made from an old version is refused with 409, and
    /// the game sees the flag at once (the currency starts to earn).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_flag_of_the_income_is_changed_with_a_reason_and_a_journal()
    {
        await EconomyTestData.ResetAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource("Талери", "Thalers", earnsIncome: false)));

        HttpResponseMessage withoutReason = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource("Талери", "Thalers", earnsIncome: true), reason: null);
        await ReferenceAdmin.AssertFieldAsync(withoutReason, HttpStatusCode.BadRequest, "reason", AdminReason.Required);

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource("Талери", "Thalers", earnsIncome: true)));

        Assert.Equal(version + 1, changed);

        using IServiceScope scope = _factory.Services.CreateScope();
        AdminAuditLogEntity audit = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == owner.AdminId && record.Action == WorldReferenceAdminService.ResourceDescriptor.Permissions.Updated)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);

        Assert.Contains("\"earns_population_income\":false", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("\"earns_population_income\":true", audit.NewValue, StringComparison.Ordinal);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);

        // A change made from the old version is refused and writes nothing.
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource("Талери", "Thalers", earnsIncome: false));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, id);

        Assert.True(record.GetProperty("values").GetProperty("earns_population_income").GetBoolean());

        // The game sees the flag without a restart: the currency now earns the income.
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.True(catalog.Resources.Single(resource => resource.Id == id).EarnsPopulationIncome);

        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        IServiceProvider services = scope.ServiceProvider;
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 100, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, id, 0, now.AddHours(-10));

        CurrencyBalanceFacts earned = await services.GetRequiredService<IEconomyModule>().GetBalanceAsync(player.Account.UserId, id, Token);

        Assert.True(earned.Amount > 0);
        Assert.True(earned.IncomePerHour > 0m);
    }

    private static object Resource(string ukrainian, string english, bool earnsIncome) => new
    {
        image_key = (string?)null,
        resource_group_id = (int?)null,
        scope = ResourceScopes.Civilization,
        earns_population_income = earnsIncome,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
    };

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));
}
