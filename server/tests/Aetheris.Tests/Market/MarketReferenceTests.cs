using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Market.Services;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Market;

/// <summary>
/// Tests of the kinds of the market in the admin panel: the levels of the market and the
/// reference prices of the items (task 00024, section 4.1).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MarketReferenceTests
{
    private const string Levels = "/market/levels";

    private const string Prices = "/market/reference-prices";

    private const string Resources = "/world/resources";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public MarketReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A level of the market is created, changed and archived in the admin panel: the server
    /// gives the neutral code, the texts live in the table of the texts, the journal keeps the
    /// old and the new values with the reason, a change without a reason is refused and a change
    /// from an old version is refused with 409 (acceptance criteria 1 and 6 of task 00023).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_level_of_the_market_is_created_changed_and_archived_from_the_admin_panel()
    {
        await ResetAsync();
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Levels,
            Level("Секторний ринок", "Sector market", MarketVisibilities.Sector, 5m)));
        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Levels, id);
        string code = record.GetProperty("code").GetString()!;

        Assert.Matches("^market_level_[0-9]+$", code);
        Assert.Equal(version, record.GetProperty("version").GetInt32());
        Assert.Equal("Секторний ринок", record.GetProperty("values").GetProperty("name").GetProperty("uk").GetString());
        Assert.Equal("Sector market", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(MarketVisibilities.Sector, record.GetProperty("values").GetProperty("visibility").GetString());
        Assert.Equal(5m, record.GetProperty("values").GetProperty("commission_percent").GetDecimal());
        Assert.True(record.GetProperty("values").GetProperty("is_active").GetBoolean());

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            MarketLevelEntity stored = await services.GetRequiredService<MarketDbContext>().MarketLevels.AsNoTracking()
                .SingleAsync(entry => entry.Id == id, Token);
            LocalizationPack english = await services.GetRequiredService<ILocalizationTexts>().GetPackAsync("en", Token);

            Assert.Equal("Sector market", english.Texts[stored.NameKey]);
            Assert.Equal(MarketTextKeys.Name(MarketTextKeys.MarketLevel, code), stored.NameKey);
            Assert.Equal(MarketTextKeys.Description(MarketTextKeys.MarketLevel, code), stored.DescriptionKey);
        }

        // A change needs a reason.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Levels, id, version, Level("Секторний ринок 2", "Sector market 2", MarketVisibilities.Sector, 5m), reason: null),
            HttpStatusCode.BadRequest,
            "reason",
            AdminReason.Required);

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
            admin,
            Levels,
            id,
            version,
            Level("Секторний ринок 2", "Sector market 2", MarketVisibilities.Galactic, 7m)));
        Assert.Equal(version + 1, changed);

        JsonElement updated = await ReferenceAdmin.ReadAsync(admin, Levels, id);
        Assert.Equal("Sector market 2", updated.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(MarketVisibilities.Galactic, updated.GetProperty("values").GetProperty("visibility").GetString());
        Assert.Equal(7m, updated.GetProperty("values").GetProperty("commission_percent").GetDecimal());

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AdminAuditLogEntity audit = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
                .Where(record => record.AdminId == owner.AdminId && record.Action == MarketReferenceAdminService.MarketLevelDescriptor.Permissions.Updated)
                .OrderByDescending(record => record.Id)
                .FirstAsync(Token);

            Assert.Equal(AdminReferenceEndpoints.Target(MarketReferenceAdminService.MarketLevelKind, id), audit.Target);
            Assert.Equal(ReferenceAdmin.Reason, audit.Reason);
            Assert.Contains(MarketVisibilities.Sector, audit.OldValue, StringComparison.Ordinal);
            Assert.Contains(MarketVisibilities.Galactic, audit.NewValue, StringComparison.Ordinal);
        }

        // A change from an old version is refused and writes nothing.
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(
            admin,
            Levels,
            id,
            version,
            Level("Секторний ринок 3", "Sector market 3", MarketVisibilities.Sector, 1m));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Levels, id, changed));
        Assert.DoesNotContain(code, await ReferenceAdmin.ListCodesAsync(admin, Levels));
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Levels, "archived=true"));

        // An archived row is archived once; a second archiving is refused by the rule of the archive.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.ArchiveAsync(admin, Levels, id, archived),
            HttpStatusCode.UnprocessableEntity,
            "is_archived",
            AdminReferenceErrors.StateUnchanged);

        // The return from the archive offers the level again.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Levels, id, archived, archive: false));
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Levels));
    }

    /// <summary>
    /// A reference price is created for a resource and for a part, and it is read back with the
    /// item it prices.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_reference_price_is_created_for_a_resource_and_for_a_part()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        int resourceId = await ResourceIdAsync();
        int partId = await PartIdAsync();

        (int resourcePrice, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, null, 120)));
        (int partPrice, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Prices, Price(null, partId, 500)));

        JsonElement resource = await ReferenceAdmin.ReadAsync(admin, Prices, resourcePrice);
        Assert.Equal(resourceId, resource.GetProperty("values").GetProperty("resource_type_id").GetInt32());
        Assert.Equal(120, resource.GetProperty("values").GetProperty("price").GetInt32());

        JsonElement part = await ReferenceAdmin.ReadAsync(admin, Prices, partPrice);
        Assert.Equal(partId, part.GetProperty("values").GetProperty("part_type_id").GetInt32());
        Assert.Equal(500, part.GetProperty("values").GetProperty("price").GetInt32());

        using IServiceScope scope = _factory.Services.CreateScope();
        MarketDbContext dbContext = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

        Assert.Equal(120, await dbContext.ReferencePrices.AsNoTracking()
            .Where(price => price.Id == resourcePrice)
            .Select(price => price.Price)
            .SingleAsync(Token));
        Assert.Equal(resourceId, await dbContext.ReferencePrices.AsNoTracking()
            .Where(price => price.Id == resourcePrice)
            .Select(price => price.ResourceTypeId)
            .SingleAsync(Token));
    }

    /// <summary>
    /// A price names a resource or a part and never both, an item has one price, an unknown item
    /// is refused and an archived item takes no new price.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_wrong_reference_price_is_refused()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        int resourceId = await ResourceIdAsync();
        int partId = await PartIdAsync();

        // A price is of a resource or of a part, so both of them is a value that appears twice.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, partId, 10)),
            HttpStatusCode.BadRequest,
            "resource_type_id",
            AdminReferenceErrors.Duplicate);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, partId, 10)),
            HttpStatusCode.BadRequest,
            "part_type_id",
            AdminReferenceErrors.Duplicate);

        // A price names an item.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(null, null, 10)),
            HttpStatusCode.BadRequest,
            "resource_type_id",
            AdminReferenceErrors.Required);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(null, null, 10)),
            HttpStatusCode.BadRequest,
            "part_type_id",
            AdminReferenceErrors.Required);

        // An item that does not exist is unknown.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(999999, null, 10)),
            HttpStatusCode.BadRequest,
            "resource_type_id",
            AdminReferenceErrors.Unknown);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(null, 999999, 10)),
            HttpStatusCode.BadRequest,
            "part_type_id",
            AdminReferenceErrors.Unknown);

        // The price of one unit is a number that is not negative.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, null, null)),
            HttpStatusCode.BadRequest,
            "price",
            AdminReferenceErrors.Required);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, null, -1)),
            HttpStatusCode.BadRequest,
            "price",
            AdminReferenceErrors.OutOfBounds);

        // An item has one price.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, null, 120)));
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(resourceId, null, 130)),
            HttpStatusCode.BadRequest,
            "resource_type_id",
            AdminReferenceErrors.Duplicate);

        // An archived resource takes no new price.
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);
        (int archivedResource, int archivedVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Resources,
            Resource(metals, "Архівний ресурс", "Archived resource")));
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Resources, archivedResource, archivedVersion));

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Prices, Price(archivedResource, null, 10)),
            HttpStatusCode.UnprocessableEntity,
            "resource_type_id",
            AdminReferenceErrors.Archived);
    }

    /// <summary>
    /// The key test of the thesis of the author: a level of the market, written straight into the
    /// table, without the seed and without the admin panel, reaches the admin panel of the game
    /// (task 00024, acceptance criterion 9 of the section 4.2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_level_added_only_to_the_tables_reaches_the_admin_panel()
    {
        await ResetAsync();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            MarketDbContext dbContext = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

            MarketLevelEntity level = new()
            {
                Code = "market_level_900",
                Visibility = MarketVisibilities.Sector,
                CommissionPercent = 3m,
                IsActive = true,
                SortOrder = 900,
                NameKey = MarketTextKeys.Name(MarketTextKeys.MarketLevel, "market_level_900"),
                DescriptionKey = MarketTextKeys.Description(MarketTextKeys.MarketLevel, "market_level_900"),
            };

            dbContext.MarketLevels.Add(level);
            await dbContext.SaveChangesAsync(Token);
        }

        using HttpClient admin = await OwnerAsync();
        Assert.Contains("market_level_900", await ReferenceAdmin.ListCodesAsync(admin, Levels));

        // The code of the row is in the code of the program nowhere.
        string sources = Path.Combine(ServerRoot(), "src", "Aetheris.Market");
        Assert.False(File.ReadAllText(Path.Combine(sources, "Data", "market-reference-seed.json")).Contains("market_level_900", StringComparison.Ordinal));
    }

    private static string ServerRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !Directory.Exists(Path.Combine(directory, "src", "Aetheris.Market")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("The root of the server was not found.");
    }

    private static object Level(string ukrainian, string english, string visibility, decimal commission) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        visibility,
        commission_percent = commission,
        is_active = true,
        sort_order = 10,
    };

    private static object Price(int? resourceTypeId, int? partTypeId, long? price) => new
    {
        resource_type_id = resourceTypeId,
        part_type_id = partTypeId,
        price,
        sort_order = 10,
    };

    private static object Resource(int groupId, string ukrainian, string english) => new
    {
        resource_group_id = groupId,
        scope = ResourceScopes.Planet,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
    };

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private async Task<int> ResourceIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .Where(resource => resource.Scope == ResourceScopes.Planet)
            .OrderBy(resource => resource.Id)
            .Select(resource => resource.Id)
            .FirstAsync(Token);
    }

    private async Task<int> PartIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartTypes.AsNoTracking()
            .OrderBy(part => part.Id)
            .Select(part => part.Id)
            .FirstAsync(Token);
    }

    private async Task<int> GroupIdAsync(string code)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceGroups.AsNoTracking()
            .Where(group => group.Code == code)
            .Select(group => group.Id)
            .SingleAsync(Token);
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));
}
