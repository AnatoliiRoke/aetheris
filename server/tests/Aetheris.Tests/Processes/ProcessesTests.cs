using System.Net;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Scheduling;
using Aetheris.Planets.Data;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Api;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Events;
using Aetheris.Processes.Services;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Research.Data;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Tests of the processes: the conversion of matter, the recycling, the repair and the
/// replacement, the queue of a building, its events and its owner (task 00014). The
/// terraforming is no process of a queue; its tests are in <see cref="TerraformingTests"/>.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class ProcessesTests
{
    private const string Fuel = "resource_group_3";

    private const string Deuterium = "resource_12";

    private const string Helium = "resource_13";

    private const string Quartzite = "resource_8";

    private const string BuildingMaterials = "resource_20";

    private const string ConversionTechnology = "tech_18";

    private const string Hull = "part_1";

    private const string Engine = "part_4";

    private const string LifeSupport = "part_10";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessesTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProcessesTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The kinds of the processes, their recipes and their parameters are data, and a recipe
    /// that is not available says why it is not (task 00014, criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_processes_are_data_and_the_closed_ones_say_why()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Recycling);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessBuildingDto building = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);

        ProcessRecipeDto recipe = Assert.Single(building.Recipes);
        Assert.Equal(ProcessPartSubjects.Any, recipe.PartSubject);
        Assert.False(recipe.NeedsShip);

        // A requirement the player does not meet closes the recipe with its reason.
        ProcessRecipeInfo info = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.Recycling);
        int buildingTypeId = (await PlanetsTestData.TypeAsync(scope.ServiceProvider, "building_3")).Id;
        await scope.ServiceProvider.GetRequiredService<Aetheris.Requirements.IRequirementsModule>().AddAsync(
            Aetheris.Requirements.Services.RequirementSubjectTypes.ProcessRecipe,
            info.Id,
            1,
            [new Aetheris.Requirements.Services.RequirementDefinition(Aetheris.Requirements.Services.RequirementTypes.BuildingLevel, buildingTypeId, 50)],
            Token);
        scope.ServiceProvider.GetRequiredService<ProcessCatalogCache>().Invalidate();

        ProcessRecipeDto closed = Assert.Single((await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token)).Recipes);
        Assert.False(closed.IsAvailable);
        ProcessReasonDto reason = Assert.Single(closed.Reasons);
        Assert.Equal(ProcessErrorCodes.RequirementBuildingLevel, reason.Code);
        Assert.Equal(50, reason.TargetLevel);
        Assert.NotNull(reason.TargetNameKey);
    }

    /// <summary>
    /// The conversion takes the source of the whole batch at once and gives the source times the
    /// percentage of the level of the technology; a higher level gives more (task 00014,
    /// criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_conversion_gives_the_percentage_of_the_level_of_the_technology()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        int sourceId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int targetId = await ProcessesTestData.ResourceIdAsync(services, Helium);

        long sourceBefore = await ProcessesTestData.BalanceAsync(player, services, sourceId);
        long targetBefore = await ProcessesTestData.BalanceAsync(player, services, targetId);

        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(10, sourceId, targetId), Token);

        // The source of the whole batch is paid when the order enters the queue.
        Assert.Equal(sourceBefore - 10, await ProcessesTestData.BalanceAsync(player, services, sourceId));
        Assert.Equal(targetBefore, await ProcessesTestData.BalanceAsync(player, services, targetId));

        Assert.Equal(10, await ProcessesTestData.CompleteAsync(player, services, buildingId));

        // The first level keeps 30 per cent of the source.
        Assert.Equal(targetBefore + 3, await ProcessesTestData.BalanceAsync(player, services, targetId));

        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 10);
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(10, sourceId, targetId), Token);
        await ProcessesTestData.CompleteAsync(player, services, buildingId);

        // The highest level keeps 80 per cent of the source.
        Assert.Equal(targetBefore + 3 + 8, await ProcessesTestData.BalanceAsync(player, services, targetId));
        Assert.Equal(sourceBefore - 20, await ProcessesTestData.BalanceAsync(player, services, sourceId));

        // The level of the converter only makes a unit quicker, by the multiplier of its level.
        ProcessParamInfo factor = (await ProcessesTestData.CatalogAsync(services)).ParamAt(recipe.ProcessTypeId, ProcessParamKinds.DurationFactor, ProcessLevelSources.Building, null, 10)!;
        await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter, 10);
        ProcessBuildingDto quick = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(10, sourceId, targetId), Token);
        ProcessOrderDto order = quick.Queue[0];

        Assert.True(factor.Value < 1m);
        Assert.Equal((long)decimal.Ceiling(recipe.DurationSeconds * factor.Value), (long)(order.NextUnitAt!.Value - order.StartsAt!.Value).TotalSeconds);
        await ProcessesTestData.CompleteAsync(player, services, buildingId);
        Assert.Equal(targetBefore + 3 + 8 + 8, await ProcessesTestData.BalanceAsync(player, services, targetId));
    }

    /// <summary>
    /// The conversion refuses a source of another group, a produced good, a target
    /// of another group, the same resource and a player without the technology, and nothing is
    /// spent (task 00014, criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_conversion_refuses_what_it_may_not_convert()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        int deuterium = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int plasma = await ProcessesTestData.ResourceIdAsync(services, Helium);
        int quartzite = await ProcessesTestData.ResourceIdAsync(services, Quartzite);
        int produced = await ProcessesTestData.ResourceIdAsync(services, BuildingMaterials);

        // Without the technology the conversion would burn the source, so it is refused.
        Assert.Equal(ProcessErrorCodes.RequirementTechnology, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(10, deuterium, plasma)));

        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);
        long before = await ProcessesTestData.BalanceAsync(player, services, deuterium);

        Assert.Equal(ProcessErrorCodes.SourceGroupMismatch, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(10, quartzite, plasma)));
        Assert.Equal(ProcessErrorCodes.SourceGroupMismatch, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(10, produced, plasma)));
        Assert.Equal(ProcessErrorCodes.TargetGroupMismatch, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(10, deuterium, quartzite)));
        Assert.Equal(ProcessErrorCodes.TargetGroupMismatch, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(10, deuterium, produced)));
        Assert.Equal(ProcessErrorCodes.SameSourceAndTarget, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(10, deuterium, deuterium)));
        Assert.Equal(ProcessErrorCodes.QuantityInvalid, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(0, deuterium, plasma)));
        Assert.Equal(ProcessErrorCodes.QuantityInvalid, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(long.MaxValue, deuterium, plasma)));

        Assert.Equal(before, await ProcessesTestData.BalanceAsync(player, services, deuterium));
        Assert.Empty(await ProcessesTestData.OrdersAsync(player, services, buildingId));
    }

    /// <summary>
    /// Cancelling an order returns the input of the units it has not made in full, and the
    /// units made keep theirs (task 00014, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Cancelling_returns_the_input_of_the_units_not_made()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 10);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        int sourceId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int targetId = await ProcessesTestData.ResourceIdAsync(services, Helium);
        long before = await ProcessesTestData.BalanceAsync(player, services, sourceId);

        // A whole order that is cancelled at once returns everything.
        ProcessBuildingDto first = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(10, sourceId, targetId), Token);
        await processes.CancelAsync(player.Account.UserId, player.PlanetId, buildingId, first.Queue[0].OrderId, Token);
        Assert.Equal(before, await ProcessesTestData.BalanceAsync(player, services, sourceId));

        // An order that made four units of ten returns the source of the six left.
        ProcessBuildingDto second = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(10, sourceId, targetId), Token);
        ProcessOrderDto order = second.Queue[0];
        long unitSeconds = (long)(order.NextUnitAt!.Value - order.StartsAt!.Value).TotalSeconds;
        await services.GetRequiredService<ProcessQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, order.StartsAt.Value.AddSeconds(unitSeconds * 4), Token);

        ProcessOrderEntity made = Assert.Single(await ProcessesTestData.OrdersAsync(player, services, buildingId));
        Assert.Equal(4, made.ProducedQuantity);

        await processes.CancelAsync(player.Account.UserId, player.PlanetId, buildingId, order.OrderId, Token);

        Assert.Equal(before - 4, await ProcessesTestData.BalanceAsync(player, services, sourceId));
        Assert.Empty(await ProcessesTestData.OrdersAsync(player, services, buildingId));
    }

    /// <summary>
    /// The recycling returns the share of the cost of a part by its recipe, the share grows with
    /// the level of the factory and a damaged part gives in proportion to its condition (task
    /// 00014, criteria 3 and 6a).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_recycling_returns_the_share_of_the_recipe_by_the_level_and_the_condition()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);

        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);
        await ProcessesTestData.PutPartsAsync(player, services, hull.PartTypeId, PartCondition.Full, 4);
        await ProcessesTestData.PutPartsAsync(player, services, hull.PartTypeId, PartCondition.Full / 2, 2);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.Recycling);
        Dictionary<int, long> before = await BalancesAsync(player, services, hull.ResourceCost.Keys);

        // Two new hulls at the first level of the factory give 40 per cent of their recipe.
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(2, PartTypeId: hull.PartTypeId), Token);
        Assert.Equal(2, await ProcessesTestData.PartsAsync(player, services, hull.PartTypeId, PartCondition.Full));
        await ProcessesTestData.CompleteAsync(player, services, buildingId);

        Dictionary<int, long> afterNew = await BalancesAsync(player, services, hull.ResourceCost.Keys);
        Assert.All(hull.ResourceCost, cost => Assert.Equal(before[cost.Key] + (long)decimal.Floor(2 * cost.Value * 0.40m), afterNew[cost.Key]));

        // Two hulls of half a condition give a half of it.
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(2, PartTypeId: hull.PartTypeId, PartConditionPercent: PartCondition.Full / 2), Token);
        await ProcessesTestData.CompleteAsync(player, services, buildingId);

        Dictionary<int, long> afterDamaged = await BalancesAsync(player, services, hull.ResourceCost.Keys);
        Assert.All(hull.ResourceCost, cost => Assert.Equal(afterNew[cost.Key] + (long)decimal.Floor(2 * cost.Value * 0.40m * 0.5m), afterDamaged[cost.Key]));
        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, hull.PartTypeId, PartCondition.Full / 2));

        // The tenth level of the factory gives 50 per cent.
        await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling, 10);
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(2, PartTypeId: hull.PartTypeId), Token);
        await ProcessesTestData.CompleteAsync(player, services, buildingId);

        Dictionary<int, long> afterLevel = await BalancesAsync(player, services, hull.ResourceCost.Keys);
        Assert.All(hull.ResourceCost, cost => Assert.Equal(afterDamaged[cost.Key] + (long)decimal.Floor(2 * cost.Value * 0.50m), afterLevel[cost.Key]));
        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, hull.PartTypeId, PartCondition.Full));

        // A part the storage does not hold is refused and nothing is spent.
        Assert.Equal(
            ProcessErrorCodes.PartMissing,
            await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(1, PartTypeId: hull.PartTypeId)));
    }

    /// <summary>
    /// A part has no crew, so taking it apart returns none: only the resources of the recipe
    /// come back, even when the parts carry the attribute of the crew and the batch is larger
    /// than the limit of the population of the body, and the module keeps no state of a returned
    /// crew (task 00014, section 11).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_recycling_of_a_part_returns_no_crew()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);

        // The life support is the part that carries the attribute of the crew.
        PartFacts lifeSupport = await ProcessesTestData.WithAttributeAsync(services, LifeSupport, PartAttributeKeys.Crew, 1m);
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();
        Assert.True(await production.PartAttributes.AsNoTracking().AnyAsync(
            attribute => attribute.PartTypeId == lifeSupport.PartTypeId && attribute.AttributeKey == PartAttributeKeys.Crew && attribute.NumberValue > 0,
            Token));

        long limit = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>()
            .GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.PopulationMax, Token);
        long quantity = limit + 1;
        await ProcessesTestData.PutPartsAsync(player, services, lifeSupport.PartTypeId, PartCondition.Full, quantity);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.Recycling);
        Dictionary<int, long> before = await BalancesAsync(player, services, lifeSupport.ResourceCost.Keys);

        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(quantity, PartTypeId: lifeSupport.PartTypeId), Token);
        Assert.Equal(quantity, await ProcessesTestData.CompleteAsync(player, services, buildingId));

        // The resources come back by the share of the recipe and nothing else comes back: the
        // module keeps no table of a returned crew any more.
        Dictionary<int, long> after = await BalancesAsync(player, services, lifeSupport.ResourceCost.Keys);
        Assert.All(lifeSupport.ResourceCost, cost => Assert.Equal(before[cost.Key] + (long)decimal.Floor(quantity * cost.Value * 0.40m), after[cost.Key]));
        Assert.DoesNotContain(
            services.GetRequiredService<ProcessesDbContext>().Model.GetEntityTypes(),
            entityType => entityType.GetTableName()?.Contains("population", StringComparison.Ordinal) == true);
        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, lifeSupport.PartTypeId, PartCondition.Full));
    }

    /// <summary>
    /// A part that is made of other parts is taken apart alone: only the resources of its own
    /// recipe come back, and the parts that went into it stay in the storage as items of their
    /// own, neither taken nor added (task 00014, section 11).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_composite_part_is_taken_apart_alone_and_the_nested_parts_stay()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);

        // The engine is made of two hulls besides its own resources.
        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);
        int engineId = (await ProcessesTestData.PartAsync(services, Engine)).PartTypeId;
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();
        int recipeId = await production.Recipes.AsNoTracking().Where(entry => entry.PartTypeId == engineId).Select(entry => entry.Id).SingleAsync(Token);
        production.RecipeIngredients.Add(new RecipeIngredientEntity { RecipeId = recipeId, PartTypeId = hull.PartTypeId, Amount = 2 });
        await production.SaveChangesAsync(Token);

        PartFacts engine = await ProcessesTestData.PartAsync(services, Engine);
        await ProcessesTestData.PutPartsAsync(player, services, engine.PartTypeId, PartCondition.Full, 1);
        await ProcessesTestData.PutPartsAsync(player, services, hull.PartTypeId, PartCondition.Full, 3);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.Recycling);
        HashSet<int> resourceIds = [.. engine.ResourceCost.Keys, .. hull.ResourceCost.Keys];
        Dictionary<int, long> before = await BalancesAsync(player, services, resourceIds);

        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(1, PartTypeId: engine.PartTypeId), Token);
        await ProcessesTestData.CompleteAsync(player, services, buildingId);
        Dictionary<int, long> after = await BalancesAsync(player, services, resourceIds);

        // Only the engine went into the recycling, and only its own resources came back.
        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, PartCondition.Full));
        Assert.Equal(3, await ProcessesTestData.PartsAsync(player, services, hull.PartTypeId, PartCondition.Full));
        Assert.Single(await production.PartStocks.AsNoTracking().Where(stack => stack.PlanetId == player.PlanetId && stack.PartTypeId == hull.PartTypeId).ToListAsync(Token));
        Assert.All(engine.ResourceCost, cost => Assert.Equal(before[cost.Key] + (long)decimal.Floor(cost.Value * 0.40m), after[cost.Key]));
        Assert.All(hull.ResourceCost.Keys.Except(engine.ResourceCost.Keys), resourceId => Assert.Equal(before[resourceId], after[resourceId]));
    }

    /// <summary>
    /// An archived parameter stops working at once for the new orders, while an order that is
    /// already in the queue is finished with the values it was made with (task 00014, section
    /// 11); the catalog lives in memory for an hour, yet the archive reaches the next order.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_archived_parameter_changes_no_order_in_the_queue_and_stops_the_new_ones()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, Aetheris.Processes.Configuration.ProcessesConfigKeys.CatalogCacheSeconds, "3600");

        try
        {
            long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
            await ProcessesTestData.GiveEveryResourceAsync(player, services);
            int technologyId = await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);

            IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
            ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
            int sourceId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
            int targetId = await ProcessesTestData.ResourceIdAsync(services, Helium);
            ProcessOrderRequest request = new(10, sourceId, targetId);
            long targetBefore = await ProcessesTestData.BalanceAsync(player, services, targetId);

            // The order is made while the first step of the technology holds: 30 per cent.
            ProcessCatalog catalog = await ProcessesTestData.CatalogAsync(services);
            ProcessParamInfo step = catalog.ParamAt(recipe.ProcessTypeId, ProcessParamKinds.ValueByLevel, ProcessLevelSources.Technology, technologyId, 1)!;
            await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, request, Token);

            // The step is archived at once: the next order of the same kind is refused.
            ProcessReferenceAdminService admin = services.GetRequiredService<ProcessReferenceAdminService>();
            ProcessParamEntity stored = await services.GetRequiredService<ProcessesDbContext>().ProcessParams.AsNoTracking().SingleAsync(entry => entry.Id == step.Id, Token);
            await admin.SetArchivedAsync(ProcessReferenceAdminService.ProcessParamKind, step.Id, true, new Aetheris.Core.Admin.AdminReferenceInput(stored.Version, "test", EmptyValues()), Token);

            Assert.Equal(ProcessErrorCodes.RequirementTechnology, await RefusedAsync(processes, player, buildingId, recipe.Id, request));
            Assert.Single(await ProcessesTestData.OrdersAsync(player, services, buildingId));

            // The order in the queue is finished with the values it was made with: 30 per cent
            // of the ten units of the source, though the step is archived now.
            Assert.Equal(10, await ProcessesTestData.CompleteAsync(player, services, buildingId));
            Assert.Equal(targetBefore + 3, await ProcessesTestData.BalanceAsync(player, services, targetId));
            Assert.Empty(await ProcessesTestData.OrdersAsync(player, services, buildingId));
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, Aetheris.Processes.Configuration.ProcessesConfigKeys.CatalogCacheSeconds, "0");
        }
    }

    /// <summary>
    /// An order goes by the real levels it was made at: a level of the technology or of the
    /// building that is reached while the order waits in the queue does not change what it gives
    /// (task 00014, section 11).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_order_keeps_the_real_levels_it_was_made_at()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long converterId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        long recyclerId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo conversion = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        int sourceId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int targetId = await ProcessesTestData.ResourceIdAsync(services, Helium);
        long targetBefore = await ProcessesTestData.BalanceAsync(player, services, targetId);

        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);
        await ProcessesTestData.PutPartsAsync(player, services, hull.PartTypeId, PartCondition.Full, 2);
        ProcessRecipeInfo recycling = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.Recycling);
        Dictionary<int, long> hullBefore = await BalancesAsync(player, services, hull.ResourceCost.Keys);

        // Both orders are made at the first levels: the technology at 1 and the factory at 1.
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, converterId, conversion.Id, new ProcessOrderRequest(10, sourceId, targetId), Token);
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, recyclerId, recycling.Id, new ProcessOrderRequest(2, PartTypeId: hull.PartTypeId), Token);

        // The levels rise while the orders wait; the orders keep the levels they were made at.
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 10);
        await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling, 10);

        await ProcessesTestData.CompleteAsync(player, services, converterId);
        await ProcessesTestData.CompleteAsync(player, services, recyclerId);

        Assert.Equal(targetBefore + 3, await ProcessesTestData.BalanceAsync(player, services, targetId));
        Dictionary<int, long> hullAfter = await BalancesAsync(player, services, hull.ResourceCost.Keys);

        // The conversion spends ten of the source and gives three of the target. A resource of
        // the hull that is one of those two carries both changes.
        Assert.All(hull.ResourceCost, cost =>
        {
            long returned = (long)decimal.Floor(2 * cost.Value * 0.40m);
            long conversion = cost.Key == sourceId ? 10 : cost.Key == targetId ? -3 : 0;
            Assert.Equal(hullBefore[cost.Key] + returned - conversion, hullAfter[cost.Key]);
        });
    }

    /// <summary>
    /// The matter converter is opened by the technology of the matter conversion, as data of
    /// the common table of the requirements: without the technology the building is closed and
    /// names it, and the first level of it opens the building (task 00014, section 11).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Without_the_technology_the_converter_is_not_available()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Aetheris.Planets.IPlanetsModule planets = services.GetRequiredService<Aetheris.Planets.IPlanetsModule>();
        int converterTypeId = (await planets.FindBuildingTypeIdByClassAsync(BuildingClasses.Converter, Token))!.Value;
        int technologyId = await services.GetRequiredService<ResearchDbContext>().Technologies.AsNoTracking()
            .Where(technology => technology.Code == ConversionTechnology)
            .Select(technology => technology.Id)
            .SingleAsync(Token);

        // The building is closed until the technology is learned, and the reason names it.
        Aetheris.Planets.Api.AvailabilityDto closed = await planets.CheckRequirementsAsync(
            Aetheris.Requirements.Services.RequirementSubjectTypes.BuildingType,
            converterTypeId,
            1,
            player.PlanetId,
            player.Account.UserId,
            Token);
        Assert.False(closed.IsAvailable);
        Aetheris.Planets.Api.RequirementDto reason = Assert.Single(closed.UnmetRequirements);
        Assert.Equal(Aetheris.Requirements.Services.RequirementTypes.TechnologyLevel, reason.RequirementType);
        Assert.Equal(technologyId, reason.TargetId);
        Assert.Equal(1, reason.TargetLevel);

        // The data hold the opening once, at the minimal level of the technology, and not again
        // on every level it has.
        List<int> openingLevels = await services.GetRequiredService<ResearchDbContext>().TechnologyLevels.AsNoTracking()
            .Where(level => level.TechnologyId == technologyId && level.EffectIds.Any(effect => effect.UnlockTargetId == converterTypeId))
            .Select(level => level.Level)
            .ToListAsync(Token);
        Assert.Equal([1], openingLevels);

        // The first level of the technology opens it, and a higher level changes nothing.
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);
        Assert.True((await planets.CheckRequirementsAsync(
            Aetheris.Requirements.Services.RequirementSubjectTypes.BuildingType,
            converterTypeId,
            1,
            player.PlanetId,
            player.Account.UserId,
            Token)).IsAvailable);
    }

    /// <summary>
    /// The repair of a ship repairs the hull and every module with one order; its cost and its
    /// time follow the condition the parts lost (task 00014, criterion 6a).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_repair_of_a_ship_repairs_everything_for_the_lost_condition()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.OrbitalWorkshop);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);

        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);
        PartFacts engine = await ProcessesTestData.PartAsync(services, Engine);
        ShipsOnOrbitStub ships = services.GetRequiredService<ShipsOnOrbitStub>();
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.ShipRepair);

        // A ship with nothing damaged has nothing to repair.
        ships.Place(player.Account.UserId, player.PlanetId, new ShipOnOrbit([new ShipPart(hull.PartTypeId, 1, false, PartCondition.Full)]));
        Assert.Equal(ProcessErrorCodes.NothingToRepair, await RefusedAsync(processes, player, buildingId, recipe.Id, new ProcessOrderRequest(1)));

        ships.Place(player.Account.UserId, player.PlanetId, new ShipOnOrbit(
        [
            new ShipPart(hull.PartTypeId, 1, false, 50),
            new ShipPart(engine.PartTypeId, 1, false, 80),
        ]));

        HashSet<int> resourceIds = [.. hull.ResourceCost.Keys, .. engine.ResourceCost.Keys];
        Dictionary<int, long> before = await PaidBalancesAsync(player, services, resourceIds);

        ProcessBuildingDto queued = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(1), Token);

        // The cost is the share of the cost of a new part times the lost condition, summed.
        Dictionary<int, long> paid = await PaidBalancesAsync(player, services, resourceIds);
        foreach (int resourceId in resourceIds)
        {
            decimal expected = (hull.ResourceCost.GetValueOrDefault(resourceId) * 0.3m * 0.5m) + (engine.ResourceCost.GetValueOrDefault(resourceId) * 0.3m * 0.2m);
            Assert.Equal(before[resourceId] - (long)decimal.Ceiling(expected), paid[resourceId]);
        }

        // The time is the time of the recipe times the summed lost condition.
        ProcessOrderDto order = queued.Queue[0];
        Assert.Equal((long)decimal.Ceiling(recipe.DurationSeconds * 0.7m), (long)(order.NextUnitAt!.Value - order.StartsAt!.Value).TotalSeconds);

        await ProcessesTestData.CompleteAsync(player, services, buildingId);

        ShipOnOrbit repaired = (await ships.FindAsync(player.Account.UserId, player.PlanetId, Token))!;
        Assert.All(repaired.Parts, part => Assert.Equal(PartCondition.Full, part.ConditionPercent));
    }

    /// <summary>
    /// The replacement of a module is quicker than the repair, takes a new module of the
    /// storage and puts the damaged one to the storage; the repair shop gives a damaged part
    /// of the storage its full condition back (task 00014, criterion 6a).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_replacement_and_the_repair_of_a_module_keep_the_parts()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long workshopId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.OrbitalWorkshop);
        long shopId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.ModuleRepair);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);

        PartFacts engine = await ProcessesTestData.PartAsync(services, Engine);
        ShipsOnOrbitStub ships = services.GetRequiredService<ShipsOnOrbitStub>();
        ships.Place(player.Account.UserId, player.PlanetId, new ShipOnOrbit([new ShipPart(engine.PartTypeId, 1, false, 40)]));
        await ProcessesTestData.PutPartsAsync(player, services, engine.PartTypeId, PartCondition.Full, 1);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo replacement = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.ModuleReplacement);
        ProcessRecipeInfo shipRepair = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.ShipRepair);
        ProcessRecipeInfo moduleRepair = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.ModuleRepair);

        // A kind of a module the ship does not carry damaged is refused.
        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);
        Assert.Equal(ProcessErrorCodes.ModuleNotOnShip, await RefusedAsync(processes, player, workshopId, replacement.Id, new ProcessOrderRequest(1, PartTypeId: hull.PartTypeId)));

        ProcessBuildingDto queued = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, workshopId, replacement.Id, new ProcessOrderRequest(1, PartTypeId: engine.PartTypeId), Token);
        ProcessOrderDto order = queued.Queue[0];

        // The replacement is quicker than the repair of the same ship.
        long replacementSeconds = (long)(order.NextUnitAt!.Value - order.StartsAt!.Value).TotalSeconds;
        Assert.True(replacementSeconds < decimal.Ceiling(shipRepair.DurationSeconds * 0.6m));
        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, PartCondition.Full));

        await ProcessesTestData.CompleteAsync(player, services, workshopId);

        // The damaged module is in the storage, the ship carries the new one.
        Assert.Equal(1, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, 40));
        ShipPart installed = Assert.Single((await ships.FindAsync(player.Account.UserId, player.PlanetId, Token))!.Parts);
        Assert.Equal(PartCondition.Full, installed.ConditionPercent);

        // The repair shop repairs the module that came off: the cost and the time follow the
        // lost condition, and the part comes back with its full condition.
        await ProcessesTestData.PutPartsAsync(player, services, engine.PartTypeId, 40, 1);
        Dictionary<int, long> before = await PaidBalancesAsync(player, services, engine.ResourceCost.Keys);

        Assert.Equal(ProcessErrorCodes.PartNotDamaged, await RefusedAsync(processes, player, shopId, moduleRepair.Id, new ProcessOrderRequest(2, PartTypeId: engine.PartTypeId)));

        ProcessBuildingDto repairing = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, shopId, moduleRepair.Id, new ProcessOrderRequest(2, PartTypeId: engine.PartTypeId, PartConditionPercent: 40), Token);
        ProcessOrderDto repairOrder = repairing.Queue[0];
        Assert.Equal((long)decimal.Ceiling(moduleRepair.DurationSeconds * 0.6m), (long)(repairOrder.NextUnitAt!.Value - repairOrder.StartsAt!.Value).TotalSeconds);

        Dictionary<int, long> paid = await PaidBalancesAsync(player, services, engine.ResourceCost.Keys);
        Assert.All(engine.ResourceCost, cost => Assert.Equal(before[cost.Key] - (long)decimal.Ceiling(cost.Value * 0.3m * 0.6m * 2), paid[cost.Key]));

        await ProcessesTestData.CompleteAsync(player, services, shopId);

        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, 40));
        Assert.Equal(2, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, PartCondition.Full));
    }

    /// <summary>
    /// The queue of a building has two places from the data, and an order starts when the one
    /// before it ends (task 00014, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_queue_has_two_places_and_runs_one_order_after_another()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        ProcessOrderRequest request = new(3, await ProcessesTestData.ResourceIdAsync(services, Deuterium), await ProcessesTestData.ResourceIdAsync(services, Helium));

        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, request, Token);
        ProcessBuildingDto two = await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, request, Token);

        Assert.Equal(2, two.Queue.Count);
        Assert.NotNull(two.Queue[0].StartsAt);
        Assert.Null(two.Queue[1].StartsAt);
        Assert.Equal(ProcessErrorCodes.QueueFull, await RefusedAsync(processes, player, buildingId, recipe.Id, request));

        DateTimeOffset firstEnd = two.Queue[0].FinishesAt!.Value;
        await services.GetRequiredService<ProcessQueueService>().CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, firstEnd, Token);

        // The second order starts at the end of the first one, not at the moment of the read.
        ProcessOrderEntity second = Assert.Single(await ProcessesTestData.OrdersAsync(player, services, buildingId));
        Assert.Equal(two.Queue[1].OrderId, second.Id);
        Assert.Equal(firstEnd, second.StartsAt);
        Assert.Equal(1, second.Position);
    }

    /// <summary>
    /// The event of a ready unit makes it and plans the event of the next unit, so the queue
    /// goes on without a read of the player; a repeated delivery of the same event changes
    /// nothing, and a new scope finds the queue where the last one left it (task 00014,
    /// section 3.1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_ready_event_makes_the_unit_plans_the_next_and_a_repeated_event_changes_nothing()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        long buildingId;
        long orderId;
        int targetId;
        long targetBefore;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
            await ProcessesTestData.GiveEveryResourceAsync(player, services);
            await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 10);

            ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
            targetId = await ProcessesTestData.ResourceIdAsync(services, Helium);
            targetBefore = await ProcessesTestData.BalanceAsync(player, services, targetId);

            ProcessBuildingDto queued = await services.GetRequiredService<IProcessesModule>().EnqueueAsync(
                player.Account.UserId,
                player.PlanetId,
                buildingId,
                recipe.Id,
                new ProcessOrderRequest(2, await ProcessesTestData.ResourceIdAsync(services, Deuterium), targetId),
                Token);
            orderId = queued.Queue[0].OrderId;
        }

        // The first event is the one of the first unit.
        ScheduledEventEntity first = await PendingEventAsync(orderId, 0);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await HandleAsync(scope.ServiceProvider, first);
            Assert.Equal(1, (await ProcessesTestData.OrdersAsync(player, scope.ServiceProvider, buildingId)).Single().ProducedQuantity);

            // The same event delivered once more makes nothing.
            await HandleAsync(scope.ServiceProvider, first);
            Assert.Equal(1, (await ProcessesTestData.OrdersAsync(player, scope.ServiceProvider, buildingId)).Single().ProducedQuantity);
        }

        // The event of the second unit is planned by the first one.
        ScheduledEventEntity second = await PendingEventAsync(orderId, 1);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ProcessCatalogCache>().Invalidate();
            await HandleAsync(scope.ServiceProvider, second);

            Assert.Empty(await ProcessesTestData.OrdersAsync(player, scope.ServiceProvider, buildingId));
            Assert.Equal(targetBefore + 1, await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, targetId));
        }
    }

    /// <summary>
    /// Twenty parallel orders of two buildings that take the same parts of the storage never
    /// spend them twice and never leave a balance below zero (task 00014, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_orders_never_spend_twice_or_go_below_zero()
    {
        const int parallelCount = 20;
        const int damaged = 50;

        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        long recyclingId;
        long shopId;
        PartFacts engine;
        ProcessRecipeInfo recycling;
        ProcessRecipeInfo repair;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            recyclingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling);
            shopId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.ModuleRepair);
            await ProcessesTestData.GiveEveryResourceAsync(player, services);
            engine = await ProcessesTestData.PartAsync(services, Engine);
            await ProcessesTestData.PutPartsAsync(player, services, engine.PartTypeId, damaged, 5);
            recycling = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.Recycling);
            repair = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.ModuleRepair);
        }

        ProcessOrderRequest request = new(1, PartTypeId: engine.PartTypeId, PartConditionPercent: damaged);
        Task<bool>[] tasks =
        [
            .. Enumerable.Range(0, parallelCount).Select(index => index % 2 == 0
                ? TryEnqueueAsync(player, recyclingId, recycling.Id, request)
                : TryEnqueueAsync(player, shopId, repair.Id, request)),
        ];

        bool[] accepted = await Task.WhenAll(tasks);

        using IServiceScope check = _factory.Services.CreateScope();

        // Every building has two places, so four orders take four parts of the five.
        Assert.Equal(4, accepted.Count(value => value));
        Assert.Equal(1, await ProcessesTestData.PartsAsync(player, check.ServiceProvider, engine.PartTypeId, damaged));
        Assert.Equal(2, (await ProcessesTestData.OrdersAsync(player, check.ServiceProvider, recyclingId)).Count);
        Assert.Equal(2, (await ProcessesTestData.OrdersAsync(player, check.ServiceProvider, shopId)).Count);

        // The stacks of the body hold the one part left, so no part was lost or counted twice.
        long held = await check.ServiceProvider.GetRequiredService<Aetheris.Production.Data.ProductionDbContext>().PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == player.PlanetId)
            .SumAsync(stack => stack.Quantity, Token);
        Assert.Equal(1, held);
        Assert.All(
            await check.ServiceProvider.GetRequiredService<PlanetsDbContext>().PlanetResources.AsNoTracking().Where(row => row.PlanetId == player.PlanetId).ToListAsync(Token),
            row => Assert.True(row.Amount >= 0));
    }

    /// <summary>
    /// Another player can neither read a building of the colony, nor order a process in it,
    /// nor cancel its order, by the module and by the commands; the order stays (task 00014,
    /// documentation/tech/09, section 10.1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Another_player_can_neither_read_nor_order_nor_cancel()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(owner, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(owner, services);
        await ProcessesTestData.GiveEveryResourceAsync(stranger, services);
        await ProcessesTestData.LearnAsync(owner, services, ConversionTechnology, 1);
        await ProcessesTestData.LearnAsync(stranger, services, ConversionTechnology, 1);

        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        ProcessOrderRequest request = new(2, await ProcessesTestData.ResourceIdAsync(services, Deuterium), await ProcessesTestData.ResourceIdAsync(services, Helium));
        ProcessBuildingDto queued = await processes.EnqueueAsync(owner.Account.UserId, owner.PlanetId, buildingId, recipe.Id, request, Token);
        long orderId = queued.Queue[0].OrderId;

        GameException read = await Assert.ThrowsAsync<GameException>(() => processes.GetBuildingAsync(stranger.Account.UserId, owner.PlanetId, buildingId, Token));
        Assert.Equal(ProcessErrorCodes.PlanetUnknown, read.Code);
        GameException list = await Assert.ThrowsAsync<GameException>(() => processes.GetBuildingsAsync(stranger.Account.UserId, owner.PlanetId, Token));
        Assert.Equal(ProcessErrorCodes.PlanetUnknown, list.Code);
        GameException order = await Assert.ThrowsAsync<GameException>(() => processes.EnqueueAsync(stranger.Account.UserId, owner.PlanetId, buildingId, recipe.Id, request, Token));
        Assert.Equal(ProcessErrorCodes.PlanetUnknown, order.Code);
        GameException cancel = await Assert.ThrowsAsync<GameException>(() => processes.CancelAsync(stranger.Account.UserId, owner.PlanetId, buildingId, orderId, Token));
        Assert.Equal(ProcessErrorCodes.PlanetUnknown, cancel.Code);

        // The commands check the owner of the body from the session, before the module.
        using HttpResponseMessage enqueue = await TestAccounts.SendCommandAsync(
            stranger.Client,
            EnqueueProcessCommand.RoutePath,
            new Dictionary<string, object?>
            {
                ["planet_id"] = owner.PlanetId,
                ["planet_building_id"] = buildingId,
                ["recipe_id"] = recipe.Id,
                ["quantity"] = 2,
                ["source_resource_type_id"] = request.SourceResourceTypeId,
                ["target_resource_type_id"] = request.TargetResourceTypeId,
            },
            "stranger-enqueue");
        using HttpResponseMessage cancelled = await TestAccounts.SendCommandAsync(
            stranger.Client,
            CancelProcessCommand.RoutePath,
            new Dictionary<string, object?> { ["planet_id"] = owner.PlanetId, ["planet_building_id"] = buildingId, ["order_id"] = orderId },
            "stranger-cancel");

        // The owner reaches the same command, so the refusal above is the one of the owner.
        using HttpResponseMessage own = await TestAccounts.SendCommandAsync(
            owner.Client,
            CancelProcessCommand.RoutePath,
            new Dictionary<string, object?> { ["planet_id"] = owner.PlanetId, ["planet_building_id"] = buildingId, ["order_id"] = 0 },
            "owner-cancel");
        using HttpResponseMessage reading = await stranger.Client.GetAsync(
            $"{ProcessesEndpoints.Prefix}/buildings/{buildingId}?planet_id={owner.PlanetId}",
            Token);

        // The owner reads the building by the same address the client uses.
        using HttpResponseMessage ownReading = await owner.Client.GetAsync(
            $"{ProcessesEndpoints.Prefix}/buildings/{buildingId}?planet_id={owner.PlanetId}",
            Token);
        using HttpResponseMessage ownList = await owner.Client.GetAsync(
            $"{ProcessesEndpoints.Prefix}/buildings?planet_id={owner.PlanetId}",
            Token);
        Assert.Equal(HttpStatusCode.OK, ownReading.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownList.StatusCode);

        Assert.NotEqual(HttpStatusCode.OK, enqueue.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, reading.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);

        ProcessOrderEntity kept = Assert.Single(await ProcessesTestData.OrdersAsync(owner, services, buildingId));
        Assert.Equal(orderId, kept.Id);
    }

    /// <summary>
    /// An archived recipe and a recipe of an archived kind are not ordered, but the order of
    /// the queue that makes it finishes; the archive reaches the player at once although the
    /// catalog lives in memory for an hour (task 00023, decisions 5 and 10a.6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_archived_recipe_is_not_ordered_but_its_order_finishes()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, Aetheris.Processes.Configuration.ProcessesConfigKeys.CatalogCacheSeconds, "3600");

        try
        {
            long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
            await ProcessesTestData.GiveEveryResourceAsync(player, services);
            await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);

            IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
            ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
            ProcessOrderRequest request = new(1, await ProcessesTestData.ResourceIdAsync(services, Deuterium), await ProcessesTestData.ResourceIdAsync(services, Helium));
            await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, request, Token);

            ProcessReferenceAdminService admin = services.GetRequiredService<ProcessReferenceAdminService>();
            ProcessRecipeEntity stored = await services.GetRequiredService<ProcessesDbContext>().ProcessRecipes.AsNoTracking().SingleAsync(entry => entry.Id == recipe.Id, Token);
            await admin.SetArchivedAsync(ProcessReferenceAdminService.ProcessRecipeKind, recipe.Id, true, new Aetheris.Core.Admin.AdminReferenceInput(stored.Version, "test", EmptyValues()), Token);

            Assert.Equal(ProcessErrorCodes.RecipeArchived, await RefusedAsync(processes, player, buildingId, recipe.Id, request));

            // The order that makes it finishes, and the queue keeps the name of what it makes.
            ProcessBuildingDto building = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);
            Assert.False(building.Recipes.Single(entry => entry.RecipeId == recipe.Id).IsAvailable);
            Assert.Equal(1, await ProcessesTestData.CompleteAsync(player, services, buildingId));

            ProcessTypeEntity type = await services.GetRequiredService<ProcessesDbContext>().ProcessTypes.AsNoTracking().SingleAsync(entry => entry.Id == recipe.ProcessTypeId, Token);
            ProcessRecipeInfo other = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, "resource_group_1");
            await admin.SetArchivedAsync(ProcessReferenceAdminService.ProcessTypeKind, type.Id, true, new Aetheris.Core.Admin.AdminReferenceInput(type.Version, "test", EmptyValues()), Token);

            Assert.Equal(
                ProcessErrorCodes.TypeArchived,
                await RefusedAsync(processes, player, buildingId, other.Id, new ProcessOrderRequest(1, await ProcessesTestData.ResourceIdAsync(services, "resource_1"), await ProcessesTestData.ResourceIdAsync(services, "resource_2"))));
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, Aetheris.Processes.Configuration.ProcessesConfigKeys.CatalogCacheSeconds, "0");
        }
    }

    /// <summary>
    /// A new process is one class of a handler and rows of the tables: the engine runs it
    /// without a change, and the engine names no concrete process (task 00014, criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_process_is_one_handler_and_rows_without_a_change_of_the_engine()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        int gift = await ProcessesTestData.ResourceIdAsync(services, Helium);
        int buildingTypeId = (await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>().FindBuildingTypeIdByClassAsync(BuildingClasses.Converter, Token))!.Value;

        ProcessesDbContext dbContext = services.GetRequiredService<ProcessesDbContext>();
        ProcessTypeEntity type = new()
        {
            Code = "process_type_900",
            NameKey = "processes.process_type.process_type_900.name",
            DescriptionKey = "processes.process_type.process_type_900.description",
            HandlerCode = StubHandler.Code,
            RefundsInput = true,
            SortOrder = 900,
        };
        dbContext.ProcessTypes.Add(type);
        await dbContext.SaveChangesAsync(Token);

        ProcessRecipeEntity recipe = new()
        {
            Code = "process_recipe_900",
            NameKey = "processes.process_recipe.process_recipe_900.name",
            DescriptionKey = "processes.process_recipe.process_recipe_900.description",
            ProcessTypeId = type.Id,
            BuildingTypeId = buildingTypeId,
            DurationSeconds = 10,
            SortOrder = 900,
        };
        dbContext.ProcessRecipes.Add(recipe);
        await dbContext.SaveChangesAsync(Token);
        services.GetRequiredService<ProcessCatalogCache>().Invalidate();

        // The engine is the one of the module, with one more handler in the container.
        ProcessQueueService engine = new(
            dbContext,
            services.GetRequiredService<ProcessesUnitOfWork>(),
            services.GetRequiredService<ProcessCatalogCache>(),
            services.GetRequiredService<Aetheris.Planets.IPlanetsModule>(),
            services.GetRequiredService<IPlanetResources>(),
            services.GetRequiredService<Aetheris.Economy.IEconomyModule>(),
            services.GetRequiredService<Aetheris.Economy.Services.CostSplitter>(),
            services.GetRequiredService<PartStockService>(),
            services.GetRequiredService<ITechnologyLevels>(),
            services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
            services.GetRequiredService<Aetheris.World.IWorldModule>(),
            services.GetRequiredService<IEventScheduler>(),
            services.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>(),
            services.GetRequiredService<Aetheris.Core.Time.IGameClock>(),
            [.. services.GetServices<IProcessHandler>(), new StubHandler(gift)]);

        long before = await ProcessesTestData.BalanceAsync(player, services, gift);
        ProcessBuildingDto building = await engine.ReadBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);
        Assert.True(building.Recipes.Single(entry => entry.RecipeId == recipe.Id).IsAvailable);

        ProcessBuildingDto queued = await engine.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, new ProcessOrderRequest(3), Token);
        await engine.CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, queued.Queue[0].FinishesAt!.Value, Token);

        Assert.Equal(before + (3 * StubHandler.Gift), await ProcessesTestData.BalanceAsync(player, services, gift));

        // The engine reads the handler of a kind by its code and names no concrete process.
        string source = File.ReadAllText(Path.Combine(ServerRoot(), "src", "Aetheris.Processes", "Services", "ProcessQueueService.cs"));
        Assert.All(ProcessHandlerCodes.All, handlerCode => Assert.DoesNotContain(handlerCode, source, StringComparison.Ordinal));
        Assert.DoesNotContain("Handlers", source, StringComparison.Ordinal);

        // Every handler of the starting data has a class in the container.
        List<IProcessHandler> handlers = [.. services.GetServices<IProcessHandler>()];
        Assert.All(ProcessHandlerCodes.All, handlerCode => Assert.Contains(handlers, handler => handler.HandlerCode == handlerCode));
    }

    /// <summary>
    /// The module of the processes holds no number of a rule of the game: the shares, the steps,
    /// the ranges and the times are rows of the tables, and the only numeric constants are the
    /// lengths of the columns and the places of the kinds in the menu of the admin panel (task
    /// 00014, criterion 7).
    /// </summary>
    [Fact]
    public void The_module_holds_no_number_of_a_rule_of_the_game()
    {
        string module = Path.Combine(ServerRoot(), "src", "Aetheris.Processes");
        string[] allowed = ["CodeLength", "NameLength", "ProcessTypeSortOrder", "ProcessRecipeSortOrder", "ProcessParamSortOrder"];

        List<string> constants =
        [
            .. Directory.EnumerateFiles(module, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .SelectMany(path => File.ReadLines(path))
                .Select(line => NumericConstantPattern().Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups["name"].Value)
                .Where(name => !allowed.Contains(name, StringComparer.Ordinal)),
        ];

        Assert.Empty(constants);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\bconst\s+(?:int|long|decimal|double|float)\s+(?<name>\w+)")]
    private static partial System.Text.RegularExpressions.Regex NumericConstantPattern();

    private static async Task<string> RefusedAsync(
        IProcessesModule processes,
        PlanetsTestPlayer player,
        long buildingId,
        int recipeId,
        ProcessOrderRequest request)
    {
        GameException refused = await Assert.ThrowsAsync<GameException>(() => processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipeId,
            request,
            Token));

        return refused.Code;
    }

    private static async Task<Dictionary<int, long>> BalancesAsync(PlanetsTestPlayer player, IServiceProvider services, IEnumerable<int> resourceIds)
    {
        Dictionary<int, long> balances = [];

        foreach (int resourceId in resourceIds)
        {
            balances[resourceId] = await ProcessesTestData.BalanceAsync(player, services, resourceId);
        }

        return balances;
    }

    /// <summary>
    /// The balances the repair pays: a currency from the civilization, the other resources from
    /// the body.
    /// </summary>
    private static async Task<Dictionary<int, long>> PaidBalancesAsync(PlanetsTestPlayer player, IServiceProvider services, IEnumerable<int> resourceIds)
    {
        HashSet<int> currencyIds = await PlanetsTestData.CurrencyIdsAsync(services);
        Dictionary<int, long> balances = [];

        foreach (int resourceId in resourceIds)
        {
            balances[resourceId] = currencyIds.Contains(resourceId)
                ? await EconomyTestData.BalanceAsync(services, player.Account.UserId, resourceId)
                : await ProcessesTestData.BalanceAsync(player, services, resourceId);
        }

        return balances;
    }

    private static async Task HandleAsync(IServiceProvider services, ScheduledEventEntity scheduled)
    {
        ProcessReadyHandler handler = services.GetServices<IScheduledEventHandler>().OfType<ProcessReadyHandler>().Single();

        await handler.HandleAsync(new ScheduledEvent(scheduled.Id, scheduled.SectorId, scheduled.Type, scheduled.RunAt, scheduled.Payload), Token);
    }

    private static JsonElement EmptyValues()
    {
        using JsonDocument document = JsonDocument.Parse("{}");

        return document.RootElement.Clone();
    }

    private static string ServerRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !Directory.Exists(Path.Combine(directory, "src", "Aetheris.Processes")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("The root of the server was not found.");
    }

    private async Task<ScheduledEventEntity> PendingEventAsync(long orderId, long produced)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        string key = ProcessQueueService.CompleteEventType + ":" + orderId + ":" + produced;

        return await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().ScheduledEvents.AsNoTracking()
            .SingleAsync(entry => entry.DedupeKey == key && entry.Status == ScheduledEventStatus.Pending, Token);
    }

    private async Task<bool> TryEnqueueAsync(PlanetsTestPlayer player, long buildingId, int recipeId, ProcessOrderRequest request)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<IProcessesModule>()
                .EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipeId, request, Token);

            return true;
        }
        catch (GameException exception) when (exception.Code is ProcessErrorCodes.QueueFull or ProcessErrorCodes.PartMissing)
        {
            return false;
        }
    }

    /// <summary>
    /// A process that is not in the starting data: one class that gives a resource for every
    /// unit.
    /// </summary>
    /// <param name="resourceTypeId">The resource the process gives.</param>
    private sealed class StubHandler(int resourceTypeId = 0) : IProcessHandler
    {
        /// <summary>The logical code of the stub.</summary>
        public const string Code = "test_stub";

        /// <summary>What one unit gives.</summary>
        public const long Gift = 7;

        public string HandlerCode => Code;

        public string PartSubject => ProcessPartSubjects.None;

        public Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProcessReason>>([]);

        public Task<ProcessInput> InputAsync(ProcessCheckContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessInput.None);

        public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessResult(new Dictionary<int, long> { [resourceTypeId] = Gift }, []));

        public Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
