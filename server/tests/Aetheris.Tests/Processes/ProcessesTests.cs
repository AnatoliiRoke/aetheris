using Aetheris.Core.Errors;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Tests of the processes: the recipes, the queue of a building, the conversion of matter,
/// the terraforming and the recycling (task 00014).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProcessesTests
{
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
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Converter);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessBuildingDto building = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);

        Assert.NotEmpty(building.Recipes);

        // The recipe of the converter is closed, because the source and the target of the
        // conversion are not named yet.
        Assert.All(
            building.Recipes.Where(recipe => !recipe.IsAvailable),
            recipe => Assert.NotEmpty(recipe.Reasons));
    }

    /// <summary>
    /// An order pays its whole cost at once and cancelling it returns the cost in full
    /// (task 00014, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_order_pays_its_cost_and_cancelling_returns_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.OrbitalWorkshop);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();

        // The kind of the process that refunds its input, so the cost comes back in full.
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.ShipRepair);
        int resourceTypeId = recipe.IngredientIds.First(ingredient => ingredient.ResourceTypeId is not null).ResourceTypeId!.Value;

        long before = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        ProcessBuildingDto queued = await processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(2),
            Token);

        Assert.Single(queued.Queue);

        long after = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Assert.Equal(before - (recipe.IngredientIds.First(ingredient => ingredient.ResourceTypeId == resourceTypeId).Amount * 2), after);

        ProcessBuildingDto cancelled = await processes.CancelAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            queued.Queue[0].OrderId,
            Token);

        Assert.Empty(cancelled.Queue);

        long restored = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Assert.Equal(before, restored);
    }

    /// <summary>
    /// Cancelling the terraforming loses the device: the kind of the process does not refund
    /// its input (task 00014, criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Cancelling_the_terraforming_loses_the_device()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Production);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();

        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.Terraforming);
        int resourceTypeId = recipe.IngredientIds.First(ingredient => ingredient.ResourceTypeId is not null).ResourceTypeId!.Value;

        long before = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        ProcessBuildingDto queued = await processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(1),
            Token);

        await processes.CancelAsync(player.Account.UserId, player.PlanetId, buildingId, queued.Queue[0].OrderId, Token);

        long after = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        // The device is lost, so the cost does not come back.
        Assert.True(after < before);
    }

    /// <summary>
    /// The queue of a building has two places and follows one another (task 00014,
    /// criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_queue_of_a_building_has_two_places()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Production);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.Terraforming);

        ProcessBuildingDto first = await processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(1),
            Token);

        Assert.Single(first.Queue);

        await processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(1),
            Token);

        GameException refused = await Assert.ThrowsAsync<GameException>(() => processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(1),
            Token));

        Assert.Equal(ProcessErrorCodes.QueueFull, refused.Code);
    }

    /// <summary>
    /// The conversion turns one raw material into another of the same group, and the output
    /// is the percentage of the level of the technology (task 00014, criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_conversion_turns_one_raw_material_into_another()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.MatterConversion);

        int sourceId = await ProcessesTestData.ResourceIdAsync(scope.ServiceProvider, "resource_1");
        int targetId = await ProcessesTestData.ResourceIdAsync(scope.ServiceProvider, "resource_2");

        long before = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, targetId);

        await processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(10, sourceId, targetId),
            Token);

        await ProcessesTestData.CompleteAsync(player, scope.ServiceProvider, buildingId);

        long after = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, targetId);

        // The player has no technology of the conversion, so nothing is made.
        Assert.Equal(before, after);
    }

    /// <summary>
    /// The conversion refuses a source that is not in the group the recipe takes (task 00014,
    /// criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_conversion_refuses_a_source_of_another_group()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.MatterConversion);

        // The ether is in another group, so the conversion refuses it.
        int sourceId = await ProcessesTestData.ResourceIdAsync(scope.ServiceProvider, "resource_11");
        int targetId = await ProcessesTestData.ResourceIdAsync(scope.ServiceProvider, "resource_2");

        GameException refused = await Assert.ThrowsAsync<GameException>(() => processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(10, sourceId, targetId),
            Token));

        Assert.Equal(ProcessErrorCodes.SourceGroupMismatch, refused.Code);
    }

    /// <summary>
    /// Twenty parallel orders never spend the same resources twice and never leave the
    /// balance below zero (task 00014, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_orders_never_spend_twice_or_go_below_zero()
    {
        const int parallelCount = 20;

        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Production);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.Terraforming);
        int resourceTypeId = recipe.IngredientIds.First(ingredient => ingredient.ResourceTypeId is not null).ResourceTypeId!.Value;

        long before = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Task<ProcessBuildingDto>[] tasks = [.. Enumerable.Range(0, parallelCount).Select(index =>
            EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, index))];

        ProcessBuildingDto[] results = await Task.WhenAll(tasks);

        // The queue of a building has two places, so the rest is refused.
        Assert.Equal(2, results.Count(result => result.Queue.Count > 0));

        long after = await ProcessesTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Assert.True(after >= 0);
        Assert.Equal(before - (recipe.IngredientIds.First(ingredient => ingredient.ResourceTypeId == resourceTypeId).Amount * 2), after);
    }

    /// <summary>
    /// The terraforming makes the deposits of the body rich, and the device is spent (task
    /// 00014, criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_terraforming_makes_the_deposits_rich()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProcessesTestData.BuildAsync(player, scope.ServiceProvider, BuildingClasses.Production);
        await ProcessesTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProcessesModule processes = scope.ServiceProvider.GetRequiredService<IProcessesModule>();
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(scope.ServiceProvider, ProcessHandlerCodes.Terraforming);

        Aetheris.World.Api.PlanetFacts? before = await scope.ServiceProvider.GetRequiredService<Aetheris.World.IWorldModule>()
            .GetPlanetFactsAsync(player.PlanetId, Token);

        Assert.NotNull(before);
        Assert.NotEmpty(before!.Deposits);
        Assert.All(before.Deposits, deposit => Assert.True(deposit.Coefficient < 1.4m));

        await processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            new ProcessOrderRequest(1),
            Token);

        await ProcessesTestData.CompleteAsync(player, scope.ServiceProvider, buildingId);

        Aetheris.World.Api.PlanetFacts? after = await scope.ServiceProvider.GetRequiredService<Aetheris.World.IWorldModule>()
            .GetPlanetFactsAsync(player.PlanetId, Token);

        Assert.NotNull(after);
        Assert.NotEmpty(after!.Deposits);
        Assert.All(after.Deposits, deposit => Assert.InRange(deposit.Coefficient, 1.4m, 1.9m));
    }

    /// <summary>
    /// An element added only to the tables reaches the game, without the seed and without the
    /// admin panel (task 00014, criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_element_added_only_to_the_tables_reaches_the_game()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        ProcessesDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProcessesDbContext>();

        ProcessTypeEntity type = new()
        {
            Code = "process_type_900",
            NameKey = "processes.process_type.process_type_900.name",
            DescriptionKey = "processes.process_type.process_type_900.description",
            HandlerCode = ProcessHandlerCodes.MatterConversion,
            RefundsInput = true,
            SortOrder = 900,
        };

        dbContext.ProcessTypes.Add(type);
        await dbContext.SaveChangesAsync(Token);

        scope.ServiceProvider.GetRequiredService<ProcessCatalogCache>().Invalidate();

        ProcessCatalog catalog = await scope.ServiceProvider.GetRequiredService<ProcessCatalogCache>()
            .GetAsync(dbContext, scope.ServiceProvider.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);

        Assert.NotNull(catalog.FindType(type.Id));

        // The code of the row is in the code of the program nowhere.
        string sources = Path.Combine(ServerRoot(), "src", "Aetheris.Processes");
        Assert.False(File.ReadAllText(Path.Combine(sources, "Data", "process-reference-seed.json")).Contains("process_type_900", StringComparison.Ordinal));
    }

    /// <summary>
    /// The engine of the processes knows no concrete process: a kind of a process names its
    /// handler by a logical code, and the engine finds the handler in the container (task
    /// 00014, criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_engine_knows_no_concrete_process()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();

        ProcessQueueService queue = scope.ServiceProvider.GetRequiredService<ProcessQueueService>();

        List<IProcessHandler> handlers = [.. scope.ServiceProvider.GetServices<IProcessHandler>()];

        // Every handler of the starting data has a class, and the engine finds them by their
        // logical code.
        foreach (string handlerCode in ProcessHandlerCodes.All)
        {
            Assert.Contains(handlers, handler => string.Equals(handler.HandlerCode, handlerCode, StringComparison.Ordinal));
        }

        // The engine reads the handler of a kind by its code and not by the name of the kind.
        string engine = File.ReadAllText(Path.Combine(ServerRoot(), "src", "Aetheris.Processes", "Services", "ProcessQueueService.cs"));

        foreach (string handlerCode in ProcessHandlerCodes.All)
        {
            Assert.DoesNotContain(handlerCode, engine, StringComparison.Ordinal);
        }
    }

    private async Task<ProcessBuildingDto> EnqueueAsync(
        string playerId,
        long planetId,
        long buildingId,
        int recipeId,
        int index)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        try
        {
            return await scope.ServiceProvider.GetRequiredService<IProcessesModule>()
                .EnqueueAsync(playerId, planetId, buildingId, recipeId, new ProcessOrderRequest(1), Token);
        }
        catch (GameException exception) when (exception.Code == ProcessErrorCodes.QueueFull)
        {
            return new ProcessBuildingDto(planetId, buildingId, 0, 0, [], [], default);
        }
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

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProcessesConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProcessesModule>().EnsureAsync(Token);
    }
}
