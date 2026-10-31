using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Api;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Tests of the stubs among the processes: the repair of a ship, the replacement of a module and
/// the repair of a module are not offered until the fleet and its parts exist (author decision
/// 31.10.2026); the seed archives them, and the logic of their handlers stays tested elsewhere.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProcessStubTests
{
    private const string Engine = "part_4";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStubTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProcessStubTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A fresh database holds the kinds and the recipes of the repair and the replacement
    /// archived: no workshop lists them as available, an order of them is refused, and the other
    /// kinds are not archived.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fresh_database_does_not_offer_the_repair_and_the_replacement()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProcessesDbContext dbContext = services.GetRequiredService<ProcessesDbContext>();

        List<ProcessTypeEntity> types = await dbContext.ProcessTypes.AsNoTracking().ToListAsync(Token);
        HashSet<string> stubs = [ProcessHandlerCodes.ShipRepair, ProcessHandlerCodes.ModuleReplacement, ProcessHandlerCodes.ModuleRepair];

        Assert.All(types.Where(type => stubs.Contains(type.HandlerCode)), type => Assert.True(type.IsArchived));
        Assert.Equal(stubs.Count, types.Count(type => stubs.Contains(type.HandlerCode)));
        Assert.All(types.Where(type => !stubs.Contains(type.HandlerCode)), type => Assert.False(type.IsArchived));

        HashSet<int> stubTypeIds = [.. types.Where(type => stubs.Contains(type.HandlerCode)).Select(type => type.Id)];
        List<ProcessRecipeEntity> recipes = await dbContext.ProcessRecipes.AsNoTracking().ToListAsync(Token);

        Assert.All(recipes.Where(recipe => stubTypeIds.Contains(recipe.ProcessTypeId)), recipe => Assert.True(recipe.IsArchived));
        Assert.All(recipes.Where(recipe => !stubTypeIds.Contains(recipe.ProcessTypeId)), recipe => Assert.False(recipe.IsArchived));

        // The workshops do not list the stubs, and an order of one is refused.
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        long workshopId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.OrbitalWorkshop);
        long shopId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.ModuleRepair);

        foreach (long buildingId in new[] { workshopId, shopId })
        {
            ProcessBuildingDto building = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, buildingId, Token);
            Assert.DoesNotContain(building.Recipes, entry => entry.IsAvailable);
        }

        ProcessRecipeEntity shipRepair = recipes.First(recipe => recipe.ProcessTypeId == types.First(type => type.HandlerCode == ProcessHandlerCodes.ShipRepair).Id);
        GameException refused = await Assert.ThrowsAsync<GameException>(() => processes.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            workshopId,
            shipRepair.Id,
            new ProcessOrderRequest(1),
            Token));
        Assert.Equal(ProcessErrorCodes.RecipeArchived, refused.Code);
    }

    /// <summary>
    /// An order of a stub kind that was queued before the kind was archived finishes: the stub
    /// is archived, not removed.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_order_of_a_stub_kind_that_was_queued_before_the_archive_finishes()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long shopId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.ModuleRepair);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        PartFacts engine = await ProcessesTestData.PartAsync(services, Engine);
        await ProcessesTestData.PutPartsAsync(player, services, engine.PartTypeId, 40, 1);

        // The helper offers the stub again, as the admin panel does; the order enters the queue.
        ProcessRecipeInfo recipe = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.ModuleRepair);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        await processes.EnqueueAsync(player.Account.UserId, player.PlanetId, shopId, recipe.Id, new ProcessOrderRequest(1, PartTypeId: engine.PartTypeId, PartConditionPercent: 40), Token);

        // The kind and the recipe are archived again, as the seed has them.
        ProcessReferenceAdminService admin = services.GetRequiredService<ProcessReferenceAdminService>();
        ProcessesDbContext dbContext = services.GetRequiredService<ProcessesDbContext>();
        ProcessRecipeEntity storedRecipe = await dbContext.ProcessRecipes.AsNoTracking().SingleAsync(entry => entry.Id == recipe.Id, Token);
        ProcessTypeEntity storedType = await dbContext.ProcessTypes.AsNoTracking().SingleAsync(entry => entry.Id == recipe.ProcessTypeId, Token);
        await admin.SetArchivedAsync(ProcessReferenceAdminService.ProcessRecipeKind, storedRecipe.Id, true, new AdminReferenceInput(storedRecipe.Version, "test", EmptyValues()), Token);
        await admin.SetArchivedAsync(ProcessReferenceAdminService.ProcessTypeKind, storedType.Id, true, new AdminReferenceInput(storedType.Version, "test", EmptyValues()), Token);

        Assert.Equal(1, await ProcessesTestData.CompleteAsync(player, services, shopId));
        Assert.Equal(1, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, Aetheris.Production.Data.Entities.PartCondition.Full));
        Assert.Equal(0, await ProcessesTestData.PartsAsync(player, services, engine.PartTypeId, 40));
    }

    private static JsonElement EmptyValues()
    {
        using JsonDocument document = JsonDocument.Parse("{}");

        return document.RootElement.Clone();
    }
}
