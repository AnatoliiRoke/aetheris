using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of what the modules of extraction take and where (task 00055): the rows live in the tables of the
/// production, the starting data fills them, the catalog of the ships carries them, the admin panel edits
/// them, and a row added to the tables only reaches the game.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PartExtractionTests
{
    private const string Parts = "/production/parts";
    private const string DrillRig = "part_m_54";
    private const string CrystalExtractor = "part_m_55";
    private const string HeliumExtractor = "part_m_53";
    private const string EtherExtractor = "part_m_56";
    private const string MatterExtractor = "part_m_57";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartExtractionTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public PartExtractionTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The starting data gives every module of extraction its rows: the drill rig takes every resource of
    /// a field and its mass, the crystal extractor the crystals of a field evenly, the helium extractor the
    /// helium of a gas giant, and the extractors of ether and of matter their resource and the deuterium of
    /// a nebula.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_starting_data_gives_every_module_of_extraction_its_rows()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ResourceCatalog resources = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        ShipCatalogDto catalog = await services.GetRequiredService<IProductionModule>().GetShipCatalogAsync(Token);

        int Resource(string code) => resources.Resources.Single(resource => resource.Code == code).Id;

        IReadOnlyList<ShipModuleExtractionDto> Rows(string code) =>
            catalog.Modules.Single(module => module.Code == code).Extraction ?? [];

        Assert.Equal(
            [new ShipModuleExtractionDto(ExtractionPlaceKinds.AsteroidField, ExtractionModes.AllShares, null, null, true)],
            Rows(DrillRig));
        Assert.Equal(
            [new ShipModuleExtractionDto(ExtractionPlaceKinds.AsteroidField, ExtractionModes.GroupEven, null, resources.Groups.Single(group => group.Code == SeedCodes.CrystalGroup).Id, false)],
            Rows(CrystalExtractor));
        Assert.Equal(
            [new ShipModuleExtractionDto(ExtractionPlaceKinds.GasGiant, ExtractionModes.Listed, Resource(SeedCodes.Helium), null, false)],
            Rows(HeliumExtractor));
        Assert.Equal(
            [Resource(SeedCodes.Ether), Resource(SeedCodes.Deuterium)],
            Rows(EtherExtractor).Select(row => row.ResourceId!.Value));
        Assert.Equal(
            [Resource(SeedCodes.DarkMatter), Resource(SeedCodes.Deuterium)],
            Rows(MatterExtractor).Select(row => row.ResourceId!.Value));
        Assert.All(Rows(EtherExtractor).Concat(Rows(MatterExtractor)), row =>
        {
            Assert.Equal(ExtractionPlaceKinds.Nebula, row.PlaceKind);
            Assert.Equal(ExtractionModes.Listed, row.Mode);
            Assert.False(row.ReducesMass);
        });

        // A module that takes nothing has no rows, so the fleet does not count it as a module of extraction.
        Assert.All(
            catalog.Modules.Where(module => module.Code is not (DrillRig or CrystalExtractor or HeliumExtractor or EtherExtractor or MatterExtractor)),
            module => Assert.Empty(module.Extraction ?? []));
    }

    /// <summary>
    /// A row added to the tables only (not through the starting data or the admin panel) reaches the
    /// catalog of the ships, and an archived row does not.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_row_added_to_the_tables_only_reaches_the_catalog_and_an_archived_row_does_not()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        int drillId = await dbContext.PartTypes.AsNoTracking().Where(part => part.Code == DrillRig).Select(part => part.Id).SingleAsync(Token);

        PartExtractionEntity added = new()
        {
            PartTypeId = drillId,
            PlaceKind = ExtractionPlaceKinds.Nebula,
            Mode = ExtractionModes.AllShares,
        };

        dbContext.PartExtractions.Add(added);
        await dbContext.SaveChangesAsync(Token);

        IProductionModule production = services.GetRequiredService<IProductionModule>();
        ShipModuleDto drill = (await production.GetShipCatalogAsync(Token)).Modules.Single(module => module.Code == DrillRig);

        Assert.Contains(drill.Extraction!, row => row.PlaceKind == ExtractionPlaceKinds.Nebula && row.Mode == ExtractionModes.AllShares);

        added.IsArchived = true;
        await dbContext.SaveChangesAsync(Token);

        drill = (await production.GetShipCatalogAsync(Token)).Modules.Single(module => module.Code == DrillRig);

        Assert.DoesNotContain(drill.Extraction!, row => row.PlaceKind == ExtractionPlaceKinds.Nebula);
    }

    /// <summary>
    /// The seeder adds the rows of the starting data that are missing to a database that already has them
    /// (the rows of the ether extractor and of the matter extractor join the data later), and it adds none
    /// twice.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seeder_adds_the_missing_rows_to_a_filled_database_and_never_twice()
    {
        await ResetAsync();

        int total;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
            total = await dbContext.PartExtractions.CountAsync(Token);

            Assert.Equal(ProductionSeedExtractionCount(), total);

            // The rows that the dev database does not have yet: the deuterium of the two extractors.
            ResourceCatalog resources = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
            int deuteriumId = resources.Resources.Single(resource => resource.Code == SeedCodes.Deuterium).Id;

            Assert.Equal(2, await dbContext.PartExtractions.Where(row => row.ResourceId == deuteriumId).ExecuteDeleteAsync(Token));
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartExtractions.CountAsync(Token) < total);

            await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
            await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        }

        using IServiceScope after = _factory.Services.CreateScope();

        Assert.Equal(total, await after.ServiceProvider.GetRequiredService<ProductionDbContext>().PartExtractions.CountAsync(Token));
    }

    /// <summary>
    /// The admin panel saves what a module takes with the form of the part, reads it back, replaces it
    /// when the form is saved again, and the catalog of the ships carries the change at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_admin_panel_changes_what_a_module_takes_and_the_change_reaches_the_catalog()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        (int groupId, int helium, int groupOfResources, int hullId) = await IdsAsync();

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Parts,
            Part(
                groupId,
                hullId,
                901,
                new object[]
                {
                    new { place_kind = ExtractionPlaceKinds.GasGiant, mode = ExtractionModes.Listed, resource_id = helium },
                    new { place_kind = ExtractionPlaceKinds.AsteroidField, mode = ExtractionModes.GroupEven, resource_group_id = groupOfResources },
                    new { place_kind = ExtractionPlaceKinds.AsteroidField, mode = ExtractionModes.AllShares, reduces_mass = true },
                })));

        JsonElement stored = (await ReferenceAdmin.ReadAsync(admin, Parts, id)).GetProperty("values").GetProperty("extraction");

        Assert.Equal(3, stored.GetArrayLength());
        Assert.Equal(ExtractionPlaceKinds.GasGiant, stored[0].GetProperty("place_kind").GetString());
        Assert.Equal(helium, stored[0].GetProperty("resource_id").GetInt32());
        Assert.Equal(groupOfResources, stored[1].GetProperty("resource_group_id").GetInt32());
        Assert.True(stored[2].GetProperty("reduces_mass").GetBoolean());

        // Saving the form again replaces the rows: the gas giant row stays, the other two are gone.
        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
            admin,
            Parts,
            id,
            version,
            Part(groupId, hullId, 901, new object[] { new { place_kind = ExtractionPlaceKinds.GasGiant, mode = ExtractionModes.Listed, resource_id = helium, reduces_mass = false } })));

        Assert.True(changed > version);

        using IServiceScope scope = _factory.Services.CreateScope();
        ShipModuleDto module = (await scope.ServiceProvider.GetRequiredService<IProductionModule>().GetShipCatalogAsync(Token)).Modules
            .SingleOrDefault(entry => entry.Id == id)
            ?? throw new InvalidOperationException("The new part is not a module of the catalog of the ships.");

        ShipModuleExtractionDto row = Assert.Single(module.Extraction!);

        Assert.Equal(new ShipModuleExtractionDto(ExtractionPlaceKinds.GasGiant, ExtractionModes.Listed, helium, null, false), row);
    }

    /// <summary>
    /// The form refuses what the rows cannot mean: an unknown kind of place or way, a way without the
    /// resource or the group it needs, a resource with a way that does not take one, a resource of the world
    /// that does not exist, a gas giant with a way that needs a composition, the mass of a place that has
    /// none, and the same row twice.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_form_of_a_part_refuses_the_rows_that_mean_nothing()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        (int groupId, int helium, int groupOfResources, int hullId) = await IdsAsync();
        int number = 910;

        async Task AssertRefusedAsync(object[] rows, string field, string code) =>
            await ReferenceAdmin.AssertFieldAsync(
                await ReferenceAdmin.CreateAsync(admin, Parts, Part(groupId, hullId, number++, rows)),
                HttpStatusCode.BadRequest,
                field,
                code);

        await AssertRefusedAsync([new { place_kind = "planet", mode = ExtractionModes.AllShares }], "extraction.0.place_kind", AdminReferenceErrors.Unknown);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = "everything" }], "extraction.0.mode", AdminReferenceErrors.Unknown);
        await AssertRefusedAsync([new { place_kind = (string?)null, mode = ExtractionModes.AllShares }], "extraction.0.place_kind", AdminReferenceErrors.Required);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = ExtractionModes.Listed }], "extraction.0.resource_id", AdminReferenceErrors.Required);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = ExtractionModes.GroupEven }], "extraction.0.resource_group_id", AdminReferenceErrors.Required);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = ExtractionModes.AllShares, resource_id = helium }], "extraction.0.resource_id", AdminReferenceErrors.NotApplicable);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = ExtractionModes.Listed, resource_id = 999999 }], "extraction.0.resource_id", AdminReferenceErrors.Unknown);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = ExtractionModes.GroupEven, resource_group_id = 999999 }], "extraction.0.resource_group_id", AdminReferenceErrors.Unknown);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.GasGiant, mode = ExtractionModes.GroupEven, resource_group_id = groupOfResources }], "extraction.0.mode", AdminReferenceErrors.NotApplicable);
        await AssertRefusedAsync([new { place_kind = ExtractionPlaceKinds.Nebula, mode = ExtractionModes.AllShares, reduces_mass = true }], "extraction.0.reduces_mass", AdminReferenceErrors.NotApplicable);
        await AssertRefusedAsync(
            [
                new { place_kind = ExtractionPlaceKinds.GasGiant, mode = ExtractionModes.Listed, resource_id = helium },
                new { place_kind = ExtractionPlaceKinds.GasGiant, mode = ExtractionModes.Listed, resource_id = helium },
            ],
            "extraction.1.place_kind",
            AdminReferenceErrors.Duplicate);
    }

    private static object Part(int groupId, int hullId, int sortOrder, object[] extraction) => new
    {
        name = ReferenceAdmin.Texts("Тестовий екстрактор", "Test extractor"),
        description = ReferenceAdmin.Texts("Екстрактор для тесту", "An extractor for the test"),
        part_group_id = groupId,
        mark_level = 1,
        sort_order = sortOrder,
        weight = 1,
        compatible_hulls = new[] { new { hull_part_type_id = hullId } },
        extraction,
    };

    /// <summary>
    /// The number of the rows of what the modules take in the starting data.
    /// </summary>
    private static int ProductionSeedExtractionCount() => ProductionReferenceSeed.Load().PartExtractions?.Count ?? 0;

    /// <summary>
    /// The group of the modules of extraction, the helium, the group of the crystals and a hull a module may be fitted
    /// to (so the part is a module of the catalog of the ships), by their identifiers.
    /// </summary>
    private async Task<(int GroupId, int Helium, int CrystalGroup, int HullId)> IdsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ResourceCatalog resources = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        int groupId = await services.GetRequiredService<ProductionDbContext>().PartTypes.AsNoTracking()
            .Where(part => part.Code == HeliumExtractor)
            .Select(part => part.PartGroupId)
            .SingleAsync(Token);

        ShipCatalogDto catalog = await services.GetRequiredService<IProductionModule>().GetShipCatalogAsync(Token);
        ShipModuleDto helium = catalog.Modules.Single(module => module.Code == HeliumExtractor);

        return (
            groupId,
            resources.Resources.Single(resource => resource.Code == SeedCodes.Helium).Id,
            resources.Groups.Single(group => group.Code == SeedCodes.CrystalGroup).Id,
            helium.CompatibleHullIds[0]);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));
}
