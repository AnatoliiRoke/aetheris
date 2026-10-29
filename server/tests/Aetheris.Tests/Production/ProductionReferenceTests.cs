using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the groups of the parts, the parts and the recipes in the admin panel, and of the
/// way a change there reaches the players (task 00013, the requirements of task 00023).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductionReferenceTests
{
    private const string Groups = "/production/part-groups";

    private const string Parts = "/production/parts";

    private const string Recipes = "/production/recipes";

    private const string Technologies = "/research/technologies";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProductionReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A group, a part and a recipe are created and changed in the admin panel: the server gives
    /// the neutral codes, the texts live in the table of the texts, the record shows the
    /// ingredients and the requirements, and the journal keeps the whole old and the new values
    /// with the reason.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_group_a_part_and_a_recipe_are_created_and_changed_from_the_admin_panel()
    {
        await ResetAsync();
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        Ids ids = await IdsAsync();

        (int groupId, int groupVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Groups, Group("Сенсори", "Sensors", 70)));
        (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(groupId, "Антена", "Antenna", attributes: [new { attribute_key = PartAttributeKeys.Range, number_value = 12m }])));
        (int recipeId, int recipeVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Recipes,
            Recipe(ids, "Антена", "Antenna", partTypeId: partId, requirements: [new { requirement_type = RequirementTypes.BuildingLevel, target_id = ids.FactoryTypeId, target_level = 1 }])));

        JsonElement group = await ReferenceAdmin.ReadAsync(admin, Groups, groupId);
        JsonElement part = await ReferenceAdmin.ReadAsync(admin, Parts, partId);
        JsonElement recipe = await ReferenceAdmin.ReadAsync(admin, Recipes, recipeId);
        Assert.Matches("^part_group_[0-9]+$", group.GetProperty("code").GetString());
        Assert.Matches("^part_[0-9]+$", part.GetProperty("code").GetString());
        Assert.Matches("^recipe_[0-9]+$", recipe.GetProperty("code").GetString());
        Assert.Equal("Sensors", group.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(groupId, part.GetProperty("values").GetProperty("part_group_id").GetInt32());
        Assert.Equal(1, part.GetProperty("values").GetProperty("attributes").GetArrayLength());
        Assert.Equal(1, recipe.GetProperty("values").GetProperty("ingredients").GetArrayLength());
        Assert.Equal(1, recipe.GetProperty("values").GetProperty("requirements").GetArrayLength());

        (_, int changedGroup) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Groups, groupId, groupVersion, Group("Сенсори далекі", "Long sensors", 71)));
        Assert.Equal(groupVersion + 1, changedGroup);

        (_, int changedRecipe) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
            admin,
            Recipes,
            recipeId,
            recipeVersion,
            Recipe(ids, "Антена дальня", "Long antenna", partTypeId: partId, durationSeconds: 90)));
        Assert.Equal(recipeVersion + 1, changedRecipe);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        AdminAuditLogEntity audit = await services.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == owner.AdminId && record.Action == ProductionReferenceAdminService.RecipeDescriptor.Permissions.Updated)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);

        // The old values hold everything the form shows, the ingredients and the requirements too.
        Assert.Equal(AdminReferenceEndpoints.Target(ProductionReferenceAdminService.RecipeKind, recipeId), audit.Target);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);
        Assert.Contains("\"Antenna\"", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("ingredients", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains(RequirementTypes.BuildingLevel, audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("Long antenna", audit.NewValue, StringComparison.Ordinal);

        RecipeEntity stored = await services.GetRequiredService<ProductionDbContext>().Recipes.AsNoTracking().SingleAsync(entry => entry.Id == recipeId, Token);
        Assert.Equal(90, stored.DurationSeconds);
        Assert.Empty(await services.GetRequiredService<IRequirementsModule>().GetAsync(RequirementSubjectTypes.Recipe, recipeId, 1, Token));
    }

    /// <summary>
    /// A change without a reason, from an old version, of a row that does not exist or with
    /// wrong values is refused with the codes of the shared shell (task 00023: 400, 404, 409,
    /// 422).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_wrong_change_of_the_production_is_refused()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Щит", "Shield")));
        (int recipeId, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Щит", "Shield", partTypeId: partId)));

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, "Щит 2", "Shield 2", partTypeId: partId), reason: null),
            HttpStatusCode.BadRequest,
            "reason",
            AdminReason.Required);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, "Щит 2", "Shield 2", partTypeId: partId)));
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, "Щит 3", "Shield 3", partTypeId: partId));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        // A row that does not exist is not found, and not a failure of the server.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(AdminEndpoints.Prefix + Recipes + "/999999", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReferenceAdmin.UpdateAsync(admin, Recipes, 999999, 1, Recipe(ids, "Ніщо", "Nothing", partTypeId: partId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReferenceAdmin.UpdateAsync(admin, Parts, 999999, 1, Part(ids.GroupId, "Ніщо", "Nothing"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReferenceAdmin.ArchiveAsync(admin, Groups, 999999, 1)).StatusCode);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids with { FactoryTypeId = 999999 }, "Щит", "Shield", partTypeId: partId)),
            HttpStatusCode.BadRequest,
            "building_type_id",
            AdminReferenceErrors.Unknown);

        // A recipe makes one product.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Щит", "Shield", partTypeId: partId, resourceTypeId: ids.Iron)),
            HttpStatusCode.BadRequest,
            "resource_type_id",
            AdminReferenceErrors.NotApplicable);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Щит", "Shield")),
            HttpStatusCode.BadRequest,
            "part_type_id",
            AdminReferenceErrors.Required);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Щит", "Shield", partTypeId: partId, ingredients: [new { resource_type_id = ids.Iron, amount = 1 }, new { resource_type_id = ids.Iron, amount = 2 }])),
            HttpStatusCode.BadRequest,
            "ingredients.1.resource_type_id",
            AdminReferenceErrors.Duplicate);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Щит", "Shield", partTypeId: partId, ingredients: [])),
            HttpStatusCode.UnprocessableEntity,
            "ingredients",
            AdminReferenceErrors.CostMissing);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Щит", "Shield", partTypeId: partId, requirements: [new { requirement_type = RequirementTypes.TechnologyLevel, target_id = 999999, target_level = 1 }])),
            HttpStatusCode.BadRequest,
            "requirements.0.requirement_type",
            AdminReferenceErrors.Unknown);

        // A logical code of an attribute is one the program knows.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Щит", "Shield", attributes: [new { attribute_key = PartAttributeKeys.DamageKind, code_value = "code_unknown" }])),
            HttpStatusCode.BadRequest,
            "attributes.0.code_value",
            AdminReferenceErrors.Unknown);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Щит", "Shield", attributes: [new { attribute_key = PartAttributeKeys.Mass, number_value = 1m }, new { attribute_key = PartAttributeKeys.Mass, number_value = 2m }])),
            HttpStatusCode.BadRequest,
            "attributes.1.attribute_key",
            AdminReferenceErrors.Duplicate);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Recipes, recipeId);
        Assert.Equal("Shield 2", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
    }

    /// <summary>
    /// An archived recipe keeps working for the player who already makes it: the batch is
    /// finished and its parts reach the storage, but no new batch starts and a player without
    /// it does not see it. An archived part or group closes its recipes the same way and is not
    /// offered for a new row; the return from the archive offers the recipe again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_archived_recipe_works_for_its_makers_but_is_not_offered()
    {
        await ResetAsync();
        using PlanetsTestPlayer maker = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int groupId, int groupVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Groups, Group("Броня", "Armour", 80)));
        (int partId, int partVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(groupId, "Плита", "Plate")));
        (int recipeId, int recipeVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Плита", "Plate", partTypeId: partId)));
        string recipeCode = (await ReferenceAdmin.ReadAsync(admin, Recipes, recipeId)).GetProperty("code").GetString()!;

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        long makerFactory = await ProductionTestData.BuildFactoryAsync(maker, services);
        long strangerFactory = await ProductionTestData.BuildFactoryAsync(stranger, services);
        await ProductionTestData.GiveEveryResourceAsync(maker, services);
        await ProductionTestData.BuildStorageAsync(maker, services);

        await production.EnqueueAsync(maker.Account.UserId, maker.PlanetId, makerFactory, recipeId, 2, Token);
        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Recipes, recipeId, recipeVersion));

        Assert.DoesNotContain(recipeCode, await ReferenceAdmin.ListCodesAsync(admin, Recipes));
        Assert.Contains(recipeCode, await ReferenceAdmin.ListCodesAsync(admin, Recipes, "archived=true"));

        // The maker keeps the name of what the queue makes, but a new batch is refused.
        ProductionFactoryDto mine = await production.GetFactoryAsync(maker.Account.UserId, maker.PlanetId, makerFactory, Token);
        ProductionRecipeDto kept = mine.Recipes.Single(entry => entry.RecipeId == recipeId);
        Assert.False(kept.IsAvailable);
        Assert.Equal(ProductionErrorCodes.RecipeArchived, kept.Reasons[0].Code);
        GameException refused = await Assert.ThrowsAsync<GameException>(() => production.EnqueueAsync(maker.Account.UserId, maker.PlanetId, makerFactory, recipeId, 1, Token));
        Assert.Equal(ProductionErrorCodes.RecipeArchived, refused.Code);

        await ProductionTestData.CompleteAsync(maker, services, makerFactory);
        ProductionStockDto stock = await production.GetStockAsync(maker.Account.UserId, maker.PlanetId, Token);
        Assert.Equal(2, stock.Stacks.Where(stack => stack.PartTypeId == partId).Sum(stack => stack.Quantity));

        ProductionFactoryDto theirs = await production.GetFactoryAsync(stranger.Account.UserId, stranger.PlanetId, strangerFactory, Token);
        Assert.DoesNotContain(theirs.Recipes, entry => entry.RecipeId == recipeId);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Recipes, recipeId, archived, archive: false));
        ProductionFactoryDto restored = await production.GetFactoryAsync(stranger.Account.UserId, stranger.PlanetId, strangerFactory, Token);
        Assert.True(restored.Recipes.Single(entry => entry.RecipeId == recipeId).IsAvailable);

        // An archived part closes its recipe and is not offered for a new recipe.
        (_, int archivedPart) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Parts, partId, partVersion));
        ProductionFactoryDto withoutPart = await production.GetFactoryAsync(stranger.Account.UserId, stranger.PlanetId, strangerFactory, Token);
        Assert.DoesNotContain(withoutPart.Recipes, entry => entry.RecipeId == recipeId);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Плита 2", "Plate 2", partTypeId: partId)),
            HttpStatusCode.UnprocessableEntity,
            "part_type_id",
            AdminReferenceErrors.Archived);
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Parts, partId, archivedPart, archive: false));

        // An archived group closes the recipes of its parts and takes no new part.
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Groups, groupId, groupVersion));
        ProductionFactoryDto withoutGroup = await production.GetFactoryAsync(stranger.Account.UserId, stranger.PlanetId, strangerFactory, Token);
        Assert.DoesNotContain(withoutGroup.Recipes, entry => entry.RecipeId == recipeId);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Parts, Part(groupId, "Плита 2", "Plate 2")),
            HttpStatusCode.UnprocessableEntity,
            "part_group_id",
            AdminReferenceErrors.Archived);
    }

    /// <summary>
    /// Twenty parallel creations of every kind of the production get twenty different neutral
    /// codes each.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_creations_get_twenty_different_codes()
    {
        const int parallelCount = 20;

        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();
        (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Деталь", "Part")));

        HttpResponseMessage[] groups = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Groups, Group("Група " + index, "Group " + index, index))));
        HttpResponseMessage[] parts = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Деталь " + index, "Part " + index))));
        HttpResponseMessage[] recipes = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Рецепт " + index, "Recipe " + index, partTypeId: partId))));

        List<int> groupIds = await IdsOfAsync(groups);
        List<int> partIds = await IdsOfAsync(parts);
        List<int> recipeIds = await IdsOfAsync(recipes);

        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
        List<string> groupCodes = await dbContext.PartGroups.AsNoTracking().Where(group => groupIds.Contains(group.Id)).Select(group => group.Code).ToListAsync(Token);
        List<string> partCodes = await dbContext.PartTypes.AsNoTracking().Where(part => partIds.Contains(part.Id)).Select(part => part.Code).ToListAsync(Token);
        List<string> recipeCodes = await dbContext.Recipes.AsNoTracking().Where(recipe => recipeIds.Contains(recipe.Id)).Select(recipe => recipe.Code).ToListAsync(Token);

        Assert.Equal(parallelCount, groupCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(parallelCount, partCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(parallelCount, recipeCodes.Distinct(StringComparer.Ordinal).Count());

        // The new codes come after the codes of the starting data.
        Assert.Empty(groupCodes.Intersect(await dbContext.PartGroups.AsNoTracking().Where(group => !groupIds.Contains(group.Id)).Select(group => group.Code).ToListAsync(Token)));
    }

    /// <summary>
    /// A recipe, a part and a group changed in the admin panel keep their change after a repeated
    /// start of the module.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_does_not_overwrite_a_changed_recipe()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        RecipeEntity first = await dbContext.Recipes.AsNoTracking().Where(recipe => recipe.PartTypeId != null).OrderBy(recipe => recipe.Id).FirstAsync(Token);
        PartGroupEntity group = await dbContext.PartGroups.AsNoTracking().OrderBy(entry => entry.Id).FirstAsync(Token);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
            admin,
            Recipes,
            first.Id,
            first.Version,
            Recipe(ids with { FactoryTypeId = first.BuildingTypeId!.Value }, "Змінений", "Changed", partTypeId: first.PartTypeId, durationSeconds: 4242)));
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Groups, group.Id, group.Version, Group("Змінена", "Changed group", 4242)));

        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);

        JsonElement recipe = await ReferenceAdmin.ReadAsync(admin, Recipes, first.Id);
        Assert.Equal("Changed", recipe.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(4242, recipe.GetProperty("values").GetProperty("duration_seconds").GetInt64());
        Assert.Equal(1, recipe.GetProperty("values").GetProperty("ingredients").GetArrayLength());

        JsonElement changedGroup = await ReferenceAdmin.ReadAsync(admin, Groups, group.Id);
        Assert.Equal("Changed group", changedGroup.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(4242, changedGroup.GetProperty("values").GetProperty("sort_order").GetInt32());
    }

    /// <summary>
    /// The catalog of the production lives in memory for an hour, and a change of the admin
    /// panel drops it after the commit, so the player sees the change at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_change_of_the_admin_panel_reaches_the_player_at_once()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "3600");

        try
        {
            IProductionModule production = services.GetRequiredService<IProductionModule>();
            long factoryId = await ProductionTestData.BuildFactoryAsync(player, services);
            (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Гармата", "Gun")));
            (int recipeId, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Гармата", "Gun", partTypeId: partId)));

            ProductionFactoryDto before = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token);
            Assert.Equal(60, before.Recipes.Single(recipe => recipe.RecipeId == recipeId).DurationSeconds);

            await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, "Гармата", "Gun", partTypeId: partId, durationSeconds: 75)));

            ProductionFactoryDto after = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token);
            Assert.Equal(75, after.Recipes.Single(recipe => recipe.RecipeId == recipeId).DurationSeconds);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        }
    }

    /// <summary>
    /// A technology opens a recipe with its effect «opens»: the recipe is closed with the name of
    /// the technology until the player learns it (task 00023, decision 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_that_opens_a_recipe_closes_the_recipe()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Лазер", "Laser")));
        (int recipeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Лазер", "Laser", partTypeId: partId)));
        (int technologyId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids, recipeId)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        long factoryId = await ProductionTestData.BuildFactoryAsync(player, services);

        IReadOnlyList<RequirementDefinition> requirements = await services.GetRequiredService<IRequirementsModule>()
            .GetAsync(RequirementSubjectTypes.Recipe, recipeId, 1, Token);
        Assert.Contains(requirements, requirement => requirement.RequirementType == RequirementTypes.TechnologyLevel && requirement.TargetId == technologyId);

        ProductionRecipeDto closed = (await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token)).Recipes.Single(recipe => recipe.RecipeId == recipeId);
        Assert.False(closed.IsAvailable);
        ProductionReasonDto reason = Assert.Single(closed.Reasons);
        Assert.Equal(ProductionErrorCodes.RequirementTechnology, reason.Code);
        Assert.Equal(technologyId, reason.TargetId);
        Assert.False(string.IsNullOrEmpty(reason.TargetNameKey));

        ResearchDbContext research = services.GetRequiredService<ResearchDbContext>();
        research.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = player.Account.UserId, TechnologyId = technologyId, Level = 1 });
        await research.SaveChangesAsync(Token);

        ProductionRecipeDto opened = (await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token)).Recipes.Single(recipe => recipe.RecipeId == recipeId);
        Assert.True(opened.IsAvailable);
    }

    /// <summary>
    /// A recipe of a resource gives its output quantity for every unit, a recipe takes parts of
    /// the storage as its ingredients, and a batch that waits for room goes on once the parts
    /// leave the storage, counting its unit from that moment.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_recipe_takes_parts_and_gives_its_output_and_a_blocked_batch_goes_on()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Болт", "Bolt")));
        (int boltRecipeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Болт", "Bolt", partTypeId: partId, durationSeconds: 0)));
        (int scrapRecipeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Recipes,
            Recipe(ids, "Брухт", "Scrap", resourceTypeId: ids.Produced, outputQuantity: 3, ingredients: [new { part_type_id = partId, amount = 1 }])));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        long factoryId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        long capacity = await ProductionTestData.BuildStorageAsync(player, services, 1);

        // The storage has room for one bolt, so the second one waits. A bolt takes the shortest
        // time, a second, so the batch stops a few seconds from now.
        DateTimeOffset startedAt = PlanetsTestData.GameNow(services);
        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, factoryId, boltRecipeId, 2, Token);
        await services.GetRequiredService<ProductionQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, factoryId, startedAt.AddSeconds(5), Token);

        ProductionFactoryDto blocked = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token);
        Assert.True(Assert.Single(blocked.Queue).IsBlocked);
        Assert.Equal(1, blocked.Queue[0].ProducedQuantity);
        Assert.Equal(capacity, blocked.Storage.Stored);

        // The scrap takes a bolt out of the storage, so the blocked batch goes on.
        long producedBefore = await ProductionTestData.BalanceAsync(player, services, ids.Produced);
        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, factoryId, scrapRecipeId, 1, Token);
        Assert.Equal(capacity - 1, (await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token)).Storage.Stored);

        // The woken event runs at the moment the room freed up: the waiting unit is made then,
        // and the scrap starts from that moment and not from the old end of the bolt.
        DateTimeOffset freedAt = startedAt.AddSeconds(10);
        await services.GetRequiredService<ProductionQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, factoryId, freedAt, Token);

        ProductionFactoryDto woken = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token);
        ProductionOrderDto scrap = Assert.Single(woken.Queue);
        Assert.Equal(scrapRecipeId, scrap.RecipeId);
        Assert.Equal(freedAt, scrap.StartsAt);
        Assert.Equal(capacity, woken.Storage.Stored);

        await ProductionTestData.CompleteAsync(player, services, factoryId);

        ProductionFactoryDto done = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token);
        Assert.Empty(done.Queue);
        Assert.Equal(capacity, done.Storage.Stored);
        Assert.Equal(producedBefore + 3, await ProductionTestData.BalanceAsync(player, services, ids.Produced));
    }

    /// <summary>
    /// A cancelled batch returns the cost of the units it has not made yet, the parts among its
    /// ingredients too, and keeps the cost of the units it made.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_cancelled_batch_returns_the_cost_of_the_units_not_made()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Гвинт", "Screw")));
        (int recipeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Recipes,
            Recipe(ids, "Брухт", "Scrap", resourceTypeId: ids.Produced, ingredients: [new { part_type_id = partId, amount = 2 }, new { resource_type_id = ids.Iron, amount = 10 }])));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        long factoryId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        await ProductionTestData.BuildStorageAsync(player, services);

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        dbContext.PartStocks.Add(new PartStockEntity { PlanetId = player.PlanetId, SectorId = 0, PartTypeId = partId, Quantity = 6 });
        PartStorageEntity storage = await dbContext.PartStorages.SingleAsync(entry => entry.PlanetId == player.PlanetId, Token);
        storage.Quantity = 6;
        await dbContext.SaveChangesAsync(Token);

        long ironBefore = await ProductionTestData.BalanceAsync(player, services, ids.Iron);
        long producedBefore = await ProductionTestData.BalanceAsync(player, services, ids.Produced);
        ProductionOrderDto order = (await production.EnqueueAsync(player.Account.UserId, player.PlanetId, factoryId, recipeId, 3, Token)).Queue[0];
        Assert.Equal(0, (await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token)).Storage.Stored);

        // One unit of three is made, so two thirds of the cost come back.
        await services.GetRequiredService<ProductionQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, factoryId, order.NextUnitAt!.Value, Token);
        await production.CancelAsync(player.Account.UserId, player.PlanetId, factoryId, order.OrderId, Token);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        Assert.Equal(4, stock.Stacks.Where(stack => stack.PartTypeId == partId).Sum(stack => stack.Quantity));
        Assert.Equal(4, stock.Storage.Stored);

        // The unit made cost ten iron and gave its product; the two units not made cost nothing.
        Assert.Equal(ironBefore - 10, await ProductionTestData.BalanceAsync(player, services, ids.Iron));
        Assert.Equal(producedBefore + 1, await ProductionTestData.BalanceAsync(player, services, ids.Produced));
    }

    /// <summary>
    /// A picture uploaded in the form of a recipe is kept by its key and shown to the player
    /// with the recipe; a recipe without a picture has none, so the client shows the stub.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_picture_of_a_recipe_reaches_the_player()
    {
        await ResetAsync();
        string folder = Path.Combine(Path.GetTempPath(), "aetheris-production-images-" + Guid.NewGuid().ToString("N"));

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ReferenceImageKeys.Directory, folder, reload: false);
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ReferenceImageKeys.MaxBytes, "2048");
        }

        try
        {
            using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
            using HttpClient admin = await OwnerAsync();
            Ids ids = await IdsAsync();

            byte[] png = [.. Convert.FromHexString("89504E470D0A1A0A"), .. new byte[64]];
            using ByteArrayContent body = new(png);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            HttpResponseMessage uploaded = await admin.PostAsync(AdminEndpoints.Prefix + ReferenceImageEndpoints.UploadRoute, body, Token);
            string uploadBody = await uploaded.Content.ReadAsStringAsync(Token);
            Assert.True(uploaded.StatusCode == HttpStatusCode.OK, uploadBody);
            string imageKey = JsonDocument.Parse(uploadBody).RootElement.GetProperty("image_key").GetString()!;

            (int partId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Parts, Part(ids.GroupId, "Реактор", "Reactor", imageKey: imageKey)));
            (int recipeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Реактор", "Reactor", partTypeId: partId, imageKey: imageKey)));
            (int plainId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Реактор простий", "Plain reactor", partTypeId: partId)));
            Assert.Equal(imageKey, (await ReferenceAdmin.ReadAsync(admin, Parts, partId)).GetProperty("image_key").GetString());

            await ReferenceAdmin.AssertFieldAsync(
                await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, "Реактор", "Reactor", partTypeId: partId, imageKey: new string('c', 32) + ".png")),
                HttpStatusCode.BadRequest,
                AdminReferenceRules.ImageField,
                AdminReferenceErrors.Unknown);

            using IServiceScope scope = _factory.Services.CreateScope();
            long factoryId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
            ProductionFactoryDto factory = await scope.ServiceProvider.GetRequiredService<IProductionModule>()
                .GetFactoryAsync(player.Account.UserId, player.PlanetId, factoryId, Token);

            Assert.Equal(imageKey, factory.Recipes.Single(recipe => recipe.RecipeId == recipeId).ImageKey);
            Assert.Null(factory.Recipes.Single(recipe => recipe.RecipeId == plainId).ImageKey);
        }
        finally
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            await AetherisTestFactory.ResetDatabaseAsync(scope.ServiceProvider);

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static object Group(string ukrainian, string english, int sortOrder) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        sort_order = sortOrder,
    };

    private static object Part(int groupId, string ukrainian, string english, object[]? attributes = null, string? imageKey = null) => new
    {
        image_key = imageKey,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        part_group_id = groupId,
        mark_level = PartMarkLevels.First,
        sort_order = 99,
        attributes = attributes ?? [],
    };

    private static object Recipe(
        Ids ids,
        string ukrainian,
        string english,
        int? partTypeId = null,
        int? resourceTypeId = null,
        long outputQuantity = 1,
        long durationSeconds = 60,
        object[]? ingredients = null,
        object[]? requirements = null,
        string? imageKey = null) => new
        {
            image_key = imageKey,
            name = ReferenceAdmin.Texts(ukrainian, english),
            description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
            building_type_id = ids.FactoryTypeId,
            part_type_id = partTypeId,
            resource_type_id = resourceTypeId,
            output_quantity = outputQuantity,
            duration_seconds = durationSeconds,
            sort_order = 99,
            ingredients = ingredients ?? [new { resource_type_id = ids.Iron, amount = 5 }],
            requirements = requirements ?? [],
        };

    private static object Technology(Ids ids, int recipeId) => new
    {
        name = ReferenceAdmin.Texts("Оптика", "Optics"),
        description = ReferenceAdmin.Texts("Оптика опис", "Optics description"),
        branch_id = ids.BranchId,
        sort_order = 99,
        research_center_level = 1,
        levels = new[]
        {
            new
            {
                level = 1,
                duration_seconds = 60,
                costs = new[] { new { resource_type_id = ids.Iron, amount = 10 } },
                effects = new[] { new { effect_type = TechnologyEffectTypes.Unlocks, unlock_kind = TechnologyUnlockKinds.Recipe, unlock_target_id = recipeId, value = 1 } },
                requirements = Array.Empty<object>(),
            },
        },
    };

    private static async Task<List<int>> IdsOfAsync(HttpResponseMessage[] responses)
    {
        List<int> ids = [];

        foreach (HttpResponseMessage response in responses)
        {
            ids.Add((await ReferenceAdmin.ChangedAsync(response)).Id);
        }

        return ids;
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ResearchConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<Aetheris.Research.IResearchModule>().EnsureAsync(Token);
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private async Task<Ids> IdsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        int factoryTypeId = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>().FindBuildingTypeIdAsync("building_13", Token)
            ?? throw new InvalidOperationException("The factory type does not exist.");
        int groupId = await services.GetRequiredService<ProductionDbContext>().PartGroups.AsNoTracking()
            .OrderBy(group => group.SortOrder).Select(group => group.Id).FirstAsync(Token);
        int branchId = await services.GetRequiredService<ResearchDbContext>().TechnologyBranches.AsNoTracking()
            .OrderBy(branch => branch.SortOrder).Select(branch => branch.Id).FirstAsync(Token);

        // A produced resource has no storage building, so its units always find room.
        int producedId = (await ProductionTestData.FirstRecipeAsync(services)).ResourceTypeId
            ?? throw new InvalidOperationException("The catalog has no recipe of a resource.");

        return new Ids(await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron), producedId, factoryTypeId, groupId, branchId);
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

    /// <summary>The rows the values of the tests name.</summary>
    /// <param name="Iron">The first metal.</param>
    /// <param name="Produced">A produced resource, the product of the new recipes of a resource.</param>
    /// <param name="FactoryTypeId">The type of the factory of the new recipes.</param>
    /// <param name="GroupId">The group of the new parts.</param>
    /// <param name="BranchId">The branch of the new technologies.</param>
    private sealed record Ids(int Iron, int Produced, int FactoryTypeId, int GroupId, int BranchId);
}
