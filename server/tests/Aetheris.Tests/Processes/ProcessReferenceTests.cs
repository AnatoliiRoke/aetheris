using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Tests of the kinds, the recipes and the parameters of the processes in the admin panel, and
/// of the way a change there reaches the players (task 00014, the requirements of task 00023).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProcessReferenceTests
{
    private const string Types = "/processes/process-types";

    private const string Recipes = "/processes/process-recipes";

    private const string Params = "/processes/process-params";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProcessReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A kind, a recipe and a parameter are created and changed in the admin panel: the server
    /// gives the neutral codes, the texts live in the table of the texts, and the journal keeps
    /// the old and the new values with the reason.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_kind_a_recipe_and_a_parameter_are_created_and_changed_from_the_admin_panel()
    {
        await ProcessesTestData.ResetAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        Ids ids = await IdsAsync();

        (int typeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Types, Type("Перетворення", "Conversion")));
        (int recipeId, int recipeVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, typeId, "Пальне", "Fuel")));
        (int paramId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Params, Param(typeId, ProcessLevelSources.Technology, ids.TechnologyId)));

        JsonElement type = await ReferenceAdmin.ReadAsync(admin, Types, typeId);
        JsonElement recipe = await ReferenceAdmin.ReadAsync(admin, Recipes, recipeId);
        JsonElement parameter = await ReferenceAdmin.ReadAsync(admin, Params, paramId);
        Assert.Matches("^process_type_[0-9]+$", type.GetProperty("code").GetString());
        Assert.Matches("^process_recipe_[0-9]+$", recipe.GetProperty("code").GetString());
        Assert.Matches("^process_param_[0-9]+$", parameter.GetProperty("code").GetString());
        Assert.Equal("Fuel", recipe.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(1, recipe.GetProperty("values").GetProperty("ingredients").GetArrayLength());
        Assert.Equal(ids.TechnologyId, parameter.GetProperty("values").GetProperty("level_target_id").GetInt32());

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, recipeVersion, Recipe(ids, typeId, "Пальне довге", "Long fuel", durationSeconds: 90)));
        Assert.Equal(recipeVersion + 1, changed);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        AdminAuditLogEntity audit = await services.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == owner.AdminId && record.Action == ProcessReferenceAdminService.ProcessRecipeDescriptor.Permissions.Updated)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);

        Assert.Equal(AdminReferenceEndpoints.Target(ProcessReferenceAdminService.ProcessRecipeKind, recipeId), audit.Target);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);
        Assert.Contains("\"Fuel\"", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("ingredients", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("Long fuel", audit.NewValue, StringComparison.Ordinal);

        ProcessRecipeEntity stored = await services.GetRequiredService<ProcessesDbContext>().ProcessRecipes.AsNoTracking().SingleAsync(entry => entry.Id == recipeId, Token);
        Assert.Equal(90, stored.DurationSeconds);
    }

    /// <summary>
    /// A change without a reason, from an old version, of a row that does not exist or with
    /// wrong values is refused with the codes of the shared shell (task 00023: 400, 404, 409).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_wrong_change_of_the_processes_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int typeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Types, Type("Вид", "Kind")));
        (int recipeId, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, typeId, "Рецепт", "Recipe")));

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, typeId, "Рецепт 2", "Recipe 2"), reason: null),
            HttpStatusCode.BadRequest,
            "reason",
            AdminReason.Required);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, typeId, "Рецепт 2", "Recipe 2")));
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Recipes, recipeId, version, Recipe(ids, typeId, "Рецепт 3", "Recipe 3"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(AdminEndpoints.Prefix + Recipes + "/999999", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReferenceAdmin.ArchiveAsync(admin, Params, 999999, 1)).StatusCode);

        // A handler is a logical code the program knows.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Types, Type("Вид", "Kind", handlerCode: "handler_unknown")),
            HttpStatusCode.BadRequest,
            "handler_code",
            AdminReferenceErrors.Unknown);

        // A part an ingredient takes must exist.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, typeId, "Рецепт", "Recipe", partTypeId: 999999)),
            HttpStatusCode.BadRequest,
            "ingredients.0.part_type_id",
            AdminReferenceErrors.Unknown);

        // A parameter of a technology names the technology; one of the building names nothing.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Params, Param(typeId, ProcessLevelSources.Technology, null)),
            HttpStatusCode.BadRequest,
            "level_target_id",
            AdminReferenceErrors.Required);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Params, Param(typeId, ProcessLevelSources.Technology, 999999)),
            HttpStatusCode.BadRequest,
            "level_target_id",
            AdminReferenceErrors.Unknown);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Params, Param(typeId, ProcessLevelSources.Building, ids.TechnologyId)),
            HttpStatusCode.BadRequest,
            "level_target_id",
            AdminReferenceErrors.NotApplicable);

        // A range has both of its values, the highest not below the lowest.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Params, Param(typeId, ProcessLevelSources.Part, null, kind: ProcessParamKinds.Range)),
            HttpStatusCode.BadRequest,
            "value_max",
            AdminReferenceErrors.Required);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Params, Param(typeId, ProcessLevelSources.Part, null, kind: ProcessParamKinds.Range, value: 1.9m, valueMax: 1.4m)),
            HttpStatusCode.BadRequest,
            "value_max",
            AdminReferenceErrors.OutOfBounds);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Recipes, recipeId);
        Assert.Equal("Recipe 2", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
    }

    /// <summary>
    /// Twenty parallel creations of a recipe get twenty different neutral codes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_creations_get_twenty_different_codes()
    {
        const int parallelCount = 20;

        await ProcessesTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();
        (int typeId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Types, Type("Вид", "Kind")));

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Recipes, Recipe(ids, typeId, "Рецепт " + index, "Recipe " + index))));

        List<int> recipeIds = [];

        foreach (HttpResponseMessage response in responses)
        {
            recipeIds.Add((await ReferenceAdmin.ChangedAsync(response)).Id);
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        ProcessesDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProcessesDbContext>();
        List<string> codes = await dbContext.ProcessRecipes.AsNoTracking().Where(recipe => recipeIds.Contains(recipe.Id)).Select(recipe => recipe.Code).ToListAsync(Token);

        Assert.Equal(parallelCount, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(codes.Intersect(await dbContext.ProcessRecipes.AsNoTracking().Where(recipe => !recipeIds.Contains(recipe.Id)).Select(recipe => recipe.Code).ToListAsync(Token)));
    }

    /// <summary>
    /// A recipe changed in the admin panel keeps its change after a repeated start of the module,
    /// and the repeated start writes no second copy of the starting data.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_does_not_overwrite_a_changed_recipe()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProcessesDbContext dbContext = services.GetRequiredService<ProcessesDbContext>();
        ProcessRecipeEntity first = await dbContext.ProcessRecipes.AsNoTracking().OrderBy(recipe => recipe.Id).FirstAsync(Token);
        int recipes = await dbContext.ProcessRecipes.CountAsync(Token);
        int parameters = await dbContext.ProcessParams.CountAsync(Token);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
            admin,
            Recipes,
            first.Id,
            first.Version,
            Recipe(ids with { BuildingTypeId = first.BuildingTypeId!.Value }, first.ProcessTypeId, "Змінений", "Changed", durationSeconds: 4242)));

        ProcessesBootstrapResult again = await services.GetRequiredService<IProcessesModule>().EnsureAsync(Token);

        Assert.False(again.Seeded);
        Assert.Equal(recipes, await dbContext.ProcessRecipes.CountAsync(Token));
        Assert.Equal(parameters, await dbContext.ProcessParams.CountAsync(Token));

        JsonElement recipe = await ReferenceAdmin.ReadAsync(admin, Recipes, first.Id);
        Assert.Equal("Changed", recipe.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(4242, recipe.GetProperty("values").GetProperty("duration_seconds").GetInt64());
    }

    /// <summary>
    /// The catalog of the processes lives in memory for an hour, and a change of the admin
    /// panel drops it after the commit, so the player sees the change at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_change_of_the_admin_panel_reaches_the_player_at_once()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.CatalogCacheSeconds, "3600");

        try
        {
            IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
            long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
            ProcessRecipeEntity recipe = await services.GetRequiredService<ProcessesDbContext>().ProcessRecipes.AsNoTracking()
                .Where(entry => entry.BuildingTypeId == ids.BuildingTypeId)
                .OrderBy(entry => entry.Id)
                .FirstAsync(Token);

            ProcessBuildingDto before = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);
            Assert.Equal(recipe.DurationSeconds, before.Recipes.Single(entry => entry.RecipeId == recipe.Id).DurationSeconds);

            await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(
                admin,
                Recipes,
                recipe.Id,
                recipe.Version,
                Recipe(ids, recipe.ProcessTypeId, "Швидкий", "Quick", durationSeconds: 75, sourceGroupId: recipe.SourceGroupId, targetGroupId: recipe.TargetGroupId, withIngredient: false)));

            ProcessBuildingDto after = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);
            Assert.Equal(75, after.Recipes.Single(entry => entry.RecipeId == recipe.Id).DurationSeconds);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.CatalogCacheSeconds, "0");
        }
    }

    private static object Type(string ukrainian, string english, string handlerCode = ProcessHandlerCodes.MatterConversion) => new
    {
        image_key = (string?)null,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + ": опис", english + ": description"),
        handler_code = handlerCode,
        refunds_input = true,
        sort_order = 10,
    };

    private static object Recipe(
        Ids ids,
        int processTypeId,
        string ukrainian,
        string english,
        long durationSeconds = 60,
        int? partTypeId = null,
        int? sourceGroupId = null,
        int? targetGroupId = null,
        bool withIngredient = true) => new
        {
            image_key = (string?)null,
            name = ReferenceAdmin.Texts(ukrainian, english),
            description = ReferenceAdmin.Texts(ukrainian + ": опис", english + ": description"),
            process_type_id = processTypeId,
            building_type_id = ids.BuildingTypeId,
            source_group_id = sourceGroupId ?? ids.FuelGroupId,
            target_group_id = targetGroupId ?? ids.FuelGroupId,
            duration_seconds = durationSeconds,
            ship_stub = (string?)null,
            sort_order = 10,
            ingredients = withIngredient
                ? partTypeId is null
                    ? new object[] { new { resource_type_id = (int?)ids.ResourceId, part_type_id = (int?)null, amount = 1 } }
                    : [new { resource_type_id = (int?)null, part_type_id = partTypeId, amount = 1 }]
                : [],
            requirements = Array.Empty<object>(),
        };

    private static object Param(
        int processTypeId,
        string levelSource,
        int? levelTargetId,
        string kind = ProcessParamKinds.ValueByLevel,
        decimal value = 0.5m,
        decimal? valueMax = null) => new
        {
            name = ReferenceAdmin.Texts("Параметр", "Parameter"),
            description = ReferenceAdmin.Texts("Параметр: опис", "Parameter: description"),
            process_type_id = processTypeId,
            param_kind = kind,
            level_source = levelSource,
            level_target_id = levelTargetId,
            level = 1,
            value,
            value_max = valueMax,
            sort_order = 10,
        };

    private async Task<Ids> IdsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        int buildingTypeId = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>().FindBuildingTypeIdByClassAsync(BuildingClasses.Converter, Token)
            ?? throw new InvalidOperationException("The converter does not exist.");
        int technologyId = (await services.GetRequiredService<ITechnologyLevels>().GetTechnologyIdsByCodeAsync(Token))["tech_18"];
        int fuelGroupId = await services.GetRequiredService<Aetheris.World.Data.WorldDbContext>().ResourceGroups.AsNoTracking()
            .Where(group => group.Code == "resource_group_3")
            .Select(group => group.Id)
            .SingleAsync(Token);
        int resourceId = await ProcessesTestData.ResourceIdAsync(services, "resource_13");

        return new Ids(buildingTypeId, technologyId, fuelGroupId, resourceId);
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

    /// <summary>
    /// The rows of the other modules a form of the processes names.
    /// </summary>
    /// <param name="BuildingTypeId">The converter.</param>
    /// <param name="TechnologyId">The technology of the conversion.</param>
    /// <param name="FuelGroupId">The group of the fuel.</param>
    /// <param name="ResourceId">A resource an ingredient takes.</param>
    private sealed record Ids(int BuildingTypeId, int TechnologyId, int FuelGroupId, int ResourceId);
}
