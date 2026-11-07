using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Planets.Data;
using Aetheris.Planets.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Api;

/// <summary>A stock an administrator may set.</summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="GroupNameKey">The localisation key of the group, or <see langword="null"/>.</param>
/// <param name="Amount">The amount now.</param>
public sealed record AdminColonyStockRow(int ResourceTypeId, string NameKey, string? GroupNameKey, long Amount);

/// <summary>A building of a colony as the administrator sees it.</summary>
/// <param name="BuildingTypeId">The building type.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="Level">The level built.</param>
/// <param name="MaxLevel">The largest level.</param>
/// <param name="QueuedLevel">The level once the queue is done.</param>
public sealed record AdminColonyBuildingRow(int BuildingTypeId, string NameKey, int Level, int MaxLevel, int QueuedLevel);

/// <summary>A record of the construction queue.</summary>
/// <param name="Id">The record.</param>
/// <param name="BuildingTypeId">The building type.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="TargetLevel">The level the building reaches.</param>
/// <param name="Position">The place in the queue.</param>
/// <param name="FinishesAt">The end, or <see langword="null"/> while the record waits.</param>
public sealed record AdminColonyQueueRow(long Id, int BuildingTypeId, string NameKey, int TargetLevel, int Position, DateTimeOffset? FinishesAt);

/// <summary>The colony state an administrator may change. The body itself is not here.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Population">The inhabitants now.</param>
/// <param name="PopulationMaximum">The largest population.</param>
/// <param name="PopulationGrowthPerHour">How much the population grows with in an hour.</param>
/// <param name="ExtraQueueSlots">The extra places of the construction queue.</param>
/// <param name="Energy">The energy, counted from the buildings.</param>
/// <param name="Stocks">The stocks of the storage.</param>
/// <param name="Buildings">The buildings.</param>
/// <param name="ConstructionQueue">The construction queue.</param>
public sealed record AdminColonyView(
    long PlanetId,
    long Population,
    long PopulationMaximum,
    decimal PopulationGrowthPerHour,
    int ExtraQueueSlots,
    EnergyDto Energy,
    IReadOnlyList<AdminColonyStockRow> Stocks,
    IReadOnlyList<AdminColonyBuildingRow> Buildings,
    IReadOnlyList<AdminColonyQueueRow> ConstructionQueue);

