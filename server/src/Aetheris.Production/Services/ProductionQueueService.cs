using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
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
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _planets = planets;
        _resources = resources;
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
                await LockStateAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);

                // What is due is made first, so a finished batch never holds a place.
                await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                if (quantity <= 0)
                {
                    throw new GameException(ProductionErrorCodes.QuantityInvalid, "The batch must hold at least one unit.");
                }

                ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
                RecipeInfo recipe = catalog.FindRecipe(recipeId)
                    ?? throw new GameException(ProductionErrorCodes.RecipeUnknown, "The recipe does not exist.");

                // An archived recipe keeps working for the batches that already use it, but no
                // new batch of it starts (task 00023, decision 5).
                if (recipe.IsArchived)
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

                await CheckAvailableAsync(playerId, planetId, recipe, cancellationToken).ConfigureAwait(false);

                // The ingredients are paid when the batch enters the queue and returned in
                // full when it leaves it.
                Dictionary<int, long> resourceCosts = ResourceCosts(recipe, quantity);
                await _resources.TakeAsync(planetId, playerId, resourceCosts, cancellationToken).ConfigureAwait(false);

                Dictionary<int, long> partCosts = PartCosts(recipe, quantity);

                if (partCosts.Count > 0)
                {
                    await TakePartsAsync(planetId, partCosts, cancellationToken).ConfigureAwait(false);
                }

                DateTimeOffset now = _clock.GameNow;
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
                        .. recipe.IngredientIds
                            .Where(ingredient => ingredient.ResourceTypeId is not null)
                            .Select(ingredient => new ProductionOrderCostEntity
                            {
                                ResourceTypeId = ingredient.ResourceTypeId,
                                Amount = ingredient.Amount * quantity,
                            }),
                        .. recipe.IngredientIds
                            .Where(ingredient => ingredient.PartTypeId is not null)
                            .Select(ingredient => new ProductionOrderCostEntity
                            {
                                PartTypeId = ingredient.PartTypeId,
                                Amount = ingredient.Amount * quantity,
                            }),
                    ],
                };

                _dbContext.ProductionOrders.Add(item);
                queue.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await StartNextAsync(playerId, queue, now, cancellationToken).ConfigureAwait(false);

                return await ReadFactoryAsync(playerId, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a batch from the queue of a factory and returns its whole cost.
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
                await LockStateAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                ProductionOrderEntity cancelled = queue.FirstOrDefault(entry => entry.Id == orderId)
                    ?? throw new GameException(ProductionErrorCodes.OrderUnknown, "The batch of the queue does not exist.");

                bool headRemoved = queue[0].Id == cancelled.Id;

                // The costs are read before the record is deleted, since a deleted record
                // loses its cost rows.
                List<ProductionOrderCostEntity> costs = [.. cancelled.CostIds];

                queue.Remove(cancelled);
                _dbContext.ProductionOrders.Remove(cancelled);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await GiveBackAsync(planetId, playerId, costs, cancellationToken).ConfigureAwait(false);

                if (headRemoved)
                {
                    await StartNextAsync(playerId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);
                }

                return await ReadFactoryAsync(playerId, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Completes every batch of a factory that is due at a moment.
    /// </summary>
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
                await LockStateAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);

                return await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, now, cancellationToken).ConfigureAwait(false);
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
    public async Task<ProductionFactoryDto> ReadFactoryAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await CompleteDueAsync(playerId, planetId, planetBuildingId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        List<ProductionOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
        int factoryTypeId = await FactoryTypeOfAsync(planetId, planetBuildingId, catalog, cancellationToken).ConfigureAwait(false);
        PlanetBuildingLevelById factory = await _planets.GetBuildingLevelByIdAsync(
            planetId,
            planetBuildingId,
            playerId,
            cancellationToken).ConfigureAwait(false);
        int factoryLevel = factory.BuildingTypeId == factoryTypeId ? factory.Level : 0;

        List<ProductionRecipeDto> recipes = [];

        foreach (RecipeInfo recipe in catalog.Recipes)
        {
            if (factoryTypeId > 0 && recipe.BuildingTypeId != factoryTypeId)
            {
                continue;
            }

            recipes.Add(await RecipeDtoAsync(playerId, planetId, recipe, cancellationToken).ConfigureAwait(false));
        }

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
            factoryLevel,
            recipes,
            orders,
            await StorageDtoAsync(planetId, cancellationToken).ConfigureAwait(false),
            _clock.GameNow);
    }

    /// <summary>
    /// Reads the storage of the parts of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks of the parts and the storage.</returns>
    public async Task<ProductionStockDto> ReadStockAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await CompleteDueAsync(playerId, planetId, planetBuildingId: 0, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        List<PartStockEntity> stacks = await _dbContext.PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == planetId)
            .OrderBy(stack => stack.PartTypeId)
            .ThenBy(stack => stack.MarkLevel)
            .ThenBy(stack => stack.HasRaceBonus)
            .ThenBy(stack => stack.ConditionPercent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProductionStackDto> rows =
        [
            .. stacks.Select(stack => new ProductionStackDto(
                stack.PartTypeId,
                stack.MarkLevel,
                stack.HasRaceBonus,
                stack.ConditionPercent,
                stack.Quantity)),
        ];

        return new ProductionStockDto(
            planetId,
            rows,
            await StorageDtoAsync(planetId, cancellationToken).ConfigureAwait(false),
            _clock.GameNow);
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
        int[] factoryTypeIds = [.. catalog.Recipes
            .Where(recipe => recipe.BuildingTypeId is not null && !recipe.IsArchived)
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

    private async Task<int> FactoryTypeOfAsync(
        long planetId,
        long planetBuildingId,
        ProductionCatalog catalog,
        CancellationToken cancellationToken)
    {
        ProductionOrderEntity? order = await _dbContext.ProductionOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == planetId && entry.PlanetBuildingId == planetBuildingId)
            .OrderBy(entry => entry.Position)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (order is not null)
        {
            return catalog.FindRecipe(order.RecipeId)?.BuildingTypeId ?? 0;
        }

        // A factory with an empty queue has no record of its own, so its type is the type of
        // the building it is: the recipes of that type are the ones it makes.
        PlanetBuildingLevelById factory = await _planets.GetBuildingLevelByIdAsync(
            planetId,
            planetBuildingId,
            null,
            cancellationToken).ConfigureAwait(false);

        return factory.BuildingTypeId;
    }

    private async Task<ProductionRecipeDto> RecipeDtoAsync(
        string playerId,
        long planetId,
        RecipeInfo recipe,
        CancellationToken cancellationToken)
    {
        List<ProductionReasonDto> reasons = await UnmetReasonsAsync(playerId, planetId, recipe, cancellationToken).ConfigureAwait(false);

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
            reasons);
    }

    private async Task<List<ProductionReasonDto>> UnmetReasonsAsync(
        string playerId,
        long planetId,
        RecipeInfo recipe,
        CancellationToken cancellationToken)
    {
        List<ProductionReasonDto> reasons = [];
        IReadOnlyList<RequirementDefinition> requirements = await _requirements.GetAsync(
            RequirementSubjectTypes.Recipe,
            recipe.Id,
            1,
            cancellationToken).ConfigureAwait(false);

        if (requirements.Count == 0)
        {
            return reasons;
        }

        Dictionary<int, int> buildingLevels = await BuildingLevelsAsync(planetId, playerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, int> technologyLevels = await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false);
        int raceId = await RaceOfAsync(playerId, cancellationToken).ConfigureAwait(false);

        foreach (RequirementDefinition requirement in requirements)
        {
            switch (requirement.RequirementType)
            {
                case RequirementTypes.BuildingLevel:
                    if (buildingLevels.GetValueOrDefault(requirement.TargetId) < requirement.TargetLevel)
                    {
                        reasons.Add(new ProductionReasonDto(
                            ProductionErrorCodes.RequirementBuildingLevel,
                            requirement.TargetId,
                            requirement.TargetLevel));
                    }

                    break;
                case RequirementTypes.TechnologyLevel:
                    if (!technologyLevels.TryGetValue(requirement.TargetId, out int technologyLevel) || technologyLevel < requirement.TargetLevel)
                    {
                        reasons.Add(new ProductionReasonDto(
                            ProductionErrorCodes.RequirementTechnology,
                            requirement.TargetId,
                            requirement.TargetLevel));
                    }

                    break;
                case ProductionRequirementTypes.Race:
                    if (raceId != requirement.TargetId)
                    {
                        reasons.Add(new ProductionReasonDto(
                            ProductionErrorCodes.RequirementRace,
                            requirement.TargetId,
                            requirement.TargetLevel));
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

    private async Task CheckAvailableAsync(
        string playerId,
        long planetId,
        RecipeInfo recipe,
        CancellationToken cancellationToken)
    {
        List<ProductionReasonDto> reasons = await UnmetReasonsAsync(playerId, planetId, recipe, cancellationToken).ConfigureAwait(false);

        if (reasons.Count > 0)
        {
            throw new GameException(reasons[0].Code, "The recipe is not available.", new Dictionary<string, object?>
            {
                ["reason_codes"] = reasons.Select(reason => reason.Code).ToArray(),
            });
        }
    }

    private async Task<Dictionary<int, int>> BuildingLevelsAsync(long planetId, string playerId, CancellationToken cancellationToken)
    {
        PlanetOverviewDto? planet = await _planets.GetPlanetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        if (planet is null)
        {
            return [];
        }

        return planet.Buildings.ToDictionary(building => building.BuildingTypeId, building => building.Level);
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

    private async Task<ProductionStorageDto> StorageDtoAsync(long planetId, CancellationToken cancellationToken)
    {
        PartStorageEntity? storage = await _dbContext.PartStorages.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        return new ProductionStorageDto(
            storage?.Capacity ?? 0,
            storage?.Quantity ?? 0,
            storage?.FullAt);
    }

    private static Dictionary<int, long> ResourceCosts(RecipeInfo recipe, long quantity) =>
        recipe.IngredientIds
            .Where(ingredient => ingredient.ResourceTypeId is not null)
            .GroupBy(ingredient => ingredient.ResourceTypeId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(ingredient => ingredient.Amount * quantity));

    private static Dictionary<int, long> PartCosts(RecipeInfo recipe, long quantity) =>
        recipe.IngredientIds
            .Where(ingredient => ingredient.PartTypeId is not null)
            .GroupBy(ingredient => ingredient.PartTypeId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(ingredient => ingredient.Amount * quantity));

    private async Task TakePartsAsync(
        long planetId,
        IReadOnlyDictionary<int, long> needed,
        CancellationToken cancellationToken)
    {
        List<PartStockEntity> stacks = await _dbContext.PartStocks
            .Where(stack => stack.PlanetId == planetId && needed.ContainsKey(stack.PartTypeId))
            .OrderBy(stack => stack.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<int, long> left = new(needed);

        foreach (PartStockEntity stack in stacks)
        {
            if (left.GetValueOrDefault(stack.PartTypeId) <= 0)
            {
                continue;
            }

            long taken = Math.Min(stack.Quantity, left[stack.PartTypeId]);
            stack.Quantity -= taken;
            left[stack.PartTypeId] -= taken;
        }

        foreach ((int partTypeId, long missing) in left)
        {
            if (missing > 0)
            {
                throw new GameException(ProductionErrorCodes.PartMissing, "The body lacks the parts for the cost of the batch.");
            }
        }

        foreach (PartStockEntity stack in stacks.Where(stack => stack.Quantity == 0))
        {
            _dbContext.PartStocks.Remove(stack);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task GiveBackAsync(
        long planetId,
        string playerId,
        IReadOnlyList<ProductionOrderCostEntity> costs,
        CancellationToken cancellationToken)
    {
        Dictionary<int, long> resourceAmounts = new();

        foreach (ProductionOrderCostEntity cost in costs)
        {
            if (cost.ResourceTypeId is { } resourceTypeId)
            {
                resourceAmounts[resourceTypeId] = resourceAmounts.GetValueOrDefault(resourceTypeId) + cost.Amount;
            }
        }

        if (resourceAmounts.Count > 0)
        {
            await _resources.GiveAsync(planetId, playerId, resourceAmounts, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ProductionQueueStateEntity?> LockStateAsync(long planetId, long planetBuildingId, CancellationToken cancellationToken)
    {
        if (planetBuildingId <= 0)
        {
            return null;
        }

        // The row is created without a conflict first, so the first parallel commands of a
        // factory wait for one row instead of failing on a duplicate key.
        await _dbContext.Database
            .ExecuteSqlAsync($"INSERT INTO production_queue_state (planet_id, planet_building_id, extra_queue_slots) VALUES ({planetId}, {planetBuildingId}, 0) ON CONFLICT (planet_id, planet_building_id) DO NOTHING", cancellationToken)
            .ConfigureAwait(false);

        return (await _dbContext.ProductionQueueStates
            .FromSql($"SELECT * FROM production_queue_state WHERE planet_id = {planetId} AND planet_building_id = {planetBuildingId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).Single();
    }

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

    private async Task<int> CompleteDueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        List<ProductionOrderEntity> queue,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (queue.Count == 0)
        {
            return 0;
        }

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

            DateTimeOffset readyAt = head.NextUnitAt.Value;
            ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
            RecipeInfo? recipe = catalog.FindRecipe(head.RecipeId);

            if (recipe is null)
            {
                break;
            }

            if (!await HasRoomAsync(planetId, recipe, cancellationToken).ConfigureAwait(false))
            {
                // The storage is full, so the production waits for the place to free up.
                head.IsBlocked = true;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;
            }

            await AddResultAsync(playerId, planetId, recipe, cancellationToken).ConfigureAwait(false);
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
                head.FinishesAt = head.NextUnitAt.Value.AddSeconds(unitSeconds * Math.Max(head.Quantity - head.ProducedQuantity - 1, 0));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // The moment of the check moves with the unit, so the next unit of the batch is
                // made too when it is due at the same moment.
                now = head.NextUnitAt.Value;
            }
        }

        return made;
    }

    private async Task<bool> HasRoomAsync(long planetId, RecipeInfo recipe, CancellationToken cancellationToken)
    {
        if (recipe.PartTypeId is null)
        {
            // A produced resource goes to the balance of the body, which has its own limits.
            return true;
        }

        PartStorageEntity? storage = await _dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        if (storage is null)
        {
            return false;
        }

        return storage.Quantity < storage.Capacity;
    }

    private async Task AddResultAsync(string playerId, long planetId, RecipeInfo recipe, CancellationToken cancellationToken)
    {
        if (recipe.PartTypeId is not { } partTypeId)
        {
            return;
        }

        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        PartTypeInfo? part = catalog.FindPart(partTypeId);
        int markLevel = part?.MarkLevel ?? 1;
        bool hasBonus = await HasRaceBonusAsync(playerId, part?.PartGroupId ?? 0, cancellationToken).ConfigureAwait(false);

        PartStockEntity? stack = await _dbContext.PartStocks
            .Where(entry => entry.PlanetId == planetId
                && entry.PartTypeId == partTypeId
                && entry.MarkLevel == markLevel
                && entry.HasRaceBonus == hasBonus
                && entry.ConditionPercent == PartCondition.Full)
            .OrderBy(entry => entry.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stack is null)
        {
            stack = new PartStockEntity
            {
                PlanetId = planetId,
                SectorId = await SectorOfAsync(planetId, cancellationToken).ConfigureAwait(false),
                PartTypeId = partTypeId,
                MarkLevel = markLevel,
                HasRaceBonus = hasBonus,
                ConditionPercent = PartCondition.Full,
                Quantity = 0,
            };

            _dbContext.PartStocks.Add(stack);
        }

        stack.Quantity += 1;

        PartStorageEntity? storage = await _dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        if (storage is null)
        {
            storage = new PartStorageEntity
            {
                PlanetId = planetId,
                SectorId = await SectorOfAsync(planetId, cancellationToken).ConfigureAwait(false),
                Capacity = 0,
                Quantity = 0,
            };

            _dbContext.PartStorages.Add(storage);
        }

        storage.Quantity += 1;
        storage.FullAt = storage.Quantity >= storage.Capacity ? _clock.GameNow : null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task<long> UnitSecondsAsync(ProductionOrderEntity order, RecipeInfo recipe, CancellationToken cancellationToken)
    {
        if (order.StartsAt is not null && order.NextUnitAt is not null && order.NextUnitAt > order.StartsAt)
        {
            long elapsed = (long)(order.NextUnitAt.Value - order.StartsAt.Value).TotalSeconds;

            if (elapsed > 0)
            {
                return elapsed;
            }
        }

        long seconds = recipe.DurationSeconds;

        return seconds > 0 ? seconds : 1;
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
    private async Task<long> ScaledSecondsAsync(string playerId, RecipeInfo recipe, CancellationToken cancellationToken)
    {
        ProductionMultipliers multipliers = await _multipliers.GetMultipliersAsync(playerId, cancellationToken).ConfigureAwait(false);
        decimal seconds = recipe.DurationSeconds * multipliers.ProductionTimeMultiplier;
        long rounded = (long)decimal.Ceiling(seconds);

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

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(head.PlanetId, cancellationToken).ConfigureAwait(false);

        // The key of the event names the batch, so a repeated start never doubles the event,
        // and an event of a cancelled batch finds nothing due and changes nothing.
        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                facts?.SectorId ?? 0,
                CompleteEventType,
                head.NextUnitAt.Value,
                JsonSerializer.Serialize(new CompletePayload(head.PlanetId, head.PlanetBuildingId)),
                CompleteEventType + ":" + head.Id.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);

        return true;
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
}
