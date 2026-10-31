using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the characteristics of the parts in the admin panel: the characteristics
/// themselves, the characteristics of a group of the parts, the values of a characteristic of a
/// part and the weights of the resources and of the parts (task 00027, section 3.3).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CharacteristicReferenceTests
{
    private const string Characteristics = "/production/characteristics";

    private const string GroupCharacteristics = "/production/part-group-characteristics";

    private const string PartCharacteristics = "/production/part-characteristics";

    private const string Parts = "/production/parts";

    private const string Resources = "/world/resources";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacteristicReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CharacteristicReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A characteristic is created, changed and archived in the admin panel: the server gives
    /// the neutral code, the texts and the unit of measure live in the table of the texts, and
    /// the journal keeps the old and the new values with the reason.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_characteristic_is_created_changed_and_archived_from_the_admin_panel()
    {
        await ResetAsync();
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Characteristics, Characteristic("Тяга", "Thrust")));
        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Characteristics, id);
        string code = record.GetProperty("code").GetString()!;

        Assert.Matches("^characteristic_[0-9]+$", code);
        Assert.Equal(version, record.GetProperty("version").GetInt32());
        Assert.Equal("Тяга", record.GetProperty("values").GetProperty("name").GetProperty("uk").GetString());
        Assert.Equal("Thrust", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal("кВт", record.GetProperty("values").GetProperty("unit").GetProperty("uk").GetString());
        Assert.Equal(10, record.GetProperty("values").GetProperty("sort_order").GetInt32());

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            CharacteristicEntity stored = await services.GetRequiredService<ProductionDbContext>().Characteristics.AsNoTracking()
                .SingleAsync(entry => entry.Id == id, Token);
            LocalizationPack english = await services.GetRequiredService<ILocalizationTexts>().GetPackAsync("en", Token);

            Assert.Equal("Thrust", english.Texts[stored.NameKey]);
            Assert.Equal("kW", english.Texts[stored.UnitKey!]);
            Assert.Equal(ProductionTextKeys.CharacteristicUnit(stored.Code), stored.UnitKey);
        }

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Characteristics, id, version, Characteristic("Тяга велика", "Great thrust", 11)));
        Assert.Equal(version + 1, changed);

        JsonElement updated = await ReferenceAdmin.ReadAsync(admin, Characteristics, id);
        Assert.Equal("Great thrust", updated.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(11, updated.GetProperty("values").GetProperty("sort_order").GetInt32());

        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Characteristics, id, changed));
        Assert.DoesNotContain(code, await ReferenceAdmin.ListCodesAsync(admin, Characteristics));
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Characteristics, "archived=true"));

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            AdminAuditLogEntity audit = await services.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
                .Where(record => record.AdminId == owner.AdminId && record.Action == CharacteristicReferenceAdminService.CharacteristicDescriptor.Permissions.Updated)
                .OrderByDescending(record => record.Id)
                .FirstAsync(Token);

            Assert.Equal(AdminReferenceEndpoints.Target(CharacteristicReferenceAdminService.CharacteristicKind, id), audit.Target);
            Assert.Equal(ReferenceAdmin.Reason, audit.Reason);
            Assert.Contains("Thrust", audit.OldValue, StringComparison.Ordinal);
            Assert.Contains("Great thrust", audit.NewValue, StringComparison.Ordinal);
        }

        // The return from the archive offers the characteristic again.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Characteristics, id, archived, archive: false));
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Characteristics));
    }

    /// <summary>
    /// A characteristic is bound to a group of the parts and the link shows up in the group of
    /// the catalog, and the value of a characteristic of a part is read back with the part.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_characteristic_is_bound_to_a_group_and_its_value_is_written_for_a_part()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        int groupId = await GroupIdAsync();
        int partId = await PartIdAsync();

        (int characteristicId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Characteristics, Characteristic("Ємність щита", "Shield capacity")));
        (int linkId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, GroupCharacteristics, GroupCharacteristic(groupId, characteristicId)));
        (int valueId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, PartCharacteristics, PartCharacteristic(partId, characteristicId, 250m)));

        JsonElement link = await ReferenceAdmin.ReadAsync(admin, GroupCharacteristics, linkId);
        Assert.Equal(groupId, link.GetProperty("values").GetProperty("part_group_id").GetInt32());
        Assert.Equal(characteristicId, link.GetProperty("values").GetProperty("characteristic_id").GetInt32());
        Assert.True(link.GetProperty("values").GetProperty("is_required").GetBoolean());

        JsonElement value = await ReferenceAdmin.ReadAsync(admin, PartCharacteristics, valueId);
        Assert.Equal(partId, value.GetProperty("values").GetProperty("part_type_id").GetInt32());
        Assert.Equal(characteristicId, value.GetProperty("values").GetProperty("characteristic_id").GetInt32());
        Assert.Equal(250m, value.GetProperty("values").GetProperty("number_value").GetDecimal());

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // The link and the value reach the catalog of the module at once.
        CharacteristicDto catalog = Assert.Single(
            await services.GetRequiredService<CharacteristicQueryService>().ReadAsync(Token),
            row => row.CharacteristicId == characteristicId);
        Assert.Equal(groupId, Assert.Single(catalog.Groups).PartGroupId);
        Assert.True(Assert.Single(catalog.Groups).IsRequired);

        PartCharacteristicsDto part = Assert.Single(
            await services.GetRequiredService<CharacteristicQueryService>().ReadPartsAsync(Token),
            row => row.PartTypeId == partId);
        Assert.Equal(250m, Assert.Single(part.Characteristics, row => row.CharacteristicId == characteristicId).NumberValue);
    }

    /// <summary>
    /// A change without a reason, from an old version, with a wrong value or with a duplicate
    /// link is refused, and a link to an archived characteristic is refused as well.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_wrong_change_of_the_characteristics_is_refused()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        int groupId = await GroupIdAsync();
        int partId = await PartIdAsync();

        (int characteristicId, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Characteristics, Characteristic("Потужність", "Power")));
        (int linkId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, GroupCharacteristics, GroupCharacteristic(groupId, characteristicId)));

        // A change needs a reason.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Characteristics, characteristicId, version, Characteristic("Потужність 2", "Power 2"), reason: null),
            HttpStatusCode.BadRequest,
            "reason",
            AdminReason.Required);

        // A change from an old version meets the new one and writes nothing.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Characteristics, characteristicId, version, Characteristic("Потужність 2", "Power 2")));
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Characteristics, characteristicId, version, Characteristic("Потужність 3", "Power 3"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        // A group has a characteristic once.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, GroupCharacteristics, GroupCharacteristic(groupId, characteristicId)),
            HttpStatusCode.BadRequest,
            "characteristic_id",
            AdminReferenceErrors.Duplicate);

        // A part has a value of a characteristic once.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, PartCharacteristics, PartCharacteristic(partId, characteristicId, 10m)));
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, PartCharacteristics, PartCharacteristic(partId, characteristicId, 20m)),
            HttpStatusCode.BadRequest,
            "characteristic_id",
            AdminReferenceErrors.Duplicate);

        // A row that does not exist is not found.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(AdminEndpoints.Prefix + Characteristics + "/999999", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReferenceAdmin.UpdateAsync(admin, Characteristics, 999999, 1, Characteristic("Ніщо", "Nothing"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReferenceAdmin.UpdateAsync(admin, GroupCharacteristics, 999999, 1, GroupCharacteristic(groupId, characteristicId))).StatusCode);

        // A group and a characteristic that do not exist are unknown.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, GroupCharacteristics, GroupCharacteristic(999999, characteristicId)),
            HttpStatusCode.BadRequest,
            "part_group_id",
            AdminReferenceErrors.Unknown);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, GroupCharacteristics, GroupCharacteristic(groupId, 999999)),
            HttpStatusCode.BadRequest,
            "characteristic_id",
            AdminReferenceErrors.Unknown);

        // The value of a characteristic is a number.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, PartCharacteristics, PartCharacteristic(partId, characteristicId, null)),
            HttpStatusCode.BadRequest,
            "number_value",
            AdminReferenceErrors.Required);

        // An archived characteristic takes no new link, and the link it has keeps working.
        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Characteristics, characteristicId, version + 1));
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, GroupCharacteristics, GroupCharacteristic(groupId, characteristicId)),
            HttpStatusCode.UnprocessableEntity,
            "characteristic_id",
            AdminReferenceErrors.Archived);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, PartCharacteristics, PartCharacteristic(partId, characteristicId, 30m)),
            HttpStatusCode.UnprocessableEntity,
            "characteristic_id",
            AdminReferenceErrors.Archived);

        // The link of the archived characteristic is still there and can be changed.
        (_, int changedLink) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, GroupCharacteristics, linkId, 1, GroupCharacteristic(groupId, characteristicId, 5)));
        Assert.Equal(2, changedLink);
    }

    /// <summary>
    /// The weight of a resource and of a part is changed in the admin panel and read back: the
    /// weight is data and not a number of the code (task 00027, section 3.1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_weight_of_a_resource_and_of_a_part_is_changed_from_the_admin_panel()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        int metals = await ResourceGroupIdAsync(SeedCodes.MetalGroup);
        int groupId = await GroupIdAsync();

        (int resourceId, int resourceVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Олово", "Tin", 2.5m)));
        JsonElement resource = await ReferenceAdmin.ReadAsync(admin, Resources, resourceId);
        Assert.Equal(2.5m, resource.GetProperty("values").GetProperty("weight").GetDecimal());

        (_, int changedResource) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, resourceId, resourceVersion, Resource(metals, "Олово", "Tin", 3.75m)));
        Assert.Equal(resourceVersion + 1, changedResource);
        Assert.Equal(3.75m, (await ReferenceAdmin.ReadAsync(admin, Resources, resourceId)).GetProperty("values").GetProperty("weight").GetDecimal());

        // A negative weight is refused.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Resources, resourceId, changedResource, Resource(metals, "Олово", "Tin", -1m)),
            HttpStatusCode.BadRequest,
            "weight",
            AdminReferenceErrors.OutOfBounds);

        (int partId, int partVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(groupId, "Реактор", "Reactor", 12.5m)));
        JsonElement part = await ReferenceAdmin.ReadAsync(admin, Parts, partId);
        Assert.Equal(12.5m, part.GetProperty("values").GetProperty("weight").GetDecimal());

        (_, int changedPart) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Parts, partId, partVersion, Part(groupId, "Реактор", "Reactor", 20m)));
        Assert.Equal(partVersion + 1, changedPart);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        PartTypeEntity stored = await services.GetRequiredService<ProductionDbContext>().PartTypes.AsNoTracking()
            .SingleAsync(entry => entry.Id == partId, Token);
        ResourceTypeEntity storedResource = await services.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .SingleAsync(entry => entry.Id == resourceId, Token);

        Assert.Equal(20m, stored.Weight);
        Assert.Equal(3.75m, storedResource.Weight);
    }

    /// <summary>
    /// The key test of the thesis of the author: a characteristic, a link of a group of the
    /// parts and a value of a part, written straight into the tables, without the seed and
    /// without the admin panel, reach the game (task 00027, acceptance criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_characteristic_added_only_to_the_tables_reaches_the_game()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        PartGroupEntity group = new()
        {
            Code = "part_group_900",
            NameKey = "production.part_group.part_group_900.name",
            DescriptionKey = "production.part_group.part_group_900.description",
            SortOrder = 900,
        };

        dbContext.PartGroups.Add(group);
        await dbContext.SaveChangesAsync(Token);

        CharacteristicEntity characteristic = new()
        {
            Code = "characteristic_900",
            NameKey = "production.characteristic.characteristic_900.name",
            DescriptionKey = "production.characteristic.characteristic_900.description",
            UnitKey = "production.characteristic.characteristic_900.unit",
            SortOrder = 900,
        };

        dbContext.Characteristics.Add(characteristic);
        await dbContext.SaveChangesAsync(Token);

        PartGroupCharacteristicEntity link = new()
        {
            PartGroupId = group.Id,
            CharacteristicId = characteristic.Id,
            SortOrder = 1,
            IsRequired = true,
        };

        dbContext.PartGroupCharacteristics.Add(link);
        await dbContext.SaveChangesAsync(Token);

        services.GetRequiredService<ProductionCatalogCache>().Invalidate();

        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(dbContext, services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);

        PartGroupInfo? storedGroup = catalog.FindGroup(group.Id);
        Assert.NotNull(storedGroup);
        Assert.Contains(storedGroup.Characteristics, entry => entry.CharacteristicId == characteristic.Id && entry.IsRequired);

        // The code of the row is in the code of the program nowhere.
        string sources = Path.Combine(ServerRoot(), "src", "Aetheris.Production");
        Assert.False(File.ReadAllText(Path.Combine(sources, "Data", "production-reference-seed.json")).Contains("characteristic_900", StringComparison.Ordinal));
    }

    private static string ServerRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !Directory.Exists(Path.Combine(directory, "src", "Aetheris.Production")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("The root of the server was not found.");
    }

    private static object Characteristic(string ukrainian, string english, int sortOrder = 10) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        unit = ReferenceAdmin.Texts("кВт", "kW"),
        sort_order = sortOrder,
    };

    private static object GroupCharacteristic(int groupId, int characteristicId, int sortOrder = 1) => new
    {
        part_group_id = groupId,
        characteristic_id = characteristicId,
        sort_order = sortOrder,
        is_required = true,
    };

    private static object PartCharacteristic(int partTypeId, int characteristicId, decimal? numberValue) => new
    {
        part_type_id = partTypeId,
        characteristic_id = characteristicId,
        number_value = numberValue,
    };

    private static object Part(int groupId, string ukrainian, string english, decimal weight) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        part_group_id = groupId,
        mark_level = PartMarkLevels.First,
        sort_order = 99,
        weight = weight,
        attributes = Array.Empty<object>(),
    };

    private static object Resource(int groupId, string ukrainian, string english, decimal weight) => new
    {
        resource_group_id = groupId,
        scope = ResourceScopes.Planet,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        weight = weight,
    };

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private async Task<int> GroupIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartGroups.AsNoTracking()
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.Id)
            .Select(group => group.Id)
            .FirstAsync(Token);
    }

    private async Task<int> PartIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartTypes.AsNoTracking()
            .OrderBy(part => part.SortOrder)
            .ThenBy(part => part.Id)
            .Select(part => part.Id)
            .FirstAsync(Token);
    }

    private async Task<int> ResourceGroupIdAsync(string code)
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
