using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Services;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Services;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The queue of a factory: a batch of a recipe, its cost and its result (task 00013,
/// decision 2.3).
/// </summary>
/// <remarks>
/// <para>
/// The queue is one for every factory, and the batches follow one another, so the record
/// under way holds the moment the next unit is ready and the ones that wait have none. The
/// ingredients are paid when a batch enters the queue and returned in full when it leaves
/// it.
/// </para>
/// <para>
/// The result is added one unit at a time. When the storage of the body is full the
/// production stops and the record waits for the place to free up.
/// </para>
/// <para>
/// Every change locks the row of the queue of the factory first, so two parallel commands
/// of one factory never spend the same ingredients twice (task 00013, criterion 2).
/// </para>
/// </remarks>
public sealed class ProductionQueueService
{
    /// <summary>
    /// The event type of the readiness of a unit.
    /// </summary>
    public const string CompleteEventType = "production.unit_ready";

    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly IPlanetsModule _planets;
    private readonly IPlanetResources _resources;
    private readonly IEconomyModule _economy;
    private readonly CostSplitter _costs;
    private readonly IRequirementsModule _requirements;
    private readonly ITechnologyLevels _technologies;
    private readonly IProductionMultipliers _multipliers;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionQueueService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="planets">The bodies module, for the level of the factory.</param>
    /// <param name="resources">The balance of a body, for the cost of the batch.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="technologies">The technologies of the player.</param>
    /// <param name="multipliers">The multipliers of the production of the player.</param>
    /// <param name="world">The world, for the race of the player and the sector of the body.</param>
    /// <param name="scheduler">The scheduler of the events.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The game clock.</param>
    public ProductionQueueService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        ProductionCatalogCache catalogCache,
        IPlanetsModule planets,
        IPlanetResources resources,
        IEconomyModule economy,
        CostSplitter costs,
        IRequirementsModule requirements,
        ITechnologyLevels technologies,
        IProductionMultipliers multipliers,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(multipliers);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _planets = planets;
        _resources = resources;
        _economy = economy;
        _costs = costs;
        _requirements = requirements;
        _technologies = technologies;
        _multipliers = multipliers;
        _world = world;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Puts a batch of a recipe in the queue of a factory. The ingredients of the whole
    /// batch are paid at once, and cancelling the batch returns them in full.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="recipeId">The recipe.</param>
    /// <param name="quantity">How many units the batch holds.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factory after the change.</returns>
    public Task<ProductionFactoryDto> EnqueueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        int recipeId,
        long quantity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                PartStorageEntity storage = await OpenAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);

                // What is due is made first, so a finished batch never holds a place.
                await CompleteDueAsync(playerId, planetId, queue, storage, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                if (quantity <= 0)
                {
                    throw new GameException(ProductionErrorCodes.QuantityInvalid, "The batch must hold at least one unit.");
                }

                ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
                RecipeInfo recipe = catalog.FindRecipe(recipeId)
                    ?? throw new GameException(ProductionErrorCodes.RecipeUnknown, "The recipe does not exist.");

                // An archived recipe, or a recipe of an archived part or group, keeps working for
                // the batches that already use it, but no new batch of it starts (task 00023,
                // decision 5).
                if (!IsOffered(catalog, recipe, await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false)))
                {
                    throw new GameException(ProductionErrorCodes.RecipeArchived, "The recipe is archived.");
                }

                if (recipe.BuildingTypeId is not { } buildingTypeId)
                {
                    throw new GameException(ProductionErrorCodes.WrongFactory, "The recipe needs no factory.");
                }

                // The factory is the building the batch runs in, so its level is the level of
                // that building, and the recipe names the type it must be of.
                PlanetBuildingLevelById factory = await _planets.GetBuildingLevelByIdAsync(
                    planetId,
                    planetBuildingId,
                    playerId,
                    cancellationToken).ConfigureAwait(false);

                if (factory.Level <= 0 || factory.BuildingTypeId != buildingTypeId)
                {
                    throw new GameException(ProductionErrorCodes.FactoryMissing, "The body has no factory of the kind the recipe needs.");
                }

                ProductionSettings settings = ProductionSettings.From(_config);

                if (queue.Count >= await SlotsAsync(settings, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(ProductionErrorCodes.QueueFull, "The queue of the factory has no free place.");
                }

                RequirementContext context = await RequirementContextAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                List<ProductionReasonDto> reasons = UnmetReasons(catalog, recipe, context);

                if (reasons.Count > 0)
                {
                    throw new GameException(reasons[0].Code, "The recipe is not available.", new Dictionary<string, object?>
                    {
                        ["reason_codes"] = reasons.Select(reason => reason.Code).ToArray(),
                    });
                }

                // The ingredients are paid when the batch enters the queue and returned in
                // full when it leaves it.
                Dictionary<int, long> resourceCosts = Costs(recipe, quantity, ingredient => ingredient.ResourceTypeId);
                Dictionary<int, long> partCosts = Costs(recipe, quantity, ingredient => ingredient.PartTypeId);
                DateTimeOffset now = _clock.GameNow;
                CheckDuration(now, await ScaledSecondsAsync(playerId, recipe, cancellationToken).ConfigureAwait(false), quantity);

                // The costs of a civilization are paid from its balance and the rest from the
                // balance of the body (task 00015).
                SplitCosts split = await _costs.SplitAsync(resourceCosts, cancellationToken).ConfigureAwait(false);

                await _resources.TakeAsync(planetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);
                await _economy.TakeAsync(playerId, split.Currencies, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderCostEntity> partCostRows = await TakePartsAsync(planetId, storage, partCosts, cancellationToken).ConfigureAwait(false);

                ProductionOrderEntity item = new()
                {
                    PlanetId = planetId,
                    PlanetBuildingId = planetBuildingId,
                    SectorId = await SectorOfAsync(planetId, cancellationToken).ConfigureAwait(false),
                    RecipeId = recipeId,
                    Quantity = quantity,
                    ProducedQuantity = 0,
                    Position = queue.Count + 1,
                    CreatedAt = now,
                    CostIds =
                    [
                        .. resourceCosts.Select(cost => new ProductionOrderCostEntity
                        {
                            ResourceTypeId = cost.Key,
                            Amount = cost.Value,
                        }),
                        .. partCostRows,
                    ],
                };

                _dbContext.ProductionOrders.Add(item);
                queue.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await StartNextAsync(playerId, queue, now, cancellationToken).ConfigureAwait(false);
                await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

                return await ReadFactoryAsync(playerId, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a batch from the queue of a factory and returns the whole cost of the units it
    /// has not made yet.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="orderId">The batch.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factory after the change.</returns>
    public Task<ProductionFactoryDto> CancelAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        long orderId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                PartStorageEntity storage = await OpenAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, planetId, queue, storage, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                ProductionOrderEntity cancelled = queue.FirstOrDefault(entry => entry.Id == orderId)
                    ?? throw new GameException(ProductionErrorCodes.OrderUnknown, "The batch of the queue does not exist.");

                bool headRemoved = queue[0].Id == cancelled.Id;

                // The costs are read before the record is deleted, since a deleted record
                // loses its cost rows. The units already made keep their cost, the rest is
                // returned in full (task 00013, decision 2.3).
                List<ProductionOrderCostEntity> costs = [.. cancelled.CostIds];
                long quantity = cancelled.Quantity;
                long produced = cancelled.ProducedQuantity;

                queue.Remove(cancelled);
                _dbContext.ProductionOrders.Remove(cancelled);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await GiveBackAsync(planetId, playerId, storage, costs, quantity, produced, cancellationToken).ConfigureAwait(false);

                if (headRemoved)
                {
                    await StartNextAsync(playerId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);
                }

                await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

                return await ReadFactoryAsync(playerId, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Completes every unit of a factory that is due at a moment.
    /// </summary>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the units that were made.</returns>
    public Task<int> CompleteDueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                if (planetBuildingId <= 0)
                {
                    return 0;
                }

                PartStorageEntity storage = await OpenAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);

                return await CompleteDueAsync(playerId, planetId, queue, storage, now, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Reads a factory with its recipes, its queue and the storage of the parts.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factory.</returns>
    /// <exception cref="GameException">The body is not a colony of the player, or the factory is not on it.</exception>
    public async Task<ProductionFactoryDto> ReadFactoryAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        // A player reads only the factories of his own colonies, so the moment of a unit and the
        // mark of the race are always those of the owner.
        await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        PlanetBuildingLevelById factory = await _planets.GetBuildingLevelByIdAsync(
            planetId,
            planetBuildingId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        if (factory.Level <= 0)
        {
            throw new GameException(ProductionErrorCodes.FactoryMissing, "The body has no such factory.");
        }

        await CompleteDueAsync(playerId, planetId, planetBuildingId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        RequirementContext context = await RequirementContextAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        ProductionMultipliers multipliers = await _multipliers.GetMultipliersAsync(playerId, cancellationToken).ConfigureAwait(false);
        HashSet<int> queuedRecipeIds = [.. queue.Select(order => order.RecipeId)];

        // An archived recipe is not offered, but the one a batch of the queue still makes is
        // listed, so the queue keeps the name of what it makes (task 00023, decision 5).
        List<ProductionRecipeDto> recipes =
        [
            .. catalog.RecipesOfFactory(factory.BuildingTypeId)
                .Where(recipe => IsOffered(catalog, recipe, resources) || queuedRecipeIds.Contains(recipe.Id))
                .Select(recipe => RecipeDto(catalog, resources, recipe, context, multipliers)),
        ];

        List<ProductionOrderDto> orders =
        [
            .. queue.Select(order => new ProductionOrderDto(
                order.Id,
                order.RecipeId,
                order.Quantity,
                order.ProducedQuantity,
                order.Position,
                order.StartsAt,
                order.NextUnitAt,
                order.FinishesAt,
                order.IsBlocked)),
        ];

        return new ProductionFactoryDto(
            planetId,
            planetBuildingId,
            factory.Level,
            recipes,
            orders,
            await StorageDtoAsync(planetId, cancellationToken).ConfigureAwait(false),
            _clock.GameNow);
    }

    /// <summary>
    /// Reads every batch that is in the queues of the factories of a body, with the places of
    /// those queues (task 00020, section 7.5). It only reads: the batch is not changed, the
    /// units that are due are made first, as a read of one factory does.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The batches of all the factories, in the order of the queues, and the places.</returns>
    /// <exception cref="GameException">The body is not a colony of the player.</exception>
    public async Task<ProductionActiveOrdersDto> ReadActiveOrdersAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _clock.GameNow;
        List<ProductionOrderEntity> orders = await LoadPlanetOrdersAsync(planetId, cancellationToken).ConfigureAwait(false);
        long[] dueFactoryIds = [.. orders
            .Where(order => order.NextUnitAt is not null && order.NextUnitAt <= now)
            .Select(order => order.PlanetBuildingId)
            .Distinct()];

        foreach (long planetBuildingId in dueFactoryIds)
        {
            await CompleteDueAsync(playerId, planetId, planetBuildingId, now, cancellationToken).ConfigureAwait(false);
        }

        if (dueFactoryIds.Length > 0)
        {
            orders = await LoadPlanetOrdersAsync(planetId, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ProductionFactorySummaryDto> factories = await ReadFactoriesAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        Dictionary<long, string> factoryNameKeys = factories.ToDictionary(factory => factory.PlanetBuildingId, factory => factory.NameKey);
        ProductionSettings settings = ProductionSettings.From(_config);
        int slots = 0;

        // The places of the queues add up: every factory of the body has its own queue.
        foreach (long planetBuildingId in factoryNameKeys.Keys.Union(orders.Select(order => order.PlanetBuildingId)))
        {
            slots += await SlotsAsync(settings, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
        }

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        ProductionMultipliers multipliers = await _multipliers.GetMultipliersAsync(playerId, cancellationToken).ConfigureAwait(false);
        List<ProductionActiveOrderDto> items = [];

        foreach (ProductionOrderEntity order in orders)
        {
            RecipeInfo? recipe = catalog.FindRecipe(order.RecipeId);

            items.Add(new ProductionActiveOrderDto(
                order.Id,
                order.PlanetBuildingId,
                factoryNameKeys.GetValueOrDefault(order.PlanetBuildingId, string.Empty),
                order.RecipeId,
                recipe?.NameKey ?? string.Empty,
                order.Quantity,
                order.ProducedQuantity,
                order.Position,
                recipe is null ? 0 : ScaledSeconds(recipe, multipliers),
                order.StartsAt,
                order.NextUnitAt,
                order.FinishesAt,
                order.IsBlocked));
        }

        return new ProductionActiveOrdersDto(planetId, slots, items.Count, items, _clock.GameNow);
    }

    /// <summary>
    /// Reads the storage of the parts of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks of the parts and the storage.</returns>
    /// <exception cref="GameException">The body is not a colony of the player.</exception>
    public async Task<ProductionStockDto> ReadStockAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        // Every factory of the body brings its due units first, each in its own transaction, so
        // the stock is the one of the moment and two factories never wait for each other.
        List<long> factoryIds = await _dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.PlanetId == planetId)
            .Select(order => order.PlanetBuildingId)
            .Distinct()
            .OrderBy(factoryId => factoryId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (long factoryId in factoryIds)
        {
            await CompleteDueAsync(playerId, planetId, factoryId, _clock.GameNow, cancellationToken).ConfigureAwait(false);
        }

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        List<PartStockEntity> stacks = await _dbContext.PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == planetId)
            .OrderBy(stack => stack.PartTypeId)
            .ThenBy(stack => stack.MarkLevel)
            .ThenBy(stack => stack.HasRaceBonus)
            .ThenByDescending(stack => stack.ConditionPercent)
            .ThenBy(stack => stack.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProductionStackDto> rows =
        [
            .. stacks.Select(stack =>
            {
                PartTypeInfo? part = catalog.FindPart(stack.PartTypeId);
                PartGroupInfo? group = part is null ? null : catalog.FindGroup(part.PartGroupId);

                return new ProductionStackDto(
                    stack.PartTypeId,
                    stack.MarkLevel,
                    stack.HasRaceBonus,
                    stack.ConditionPercent,
                    stack.Quantity,
                    part?.NameKey ?? string.Empty,
                    part?.ImageKey,
                    part?.PartGroupId ?? 0,
                    group?.NameKey);
            }),
        ];

        return new ProductionStockDto(
            planetId,
            rows,
            await StorageDtoAsync(planetId, cancellationToken).ConfigureAwait(false),
            _clock.GameNow,
            [.. catalog.Recipes.Where(recipe => recipe.ResourceTypeId is not null).Select(recipe => recipe.ResourceTypeId!.Value).Distinct().Order()]);
    }

    /// <summary>
    /// Reads every factory of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factories of the body.</returns>
    public async Task<IReadOnlyList<ProductionFactorySummaryDto>> ReadFactoriesAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        // The types of the factories are the types the recipes of the catalog name, so a new
        // recipe opens a new factory without a change in the code (task 00013, decision 2.1).
        // A factory whose recipes are all archived stays listed, so its queue stays in sight.
        int[] factoryTypeIds = [.. catalog.Recipes
            .Where(recipe => recipe.BuildingTypeId is not null)
            .Select(recipe => recipe.BuildingTypeId!.Value)
            .Distinct()
            .OrderBy(typeId => typeId)];

        if (factoryTypeIds.Length == 0)
        {
            return [];
        }

        IReadOnlyList<PlanetBuildingLevelById> buildings = await _planets.GetBuildingsByTypesAsync(
            planetId,
            factoryTypeIds,
            playerId,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<int, string> nameKeys = await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false);

        List<ProductionFactorySummaryDto> factories =
        [
            .. buildings.Select(building => new ProductionFactorySummaryDto(
                planetId,
                building.PlanetBuildingId,
                building.BuildingTypeId,
                building.Level,
                nameKeys.GetValueOrDefault(building.BuildingTypeId, string.Empty)))
        ];

        return factories;
    }

    /// <summary>
    /// Whether new batches of a recipe may start: the recipe, the part it makes, the group of
    /// that part and the resource it makes are not archived (task 00023, decision 5).
    /// </summary>
    /// <param name="catalog">The catalog of the production.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <returns><see langword="true"/> when the recipe is offered to the players.</returns>
    private static bool IsOffered(ProductionCatalog catalog, RecipeInfo recipe, ResourceCatalog resources)
    {
        if (recipe.IsArchived)
        {
            return false;
        }

        if (recipe.PartTypeId is { } partTypeId
            && (catalog.FindPart(partTypeId) is not { } part
                || part.IsArchived
                || catalog.FindGroup(part.PartGroupId)?.IsArchived != false))
        {
            return false;
        }

        return recipe.ResourceTypeId is not { } resourceTypeId
            || resources.Resources.FirstOrDefault(resource => resource.Id == resourceTypeId) is { IsArchived: false };
    }

    private ProductionRecipeDto RecipeDto(
        ProductionCatalog catalog,
        ResourceCatalog resources,
        RecipeInfo recipe,
        RequirementContext context,
        ProductionMultipliers multipliers)
    {
        List<ProductionReasonDto> reasons = UnmetReasons(catalog, recipe, context);

        if (!IsOffered(catalog, recipe, resources))
        {
            // A recipe a batch of the queue still makes is listed, but no new batch starts.
            reasons.Insert(0, new ProductionReasonDto(ProductionErrorCodes.RecipeArchived, recipe.Id, 0));
        }

        return new ProductionRecipeDto(
            recipe.Id,
            recipe.Code,
            recipe.NameKey,
            recipe.DescriptionKey,
            recipe.BuildingTypeId,
            recipe.PartTypeId,
            recipe.ResourceTypeId,
            recipe.OutputQuantity,
            recipe.DurationSeconds,
            reasons.Count == 0,
            reasons,
            recipe.ImageKey,
            ScaledSeconds(recipe, multipliers),
            [
                .. recipe.IngredientIds.Select(ingredient => new ProductionIngredientDto(
                    ingredient.ResourceTypeId,
                    ingredient.PartTypeId,
                    ingredient.Amount,
                    ingredient.ResourceTypeId is { } resourceTypeId
                        ? resources.Resources.FirstOrDefault(resource => resource.Id == resourceTypeId)?.NameKey ?? string.Empty
                        : catalog.FindPart(ingredient.PartTypeId ?? 0)?.NameKey ?? string.Empty)),
            ]);
    }

    /// <summary>
    /// The requirements of a recipe that the player does not meet, each with the name of what
    /// is required, so a closed recipe says why it is closed (task 00013, decision 7).
    /// </summary>
    /// <param name="catalog">The catalog of the production, with the requirements of the recipes.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="context">The levels, the technologies and the race of the player.</param>
    /// <returns>The unmet requirements.</returns>
    private static List<ProductionReasonDto> UnmetReasons(ProductionCatalog catalog, RecipeInfo recipe, RequirementContext context)
    {
        List<ProductionReasonDto> reasons = [];

        foreach (ProductionRequirementInfo requirement in catalog.RequirementsOf(recipe.Id))
        {
            switch (requirement.RequirementType)
            {
                case RequirementTypes.BuildingLevel:
                    if (context.BuildingLevels.GetValueOrDefault(requirement.TargetId) < requirement.TargetLevel)
                    {
                        reasons.Add(new ProductionReasonDto(
                            ProductionErrorCodes.RequirementBuildingLevel,
                            requirement.TargetId,
                            requirement.TargetLevel,
                            context.BuildingNameKeys.GetValueOrDefault(requirement.TargetId)));
                    }

                    break;
                case RequirementTypes.TechnologyLevel:
                    if (context.TechnologyLevels.GetValueOrDefault(requirement.TargetId) < requirement.TargetLevel)
                    {
                        reasons.Add(new ProductionReasonDto(
                            ProductionErrorCodes.RequirementTechnology,
                            requirement.TargetId,
                            requirement.TargetLevel,
                            context.TechnologyNameKeys.GetValueOrDefault(requirement.TargetId)));
                    }

                    break;
                case ProductionRequirementTypes.Race:
                    if (context.RaceId != requirement.TargetId)
                    {
                        reasons.Add(new ProductionReasonDto(
                            ProductionErrorCodes.RequirementRace,
                            requirement.TargetId,
                            requirement.TargetLevel,
                            context.RaceNameKeys.GetValueOrDefault(requirement.TargetId)));
                    }

                    break;
                default:
                    reasons.Add(new ProductionReasonDto(
                        ProductionErrorCodes.RequirementUnknown,
                        requirement.TargetId,
                        requirement.TargetLevel));
                    break;
            }
        }

        return reasons;
    }

    /// <summary>
    /// Reads at once what the requirements of the recipes of a body look at, so a factory with
    /// many recipes asks the other modules once and not once for every recipe.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels, the technologies and the race of the player, with their names.</returns>
    private async Task<RequirementContext> RequirementContextAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetOverviewDto? planet = await _planets.GetPlanetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RaceDto> races = await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false);

        return new RequirementContext(
            planet?.Buildings.ToDictionary(building => building.BuildingTypeId, building => building.Level) ?? [],
            await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false),
            await RaceOfAsync(playerId, cancellationToken).ConfigureAwait(false),
            await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false),
            await _technologies.GetTechnologyNameKeysAsync(cancellationToken).ConfigureAwait(false),
            races.ToDictionary(race => race.Id, race => race.NameKey));
    }

    private async Task EnsureOwnedAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(ProductionErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }
    }

    private async Task<int> RaceOfAsync(string playerId, CancellationToken cancellationToken)
    {
        CivilizationSummary? civilization = await _world.GetCivilizationAsync(playerId, cancellationToken).ConfigureAwait(false);

        return civilization?.RaceId ?? 0;
    }

    private async Task<int> SectorOfAsync(long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        return facts?.SectorId ?? 0;
    }

    /// <summary>
    /// The storage of the parts as the player sees it: the room comes from the buildings of the
    /// body as they are now, so a new level of the storage shows at once. Every kind of part has
    /// the same room of its own, so the answer names how many parts every kind holds against it.
    /// </summary>
    private async Task<ProductionStorageDto> StorageDtoAsync(long planetId, CancellationToken cancellationToken)
    {
        long capacity = await _planets.GetBuildingEffectTotalAsync(planetId, BuildingEffectTypes.PartStorageCapacity, cancellationToken).ConfigureAwait(false);

        var quantities = await _dbContext.PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == planetId)
            .GroupBy(stack => stack.PartTypeId)
            .Select(group => new { PartTypeId = group.Key, Quantity = group.Sum(stack => stack.Quantity) })
            .OrderBy(kind => kind.PartTypeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProductionPartKindDto> kinds =
        [
            .. quantities.Select(kind => new ProductionPartKindDto(kind.PartTypeId, kind.Quantity, capacity, kind.Quantity > 0 && kind.Quantity >= capacity)),
        ];

        return new ProductionStorageDto(capacity, kinds);
    }

    /// <summary>
    /// The amounts of one kind of the ingredients of a batch, with a check that the amounts fit
    /// a number, so a huge batch is refused instead of turning into a negative cost.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="quantity">How many units the batch holds.</param>
    /// <param name="key">The resource or the part of an ingredient, or <see langword="null"/> for the other kind.</param>
    /// <returns>The amounts by resource or by part.</returns>
    private static Dictionary<int, long> Costs(RecipeInfo recipe, long quantity, Func<IngredientInfo, int?> key)
    {
        try
        {
            return recipe.IngredientIds
                .Where(ingredient => key(ingredient) is not null)
                .GroupBy(ingredient => key(ingredient)!.Value)
                .ToDictionary(group => group.Key, group => group.Sum(ingredient => checked(ingredient.Amount * quantity)));
        }
        catch (OverflowException)
        {
            throw new GameException(ProductionErrorCodes.QuantityInvalid, "The batch is too large.");
        }
    }

    /// <summary>
    /// Refuses a batch whose end does not fit the calendar of the game.
    /// </summary>
    private static void CheckDuration(DateTimeOffset now, long unitSeconds, long quantity)
    {
        try
        {
            _ = now.AddSeconds(checked(unitSeconds * quantity));
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new GameException(ProductionErrorCodes.QuantityInvalid, "The batch is too large.");
        }
    }

    /// <summary>
    /// Takes the parts of the cost of a batch from the new stacks of the body: the plain parts
    /// first, then the ones with the mark of a race. A damaged part is never an ingredient.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="storage">The locked storage of the parts of the body.</param>
    /// <param name="needed">The parts by type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The cost rows, one for every kind of stack the parts came from.</returns>
    private async Task<List<ProductionOrderCostEntity>> TakePartsAsync(
        long planetId,
        PartStorageEntity storage,
        IReadOnlyDictionary<int, long> needed,
        CancellationToken cancellationToken)
    {
        if (needed.Count == 0)
        {
            return [];
        }

        int[] partTypeIds = [.. needed.Keys];
        List<PartStockEntity> stacks = await _dbContext.PartStocks
            .Where(stack => stack.PlanetId == planetId
                && partTypeIds.Contains(stack.PartTypeId)
                && stack.ConditionPercent == PartCondition.Full)
            .OrderBy(stack => stack.HasRaceBonus)
            .ThenBy(stack => stack.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<int, long> left = new(needed);
        Dictionary<(int PartTypeId, bool HasRaceBonus), long> taken = [];

        foreach (PartStockEntity stack in stacks)
        {
            long amount = Math.Min(stack.Quantity, left[stack.PartTypeId]);

            if (amount <= 0)
            {
                continue;
            }

            stack.Quantity -= amount;
            left[stack.PartTypeId] -= amount;
            taken[(stack.PartTypeId, stack.HasRaceBonus)] = taken.GetValueOrDefault((stack.PartTypeId, stack.HasRaceBonus)) + amount;
        }

        if (left.Values.Any(missing => missing > 0))
        {
            throw new GameException(ProductionErrorCodes.PartMissing, "The body lacks the parts for the cost of the batch.");
        }

        foreach (PartStockEntity stack in stacks.Where(stack => stack.Quantity == 0))
        {
            _dbContext.PartStocks.Remove(stack);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The parts left the storage, so the factories of the body that wait for room may go on.
        await WakeBlockedAsync(planetId, cancellationToken).ConfigureAwait(false);

        return
        [
            .. taken.OrderBy(entry => entry.Key.PartTypeId).ThenBy(entry => entry.Key.HasRaceBonus).Select(entry => new ProductionOrderCostEntity
            {
                PartTypeId = entry.Key.PartTypeId,
                HasRaceBonus = entry.Key.HasRaceBonus,
                Amount = entry.Value,
            }),
        ];
    }

    /// <summary>
    /// Gives back the cost of the units a cancelled batch has not made: the resources to the
    /// balance of the body and the parts to the stacks they came from.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="storage">The locked storage of the parts of the body.</param>
    /// <param name="costs">What the batch paid.</param>
    /// <param name="quantity">How many units the batch held.</param>
    /// <param name="produced">How many units the batch has made.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the cost is given back.</returns>
    private async Task GiveBackAsync(
        long planetId,
        string playerId,
        PartStorageEntity storage,
        IReadOnlyList<ProductionOrderCostEntity> costs,
        long quantity,
        long produced,
        CancellationToken cancellationToken)
    {
        Dictionary<int, long> resourceAmounts = [];

        foreach (ProductionOrderCostEntity cost in costs.Where(cost => cost.ResourceTypeId is not null))
        {
            int resourceTypeId = cost.ResourceTypeId!.Value;
            resourceAmounts[resourceTypeId] = resourceAmounts.GetValueOrDefault(resourceTypeId) + Unused(cost.Amount, quantity, produced);
        }

        resourceAmounts = resourceAmounts.Where(entry => entry.Value > 0).ToDictionary(entry => entry.Key, entry => entry.Value);

        if (resourceAmounts.Count > 0)
        {
            // The input comes back where it was taken from: the credits to the balance of the
            // civilization and the rest to the balance of the body (task 00015).
            SplitCosts split = await _costs.SplitAsync(resourceAmounts, cancellationToken).ConfigureAwait(false);

            await _resources.GiveAsync(planetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);
            await _economy.GiveAsync(playerId, split.Currencies, cancellationToken).ConfigureAwait(false);
        }

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        foreach (IGrouping<int, ProductionOrderCostEntity> partCosts in costs.Where(cost => cost.PartTypeId is not null).GroupBy(cost => cost.PartTypeId!.Value))
        {
            // The units made used the plain parts first, like the payment took them, so what is
            // left of the parts goes back to the stacks it came from.
            long used = partCosts.Sum(cost => cost.Amount) - Unused(partCosts.Sum(cost => cost.Amount), quantity, produced);

            foreach (ProductionOrderCostEntity cost in partCosts.OrderBy(cost => cost.HasRaceBonus))
            {
                long consumed = Math.Min(cost.Amount, used);
                used -= consumed;

                if (cost.Amount - consumed > 0)
                {
                    await AddPartsAsync(
                        planetId,
                        storage,
                        partCosts.Key,
                        catalog.FindPart(partCosts.Key)?.MarkLevel ?? PartMarkLevels.First,
                        cost.HasRaceBonus,
                        cost.Amount - consumed,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// The part of a paid amount that belongs to the units a batch has not made.
    /// </summary>
    private static long Unused(long amount, long quantity, long produced) =>
        quantity <= 0 ? 0 : amount / quantity * (quantity - produced);

    private async Task AddPartsAsync(
        long planetId,
        PartStorageEntity storage,
        int partTypeId,
        int markLevel,
        bool hasRaceBonus,
        long amount,
        CancellationToken cancellationToken)
    {
        // The new parts of one kind are one stack; a damaged part is a row of its own and is
        // never added to (task 00013, decision 2.13).
        PartStockEntity? stack = await _dbContext.PartStocks
            .Where(entry => entry.PlanetId == planetId
                && entry.PartTypeId == partTypeId
                && entry.MarkLevel == markLevel
                && entry.HasRaceBonus == hasRaceBonus
                && entry.ConditionPercent == PartCondition.Full)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stack is null)
        {
            stack = new PartStockEntity
            {
                PlanetId = planetId,
                SectorId = storage.SectorId,
                PartTypeId = partTypeId,
                MarkLevel = markLevel,
                HasRaceBonus = hasRaceBonus,
                ConditionPercent = PartCondition.Full,
                Quantity = 0,
            };

            _dbContext.PartStocks.Add(stack);
        }

        stack.Quantity += amount;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Locks the queue of a factory and the storage of the parts of its body, in this order in
    /// every change, so two changes of one body never wait for each other in a circle. The room
    /// of the storage is read again from the buildings of the body (task 00013, decision 9).
    /// </summary>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The locked storage of the parts of the body.</returns>
    private async Task<PartStorageEntity> OpenAsync(long planetId, long planetBuildingId, CancellationToken cancellationToken)
    {
        // The rows are created without a conflict first, so the first parallel commands of a
        // factory wait for one row instead of failing on a duplicate key.
        await _dbContext.Database
            .ExecuteSqlAsync($"INSERT INTO production_queue_state (planet_id, planet_building_id, extra_queue_slots) VALUES ({planetId}, {planetBuildingId}, 0) ON CONFLICT (planet_id, planet_building_id) DO NOTHING", cancellationToken)
            .ConfigureAwait(false);

        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM production_queue_state WHERE planet_id = {planetId} AND planet_building_id = {planetBuildingId} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        int sectorId = await SectorOfAsync(planetId, cancellationToken).ConfigureAwait(false);

        await _dbContext.Database
            .ExecuteSqlAsync($"INSERT INTO part_storage (planet_id, sector_id, capacity) VALUES ({planetId}, {sectorId}, 0) ON CONFLICT (planet_id) DO NOTHING", cancellationToken)
            .ConfigureAwait(false);

        PartStorageEntity storage = (await _dbContext.PartStorages
            .FromSql($"SELECT * FROM part_storage WHERE planet_id = {planetId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).Single();

        storage.Capacity = await _planets.GetBuildingEffectTotalAsync(planetId, BuildingEffectTypes.PartStorageCapacity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return storage;
    }

    /// <summary>
    /// Reads the batches of every factory of a body for a read, in the order of the queues:
    /// the batch under way of every factory first, then the ones that wait.
    /// </summary>
    private async Task<List<ProductionOrderEntity>> LoadPlanetOrdersAsync(long planetId, CancellationToken cancellationToken) =>
        await _dbContext.ProductionOrders.AsNoTracking()
            .Where(item => item.PlanetId == planetId)
            .OrderBy(item => item.Position)
            .ThenBy(item => item.PlanetBuildingId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<List<ProductionOrderEntity>> LoadQueueAsync(long planetId, long planetBuildingId, CancellationToken cancellationToken)
    {
        if (planetBuildingId <= 0)
        {
            return [];
        }

        return
        [
            .. await _dbContext.ProductionOrders
                .Include(item => item.CostIds)
                .Where(item => item.PlanetId == planetId && item.PlanetBuildingId == planetBuildingId)
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
        ];
    }

    /// <summary>
    /// Makes the due units of the queue of a factory one by one, each at its own moment.
    /// </summary>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="queue">The locked queue of the factory.</param>
    /// <param name="storage">The locked storage of the parts of the body.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the units that were made.</returns>
    /// <remarks>
    /// A unit waits while the storage is full and is made at the moment the room is there, so the
    /// next unit takes its whole time from that moment (task 00013, decision 2.4).
    /// </remarks>
    private async Task<int> CompleteDueAsync(
        string playerId,
        long planetId,
        List<ProductionOrderEntity> queue,
        PartStorageEntity storage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int made = 0;

        while (queue.Count > 0)
        {
            ProductionOrderEntity head = queue[0];

            if (head.NextUnitAt is null)
            {
                if (!await StartNextAsync(playerId, queue, now, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            if (head.NextUnitAt > now)
            {
                break;
            }

            ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
            RecipeInfo? recipe = catalog.FindRecipe(head.RecipeId);

            if (recipe is null)
            {
                break;
            }

            if (!await HasRoomAsync(playerId, planetId, storage, recipe, cancellationToken).ConfigureAwait(false))
            {
                // The storage is full, so the production waits for the place to free up.
                head.IsBlocked = true;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;
            }

            DateTimeOffset readyAt = head.IsBlocked ? now : head.NextUnitAt.Value;

            await AddResultAsync(playerId, planetId, storage, catalog, recipe, cancellationToken).ConfigureAwait(false);
            head.ProducedQuantity++;
            head.IsBlocked = false;
            made++;

            if (head.ProducedQuantity >= head.Quantity)
            {
                queue.RemoveAt(0);
                _dbContext.ProductionOrders.Remove(head);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await StartNextAsync(playerId, queue, readyAt, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                long unitSeconds = await ScaledSecondsAsync(playerId, recipe, cancellationToken).ConfigureAwait(false);
                head.NextUnitAt = readyAt.AddSeconds(unitSeconds);
                head.FinishesAt = head.NextUnitAt.Value.AddSeconds(unitSeconds * (head.Quantity - head.ProducedQuantity - 1));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

        return made;
    }

    /// <summary>
    /// Whether the storage the product goes to has room for one unit of the recipe: the room of
    /// the kind of the part for a part, the room of the resource itself for a produced resource.
    /// Every kind of part and every resource has its own room (task 00013, decisions 2.4 and 2.9;
    /// per kind and per resource from 31.10.2026).
    /// </summary>
    private async Task<bool> HasRoomAsync(
        string playerId,
        long planetId,
        PartStorageEntity storage,
        RecipeInfo recipe,
        CancellationToken cancellationToken)
    {
        if (recipe.PartTypeId is { } partTypeId)
        {
            long kindQuantity = await _dbContext.PartStocks.AsNoTracking()
                .Where(stack => stack.PlanetId == planetId && stack.PartTypeId == partTypeId)
                .SumAsync(stack => stack.Quantity, cancellationToken)
                .ConfigureAwait(false);

            return kindQuantity + recipe.OutputQuantity <= storage.Capacity;
        }

        if (recipe.ResourceTypeId is not { } resourceTypeId)
        {
            return true;
        }

        PlanetOverviewDto? planet = await _planets.GetPlanetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        PlanetResourceDto? resource = planet?.Resources.FirstOrDefault(entry => entry.ResourceTypeId == resourceTypeId);

        // The one rule of a zero capacity (task 00020, section 7.9), the same as in the economy
        // of the body: a group without a storage has no limit, a group with one stops the
        // production when the next unit does not fit.
        return resource is null || resource.Capacity <= 0 || resource.Amount + recipe.OutputQuantity <= resource.Capacity;
    }

    /// <summary>
    /// Adds one unit of a recipe to the body: a new part to its stack with the mark of the race
    /// when the race has a bonus in the group of the part, or a produced resource to the balance.
    /// </summary>
    private async Task AddResultAsync(
        string playerId,
        long planetId,
        PartStorageEntity storage,
        ProductionCatalog catalog,
        RecipeInfo recipe,
        CancellationToken cancellationToken)
    {
        if (recipe.ResourceTypeId is { } resourceTypeId)
        {
            await _resources.GiveAsync(
                planetId,
                playerId,
                new Dictionary<int, long> { [resourceTypeId] = recipe.OutputQuantity },
                cancellationToken).ConfigureAwait(false);
        }

        if (recipe.PartTypeId is not { } partTypeId)
        {
            return;
        }

        PartTypeInfo? part = catalog.FindPart(partTypeId);
        bool hasBonus = await HasRaceBonusAsync(playerId, part?.PartGroupId ?? 0, cancellationToken).ConfigureAwait(false);

        await AddPartsAsync(
            planetId,
            storage,
            partTypeId,
            part?.MarkLevel ?? PartMarkLevels.First,
            hasBonus,
            recipe.OutputQuantity,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> HasRaceBonusAsync(string playerId, int partGroupId, CancellationToken cancellationToken)
    {
        if (partGroupId <= 0)
        {
            return false;
        }

        int raceId = await RaceOfAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (raceId <= 0)
        {
            return false;
        }

        return await _dbContext.RacePartBonuses.AsNoTracking()
            .AnyAsync(bonus => bonus.PartGroupId == partGroupId && bonus.RaceId == raceId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The time of one unit of a recipe for a player: the time of the recipe times the
    /// multiplier of the technologies, which is less than one for a faster production
    /// (task 00013, decision 14).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The time of one unit, in game seconds.</returns>
    private async Task<long> ScaledSecondsAsync(string playerId, RecipeInfo recipe, CancellationToken cancellationToken) =>
        ScaledSeconds(recipe, await _multipliers.GetMultipliersAsync(playerId, cancellationToken).ConfigureAwait(false));

    private static long ScaledSeconds(RecipeInfo recipe, ProductionMultipliers multipliers)
    {
        long rounded = (long)decimal.Ceiling(recipe.DurationSeconds * multipliers.ProductionTimeMultiplier);

        return rounded > 0 ? rounded : 1;
    }

    private async Task<bool> StartNextAsync(string playerId, List<ProductionOrderEntity> queue, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0].NextUnitAt is not null)
        {
            return false;
        }

        ProductionOrderEntity head = queue[0];
        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        RecipeInfo? recipe = catalog.FindRecipe(head.RecipeId);

        if (recipe is null)
        {
            return false;
        }

        long seconds = await ScaledSecondsAsync(playerId, recipe, cancellationToken).ConfigureAwait(false);
        head.StartsAt = now;
        head.NextUnitAt = now.AddSeconds(seconds);

        // The end of the batch is the end of its last unit, so it moves with every unit that is
        // made.
        head.FinishesAt = head.NextUnitAt.Value.AddSeconds(seconds * Math.Max(head.Quantity - 1, 0));
        head.IsBlocked = false;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Schedules the event of the next unit of the batch under way. The key names the batch and
    /// the unit, so a repeated call never doubles the event, every unit has its own event, and an
    /// event of a cancelled batch or of a unit already made finds nothing due.
    /// </summary>
    private async Task ScheduleHeadAsync(List<ProductionOrderEntity> queue, CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0] is not { NextUnitAt: { } nextUnitAt, IsBlocked: false } head)
        {
            return;
        }

        await ScheduleUnitAsync(head, nextUnitAt, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScheduleUnitAsync(ProductionOrderEntity order, DateTimeOffset runAt, CancellationToken cancellationToken)
    {
        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                order.SectorId,
                CompleteEventType,
                runAt,
                JsonSerializer.Serialize(new CompletePayload(order.PlanetId, order.PlanetBuildingId)),
                CompleteEventType
                    + ":" + order.Id.ToString(CultureInfo.InvariantCulture)
                    + ":" + order.ProducedQuantity.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Wakes the batches of a body that wait for room in the storage, now that the storage has
    /// some: their unit is made by an event at this moment (task 00013, decision 2.4).
    /// </summary>
    private async Task WakeBlockedAsync(long planetId, CancellationToken cancellationToken)
    {
        List<ProductionOrderEntity> blocked = await _dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.PlanetId == planetId && order.IsBlocked)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (ProductionOrderEntity order in blocked)
        {
            await ScheduleUnitAsync(order, _clock.GameNow, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Renumber(List<ProductionOrderEntity> queue)
    {
        for (int index = 0; index < queue.Count; index++)
        {
            queue[index].Position = index + 1;
        }
    }

    /// <summary>
    /// The places of the queue of a factory: the places of the settings and the paid ones
    /// (task 00013, decision 2.5).
    /// </summary>
    /// <param name="settings">The parameters of the production.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the places.</returns>
    private async Task<int> SlotsAsync(
        ProductionSettings settings,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken)
    {
        ProductionQueueStateEntity? state = await _dbContext.ProductionQueueStates.AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.PlanetId == planetId && entry.PlanetBuildingId == planetBuildingId,
                cancellationToken)
            .ConfigureAwait(false);

        return settings.QueueBaseSlots + Math.Clamp(state?.ExtraQueueSlots ?? 0, 0, settings.QueueMaxExtraSlots);
    }

    /// <summary>
    /// The payload of the readiness of a unit.
    /// </summary>
    /// <param name="PlanetId">The body the factory stands on.</param>
    /// <param name="PlanetBuildingId">The factory.</param>
    public sealed record CompletePayload(long PlanetId, long PlanetBuildingId);

    /// <summary>
    /// What the requirements of the recipes of a body look at, read once.
    /// </summary>
    /// <param name="BuildingLevels">The levels of the buildings of the body by type.</param>
    /// <param name="TechnologyLevels">The levels of the technologies of the player.</param>
    /// <param name="RaceId">The race of the player.</param>
    /// <param name="BuildingNameKeys">The keys of the names of the building types.</param>
    /// <param name="TechnologyNameKeys">The keys of the names of the technologies.</param>
    /// <param name="RaceNameKeys">The keys of the names of the races.</param>
    private sealed record RequirementContext(
        IReadOnlyDictionary<int, int> BuildingLevels,
        IReadOnlyDictionary<int, int> TechnologyLevels,
        int RaceId,
        IReadOnlyDictionary<int, string> BuildingNameKeys,
        IReadOnlyDictionary<int, string> TechnologyNameKeys,
        IReadOnlyDictionary<int, string> RaceNameKeys);
}
