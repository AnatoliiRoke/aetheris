using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the building categories (task 00020, section 3.1): a category is a row of data that
/// the admin panel creates, changes and archives, every building has one, and the building page
/// of the client groups the tiles by them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BuildingCategoryTests
{
    private const string Categories = "/planets/building-categories";

    private const string Buildings = "/planets/buildings";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildingCategoryTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public BuildingCategoryTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A category is created and changed from the admin panel: the server gives the neutral code,
    /// the texts live in the table of the texts, the journal keeps the old and the new values with
    /// the reason, and a change from an old version is refused with 409 (the rules of task 00023).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_category_is_created_and_changed_from_the_admin_panel()
    {
        await PlanetsTestData.ResetAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Categories, Category("Лабораторії", "Laboratories", 77)));
        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Categories, id);

        Assert.Matches("^group_[0-9]+$", record.GetProperty("code").GetString());
        Assert.Equal("Laboratories", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(77, record.GetProperty("values").GetProperty("sort_order").GetInt32());

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Categories, id, version, Category("Лабораторії 2", "Laboratories 2", 78)));
        Assert.Equal(version + 1, changed);

        using IServiceScope scope = _factory.Services.CreateScope();
        AdminAuditLogEntity audit = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(entry => entry.AdminId == owner.AdminId && entry.Action == BuildingCategoryAdminService.CategoryDescriptor.Permissions.Updated)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(Token);

        Assert.Equal(AdminReferenceEndpoints.Target(BuildingCategoryAdminService.CategoryKind, id), audit.Target);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);
        Assert.Contains("Laboratories", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("Laboratories 2", audit.NewValue, StringComparison.Ordinal);

        // A change from the version the administrator read before the change is refused.
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Categories, id, version, Category("Стара", "Stale", 1));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        // A change without a reason and a wrong order are refused, nothing is written.
        Assert.Equal(HttpStatusCode.BadRequest, (await ReferenceAdmin.UpdateAsync(admin, Categories, id, changed, Category("Без причини", "No reason", 1), reason: null)).StatusCode);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Categories, id, changed, Category("Мінус", "Minus", -1)),
            HttpStatusCode.BadRequest,
            "sort_order",
            AdminReferenceErrors.OutOfBounds);
        Assert.Equal(
            "Laboratories 2",
            (await ReferenceAdmin.ReadAsync(admin, Categories, id)).GetProperty("values").GetProperty("name").GetProperty("en").GetString());
    }

    /// <summary>
    /// A category that buildings have is not archived (422), an archived category is not offered
    /// to a new or a changed building, and the return from the archive offers it again. A building
    /// without a category, or with an unknown one, is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_category_in_use_is_not_archived_and_an_archived_one_is_not_offered()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));
        BuildingReferenceTests.Ids ids = await IdsAsync();

        (int usedId, int usedVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Categories, Category("Вжита", "Used", 80)));
        (int spareId, int spareVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Categories, Category("Вільна", "Spare", 81)));
        (int buildingId, int buildingVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Buildings,
            BuildingReferenceTests.Building(ids with { Category = usedId }, "Лабораторія", "Laboratory", levels: 1)));

        // A category a building has stays.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.ArchiveAsync(admin, Categories, usedId, usedVersion),
            HttpStatusCode.UnprocessableEntity,
            "is_archived",
            AdminReferenceErrors.InUse);

        // A category no building has is archived, and then it is not offered.
        (_, int archivedVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Categories, spareId, spareVersion));
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, BuildingReferenceTests.Building(ids with { Category = spareId }, "Нова", "New", levels: 1)),
            HttpStatusCode.UnprocessableEntity,
            "category_id",
            AdminReferenceErrors.Archived);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Buildings, buildingId, buildingVersion, BuildingReferenceTests.Building(ids with { Category = spareId }, "Лабораторія", "Laboratory", levels: 1)),
            HttpStatusCode.UnprocessableEntity,
            "category_id",
            AdminReferenceErrors.Archived);

        // After the return from the archive the category is offered again; the move of the
        // building frees the first category, which is archived then.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Categories, spareId, archivedVersion, archive: false));
        (_, int movedVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
            admin,
            Buildings,
            buildingId,
            buildingVersion,
            BuildingReferenceTests.Building(ids with { Category = spareId }, "Лабораторія", "Laboratory", levels: 1)));
        Assert.True(movedVersion > buildingVersion);
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Categories, usedId, usedVersion));

        // The category the building has now is in use again.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.ArchiveAsync(admin, Categories, spareId, archivedVersion + 1),
            HttpStatusCode.UnprocessableEntity,
            "is_archived",
            AdminReferenceErrors.InUse);

        // The category is mandatory and must exist.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, BuildingReferenceTests.Building(ids with { Category = 0 }, "Порожня", "Empty", levels: 1)),
            HttpStatusCode.BadRequest,
            "category_id",
            AdminReferenceErrors.Unknown);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, WithoutCategory(BuildingReferenceTests.Building(ids, "Без групи", "No group", levels: 1))),
            HttpStatusCode.BadRequest,
            "category_id",
            AdminReferenceErrors.Required);
    }

    /// <summary>
    /// The key test of the thesis of the author: a category and a building written straight into
    /// the tables, without the seed and without the admin panel, reach the game: the body lists
    /// the category with its order and the building with the category.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_category_added_only_to_the_tables_reaches_the_game()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        BuildingReferenceTests.Ids ids = await IdsAsync();

        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        BuildingCategoryEntity category = new()
        {
            Code = "group_900",
            NameKey = "planets.building_category.group_900.name",
            DescriptionKey = "planets.building_category.group_900.description",
            SortOrder = 900,
        };
        planets.BuildingCategories.Add(category);
        await planets.SaveChangesAsync(Token);

        BuildingTypeEntity building = new()
        {
            Code = "building_900",
            NameKey = "planets.building.building_900.name",
            DescriptionKey = "planets.building.building_900.description",
            SortOrder = 900,
            CategoryId = category.Id,
            LevelIds = [new BuildingLevelEntity { Level = 1, DurationSeconds = 60, CostIds = [new BuildingLevelCostEntity { ResourceTypeId = ids.Iron, Amount = 5 }] }],
        };
        planets.BuildingTypes.Add(building);
        await planets.SaveChangesAsync(Token);

        using JsonDocument overview = JsonDocument.Parse(await player.Client.GetStringAsync(PlanetsEndpoints.Prefix + "/" + player.PlanetId, Token));
        JsonElement listed = overview.RootElement.GetProperty("building_categories").EnumerateArray().Single(entry => entry.GetProperty("code").GetString() == "group_900");
        JsonElement tile = overview.RootElement.GetProperty("buildings").EnumerateArray().Single(entry => entry.GetProperty("code").GetString() == "building_900");

        Assert.Equal(category.Id, listed.GetProperty("id").GetInt32());
        Assert.Equal("planets.building_category.group_900.name", listed.GetProperty("name_key").GetString());
        Assert.Equal(900, listed.GetProperty("sort_order").GetInt32());
        Assert.Equal(category.Id, tile.GetProperty("category_id").GetInt32());

        // The categories come in their order, the new one after the seeded ones.
        List<int> order = [.. overview.RootElement.GetProperty("building_categories").EnumerateArray().Select(entry => entry.GetProperty("sort_order").GetInt32())];
        Assert.Equal([.. order.OrderBy(value => value)], order);
        Assert.Equal(category.Id, overview.RootElement.GetProperty("building_categories").EnumerateArray().Last().GetProperty("id").GetInt32());
    }

    /// <summary>
    /// A category changed in the admin panel reaches the player at once, although the catalog is
    /// kept in memory for long: the name key and the order come from the table.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_changed_category_reaches_the_player_without_a_restart()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, Aetheris.Planets.Configuration.PlanetsConfigKeys.CatalogCacheSeconds, "3600");

        try
        {
            DateTimeOffset now = PlanetsTestData.GameNow(services);
            PlanetOverviewDto before = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);
            BuildingCategoryDto first = before.BuildingCategories!.OrderBy(category => category.SortOrder).First();

            BuildingCategoryEntity stored = await services.GetRequiredService<PlanetsDbContext>().BuildingCategories.AsNoTracking().SingleAsync(entry => entry.Id == first.Id, Token);
            await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Categories, stored.Id, stored.Version, Category("Перша", "First", 5000)));

            PlanetOverviewDto after = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);
            Assert.Equal(5000, after.BuildingCategories!.Single(category => category.Id == first.Id).SortOrder);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, Aetheris.Planets.Configuration.PlanetsConfigKeys.CatalogCacheSeconds, "0");
        }
    }

    /// <summary>
    /// Twenty parallel creations get twenty different neutral codes of the group of the categories.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_creations_get_twenty_different_codes()
    {
        const int parallelCount = 20;

        await PlanetsTestData.ResetAsync(_factory);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Categories, Category("Група " + index, "Group " + index, 100 + index))));
        List<int> created = [];

        foreach (HttpResponseMessage response in responses)
        {
            created.Add((await ReferenceAdmin.ChangedAsync(response)).Id);
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        List<string> codes = await scope.ServiceProvider.GetRequiredService<PlanetsDbContext>().BuildingCategories.AsNoTracking()
            .Where(category => created.Contains(category.Id))
            .Select(category => category.Code)
            .ToListAsync(Token);
        HashSet<string> seeded = [.. PlanetsReferenceSeed.Load().Categories.Select(category => category.Code)];

        Assert.Equal(parallelCount, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.DoesNotContain(code, seeded));
    }

    /// <summary>
    /// The seed gives every building a category named in the starting data and writes the
    /// categories with their texts in every language; the order of the file is the order of the
    /// groups.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_gives_every_building_a_category_from_the_data()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        PlanetsDbContext planets = scope.ServiceProvider.GetRequiredService<PlanetsDbContext>();
        PlanetsSeedData seed = PlanetsReferenceSeed.Load();

        Dictionary<int, string> codeById = await planets.BuildingCategories.AsNoTracking().ToDictionaryAsync(category => category.Id, category => category.Code, Token);
        List<BuildingTypeEntity> buildings = await planets.BuildingTypes.AsNoTracking().ToListAsync(Token);

        Assert.Equal(seed.Categories.Count, codeById.Count);
        Assert.All(seed.BuildingTypes, type => Assert.Equal(type.Category, codeById[buildings.Single(building => building.Code == type.Code).CategoryId]));

        Aetheris.Core.Text.ILocalizationTexts texts = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Text.ILocalizationTexts>();
        IReadOnlyList<Aetheris.Core.Text.LocalizationPack> packs = await AdminReferenceRules.PacksAsync(texts, Token);

        Assert.All(seed.Categories, category => Assert.All(
            packs,
            pack => Assert.False(string.IsNullOrWhiteSpace(pack.Texts.GetValueOrDefault(PlanetsTextKeys.CategoryName(category.Code))))));
    }

    /// <summary>
    /// The seed never overwrites a category changed in the admin panel and writes again a category
    /// that is missing.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_does_not_overwrite_changes_and_adds_what_is_missing()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        List<BuildingCategoryEntity> seeded = await planets.BuildingCategories.AsNoTracking().OrderBy(category => category.SortOrder).ToListAsync(Token);
        BuildingCategoryEntity changed = seeded[0];
        BuildingCategoryEntity removed = seeded[^1];

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Categories, changed.Id, changed.Version, Category("Змінена", "Changed", 3)));

        // The last category is gone, as in a database that missed it: its buildings take the first.
        await planets.BuildingTypes.Where(type => type.CategoryId == removed.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(type => type.CategoryId, changed.Id), Token);
        await planets.BuildingCategories.Where(category => category.Id == removed.Id).ExecuteDeleteAsync(Token);

        await services.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);
        await services.GetRequiredService<Aetheris.Host.Configuration.ConfigSeedService>().SeedAsync(Token);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Categories, changed.Id);
        Assert.Equal("Changed", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(3, record.GetProperty("values").GetProperty("sort_order").GetInt32());

        Assert.True(await planets.BuildingCategories.AsNoTracking().AnyAsync(category => category.Code == removed.Code, Token));
    }

    private static object Category(string ukrainian, string english, int sortOrder) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        sort_order = sortOrder,
    };

    private static Dictionary<string, object?> WithoutCategory(object building)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(building));

        return document.RootElement.EnumerateObject()
            .Where(property => property.Name != "category_id")
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
    }

    private async Task<BuildingReferenceTests.Ids> IdsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        return new BuildingReferenceTests.Ids(
            await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron),
            (await PlanetsTestData.TypeAsync(services, SeedCodes.AdminCenter)).Id,
            await services.GetRequiredService<PlanetsDbContext>().BuildingCategories.AsNoTracking()
                .OrderBy(category => category.SortOrder).ThenBy(category => category.Id)
                .Select(category => category.Id)
                .FirstAsync(Token));
    }
}
