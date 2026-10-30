using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Economy.Services;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the scope of a resource ("where it lies") in the reference of the resources of the
/// admin panel: a currency of a civilization is made and changed there, and the seed does not
/// overwrite what the administrator changed (task 00015, rules of the reference kinds).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CurrencyReferenceTests
{
    private const string Resources = "/world/resources";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CurrencyReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A currency of a civilization is created in the admin panel with no group, and the game sees
    /// its scope at once: the splitter of the costs counts it as a currency of the civilization.
    /// A resource of a body still needs a group, and an unknown scope is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_currency_of_a_civilization_is_created_with_no_group()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient admin = await OwnerAsync();

        (int id, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource("Алмази", "Diamonds", ResourceScopes.Civilization, null)));

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, id);
        JsonElement values = record.GetProperty("values");

        Assert.Equal(ResourceScopes.Civilization, values.GetProperty("scope").GetString());
        Assert.Equal(JsonValueKind.Null, values.GetProperty("resource_group_id").ValueKind);

        // The game sees the scope without a restart.
        using IServiceScope scope = _factory.Services.CreateScope();
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        ResourceTypeFact fact = catalog.Resources.Single(resource => resource.Id == id);

        Assert.Equal(ResourceScopes.Civilization, fact.Scope);
        Assert.Null(fact.ResourceGroupId);

        SplitCosts split = await scope.ServiceProvider.GetRequiredService<CostSplitter>()
            .SplitAsync(new Dictionary<int, long> { [id] = 5 }, Token);

        Assert.Equal(5, split.Currencies[id]);
        Assert.Empty(split.PlanetResources);

        // A resource of a body without a group is refused, and so is an unknown scope.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Resources, Resource("Руда", "Ore", ResourceScopes.Planet, null)),
            HttpStatusCode.BadRequest,
            "resource_group_id",
            AdminReferenceErrors.Required);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Resources, Resource("Дивина", "Wonder", "galaxy", null)),
            HttpStatusCode.BadRequest,
            "scope",
            AdminReferenceErrors.Unknown);
    }

    /// <summary>
    /// The scope of a resource is changed in the admin panel: the change needs a reason, the journal
    /// keeps the old and the new scope, a change made from an old version is refused with 409, and
    /// the game sees the new scope at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_scope_of_a_resource_is_changed_with_a_reason_and_a_journal()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource("Талери", "Thalers", ResourceScopes.Civilization, null)));

        // A change without a reason is refused.
        HttpResponseMessage withoutReason = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource("Талери", "Thalers", ResourceScopes.Planet, metals), reason: null);
        await ReferenceAdmin.AssertFieldAsync(withoutReason, HttpStatusCode.BadRequest, "reason", AdminReason.Required);

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource("Талери", "Thalers", ResourceScopes.Planet, metals)));

        Assert.Equal(version + 1, changed);

        using IServiceScope scope = _factory.Services.CreateScope();
        AdminAuditLogEntity audit = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == owner.AdminId && record.Action == WorldReferenceAdminService.ResourceDescriptor.Permissions.Updated)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);

        Assert.Equal(AdminReferenceEndpoints.Target(WorldReferenceAdminService.ResourceKind, id), audit.Target);
        Assert.Contains(ResourceScopes.Civilization, audit.OldValue, StringComparison.Ordinal);
        Assert.DoesNotContain(ResourceScopes.Planet, audit.OldValue, StringComparison.Ordinal);
        Assert.Contains(ResourceScopes.Planet, audit.NewValue, StringComparison.Ordinal);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);

        // The game sees the new scope without a restart: the resource now lies on a body.
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.Equal(ResourceScopes.Planet, catalog.Resources.Single(resource => resource.Id == id).Scope);

        SplitCosts split = await scope.ServiceProvider.GetRequiredService<CostSplitter>()
            .SplitAsync(new Dictionary<int, long> { [id] = 5 }, Token);

        Assert.Equal(5, split.PlanetResources[id]);
        Assert.Empty(split.Currencies);

        // A change made from the old version is refused and writes nothing.
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource("Талери 2", "Thalers 2", ResourceScopes.Civilization, null));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, id);

        Assert.Equal(ResourceScopes.Planet, record.GetProperty("values").GetProperty("scope").GetString());
        Assert.Equal("Thalers", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
    }

    /// <summary>
    /// The credits of the starting data are changed in the admin panel (the name and the scope),
    /// and a repeated start of the world keeps the change: the seed never overwrites a row of the
    /// reference.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_does_not_overwrite_the_changed_credits()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);

        using IServiceScope scope = _factory.Services.CreateScope();
        ResourceTypeEntity credits = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .SingleAsync(resource => resource.Code == EconomyTestData.CreditsCode, Token);

        Assert.Equal(ResourceScopes.Civilization, credits.Scope);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, credits.Id, credits.Version, Resource("Золоті монети", "Gold coins", ResourceScopes.Planet, metals)));

        // The world starts again, as the server does at every start.
        await scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, credits.Id);
        JsonElement values = record.GetProperty("values");

        Assert.Equal(EconomyTestData.CreditsCode, record.GetProperty("code").GetString());
        Assert.Equal(ResourceScopes.Planet, values.GetProperty("scope").GetString());
        Assert.Equal(metals, values.GetProperty("resource_group_id").GetInt32());
        Assert.Equal("Gold coins", values.GetProperty("name").GetProperty("en").GetString());
        Assert.Equal("Золоті монети", values.GetProperty("name").GetProperty("uk").GetString());

        using IServiceScope check = _factory.Services.CreateScope();
        ResourceCatalog catalog = await check.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Assert.Equal(ResourceScopes.Planet, catalog.Resources.Single(resource => resource.Id == credits.Id).Scope);
    }

    private static object Resource(string ukrainian, string english, string scope, int? groupId) => new
    {
        image_key = (string?)null,
        resource_group_id = groupId,
        scope,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
    };

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

    private async Task<int> GroupIdAsync(string code)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceGroups.AsNoTracking()
            .Where(group => group.Code == code)
            .Select(group => group.Id)
            .SingleAsync(Token);
    }
}