/// <summary>An absolute amount of a stock.</summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Amount">The amount.</param>
/// <param name="Reason">Why the administrator changed it.</param>
public sealed record AdminStockSet(
    [property: System.Text.Json.Serialization.JsonPropertyName("resource_type_id")] int ResourceTypeId,
    [property: System.Text.Json.Serialization.JsonPropertyName("amount")] long Amount,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>An absolute level of a building.</summary>
/// <param name="BuildingTypeId">The building type.</param>
/// <param name="Level">The level.</param>
/// <param name="Reason">Why the administrator changed it.</param>
public sealed record AdminBuildingLevelSet(
    [property: System.Text.Json.Serialization.JsonPropertyName("building_type_id")] int BuildingTypeId,
    [property: System.Text.Json.Serialization.JsonPropertyName("level")] int Level,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>The extra places of the construction queue.</summary>
/// <param name="ExtraQueueSlots">The extra places.</param>
/// <param name="Reason">Why the administrator changed them.</param>
public sealed record AdminQueueSlotsSet(
    [property: System.Text.Json.Serialization.JsonPropertyName("extra_queue_slots")] int ExtraQueueSlots,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>A reason of a cancel.</summary>
/// <param name="Reason">Why the administrator cancelled the record.</param>
public sealed record AdminCancelRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The colony of a player in the admin panel: the stock, the buildings and the construction queue.
/// The body itself is not changed here.
/// </summary>
public static class PlayerColonyAdminEndpoints
{
    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        RouteGroupBuilder group = secured.MapGroup("/planets/players/{playerId}/colonies/{planetId:long}");

        group.MapGet(string.Empty, ReadAsync).WithName("admin.player_colony_read");
        group.MapPut("/stock", SetStockAsync).WithName("admin.player_stock_updated");
        group.MapPut("/buildings", SetBuildingAsync).WithName("admin.player_building_updated");
        group.MapPut("/queue-slots", SetSlotsAsync).WithName("admin.player_slots_updated");
        group.MapPost("/construction/{queueItemId:long}/cancel", CancelAsync).WithName("admin.player_construction_cancelled");

        return secured;
    }

    private static async Task<IResult> ReadAsync(
        string playerId,
        long planetId,
        ConstructionService construction,
        PlanetsDbContext dbContext,
        IWorldModule world,
        IGameClock clock,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        audit.Record($"colony:{playerId}:{planetId}", null, null);

        PlanetFacts? facts = await world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        DateTimeOffset now = clock.GameNow;
        PlanetRead? read = await construction.ReadWithPopulationAsync(planetId, now, facts, cancellationToken).ConfigureAwait(false);

        if (read is null)
        {
            return NotFound();
        }

        if (read.HasDueConstruction)
        {
            await construction.CompleteDueAsync(planetId, now, cancellationToken).ConfigureAwait(false);
            read = await construction.ReadWithPopulationAsync(planetId, now, facts, cancellationToken).ConfigureAwait(false);

            if (read is null)
            {
                return NotFound();
            }
        }

        int extraQueueSlots = await dbContext.PlanetStates.AsNoTracking()
            .Where(state => state.PlanetId == planetId)
            .Select(state => state.ExtraQueueSlots)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        ResourceCatalog catalog = await world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        AdminColonyView view = Build(read, extraQueueSlots, catalog);

        return Results.Json(view, PlanetsJson.Options);
    }

    private static async Task<IResult> SetStockAsync(
        string playerId,
        long planetId,
        AdminStockSet request,
        ConstructionService construction,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        string target = $"stock:{playerId}:{planetId}:{request.ResourceTypeId}";
        audit.Record(target, null, null);

        if (ReasonError(request.Reason, limits) is { } refused)
        {
            return refused;
        }

        try
        {
            AdminAmountChange applied = await construction
                .SetStockAsync(playerId, planetId, request.ResourceTypeId, request.Amount, cancellationToken)
                .ConfigureAwait(false);

            audit.Record(target, applied.Before.ToString(CultureInfo.InvariantCulture), applied.After.ToString(CultureInfo.InvariantCulture));
            audit.RecordReason(request.Reason!.Trim());

            return Results.Json(new { amount = applied.After }, PlanetsJson.Options);
        }
        catch (GameException exception)
        {
            return Failure(exception);
        }
    }

    private static async Task<IResult> SetBuildingAsync(
        string playerId,
        long planetId,
        AdminBuildingLevelSet request,
        ConstructionService construction,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        string target = $"building:{playerId}:{planetId}:{request.BuildingTypeId}";
        audit.Record(target, null, null);

        if (ReasonError(request.Reason, limits) is { } refused)
        {
            return refused;
        }

        try
        {
            AdminAmountChange applied = await construction
                .SetBuildingLevelAsync(playerId, planetId, request.BuildingTypeId, request.Level, cancellationToken)
                .ConfigureAwait(false);

            audit.Record(target, applied.Before.ToString(CultureInfo.InvariantCulture), applied.After.ToString(CultureInfo.InvariantCulture));
            audit.RecordReason(request.Reason!.Trim());

            return Results.Json(new { level = applied.After }, PlanetsJson.Options);
        }
        catch (GameException exception)
        {
            return Failure(exception);
        }
    }

    private static async Task<IResult> SetSlotsAsync(
        string playerId,
        long planetId,
        AdminQueueSlotsSet request,
        ConstructionService construction,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        string target = $"queue_slots:{playerId}:{planetId}";
        audit.Record(target, null, null);

        if (ReasonError(request.Reason, limits) is { } refused)
        {
            return refused;
        }

        try
        {
            AdminAmountChange applied = await construction
                .SetExtraQueueSlotsAsync(playerId, planetId, request.ExtraQueueSlots, cancellationToken)
                .ConfigureAwait(false);

            audit.Record(target, applied.Before.ToString(CultureInfo.InvariantCulture), applied.After.ToString(CultureInfo.InvariantCulture));
            audit.RecordReason(request.Reason!.Trim());

            return Results.Json(new { extra_queue_slots = applied.After }, PlanetsJson.Options);
        }
        catch (GameException exception)
        {
            return Failure(exception);
        }
    }

    private static async Task<IResult> CancelAsync(
        string playerId,
        long planetId,
        long queueItemId,
        AdminCancelRequest request,
        ConstructionService construction,
        IWorldModule world,
        IGameClock clock,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        string target = $"construction:{playerId}:{planetId}:{queueItemId}";
        audit.Record(target, queueItemId.ToString(CultureInfo.InvariantCulture), null);

        if (ReasonError(request.Reason, limits) is { } refused)
        {
            return refused;
        }

        PlanetFacts? facts = await world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        try
        {
            await construction.CancelAsync(planetId, queueItemId, clock.GameNow, cancellationToken).ConfigureAwait(false);
            audit.Record(target, queueItemId.ToString(CultureInfo.InvariantCulture), "cancelled");
            audit.RecordReason(request.Reason!.Trim());

            return Results.Json(new { cancelled = true }, PlanetsJson.Options);
        }
        catch (GameException exception)
        {
            return Failure(exception);
        }
    }

    private static AdminColonyView Build(PlanetRead read, int extraQueueSlots, ResourceCatalog catalog)
    {
        PlanetOverviewDto overview = read.Overview;
        HashSet<int> stockIds = StockIds(catalog);
        Dictionary<int, string> typeNames = overview.ResourceTypes.ToDictionary(entry => entry.Id, entry => entry.NameKey);
        Dictionary<int, string> groupNames = overview.ResourceGroups.ToDictionary(entry => entry.Id, entry => entry.NameKey);
        Dictionary<int, string> buildingNames = overview.Buildings.ToDictionary(entry => entry.BuildingTypeId, entry => entry.NameKey);

        List<AdminColonyStockRow> stocks = [];

        foreach (PlanetResourceDto resource in overview.Resources)
        {
            if (!stockIds.Contains(resource.ResourceTypeId))
            {
                continue;
            }

            stocks.Add(new AdminColonyStockRow(
                resource.ResourceTypeId,
                typeNames.GetValueOrDefault(resource.ResourceTypeId, string.Empty),
                groupNames.GetValueOrDefault(resource.ResourceGroupId),
                resource.Amount));
        }

        HashSet<int> present = [.. stocks.Select(stock => stock.ResourceTypeId)];

        foreach (ResourceTypeFact resource in catalog.Resources.Where(entry => stockIds.Contains(entry.Id)).OrderBy(entry => entry.Id))
        {
            if (present.Contains(resource.Id))
            {
                continue;
            }

            string? groupName = resource.ResourceGroupId is int groupId ? groupNames.GetValueOrDefault(groupId) : null;
            stocks.Add(new AdminColonyStockRow(resource.Id, resource.NameKey, groupName, 0));
        }

        stocks.Sort((left, right) => left.ResourceTypeId.CompareTo(right.ResourceTypeId));

        long population = read.Population?.Amount ?? overview.Population;
        long maximum = read.Population?.Maximum ?? overview.PopulationMax;
        decimal growth = read.Population?.GrowthPerHour ?? overview.PopulationGrowthPerHour;

        return new AdminColonyView(
            overview.PlanetId,
            population,
            maximum,
            growth,
            extraQueueSlots,
            overview.Energy,
            stocks,
            [.. overview.Buildings.Select(building => new AdminColonyBuildingRow(building.BuildingTypeId, building.NameKey, building.Level, building.MaxLevel, building.QueuedLevel))],
            [.. overview.Queue.Items.Select(item => new AdminColonyQueueRow(
                item.Id,
                item.BuildingTypeId,
                buildingNames.GetValueOrDefault(item.BuildingTypeId, string.Empty),
                item.TargetLevel,
                item.Position,
                item.FinishesAt))]);
    }

    private static HashSet<int> StockIds(ResourceCatalog catalog)
    {
        HashSet<int> balanceGroups = [.. catalog.Groups.Where(group => group.IsBalance).Select(group => group.Id)];

        return
        [
            .. catalog.Resources
                .Where(resource => !resource.IsCivilizationScope && !resource.PaidByPopulation && (resource.ResourceGroupId is not int groupId || !balanceGroups.Contains(groupId)))
                .Select(resource => resource.Id),
        ];
    }

    private static IResult NotFound() =>
        Results.Json(new { error = GameErrorCodes.NotFound }, PlanetsJson.Options, statusCode: StatusCodes.Status404NotFound);

    private static IResult? ReasonError(string? reason, IAdminQueryLimits limits)
    {
        if (AdminReason.Check(reason, limits) is not { } reasonError)
        {
            return null;
        }

        return Results.Json(
            new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["reason"] = [reasonError] } },
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult Failure(GameException exception)
    {
        int status = exception.Code is GameErrorCodes.NotFound or PlanetsErrorCodes.PlanetUnknown
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;

        return Results.Json(new { error = exception.Code }, PlanetsJson.Options, statusCode: status);
    }
}
