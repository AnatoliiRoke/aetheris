using System.Net;
using System.Net.Http.Json;
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
/// Tests of the rule of the debt (task 00024, decision 8): only a currency that carries the
/// flag of the debt in the reference table may have a balance below zero, and only an
/// administrative fine may leave a civilization in debt. A game operation never deepens a debt,
/// and an incoming amount pays the debt first.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CurrencyDebtTests
{
    private const string Resources = "/world/resources";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDebtTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CurrencyDebtTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The credits never go below zero: an administrative fine that would leave the balance in
    /// debt is refused, because the credits do not carry the flag of the debt.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_credits_never_go_below_zero()
    {
        await EconomyTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        int creditsId = await EconomyTestData.CreditsIdAsync(services);

        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        ResourceCatalog catalog = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.False(catalog.Resources.Single(resource => resource.Id == creditsId).AllowsDebt);

        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 100, now);

        HttpResponseMessage response = await UpdateBalanceAsync(admin, player.Account.UserId, creditsId, -200);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The balance stays where it was: the refused fine writes nothing.
        Assert.Equal(100, await EconomyTestData.BalanceAsync(services, player.Account.UserId, creditsId));
    }

    /// <summary>
    /// A currency that carries the flag of the debt may have a balance below zero: the
    /// administrative fine leaves the civilization in debt, and the amount that comes later pays
    /// the debt first, so the balance grows out of the debt and only then over zero.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_currency_with_the_flag_may_go_below_zero_and_an_incoming_amount_pays_the_debt()
    {
        await EconomyTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        (int currencyId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Currency("Торгова", "Trade", allowsDebt: true)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        ResourceCatalog catalog = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.True(catalog.Resources.Single(resource => resource.Id == currencyId).AllowsDebt);

        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, currencyId, 100, now);

        // The fine is bigger than the balance, so the civilization stays in debt.
        await UpdateBalanceAsync(admin, player.Account.UserId, currencyId, -300);

        Assert.Equal(-200, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyId));

        // An incoming amount pays the debt first: a hundred leaves a debt of a hundred.
        await economy.GiveAsync(player.Account.UserId, new Dictionary<int, long> { [currencyId] = 100 }, Token);

        Assert.Equal(-100, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyId));

        // The rest of the incoming amount joins the balance.
        await economy.GiveAsync(player.Account.UserId, new Dictionary<int, long> { [currencyId] = 150 }, Token);

        Assert.Equal(50, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyId));
    }

    /// <summary>
    /// A game operation never deepens a debt: the currency that may go below zero refuses a
    /// purchase that costs more than the balance, even though the fine could have left it in
    /// debt.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_game_operation_never_deepens_a_debt()
    {
        await EconomyTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        (int currencyId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Currency("Торгова", "Trade", allowsDebt: true)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, currencyId, 100, now);
        await UpdateBalanceAsync(admin, player.Account.UserId, currencyId, -300);

        Assert.Equal(-200, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyId));

        // The currency is in debt, so no game operation may spend anything from it.
        await Assert.ThrowsAsync<Aetheris.Core.Errors.GameException>(() =>
            economy.TakeAsync(player.Account.UserId, new Dictionary<int, long> { [currencyId] = 1 }, Token));

        Assert.Equal(-200, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyId));

        // The money that is there may still be spent: the balance goes more negative only by a
        // fine, never by a purchase.
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, currencyId, 50, now);

        await economy.TakeAsync(player.Account.UserId, new Dictionary<int, long> { [currencyId] = 50 }, Token);

        Assert.Equal(0, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyId));
    }

    /// <summary>
    /// The flag of the debt is changed in the admin panel: the change needs a reason, the journal
    /// keeps the old and the new value, a change made from an old version is refused with 409,
    /// and the game sees the flag at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_flag_of_the_debt_is_changed_with_a_reason_and_a_journal()
    {
        await EconomyTestData.ResetAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Currency("Торгова", "Trade", allowsDebt: false)));

        HttpResponseMessage withoutReason = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Currency("Торгова", "Trade", allowsDebt: true), reason: null);
        await ReferenceAdmin.AssertFieldAsync(withoutReason, HttpStatusCode.BadRequest, "reason", AdminReason.Required);

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Currency("Торгова", "Trade", allowsDebt: true)));

        Assert.Equal(version + 1, changed);

        using IServiceScope scope = _factory.Services.CreateScope();
        AdminAuditLogEntity audit = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == owner.AdminId && record.Action == WorldReferenceAdminService.ResourceDescriptor.Permissions.Updated)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);

        Assert.Contains("\"allows_debt\":false", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("\"allows_debt\":true", audit.NewValue, StringComparison.Ordinal);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);

        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Currency("Торгова", "Trade", allowsDebt: false));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, id);

        Assert.True(record.GetProperty("values").GetProperty("allows_debt").GetBoolean());

        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.True(catalog.Resources.Single(resource => resource.Id == id).AllowsDebt);
    }

    private static object Currency(string ukrainian, string english, bool allowsDebt) => new
    {
        image_key = (string?)null,
        resource_group_id = (int?)null,
        scope = ResourceScopes.Civilization,
        earns_population_income = false,
        allows_debt = allowsDebt,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
    };

    /// <summary>
    /// Gives or takes a currency of a player from the admin panel, the way the administrator
    /// does: the amount is a change of the balance, not the balance itself.
    /// </summary>
    /// <param name="client">The signed-in administrator.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="resourceTypeId">The currency.</param>
    /// <param name="amount">The change of the balance.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> UpdateBalanceAsync(HttpClient client, string playerId, int resourceTypeId, long amount) =>
        client.PutAsJsonAsync(
            $"{AdminEndpoints.Prefix}/economy/balances/{playerId}",
            new { resource_type_id = resourceTypeId, amount, reason = ReferenceAdmin.Reason },
            Token);

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));
}
