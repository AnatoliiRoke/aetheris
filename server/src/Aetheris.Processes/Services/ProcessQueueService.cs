using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Services;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Services;

/// <summary>
/// The engine of the processes: the queue of a building, the payment of the cost, the refund
/// when an order is cancelled and the completion of the orders (task 00014, section 3.1).
/// </summary>
/// <remarks>
/// <para>
/// The engine knows no concrete process. A kind of a process names its handler by a logical
/// code, and the engine finds the handler in the container of dependencies by that code. The
/// queue, the payment, the refund and the event of the completion are the same for every
/// process.
/// </para>
/// <para>
/// The order of the locks is fixed: the state of the queue of the building first, then the
/// balance of the resources, then the storage of the parts. Two orders of two bodies never
/// wait for each other.
/// </para>
/// </remarks>
public sealed class ProcessQueueService
{
    /// <summary>
    /// The type of the event of the completion of a unit of a process.
    /// </summary>
    public const string CompleteEventType = "processes.unit_ready";

    private readonly ProcessesDbContext _dbContext;
    private readonly ProcessesUnitOfWork _unitOfWork;
    private readonly ProcessCatalogCache _catalogCache;
    private readonly IPlanetsModule _planets;
    private readonly IPlanetResources _resources;
    private readonly PartStockService _parts;
    private readonly ITechnologyLevels _technologies;
    private readonly IRequirementsModule _requirements;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private readonly IEnumerable<IProcessHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessQueueService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the processes.</param>
    /// <param name="planets">The bodies module, for the buildings and the balance.</param>
    /// <param name="resources">The balance of the resources of a body.</param>
    /// <param name="parts">The storage of the parts of the production module.</param>
    /// <param name="technologies">The levels of the technologies of the player.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="world">The world module, for the owner of a body.</param>
    /// <param name="scheduler">The scheduler of the events.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The clock of the game.</param>
    /// <param name="handlers">The handlers of the kinds of the processes.</param>
    public ProcessQueueService(
        ProcessesDbContext dbContext,
        ProcessesUnitOfWork unitOfWork,
        ProcessCatalogCache catalogCache,
        IPlanetsModule planets,
        IPlanetResources resources,
        PartStockService parts,
        ITechnologyLevels technologies,
        IRequirementsModule requirements,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock,
        IEnumerable<IProcessHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(handlers);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _planets = planets;
        _resources = resources;
        _parts = parts;
        _technologies = technologies;
        _requirements = requirements;
        _world = world;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
        _handlers = handlers;
    }

