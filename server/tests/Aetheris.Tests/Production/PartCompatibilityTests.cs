using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the fitting of the parts: the hulls a part fits and the races it belongs to
/// (task 00042).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PartCompatibilityTests
{
    private const string Parts = "/production/parts";

    /// <summary>
    /// The token that stops a test when it is over.
    /// </summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartCompatibilityTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public PartCompatibilityTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The starting data fits the parts to the hulls and to the races, so the catalog of the
    /// production knows the hulls of a part and the races of a part without a change of the code.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_starting_data_fits_the_parts_to_the_hulls_and_to_the_races()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        PartTypeInfo? engine = (await CatalogAsync(services, dbContext)).Parts
            .FirstOrDefault(part => part.Code == "part_1");

        Assert.NotNull(engine);
        Assert.NotEmpty(engine.CompatibleHullIds);
        Assert.NotEmpty(engine.RaceIds);

        // Every hull of the mapping is a real row of the catalog of the parts.
        HashSet<int> hullIds = [.. (await CatalogAsync(services, dbContext)).Parts.Select(part => part.Id)];

        Assert.All(engine.CompatibleHullIds, hullId => Assert.Contains(hullId, hullIds));
    }

    /// <summary>
    /// A link of a part to a hull and to a race, added to the tables only, reaches the game
    /// (task 00042, section 4.3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_link_added_only_to_the_tables_reaches_the_game()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        PartTypeInfo part = (await CatalogAsync(services, dbContext)).Parts.First(entry => entry.Code == "part_10");

        // A hull the starting data does not fit the part to, so the link is new.
        PartTypeInfo hull = (await CatalogAsync(services, dbContext)).Parts.First(entry => entry.Code == "part_110");

        Assert.DoesNotContain(hull.Id, part.CompatibleHullIds);
        Assert.DoesNotContain(1, part.RaceIds);

        dbContext.PartHullCompatibilities.Add(new PartHullCompatibilityEntity
        {
            PartTypeId = part.Id,
            HullPartTypeId = hull.Id,
        });

        dbContext.PartRaceCompatibilities.Add(new PartRaceCompatibilityEntity
        {
            PartTypeId = part.Id,
            RaceId = 1,
        });

        await dbContext.SaveChangesAsync(Token);

        services.GetRequiredService<ProductionCatalogCache>().Invalidate();

        PartTypeInfo stored = (await CatalogAsync(services, dbContext)).Parts.First(entry => entry.Code == "part_10");

        Assert.Contains(hull.Id, stored.CompatibleHullIds);
        Assert.Contains(1, stored.RaceIds);
    }

    /// <summary>
    /// The admin panel changes the hulls a part fits and the races it belongs to, and the change
    /// reaches the game (task 00042, section 4.3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_admin_panel_changes_the_hulls_and_the_races_of_a_part()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        PartTypeEntity part = await dbContext.PartTypes.AsNoTracking().SingleAsync(entry => entry.Code == "part_2", Token);
        PartTypeEntity hull = await dbContext.PartTypes.AsNoTracking().SingleAsync(entry => entry.Code == "part_84", Token);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Parts,
            new
            {
                name = ReferenceAdmin.Texts("Тестовий двигун", "Test engine"),
                description = ReferenceAdmin.Texts("Двигун для тесту", "An engine for the test"),
                part_group_id = part.PartGroupId,
                mark_level = 1,
                sort_order = 900,
                weight = 1,
                compatible_hulls = new[] { new { hull_part_type_id = hull.Id } },
                races = new[] { new { race_id = 1 } },
            }));

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Parts, id);

        Assert.Equal(
            hull.Id,
            record.GetProperty("values").GetProperty("compatible_hulls")[0].GetProperty("hull_part_type_id").GetInt32());
        Assert.Equal(
            1,
            record.GetProperty("values").GetProperty("races")[0].GetProperty("race_id").GetInt32());

        using IServiceScope afterScope = _factory.Services.CreateScope();
        ProductionDbContext after = afterScope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        Assert.Single(await after.PartHullCompatibilities.AsNoTracking()
            .Where(link => link.PartTypeId == id).ToListAsync(Token));
        Assert.Single(await after.PartRaceCompatibilities.AsNoTracking()
            .Where(link => link.PartTypeId == id).ToListAsync(Token));
    }

    /// <summary>
    /// A hull that is not a hull and a race that does not exist are refused by the form of the
    /// part (task 00042, section 4.3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_form_of_a_part_refuses_a_row_that_is_not_a_hull_and_a_race_that_does_not_exist()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        PartTypeEntity part = await dbContext.PartTypes.AsNoTracking().SingleAsync(entry => entry.Code == "part_2", Token);

        // A part that is not a hull: the engine itself is not a hull.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(
                admin,
                Parts,
                new
                {
                    name = ReferenceAdmin.Texts("Тестовий двигун", "Test engine"),
                    description = ReferenceAdmin.Texts("Двигун для тесту", "An engine for the test"),
                    part_group_id = part.PartGroupId,
                    mark_level = 1,
                    sort_order = 901,
                    weight = 1,
                    compatible_hulls = new[] { new { hull_part_type_id = part.Id } },
                }),
            HttpStatusCode.BadRequest,
            "compatible_hulls.0.hull_part_type_id",
            AdminReferenceErrors.Unknown);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(
                admin,
                Parts,
                new
                {
                    name = ReferenceAdmin.Texts("Тестовий двигун", "Test engine"),
                    description = ReferenceAdmin.Texts("Двигун для тесту", "An engine for the test"),
                    part_group_id = part.PartGroupId,
                    mark_level = 1,
                    sort_order = 902,
                    weight = 1,
                    races = new[] { new { race_id = 9999 } },
                }),
            HttpStatusCode.BadRequest,
            "races.0.race_id",
            AdminReferenceErrors.Unknown);
    }

    /// <summary>
    /// Reads the catalog of the production with the cache dropped.
    /// </summary>
    private static async Task<ProductionCatalog> CatalogAsync(IServiceProvider services, ProductionDbContext dbContext) =>
        await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(dbContext, services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);

    /// <summary>
    /// Fills the database with the starting data.
    /// </summary>
    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    /// <summary>
    /// Signs in an administrator with every right.
    /// </summary>
    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));
}
