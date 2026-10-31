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
/// The engine of the processes: the queue of a building, the payment of the input, the refund
/// when an order is cancelled and the completion of the orders (task 00014, section 3.1).
/// </summary>
/// <remarks>
/// <para>
/// The engine knows no concrete process. A kind of a process names its handler by a logical
/// code, and the engine finds the handler in the container of dependencies by that code. The
/// queue, the payment, the refund, the time of a unit and the event of the completion are the
/// same for every process.
/// </para>
/// <para>
/// Every change runs in one transaction and takes its locks in one fixed order: the queue of
/// the building, the storage of the parts of the body, the balance of the resources of the
/// body. The queue of a factory of the production module takes the same order, so two
/// buildings of one body never wait for each other in a circle.
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
    private readonly IEconomyModule _economy;
    private readonly CostSplitter _costs;
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
    /// <param name="world">The world module, for the owner of a body, the resources and the races.</param>
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
        IEconomyModule economy,
        CostSplitter costs,
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
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(costs);
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
        _economy = economy;
        _costs = costs;
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
    /// Puts an order of a recipe in the queue of a building. The whole input of the order is
    /// paid at once, and cancelling it returns what the order has not used.
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
                PlanetFacts facts = await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                List<ProcessOrderEntity> queue = await OpenAsync(facts, planetBuildingId, cancellationToken).ConfigureAwait(false);

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

                ProcessTypeInfo processType = catalog.FindType(recipe.ProcessTypeId)
                    ?? throw new GameException(ProcessErrorCodes.TypeUnknown, "The kind of the process does not exist.");

                if (processType.IsArchived)
                {
                    throw new GameException(ProcessErrorCodes.TypeArchived, "The kind of the process is archived.");
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

                List<ProcessReason> requirementReasons = UnmetReasons(catalog, recipe, requirementContext);

                if (requirementReasons.Count > 0)
                {
                    throw new GameException(requirementReasons[0].Code, "A requirement of the process is not met.");
                }

                ProcessCheckContext checkContext = new(
                    playerId,
                    planetId,
                    planetBuildingId,
                    recipe,
                    processType,
                    request.Quantity,
                    building.Level,
                    requirementContext,
                    request,
                    await ResourceGroupsAsync(request, cancellationToken).ConfigureAwait(false),
                    catalog);

                IReadOnlyList<ProcessReason> reasons = await handler.CheckAsync(checkContext, cancellationToken).ConfigureAwait(false);

                if (reasons.Count > 0)
                {
                    throw new GameException(reasons[0].Code, "The process cannot start.");
                }

                ProcessInput input = await handler.InputAsync(checkContext, cancellationToken).ConfigureAwait(false);

                // The input is paid when the order enters the queue and returned when it
                // leaves it: the parts first and the resources then, in the order of the locks.
                Dictionary<int, long> resourceCosts = ResourceCosts(recipe, input, request.Quantity);
                List<ProcessPartInput> partCosts = PartCosts(recipe, input, request.Quantity);
                long unitSeconds = UnitSeconds(catalog, recipe, processType, input, building.Level);
                DateTimeOffset now = _clock.GameNow;
                CheckDuration(now, unitSeconds, request.Quantity);

                List<PartStackTaken> taken = [];

                try
                {
                    foreach (ProcessPartInput part in partCosts)
                    {
                        taken.AddRange(await _parts.TakeStackAsync(planetId, part.PartTypeId, part.ConditionPercent, part.Quantity, cancellationToken).ConfigureAwait(false));
                    }
                }
                catch (GameException exception) when (exception.Code == ProductionErrorCodes.PartMissing)
                {
                    throw new GameException(ProcessErrorCodes.PartMissing, "The body lacks the parts the order takes.");
                }

                // The costs of a civilization are paid from its balance and the rest from the
                // balance of the body (task 00015).
                SplitCosts split = await _costs.SplitAsync(resourceCosts, cancellationToken).ConfigureAwait(false);

                await _resources.TakeAsync(planetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);
                await _economy.TakeAsync(playerId, split.Currencies, cancellationToken).ConfigureAwait(false);

                ProcessOrderEntity item = new()
                {
                    PlanetId = planetId,
                    PlanetBuildingId = planetBuildingId,
                    SectorId = facts.SectorId,
                    ProcessRecipeId = recipeId,
                    Quantity = request.Quantity,
                    ProducedQuantity = 0,
                    SourceResourceTypeId = request.SourceResourceTypeId,
                    TargetResourceTypeId = request.TargetResourceTypeId,
                    PartTypeId = request.PartTypeId,
                    PartConditionPercent = request.PartConditionPercent,
                    UnitSeconds = unitSeconds,
                    Position = queue.Count + 1,
                    CreatedAt = now,

                    // The real levels of the building and of the technologies are fixed with the
                    // order: what is being built or learned up to a level has not reached it,
                    // so the order goes by the level that is real now (task 00014, section 11).
                    BuildingLevel = building.Level,
                    TechnologyIds =
                    [
                        .. requirementContext.TechnologyLevels
                            .Where(technology => technology.Value > 0)
                            .OrderBy(technology => technology.Key)
                            .Select(technology => new ProcessOrderTechnologyEntity
                            {
                                TechnologyId = technology.Key,
                                Level = technology.Value,
                            }),
                    ],

                    // The parameters that hold now are copied into the order, so an archived or
                    // changed parameter reaches new orders at once and never the ones in the
                    // queue (task 00014, section 11).
                    HasParamSnapshot = true,
                    ParamIds =
                    [
                        .. catalog.ParametersOf(processType.Id)
                            .Where(parameter => !parameter.IsArchived)
                            .Select(parameter => new ProcessOrderParamEntity
                            {
                                ProcessParamId = parameter.Id,
                                ParamKind = parameter.ParamKind,
                                LevelSource = parameter.LevelSource,
                                LevelTargetId = parameter.LevelTargetId,
                                Level = parameter.Level,
                                Value = parameter.Value,
                                ValueMax = parameter.ValueMax,
                                SortOrder = parameter.SortOrder,
                            }),
                    ],
                    CostIds =
                    [
                        .. resourceCosts.OrderBy(cost => cost.Key).Select(cost => new ProcessOrderCostEntity
                        {
                            ResourceTypeId = cost.Key,
                            Amount = cost.Value,
                        }),
                        .. taken.Select(stack => new ProcessOrderCostEntity
                        {
                            PartTypeId = stack.PartTypeId,
                            MarkLevel = stack.MarkLevel,
                            HasRaceBonus = stack.HasRaceBonus,
                            ConditionPercent = stack.ConditionPercent,
                            Amount = stack.Quantity,
                        }),
                    ],
                };

                _dbContext.ProcessOrders.Add(item);
                queue.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await StartNextAsync(queue, now, cancellationToken).ConfigureAwait(false);
                await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

                return await BuildingDtoAsync(playerId, planetId, planetBuildingId, catalog, building, queue, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes an order from the queue of a building and returns the input of the units it has
    /// not made, unless the kind of the process does not refund it (task 00014, decision 2).
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
                PlanetFacts facts = await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                List<ProcessOrderEntity> queue = await OpenAsync(facts, planetBuildingId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, planetId, planetBuildingId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                ProcessOrderEntity cancelled = queue.FirstOrDefault(entry => entry.Id == orderId)
                    ?? throw new GameException(ProcessErrorCodes.OrderUnknown, "The order of the queue does not exist.");

                bool headRemoved = queue[0].Id == cancelled.Id;

                // The costs are read before the record is deleted, since a deleted record
                // loses its cost rows.
                List<ProcessOrderCostEntity> costs = [.. cancelled.CostIds];
                long quantity = cancelled.Quantity;
                long produced = cancelled.ProducedQuantity;

                queue.Remove(cancelled);
                _dbContext.ProcessOrders.Remove(cancelled);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
                ProcessRecipeInfo? recipe = catalog.FindRecipe(cancelled.ProcessRecipeId);
                ProcessTypeInfo? processType = recipe is null ? null : catalog.FindType(recipe.ProcessTypeId);

                // A kind of a process that does not refund its input loses the device when
                // the order is cancelled, so the data of the kind decides it. The units already
                // made keep their input, the rest comes back in full.
                if (processType?.RefundsInput ?? true)
                {
                    await GiveBackAsync(planetId, playerId, costs, quantity, produced, cancellationToken).ConfigureAwait(false);
                }

                if (headRemoved)
                {
                    await StartNextAsync(queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);
                }

                await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

                PlanetBuildingLevelById building = await _planets.GetBuildingLevelByIdAsync(planetId, planetBuildingId, playerId, cancellationToken).ConfigureAwait(false);

                return await BuildingDtoAsync(playerId, planetId, planetBuildingId, catalog, building, queue, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Completes every order of a building that is due at a moment and plans the event of the
    /// next unit.
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

                PlanetFacts facts = await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                List<ProcessOrderEntity> queue = await OpenAsync(facts, planetBuildingId, cancellationToken).ConfigureAwait(false);

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

        // A player reads only the buildings of his own colonies, so a unit is never made in
        // the name of another player.
        await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        PlanetBuildingLevelById building = await _planets.GetBuildingLevelByIdAsync(
            planetId,
            planetBuildingId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        if (building.Level <= 0)
        {
            throw new GameException(ProcessErrorCodes.BuildingMissing, "The body has no such building.");
        }

        await CompleteDueAsync(playerId, planetId, planetBuildingId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        List<ProcessOrderEntity> queue = await LoadQueueAsync(planetId, planetBuildingId, tracked: false, cancellationToken).ConfigureAwait(false);

        return await BuildingDtoAsync(playerId, planetId, planetBuildingId, catalog, building, queue, cancellationToken).ConfigureAwait(false);
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

        await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        // The types of the buildings are the types the recipes of the catalog name, so a new
        // recipe opens a new building without a change in the code. A building whose recipes
        // are all archived stays listed, so its queue stays in sight.
        int[] buildingTypeIds = [.. catalog.Recipes
            .Where(recipe => recipe.BuildingTypeId is not null)
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
    /// Reads every order that is in the queues of the buildings of a body that run a process,
    /// with the places of those queues (task 00020, section 7.5). It only reads: no order is
    /// changed, the units that are due are made first, as a read of one building does.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The orders of all the buildings, in the order of the queues, and the places.</returns>
    /// <exception cref="GameException">The body is not a colony of the player.</exception>
    public async Task<ProcessActiveOrdersDto> ReadActiveOrdersAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _clock.GameNow;
        List<ProcessOrderEntity> orders = await LoadPlanetOrdersAsync(planetId, cancellationToken).ConfigureAwait(false);
        long[] dueBuildingIds = [.. orders
            .Where(order => order.NextUnitAt is not null && order.NextUnitAt <= now)
            .Select(order => order.PlanetBuildingId)
            .Distinct()];

        foreach (long planetBuildingId in dueBuildingIds)
        {
            await CompleteDueAsync(playerId, planetId, planetBuildingId, now, cancellationToken).ConfigureAwait(false);
        }

        if (dueBuildingIds.Length > 0)
        {
            orders = await LoadPlanetOrdersAsync(planetId, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ProcessBuildingSummaryDto> buildings = await ReadBuildingsAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        Dictionary<long, string> buildingNameKeys = buildings.ToDictionary(building => building.PlanetBuildingId, building => building.NameKey);
        ProcessesSettings settings = ProcessesSettings.From(_config);
        int slots = 0;

        // The places of the queues add up: every building of the body has its own queue.
        foreach (long planetBuildingId in buildingNameKeys.Keys.Union(orders.Select(order => order.PlanetBuildingId)))
        {
            slots += await SlotsAsync(settings, planetId, planetBuildingId, cancellationToken).ConfigureAwait(false);
        }

        ProcessCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        List<ProcessActiveOrderDto> items = [];

        foreach (ProcessOrderEntity order in orders)
        {
            ProcessRecipeInfo? recipe = catalog.FindRecipe(order.ProcessRecipeId);
            ProcessTypeInfo? processType = recipe is null ? null : catalog.FindType(recipe.ProcessTypeId);

            items.Add(new ProcessActiveOrderDto(
                order.Id,
                order.PlanetBuildingId,
                buildingNameKeys.GetValueOrDefault(order.PlanetBuildingId, string.Empty),
                order.ProcessRecipeId,
                recipe?.NameKey ?? string.Empty,
                processType?.NameKey ?? string.Empty,
                order.Quantity,
                order.ProducedQuantity,
                order.Position,
                recipe is null ? Math.Max(order.UnitSeconds, 0) : UnitSecondsOf(order, recipe),
                order.StartsAt,
                order.NextUnitAt,
                order.FinishesAt,
                order.IsBlocked));
        }

        return new ProcessActiveOrdersDto(planetId, slots, items.Count, items, _clock.GameNow);
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
    /// Takes the locks of a change of a queue in their fixed order and reads the queue: the
    /// queue of the building first, the storage of the parts of the body next.
    /// </summary>
    /// <param name="facts">The facts of the body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The queue of the building.</returns>
    private async Task<List<ProcessOrderEntity>> OpenAsync(PlanetFacts facts, long planetBuildingId, CancellationToken cancellationToken)
    {
        await LockQueueAsync(planetBuildingId, cancellationToken).ConfigureAwait(false);
        await _parts.LockAsync(facts.PlanetId, facts.SectorId, cancellationToken).ConfigureAwait(false);

        return await LoadQueueAsync(facts.PlanetId, planetBuildingId, tracked: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Completes the orders of a queue that are due at a moment, one unit at a time, and plans
    /// the event of the next unit.
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
            ProcessTypeInfo? processType = recipe is null ? null : catalog.FindType(recipe.ProcessTypeId);

            if (recipe is null || processType is null)
            {
                break;
            }

            IProcessHandler handler = Handler(processType);
            PlanetBuildingLevelById building = await _planets.GetBuildingLevelByIdAsync(
                planetId,
                planetBuildingId,
                playerId,
                cancellationToken).ConfigureAwait(false);

            ProcessApplyContext context = new(
                playerId,
                planetId,
                planetBuildingId,
                recipe,
                processType,
                head.Quantity,
                head.ProducedQuantity + 1,
                head.BuildingLevel ?? building.Level,
                head.BuildingLevel is null
                    ? await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false)
                    : (IReadOnlyDictionary<int, int>)head.TechnologyIds.ToDictionary(technology => technology.TechnologyId, technology => technology.Level),
                OrderCatalog(catalog, processType, head),
                new ProcessOrderRequest(
                    head.Quantity,
                    head.SourceResourceTypeId,
                    head.TargetResourceTypeId,
                    head.PartTypeId,
                    head.PartConditionPercent),
                TakenParts(head));

            ProcessResult result = await handler.ComputeAsync(context, cancellationToken).ConfigureAwait(false);
            await handler.ApplyAsync(context, result, cancellationToken).ConfigureAwait(false);

            if (result.Parts.Count > 0)
            {
                await _parts.AddAsync(planetId, result.Parts, cancellationToken).ConfigureAwait(false);
            }

            if (result.Resources.Count > 0)
            {
                await _resources.GiveAsync(planetId, playerId, result.Resources, cancellationToken).ConfigureAwait(false);
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
                long unitSeconds = UnitSecondsOf(head, recipe);
                head.NextUnitAt = readyAt.AddSeconds(unitSeconds);
                head.FinishesAt = head.NextUnitAt.Value.AddSeconds(unitSeconds * (head.Quantity - head.ProducedQuantity - 1));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // The next unit of the queue gets its event, so the queue goes on without a read of
        // the player; an event that is already planned is not planned twice.
        await ScheduleHeadAsync(queue, cancellationToken).ConfigureAwait(false);

        return made;
    }

    /// <summary>
    /// The catalog an order is finished with: the parameters the order was made with when it
    /// carries them, the parameters of the catalog as they are now when it does not.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="processType">The kind of the process of the order.</param>
    /// <param name="order">The order.</param>
    /// <returns>The catalog the handler reads its parameters from.</returns>
    private static ProcessCatalog OrderCatalog(ProcessCatalog catalog, ProcessTypeInfo processType, ProcessOrderEntity order) =>
        order.HasParamSnapshot
            ? catalog.WithOrderParameters(
                processType.Id,
                [
                    .. order.ParamIds
                        .OrderBy(parameter => parameter.ProcessParamId)
                        .Select(parameter => new ProcessParamInfo(
                            parameter.ProcessParamId,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            processType.Id,
                            parameter.ParamKind,
                            parameter.LevelSource,
                            parameter.LevelTargetId,
                            parameter.Level,
                            parameter.Value,
                            parameter.ValueMax,
                            false,
                            parameter.SortOrder)),
                ])
            : catalog;

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

        long seconds = UnitSecondsOf(head, recipe);
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
    /// Plans the event of the completion of the next unit of the head of the queue. The key of
    /// the event names the order and its unit, so a repeated plan of the same unit is one event
    /// and a repeated delivery of a done unit changes nothing.
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
    /// The time of one unit of an order, in game seconds: the time of the recipe times the work
    /// of the handler times the multiplier of the building at its level, never below a second.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="recipe">The recipe of the order.</param>
    /// <param name="processType">The kind of the process.</param>
    /// <param name="input">The input of the order.</param>
    /// <param name="buildingLevel">The level of the building the process runs in.</param>
    /// <returns>The time of one unit.</returns>
    private static long UnitSeconds(
        ProcessCatalog catalog,
        ProcessRecipeInfo recipe,
        ProcessTypeInfo processType,
        ProcessInput input,
        int buildingLevel)
    {
        decimal factor = catalog.ParamAt(processType.Id, ProcessParamKinds.DurationFactor, ProcessLevelSources.Building, null, buildingLevel)?.Value
            ?? decimal.One;

        try
        {
            long seconds = (long)decimal.Ceiling(recipe.DurationSeconds * Math.Max(input.WorkFactor, decimal.Zero) * Math.Max(factor, decimal.Zero));

            return Math.Max(seconds, 1);
        }
        catch (OverflowException)
        {
            throw new GameException(ProcessErrorCodes.QuantityInvalid, "The order is too large.");
        }
    }

    /// <summary>
    /// The time of one unit of an order in the queue: the time fixed when it entered the queue.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="recipe">The recipe of the order, for an order that has no time of its own.</param>
    /// <returns>The time of one unit.</returns>
    private static long UnitSecondsOf(ProcessOrderEntity order, ProcessRecipeInfo recipe) =>
        order.UnitSeconds > 0 ? order.UnitSeconds : Math.Max(recipe.DurationSeconds, 1);

    /// <summary>
    /// Checks that the end of the whole order is a moment the game can hold.
    /// </summary>
    /// <param name="now">The moment of game time.</param>
    /// <param name="unitSeconds">The time of one unit.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <exception cref="GameException">The order is too large.</exception>
    private static void CheckDuration(DateTimeOffset now, long unitSeconds, long quantity)
    {
        try
        {
            _ = now.AddSeconds(checked(unitSeconds * quantity));
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new GameException(ProcessErrorCodes.QuantityInvalid, "The order is too large.");
        }
    }

    /// <summary>
    /// The resources the whole order takes: the ingredients of the recipe for every unit and
    /// the input the handler names.
    /// </summary>
    /// <param name="recipe">The recipe of the order.</param>
    /// <param name="input">The input the handler names.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <returns>The resources by their resource.</returns>
    /// <exception cref="GameException">The order is too large.</exception>
    private static Dictionary<int, long> ResourceCosts(ProcessRecipeInfo recipe, ProcessInput input, long quantity)
    {
        try
        {
            Dictionary<int, long> costs = [];

            foreach (ProcessIngredientInfo ingredient in recipe.IngredientIds.Where(ingredient => ingredient.ResourceTypeId is not null))
            {
                int resourceTypeId = ingredient.ResourceTypeId!.Value;
                costs[resourceTypeId] = checked(costs.GetValueOrDefault(resourceTypeId) + checked(ingredient.Amount * quantity));
            }

            foreach ((int resourceTypeId, long amount) in input.Resources)
            {
                costs[resourceTypeId] = checked(costs.GetValueOrDefault(resourceTypeId) + amount);
            }

            return costs.Where(cost => cost.Value > 0).ToDictionary(cost => cost.Key, cost => cost.Value);
        }
        catch (OverflowException)
        {
            throw new GameException(ProcessErrorCodes.QuantityInvalid, "The order is too large.");
        }
    }

    /// <summary>
    /// The parts the whole order takes: the parts of the recipe, new, for every unit and the
    /// parts the handler names in their condition.
    /// </summary>
    /// <param name="recipe">The recipe of the order.</param>
    /// <param name="input">The input the handler names.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <returns>The parts by their kind and condition.</returns>
    /// <exception cref="GameException">The order is too large.</exception>
    private static List<ProcessPartInput> PartCosts(ProcessRecipeInfo recipe, ProcessInput input, long quantity)
    {
        try
        {
            Dictionary<(int PartTypeId, int ConditionPercent), long> parts = [];

            foreach (ProcessIngredientInfo ingredient in recipe.IngredientIds.Where(ingredient => ingredient.PartTypeId is not null))
            {
                (int, int) key = (ingredient.PartTypeId!.Value, PartCondition.Full);
                parts[key] = checked(parts.GetValueOrDefault(key) + checked(ingredient.Amount * quantity));
            }

            foreach (ProcessPartInput part in input.Parts)
            {
                (int, int) key = (part.PartTypeId, part.ConditionPercent);
                parts[key] = checked(parts.GetValueOrDefault(key) + part.Quantity);
            }

            return
            [
                .. parts
                    .Where(entry => entry.Value > 0)
                    .OrderBy(entry => entry.Key.PartTypeId)
                    .ThenBy(entry => entry.Key.ConditionPercent)
                    .Select(entry => new ProcessPartInput(entry.Key.PartTypeId, entry.Key.ConditionPercent, entry.Value)),
            ];
        }
        catch (OverflowException)
        {
            throw new GameException(ProcessErrorCodes.QuantityInvalid, "The order is too large.");
        }
    }

    /// <summary>
    /// The parts an order took from the storage, as they were.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>The stacks the order took.</returns>
    private static List<PartStackTaken> TakenParts(ProcessOrderEntity order) =>
    [
        .. order.CostIds
            .Where(cost => cost.PartTypeId is not null)
            .OrderBy(cost => cost.Id)
            .Select(cost => new PartStackTaken(
                cost.PartTypeId!.Value,
                cost.MarkLevel ?? PartMarkLevels.First,
                cost.HasRaceBonus,
                cost.ConditionPercent ?? PartCondition.Full,
                cost.Amount)),
    ];

    /// <summary>
    /// Returns the input of the units an order has not made to the balance and the storage of
    /// the body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="costs">The rows of the costs of the order.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <param name="produced">How many units the order made.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the input is returned.</returns>
    private async Task GiveBackAsync(
        long planetId,
        string playerId,
        List<ProcessOrderCostEntity> costs,
        long quantity,
        long produced,
        CancellationToken cancellationToken)
    {
        // The units made used the first stacks of every part, like the units took them, so what
        // is left goes back to the stacks it came from.
        List<PartStackAdded> parts = [];

        foreach (IGrouping<(int PartTypeId, int ConditionPercent), ProcessOrderCostEntity> group in costs
            .Where(cost => cost.PartTypeId is not null)
            .GroupBy(cost => (cost.PartTypeId!.Value, cost.ConditionPercent ?? PartCondition.Full)))
        {
            long total = group.Sum(cost => cost.Amount);
            long used = total - ProcessUnits.Unused(total, quantity, produced);

            foreach (ProcessOrderCostEntity cost in group.OrderBy(cost => cost.Id))
            {
                long consumed = Math.Min(cost.Amount, used);
                used -= consumed;

                if (cost.Amount - consumed > 0)
                {
                    parts.Add(new PartStackAdded(
                        cost.PartTypeId!.Value,
                        cost.MarkLevel ?? PartMarkLevels.First,
                        cost.HasRaceBonus,
                        cost.ConditionPercent ?? PartCondition.Full,
                        cost.Amount - consumed));
                }
            }
        }

        if (parts.Count > 0)
        {
            await _parts.AddAsync(planetId, parts, cancellationToken).ConfigureAwait(false);
        }

        Dictionary<int, long> resources = [];

        foreach (ProcessOrderCostEntity cost in costs.Where(cost => cost.ResourceTypeId is not null))
        {
            int resourceTypeId = cost.ResourceTypeId!.Value;
            resources[resourceTypeId] = resources.GetValueOrDefault(resourceTypeId) + ProcessUnits.Unused(cost.Amount, quantity, produced);
        }

        resources = resources.Where(entry => entry.Value > 0).ToDictionary(entry => entry.Key, entry => entry.Value);

        if (resources.Count > 0)
        {
            // The input comes back where it was taken from: the credits to the balance of the
            // civilization and the rest to the balance of the body (task 00015).
            SplitCosts split = await _costs.SplitAsync(resources, cancellationToken).ConfigureAwait(false);

            await _resources.GiveAsync(planetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);
            await _economy.GiveAsync(playerId, split.Currencies, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the building with its recipes and its queue for the client.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="building">The level and the type of the building.</param>
    /// <param name="queue">The queue of the building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue.</returns>
    private async Task<ProcessBuildingDto> BuildingDtoAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        ProcessCatalog catalog,
        PlanetBuildingLevelById building,
        List<ProcessOrderEntity> queue,
        CancellationToken cancellationToken)
    {
        ProcessRequirementContext requirementContext = await RequirementContextAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        HashSet<int> queuedRecipeIds = [.. queue.Select(order => order.ProcessRecipeId)];
        List<ProcessRecipeDto> recipes = [];

        foreach (ProcessRecipeInfo recipe in catalog.Recipes.Where(recipe => recipe.BuildingTypeId == building.BuildingTypeId))
        {
            ProcessTypeInfo? processType = catalog.FindType(recipe.ProcessTypeId);

            // An archived recipe is not offered, but the one an order of the queue still makes
            // is listed, so the queue keeps the name of what it makes (task 00023, decision 5).
            if (processType is null
                || ((recipe.IsArchived || processType.IsArchived) && !queuedRecipeIds.Contains(recipe.Id)))
            {
                continue;
            }

            recipes.Add(RecipeDto(catalog, recipe, processType, requirementContext));
        }

        return new ProcessBuildingDto(
            planetId,
            planetBuildingId,
            building.BuildingTypeId,
            building.Level,
            recipes,
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
            ],
            _clock.GameNow,
            PartCondition.Full);
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
        IReadOnlyList<RaceDto> races = await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false);
        CivilizationSummary? civilization = await _world.GetCivilizationAsync(playerId, cancellationToken).ConfigureAwait(false);

        Dictionary<int, int> buildingLevels = [];

        foreach (BuildingDto building in planet?.Buildings ?? [])
        {
            buildingLevels[building.BuildingTypeId] = Math.Max(buildingLevels.GetValueOrDefault(building.BuildingTypeId), building.Level);
        }

        return new ProcessRequirementContext(
            buildingLevels,
            await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false),
            await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false),
            await _technologies.GetTechnologyNameKeysAsync(cancellationToken).ConfigureAwait(false),
            civilization?.RaceId ?? 0,
            races.ToDictionary(race => race.Id, race => race.NameKey));
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

        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        // An archived resource keeps its balance, but no new conversion names it. A currency of
        // a civilization has no group, so it is not a raw material of a body (task 00015).
        return catalog.Resources
            .Where(resource => resourceTypeIds.Contains(resource.Id) && !resource.IsArchived)
            .Where(resource => resource.ResourceGroupId is not null)
            .ToDictionary(resource => resource.Id, resource => resource.ResourceGroupId!.Value);
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

                case ProductionRequirementTypes.Race:
                    if (context.RaceId != requirement.TargetId)
                    {
                        reasons.Add(new ProcessReason(
                            ProcessErrorCodes.RequirementRace,
                            requirement.TargetId,
                            requirement.TargetLevel,
                            context.RaceNameKeys.GetValueOrDefault(requirement.TargetId)));
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
    /// <returns>The record of the recipe.</returns>
    private ProcessRecipeDto RecipeDto(
        ProcessCatalog catalog,
        ProcessRecipeInfo recipe,
        ProcessTypeInfo processType,
        ProcessRequirementContext context)
    {
        List<ProcessReason> reasons = UnmetReasons(catalog, recipe, context);

        if (recipe.IsArchived || processType.IsArchived)
        {
            reasons.Insert(0, new ProcessReason(ProcessErrorCodes.RecipeArchived, recipe.Id));
        }

        IProcessHandler? handler = _handlers.FirstOrDefault(entry => string.Equals(entry.HandlerCode, processType.HandlerCode, StringComparison.Ordinal));

        if (handler is null)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.TypeUnknown, processType.Id));
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
            !string.IsNullOrEmpty(recipe.ShipStub),
            handler?.PartSubject ?? ProcessPartSubjects.None,
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

    /// <summary>
    /// Reads the orders of every building of a body for a read, in the order of the queues: the
    /// order under way of every building first, then the ones that wait.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The orders of all the buildings of the body.</returns>
    private async Task<List<ProcessOrderEntity>> LoadPlanetOrdersAsync(long planetId, CancellationToken cancellationToken) =>
        await _dbContext.ProcessOrders.AsNoTracking()
            .Where(order => order.PlanetId == planetId)
            .OrderBy(order => order.Position)
            .ThenBy(order => order.PlanetBuildingId)
            .ThenBy(order => order.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Reads the queue of a building in the order of its places.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="tracked">Whether the queue is read for a change.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The queue of the building.</returns>
    private async Task<List<ProcessOrderEntity>> LoadQueueAsync(
        long planetId,
        long planetBuildingId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ProcessOrderEntity> orders = _dbContext.ProcessOrders
            .Where(order => order.PlanetId == planetId && order.PlanetBuildingId == planetBuildingId)
            .OrderBy(order => order.Position)
            .ThenBy(order => order.Id)
            .Include(order => order.CostIds)
            .Include(order => order.ParamIds)
            .Include(order => order.TechnologyIds);

        return await (tracked ? orders : orders.AsNoTracking()).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks that the body is a colony of the player, from the facts of the world and never
    /// from the word of the client.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts of the body.</returns>
    /// <exception cref="GameException">The body is not a colony of the player.</exception>
    private async Task<PlanetFacts> EnsureOwnedAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(ProcessErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }

        return facts;
    }

    /// <summary>
    /// Locks the queue of a building for the rest of the transaction, so two changes of one
    /// queue run one after the other.
    /// </summary>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the queue is locked.</returns>
    private async Task LockQueueAsync(long planetBuildingId, CancellationToken cancellationToken)
    {
        string key = "processes_queue:" + planetBuildingId.ToString(CultureInfo.InvariantCulture);

        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The places of the queue of a building: the base places of the configuration and the
    /// paid ones, never more than the configuration allows.
    /// </summary>
    /// <param name="settings">The settings of the module.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the places.</returns>
    private async Task<int> SlotsAsync(
        ProcessesSettings settings,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken)
    {
        int extra = await _dbContext.ProcessQueueStates
            .AsNoTracking()
            .Where(state => state.PlanetId == planetId && state.PlanetBuildingId == planetBuildingId)
            .Select(state => state.ExtraSlots)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return settings.QueueBaseSlots + Math.Clamp(extra, 0, settings.QueueMaxExtraSlots);
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