    /// <summary>
    /// Puts an order of a recipe in the queue of a building. The whole cost of the order is
    /// paid at once, and cancelling it returns the cost in full.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the process runs on.</param>
    /// <param name="planetBuildingId">The building the process runs in.</param>
    /// <param name="recipeId">The recipe of the order.</param>
    /// <param name="request">What the player asked for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue after the change.</returns>
    public Task<ProcessBuildingDto> EnqueueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        int recipeId,
        ProcessOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await LockQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProcessOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);

                // What is due is made first, so a finished order never holds a place.
                await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                if (request.Quantity <= 0)
                {
                    throw new GameException(ProcessErrorCodes.QuantityInvalid, "The order must hold at least one unit.");
                }

                ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
                ProcessRecipeInfo recipe = catalog.FindRecipe(recipeId)
                    ?? throw new GameException(ProcessErrorCodes.RecipeUnknown, "The recipe does not exist.");

                // An archived recipe keeps working for the orders that already use it, but no
                // new order of it starts (task 00023, decision 5).
                if (recipe.IsArchived)
                {
                    throw new GameException(ProcessErrorCodes.RecipeArchived, "The recipe is archived.");
                }

                ProcessTypeInfo? processType = catalog.FindType(recipe.ProcessTypeId);
                if (processType is null)
                {
                    throw new GameException(ProcessErrorCodes.TypeUnknown, "The kind of the process does not exist.");
                }

                if (recipe.BuildingTypeId is not { } buildingTypeId)
                {
                    throw new GameException(ProcessErrorCodes.WrongBuilding, "The process needs no building.");
                }

                // The building is the one the order runs in, so its level is the level of
                // that building, and the recipe names the type it must be of.
                PlanetBuildingLevelById building = await _planets.GetBuildingLevelByIdAsync(
                    planetId,
                    planetBuildingId,
                    playerId,
                    cancellationToken).ConfigureAwait(false);

                if (building.Level <= 0 || building.BuildingTypeId != buildingTypeId)
                {
                    throw new GameException(ProcessErrorCodes.BuildingMissing, "The body has no building of the kind the process needs.");
                }

                ProcessesSettings settings = ProcessesSettings.From(_config);

                if (queue.Count >= await SlotsAsync(settings, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(ProcessErrorCodes.QueueFull, "The queue of the building has no free place.");
                }

                IProcessHandler handler = Handler(processType);

                ProcessRequirementContext requirementContext = await RequirementContextAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<ProcessReason> reasons = await handler.CheckAsync(
                    new ProcessCheckContext(
                        playerId,
                        planetId,
                        planetBuildingId,
                        recipe,
                        processType,
                        request.Quantity,
                        catalog.RequirementsOf(recipe.Id),
                        requirementContext,
                        request,
                        await ResourceGroupsAsync(request, cancellationToken).ConfigureAwait(false)),
                    cancellationToken).ConfigureAwait(false);

                IReadOnlyList<ProcessReason> requirementReasons = UnmetReasons(catalog, recipe, requirementContext);

                if (requirementReasons.Count > 0)
                {
                    throw new GameException(requirementReasons[0].Code, "A requirement of the process is not met.");
                }

                if (reasons.Count > 0)
                {
                    throw new GameException(reasons[0].Code, "The process cannot start.");
                }

                // The cost is paid when the order enters the queue and returned in full when
                // it leaves it.
                Dictionary<int, long> resourceCosts = ResourceCosts(recipe, request.Quantity);
                await _resources.TakeAsync(planetId, playerId, resourceCosts, cancellationToken).ConfigureAwait(false);

                List<ProcessOrderCostEntity> partCosts = await TakePartsAsync(planetId, recipe, request.Quantity, cancellationToken).ConfigureAwait(false);

                DateTimeOffset now = _clock.GameNow;
                ProcessOrderEntity item = new()
                {
                    PlanetId = planetId,
                    PlanetBuildingId = planetBuildingId,
                    SectorId = await SectorOfAsync(planetId, cancellationToken).ConfigureAwait(false),
                    ProcessRecipeId = recipeId,
                    Quantity = request.Quantity,
                    ProducedQuantity = 0,
                    Position = queue.Count + 1,
                    CreatedAt = now,
                    CostIds =
                    [
                        .. resourceCosts.Select(cost => new ProcessOrderCostEntity
                        {
                            ResourceTypeId = cost.Key,
                            Amount = cost.Value,
                        }),
                        .. partCosts,
                    ],
                };

                _dbContext.ProcessOrders.Add(item);
                queue.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await StartNextAsync(queue, now, cancellationToken).ConfigureAwait(false);
                await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

                return await ReadBuildingAsync(playerId, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes an order from the queue of a building and returns its cost, unless the kind of
    /// the process does not refund it (task 00014, decision 2).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the process runs on.</param>
    /// <param name="planetBuildingId">The building the process runs in.</param>
    /// <param name="orderId">The order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue after the change.</returns>
    public Task<ProcessBuildingDto> CancelAsync(
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
                await LockQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProcessOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                ProcessOrderEntity cancelled = queue.FirstOrDefault(entry => entry.Id == orderId)
                    ?? throw new GameException(ProcessErrorCodes.OrderUnknown, "The order of the queue does not exist.");

                bool headRemoved = queue[0].Id == cancelled.Id;

                // The costs are read before the record is deleted, since a deleted record
                // loses its cost rows.
                List<ProcessOrderCostEntity> costs = [.. cancelled.CostIds];

                queue.Remove(cancelled);
                _dbContext.ProcessOrders.Remove(cancelled);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
                ProcessRecipeInfo? recipe = catalog.FindRecipe(cancelled.ProcessRecipeId);
                ProcessTypeInfo? processType = recipe is null ? null : catalog.FindType(recipe.ProcessTypeId);

                // A kind of a process that does not refund its input loses the device when
                // the order is cancelled, so the data of the kind decides it.
                if (processType?.RefundsInput ?? true)
                {
                    await GiveBackAsync(planetId, playerId, costs, cancellationToken).ConfigureAwait(false);
                }

                if (headRemoved)
                {
                    await StartNextAsync(queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);
                }

                await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

                return await ReadBuildingAsync(playerId, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Completes every order of a building that is due at a moment.
    /// </summary>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body the process runs on.</param>
    /// <param name="planetBuildingId">The building the process runs in.</param>
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

                await LockQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
                List<ProcessOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);

                return await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, now, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Reads a building with the recipes of the processes it runs, its queue and the reasons
    /// why a recipe is closed.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the building stands on.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue.</returns>
    public async Task<ProcessBuildingDto> ReadBuildingAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await CompleteDueAsync(playerId, planetId, planetBuildingId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        List<ProcessOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
        PlanetBuildingLevelById building = await _planets.GetBuildingLevelByIdAsync(
            planetId,
            planetBuildingId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        ProcessRequirementContext requirementContext = await RequirementContextAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        List<ProcessRecipeDto> recipes = [];

        foreach (ProcessRecipeInfo recipe in catalog.Recipes)
        {
            ProcessTypeInfo? processType = catalog.FindType(recipe.ProcessTypeId);

            if (processType is null
                || recipe.BuildingTypeId != building.BuildingTypeId
                || recipe.IsArchived)
            {
                continue;
            }

            recipes.Add(await RecipeDtoAsync(catalog, recipe, processType, requirementContext, cancellationToken).ConfigureAwait(false));
        }

        List<ProcessOrderDto> orders =
        [
            .. queue.Select(order => new ProcessOrderDto(
                order.Id,
                order.ProcessRecipeId,
                order.Quantity,
                order.ProducedQuantity,
                order.Position,
                order.StartsAt,
                order.NextUnitAt,
                order.FinishesAt,
                order.IsBlocked))
        ];

        return new ProcessBuildingDto(
            planetId,
            planetBuildingId,
            building.BuildingTypeId,
            building.Level,
            recipes,
            orders,
            _clock.GameNow);
    }

    /// <summary>
    /// Reads every building of a body that runs a process.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The buildings of the body that run a process.</returns>
    public async Task<IReadOnlyList<ProcessBuildingSummaryDto>> ReadBuildingsAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        // The types of the buildings are the types the recipes of the catalog name, so a new
        // recipe opens a new building without a change in the code.
        int[] buildingTypeIds = [.. catalog.Recipes
            .Where(recipe => recipe.BuildingTypeId is not null && !recipe.IsArchived)
            .Select(recipe => recipe.BuildingTypeId!.Value)
            .Distinct()
            .OrderBy(typeId => typeId)];

        if (buildingTypeIds.Length == 0)
        {
            return [];
        }

        IReadOnlyList<PlanetBuildingLevelById> buildings = await _planets.GetBuildingsByTypesAsync(
            planetId,
            buildingTypeIds,
            playerId,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<int, string> nameKeys = await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. buildings.Select(building => new ProcessBuildingSummaryDto(
                planetId,
                building.PlanetBuildingId,
                building.BuildingTypeId,
                building.Level,
                nameKeys.GetValueOrDefault(building.BuildingTypeId, string.Empty)))
        ];
    }

    /// <summary>
    /// Finds the handler of a kind of a process by its logical code.
    /// </summary>
    /// <param name="processType">The kind of the process.</param>
    /// <returns>The handler of the kind.</returns>
    /// <exception cref="GameException">No handler is registered for the kind.</exception>
    private IProcessHandler Handler(ProcessTypeInfo processType) =>
        _handlers.FirstOrDefault(handler => string.Equals(handler.HandlerCode, processType.HandlerCode, StringComparison.Ordinal))
        ?? throw new GameException(ProcessErrorCodes.TypeUnknown, $"No handler is registered for the process '{processType.HandlerCode}'.");

    /// <summary>
    /// Completes the orders of a queue that are due at a moment, one unit at a time.
    /// </summary>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body the process runs on.</param>
    /// <param name="planetBuildingId">The building the process runs in.</param>
    /// <param name="queue">The queue of the building.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the units that were made.</returns>
    private async Task<int> CompleteDueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        List<ProcessOrderEntity> queue,
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
            ProcessOrderEntity head = queue[0];

            if (head.NextUnitAt is null)
            {
                if (!await StartNextAsync(queue, now, cancellationToken).ConfigureAwait(false))
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
            ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
            ProcessRecipeInfo? recipe = catalog.FindRecipe(head.ProcessRecipeId);

            if (recipe is null)
            {
                break;
            }

            ProcessTypeInfo? processType = catalog.FindType(recipe.ProcessTypeId);

            if (processType is null)
            {
                break;
            }

            IProcessHandler handler = Handler(processType);
            PlanetBuildingLevelById building = await _planets.GetBuildingLevelByIdAsync(
                planetId,
                planetBuildingId,
                playerId,
                cancellationToken).ConfigureAwait(false);

            IReadOnlyDictionary<int, int> technologyLevels = await TechnologyLevelsAsync(playerId, cancellationToken).ConfigureAwait(false);

            ProcessApplyContext context = new(
                playerId,
                planetId,
                planetBuildingId,
                recipe,
                processType,
                1,
                building.Level,
                technologyLevels,
                catalog,
                new ProcessOrderRequest(head.Quantity));

            ProcessResult result = await handler.ComputeAsync(context, cancellationToken).ConfigureAwait(false);
            await handler.ApplyAsync(context with { Quantity = 1 }, cancellationToken).ConfigureAwait(false);

            if (result.Resources.Count > 0)
            {
                await _resources.GiveAsync(planetId, playerId, result.Resources, cancellationToken).ConfigureAwait(false);
            }

            if (result.Parts.Count > 0)
            {
                await AddPartsAsync(planetId, result.Parts, cancellationToken).ConfigureAwait(false);
            }

            if (result.Crew > 0)
            {
                await AddCrewAsync(planetId, result.Crew, cancellationToken).ConfigureAwait(false);
            }

            head.ProducedQuantity++;
            head.IsBlocked = false;
            made++;

            if (head.ProducedQuantity >= head.Quantity || result.EndsProcess)
            {
                queue.RemoveAt(0);
                _dbContext.ProcessOrders.Remove(head);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await StartNextAsync(queue, readyAt, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                long unitSeconds = await UnitSecondsAsync(context, cancellationToken).ConfigureAwait(false);
                head.NextUnitAt = readyAt.AddSeconds(unitSeconds);
                head.FinishesAt = head.NextUnitAt.Value.AddSeconds(unitSeconds * Math.Max(head.Quantity - head.ProducedQuantity - 1, 0));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // The moment of the check moves with the unit, so the next unit of the order
                // is made too when it is due at the same moment.
                now = head.NextUnitAt.Value;
            }
        }

        return made;
    }

    /// <summary>
    /// Starts the first order of the queue, if it is not started yet.
    /// </summary>
    /// <param name="queue">The queue of the building.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether the queue was started.</returns>
    private async Task<bool> StartNextAsync(
        List<ProcessOrderEntity> queue,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0].NextUnitAt is not null)
        {
            return false;
        }

        ProcessOrderEntity head = queue[0];
        ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        ProcessRecipeInfo? recipe = catalog.FindRecipe(head.ProcessRecipeId);

        if (recipe is null)
        {
            return false;
        }

        ProcessApplyContext context = new(
            string.Empty,
            head.PlanetId,
            head.PlanetBuildingId,
            recipe,
            catalog.FindType(recipe.ProcessTypeId) ?? new ProcessTypeInfo(0, string.Empty, string.Empty, string.Empty, null, string.Empty, true, false, 0),
            head.Quantity,
            0,
            new Dictionary<int, int>(),
            catalog,
            new ProcessOrderRequest(head.Quantity));

        long seconds = await UnitSecondsAsync(context, cancellationToken).ConfigureAwait(false);
        head.StartsAt = now;
        head.NextUnitAt = now.AddSeconds(seconds);

        // The end of the order is the end of its last unit, so it moves with every unit that
        // is made.
        head.FinishesAt = head.NextUnitAt.Value.AddSeconds(seconds * Math.Max(head.Quantity - 1, 0));
        head.IsBlocked = false;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Plans the event of the completion of the head of the queue.
    /// </summary>
    /// <param name="queue">The queue of the building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event is planned.</returns>
    private async Task ScheduleHeadAsync(List<ProcessOrderEntity> queue, CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0].NextUnitAt is not { } nextUnitAt)
        {
            return;
        }

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                queue[0].SectorId,
                CompleteEventType,
                nextUnitAt,
                JsonSerializer.Serialize(new CompletePayload(queue[0].PlanetId, queue[0].PlanetBuildingId)),
                CompleteEventType
                    + ":" + queue[0].Id.ToString(CultureInfo.InvariantCulture)
                    + ":" + queue[0].ProducedQuantity.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The time of one unit of a process, in game seconds.
    /// </summary>
    /// <param name="context">The order and the recipe.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The time of one unit.</returns>
    private static async Task<long> UnitSecondsAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return context.Recipe.DurationSeconds;
    }

    /// <summary>
    /// The resources the order takes, by their resource.
    /// </summary>
    /// <param name="recipe">The recipe of the order.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <returns>The resources by their resource.</returns>
    private static Dictionary<int, long> ResourceCosts(ProcessRecipeInfo recipe, long quantity)
    {
        Dictionary<int, long> costs = [];

        foreach (ProcessIngredientInfo ingredient in recipe.IngredientIds)
        {
            if (ingredient.ResourceTypeId is not { } resourceTypeId)
            {
                continue;
            }

            costs[resourceTypeId] = costs.GetValueOrDefault(resourceTypeId) + (ingredient.Amount * quantity);
        }

        return costs;
    }

    /// <summary>
    /// Takes the parts the order needs from the storage of the body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="recipe">The recipe of the order.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The rows of the costs of the parts.</returns>
    private async Task<List<ProcessOrderCostEntity>> TakePartsAsync(
        long planetId,
        ProcessRecipeInfo recipe,
        long quantity,
        CancellationToken cancellationToken)
    {
        Dictionary<int, long> needed = [];

        foreach (ProcessIngredientInfo ingredient in recipe.IngredientIds)
        {
            if (ingredient.PartTypeId is not { } partTypeId)
            {
                continue;
            }

            needed[partTypeId] = needed.GetValueOrDefault(partTypeId) + (ingredient.Amount * quantity);
        }

        if (needed.Count == 0)
        {
            return [];
        }

        IReadOnlyList<PartStackTaken> taken = await _parts.TakeAsync(planetId, needed, cancellationToken).ConfigureAwait(false);

        return
        [
            .. taken.Select(stack => new ProcessOrderCostEntity
            {
                PartTypeId = stack.PartTypeId,
                Amount = stack.Quantity,
            })
        ];
    }

    /// <summary>
    /// Returns the cost of an order to the balance and the storage of the body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="costs">The rows of the costs of the order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the cost is returned.</returns>
    private async Task GiveBackAsync(
        long planetId,
        string playerId,
        List<ProcessOrderCostEntity> costs,
        CancellationToken cancellationToken)
    {
        Dictionary<int, long> resources = [];

        foreach (ProcessOrderCostEntity cost in costs)
        {
            if (cost.ResourceTypeId is { } resourceTypeId)
            {
                resources[resourceTypeId] = resources.GetValueOrDefault(resourceTypeId) + cost.Amount;
            }
        }

        if (resources.Count > 0)
        {
            await _resources.GiveAsync(planetId, playerId, resources, cancellationToken).ConfigureAwait(false);
        }

        List<ProcessOrderCostEntity> partCosts = [.. costs.Where(cost => cost.PartTypeId is not null)];

        if (partCosts.Count > 0)
        {
            Dictionary<(int PartTypeId, bool HasRaceBonus), long> parts = [];

            foreach (ProcessOrderCostEntity cost in partCosts)
            {
                (int, bool) key = (cost.PartTypeId!.Value, false);
                parts[key] = parts.GetValueOrDefault(key) + cost.Amount;
            }

            await _parts.GiveBackAsync(planetId, parts, PartCondition.Full, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads at once what the requirements of the recipes of a body look at, so a building
    /// with many recipes asks the other modules once and not once for every recipe.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels and the names the requirements are checked against.</returns>
    private async Task<ProcessRequirementContext> RequirementContextAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        PlanetOverviewDto? planet = await _planets.GetPlanetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, string> buildingNameKeys = await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, int> technologyLevels = await TechnologyLevelsAsync(playerId, cancellationToken).ConfigureAwait(false);

        Dictionary<int, int> buildingLevels = [];

        if (planet is not null)
        {
            foreach (BuildingDto building in planet.Buildings)
            {
                buildingLevels[building.BuildingTypeId] = building.Level;
            }
        }

        return new ProcessRequirementContext(
            buildingLevels,
            technologyLevels,
            buildingNameKeys,
            await TechnologyNameKeysAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The group of every resource the player named in the order, so a handler that works on
    /// a resource can check whether the resource is in the group its recipe takes.
    /// </summary>
    /// <param name="request">What the player asked for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The group of every named resource, by the identifier of the resource.</returns>
    private async Task<IReadOnlyDictionary<int, int>> ResourceGroupsAsync(
        ProcessOrderRequest request,
        CancellationToken cancellationToken)
    {
        List<int> resourceTypeIds = [.. new[] { request.SourceResourceTypeId, request.TargetResourceTypeId }
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()];

        if (resourceTypeIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        Aetheris.World.Api.ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        return catalog.Resources
            .Where(resource => resourceTypeIds.Contains(resource.Id))
            .ToDictionary(resource => resource.Id, resource => resource.ResourceGroupId);
    }

    /// <summary>
    /// The requirements of a recipe that the player does not meet, each with the name of what
    /// is required, so a closed recipe says why it is closed.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="context">The levels and the names the requirements are checked against.</param>
    /// <returns>The unmet requirements.</returns>
    private static List<ProcessReason> UnmetReasons(
        ProcessCatalog catalog,
        ProcessRecipeInfo recipe,
        ProcessRequirementContext context)
    {
        List<ProcessReason> reasons = [];

        foreach (ProcessRequirementInfo requirement in catalog.RequirementsOf(recipe.Id))
        {
            switch (requirement.RequirementType)
            {
                case RequirementTypes.BuildingLevel:
                    if (context.BuildingLevels.GetValueOrDefault(requirement.TargetId) < requirement.TargetLevel)
                    {
                        reasons.Add(new ProcessReason(
                            ProcessErrorCodes.RequirementBuildingLevel,
                            requirement.TargetId,
                            requirement.TargetLevel,
                            context.BuildingNameKeys.GetValueOrDefault(requirement.TargetId)));
                    }

                    break;

                case RequirementTypes.TechnologyLevel:
                    if (context.TechnologyLevels.GetValueOrDefault(requirement.TargetId) < requirement.TargetLevel)
                    {
                        reasons.Add(new ProcessReason(
                            ProcessErrorCodes.RequirementTechnology,
                            requirement.TargetId,
                            requirement.TargetLevel,
                            context.TechnologyNameKeys.GetValueOrDefault(requirement.TargetId)));
                    }

                    break;

                default:
                    reasons.Add(new ProcessReason(
                        ProcessErrorCodes.RequirementUnknown,
                        requirement.TargetId,
                        requirement.TargetLevel));
                    break;
            }
        }

        return reasons;
    }

    /// <summary>
    /// Builds the record of a recipe with the reasons why it is closed.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="processType">The kind of the process.</param>
    /// <param name="context">The levels and the names the requirements are checked against.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The record of the recipe.</returns>
    private static async Task<ProcessRecipeDto> RecipeDtoAsync(
        ProcessCatalog catalog,
        ProcessRecipeInfo recipe,
        ProcessTypeInfo processType,
        ProcessRequirementContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        List<ProcessReason> reasons = UnmetReasons(catalog, recipe, context);

        if (recipe.IsArchived)
        {
            reasons.Insert(0, new ProcessReason(ProcessErrorCodes.RecipeArchived, recipe.Id));
        }

        return new ProcessRecipeDto(
            recipe.Id,
            recipe.Code,
            recipe.NameKey,
            recipe.DescriptionKey,
            processType.NameKey,
            recipe.BuildingTypeId,
            recipe.SourceGroupId,
            recipe.TargetGroupId,
            recipe.DurationSeconds,
            recipe.ShipStub,
            reasons.Count == 0,
            [
                .. reasons.Select(reason => new ProcessReasonDto(
                    reason.Code,
                    reason.TargetId,
                    reason.TargetLevel,
                    reason.TargetNameKey))
            ],
            recipe.ImageKey,
            [
                .. recipe.IngredientIds.Select(ingredient => new ProcessIngredientDto(
                    ingredient.ResourceTypeId,
                    ingredient.PartTypeId,
                    ingredient.Amount))
            ]);
    }

    private async Task<List<ProcessOrderEntity>> LoadQueueAsync(
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken) =>
        [.. await _dbContext.ProcessOrders
            .Where(order => order.PlanetId == planetId && order.PlanetBuildingId == planetBuildingId)
            .OrderBy(order => order.Position)
            .Include(order => order.CostIds)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

    /// <summary>
    /// Puts the parts of a result into the storage of the body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="parts">The parts of the result.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are in the storage.</returns>
    private async Task AddPartsAsync(
        long planetId,
        IReadOnlyList<ProcessPartResult> parts,
        CancellationToken cancellationToken)
    {
        List<PartStackAdded> added =
        [
            .. parts.Select(part => new PartStackAdded(
                part.PartTypeId,
                part.MarkLevel,
                part.HasRaceBonus,
                part.ConditionPercent,
                part.Quantity))
        ];

        await _parts.AddAsync(planetId, added, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the crew that came back into the state of the population of the body and
    /// raises the flag when it is over the limit (task 00014, decision 3).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="crew">How many crew came back.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the crew is written.</returns>
    private async Task AddCrewAsync(long planetId, long crew, CancellationToken cancellationToken)
    {
        ProcessPopulationStateEntity? state = await _dbContext.ProcessPopulationStates
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            state = new ProcessPopulationStateEntity { PlanetId = planetId };
            _dbContext.ProcessPopulationStates.Add(state);
        }

        state.CrewPending += crew;

        long limit = await _planets.GetBuildingEffectTotalAsync(planetId, BuildingEffectTypes.PopulationMax, cancellationToken).ConfigureAwait(false);

        if (state.CrewPending > limit && !state.IsOverLimit)
        {
            state.IsOverLimit = true;
            state.OverLimitAt = _clock.GameNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The levels of the technologies of the player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels by technology.</returns>
    private async Task<IReadOnlyDictionary<int, int>> TechnologyLevelsAsync(
        string playerId,
        CancellationToken cancellationToken) =>
        await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// The keys of the names of the technologies.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The keys by technology.</returns>
    private async Task<IReadOnlyDictionary<int, string>> TechnologyNameKeysAsync(
        CancellationToken cancellationToken) =>
        await _technologies.GetTechnologyNameKeysAsync(cancellationToken).ConfigureAwait(false);

    private async Task LockQueueAsync(long planetId, long planetBuildingId, CancellationToken cancellationToken)
    {
        _ = planetId;
        _ = planetBuildingId;
        _ = cancellationToken;

        // The queue of a building is locked by the row of the state of the queue. The row is
        // created on the first order, so the lock is taken in the same order for every
        // building of every body.
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            SELECT pg_advisory_xact_lock(hashtext('processes_queue:' || @p0::text));
            """,
            [planetBuildingId.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
    }

    private async Task<int> SlotsAsync(
        ProcessesSettings settings,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken)
    {
        _ = planetId;
        _ = cancellationToken;

        int extra = await _dbContext.ProcessQueueStates
            .AsNoTracking()
            .Where(state => state.PlanetId == planetId && state.PlanetBuildingId == planetBuildingId)
            .Select(state => state.ExtraSlots)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        int paid = Math.Clamp(extra, 0, settings.QueueMaxExtraSlots);

        return settings.QueueBaseSlots + paid;
    }

    private async Task<int> SectorOfAsync(long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        return facts?.SectorId ?? 0;
    }

    private static void Renumber(List<ProcessOrderEntity> queue)
    {
        for (int index = 0; index < queue.Count; index++)
        {
            queue[index].Position = index + 1;
        }
    }

    /// <summary>
    /// The body of the event of the completion of a unit.
    /// </summary>
    /// <param name="PlanetId">The body the process runs on.</param>
    /// <param name="PlanetBuildingId">The building the process runs in.</param>
    public sealed record CompletePayload(long PlanetId, long PlanetBuildingId);
}
