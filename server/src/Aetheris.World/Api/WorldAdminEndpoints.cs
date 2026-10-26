using System.Globalization;
using System.Security.Claims;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.World.Api;

/// <summary>
/// The screens of the admin panel for the world: the generation of sectors, the tables of
/// what was generated (task 00007, decisions 2 and 3), the cards that correct a field, a body
/// or a system, and the reference tables of the generation (task 00009).
/// </summary>
/// <remarks>
/// The routes are mapped on the secured admin group of the host, which checks the session,
/// the permission of the action code (the endpoint name) and writes the audit record.
/// </remarks>
public static class WorldAdminEndpoints
{
    /// <summary>
    /// Maps the generation and the tables on the secured admin group.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapWorldAdminScreens(this RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapGet("/world/reference", async (WorldQueryService queries, CancellationToken cancellationToken) =>
            Results.Json(await queries.GetReferenceAsync(cancellationToken).ConfigureAwait(false), WorldJson.Options))
            .WithName("admin.world_reference_read");

        secured.MapGet("/world/sectors", ReadSectorsAsync)
            .WithName("admin.world_sectors_read");

        secured.MapGet("/world/systems", ReadSystemsAsync)
            .WithName("admin.world_systems_read");

        secured.MapGet("/world/objects", ReadBodiesAsync)
            .WithName("admin.world_objects_read");

        secured.MapGet("/world/fields", ReadFieldsAsync)
            .WithName("admin.world_fields_read");

        secured.MapGet("/world/fields/{fieldId:long}", ReadFieldAsync)
            .WithName("admin.world_field_read");

        secured.MapPut("/world/fields/{fieldId:long}", UpdateFieldAsync)
            .WithName("admin.world_field_updated");

        secured.MapGet("/world/objects/{planetId:long}", ReadBodyAsync)
            .WithName("admin.world_object_read");

        secured.MapPut("/world/objects/{planetId:long}", UpdateBodyAsync)
            .WithName("admin.world_object_updated");

        secured.MapGet("/world/systems/{systemId:long}", ReadSystemAsync)
            .WithName("admin.world_system_read");

        secured.MapPut("/world/systems/{systemId:long}", UpdateSystemAsync)
            .WithName("admin.world_system_updated");

        secured.MapGet("/world/object-types", ReadObjectTypesAsync)
            .WithName("admin.world_object_types_read");

        secured.MapPut("/world/object-types/{objectTypeId:int}", UpdateObjectTypeAsync)
            .WithName("admin.world_object_type_updated");

        secured.MapGet("/world/generation-rules", ReadRulesAsync)
            .WithName("admin.world_generation_rules_read");

        secured.MapPost("/world/generation-rules", AddRuleAsync)
            .WithName("admin.world_generation_rule_added");

        secured.MapPut("/world/generation-rules/{ruleId:int}", UpdateRuleAsync)
            .WithName("admin.world_generation_rule_updated");

        secured.MapPut("/world/generation-rule-groups/{ruleGroupId:int}", UpdateRuleGroupAsync)
            .WithName("admin.world_generation_rule_group_updated");

        secured.MapGet("/world/generation/estimate", EstimateAsync)
            .WithName("admin.world_generation_estimated");

        secured.MapPost("/world/generation-runs", StartAsync)
            .WithName("admin.world_generation_started");

        secured.MapGet("/world/generation-runs", ReadRunsAsync)
            .WithName("admin.world_generation_runs_read");

        secured.MapGet("/world/generation-runs/{runId:long}", ReadRunAsync)
            .WithName("admin.world_generation_run_read");

        return secured;
    }

    private static async Task<IResult> ReadSectorsAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "is_newbie")] bool? isNewbie,
        WorldAdminQueryService queries,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(await queries.GetSectorsAsync(page, isNewbie, cancellationToken).ConfigureAwait(false), WorldJson.Options);
    }

    private static async Task<IResult> ReadSystemsAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "sector_id")] int? sectorId,
        [FromQuery(Name = "in_nebula")] bool? inNebula,
        WorldAdminQueryService queries,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(await queries.GetSystemsAsync(page, sectorId, inNebula, cancellationToken).ConfigureAwait(false), WorldJson.Options);
    }

    private static async Task<IResult> ReadBodiesAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "sector_id")] int? sectorId,
        [FromQuery(Name = "system_id")] long? systemId,
        [FromQuery(Name = "object_type_id")] int? objectTypeId,
        [FromQuery(Name = "object_class")] string? objectClass,
        [FromQuery(Name = "owned")] bool? owned,
        WorldAdminQueryService queries,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(
                await queries.GetBodiesAsync(page, new AdminBodyFilter(sectorId, systemId, objectTypeId, objectClass, owned), cancellationToken).ConfigureAwait(false),
                WorldJson.Options);
    }

    private static async Task<IResult> ReadFieldsAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "sector_id")] int? sectorId,
        [FromQuery(Name = "system_id")] long? systemId,
        [FromQuery(Name = "free")] bool? free,
        [FromQuery(Name = "depleted")] bool? depleted,
        WorldAdminQueryService queries,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(
                await queries.GetFieldsAsync(page, new AdminFieldFilter(sectorId, systemId, free, depleted), cancellationToken).ConfigureAwait(false),
                WorldJson.Options);
    }

    private static async Task<IResult> ReadFieldAsync(
        long fieldId,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        audit.Record(FieldTarget(fieldId), null, null);

        return Card(await edits.GetFieldCardAsync(fieldId, cancellationToken).ConfigureAwait(false));
    }

    private static Task<IResult> UpdateFieldAsync(
        long fieldId,
        AdminFieldUpdate request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            FieldTarget(fieldId),
            request.Reason,
            audit,
            limits,
            () => edits.UpdateFieldAsync(fieldId, request, cancellationToken),
            async () => Card(await edits.GetFieldCardAsync(fieldId, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> ReadBodyAsync(
        long planetId,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        audit.Record(BodyTarget(planetId), null, null);

        return Card(await edits.GetBodyCardAsync(planetId, cancellationToken).ConfigureAwait(false));
    }

    private static Task<IResult> UpdateBodyAsync(
        long planetId,
        AdminBodyUpdate request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            BodyTarget(planetId),
            request.Reason,
            audit,
            limits,
            () => edits.UpdateBodyAsync(planetId, request, cancellationToken),
            async () => Card(await edits.GetBodyCardAsync(planetId, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> ReadSystemAsync(
        long systemId,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        audit.Record(SystemTarget(systemId), null, null);

        return Card(await edits.GetSystemCardAsync(systemId, cancellationToken).ConfigureAwait(false));
    }

    private static Task<IResult> UpdateSystemAsync(
        long systemId,
        AdminSystemUpdate request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            SystemTarget(systemId),
            request.Reason,
            audit,
            limits,
            () => edits.UpdateSystemAsync(systemId, request, cancellationToken),
            async () => Card(await edits.GetSystemCardAsync(systemId, cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> ReadObjectTypesAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "object_class")] string? objectClass,
        WorldAdminQueryService queries,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(await queries.GetObjectTypesAsync(page, objectClass, cancellationToken).ConfigureAwait(false), WorldJson.Options);
    }

    private static Task<IResult> UpdateObjectTypeAsync(
        int objectTypeId,
        AdminObjectTypeUpdate request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            "object_type:" + objectTypeId.ToString(CultureInfo.InvariantCulture),
            request.Reason,
            audit,
            limits,
            () => edits.UpdateObjectTypeAsync(objectTypeId, request, cancellationToken),
            null);

    private static async Task<IResult> ReadRulesAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "object_type_id")] int? objectTypeId,
        [FromQuery(Name = "is_active")] bool? isActive,
        WorldAdminQueryService queries,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(await queries.GetRulesAsync(page, new AdminRuleFilter(objectTypeId, isActive), cancellationToken).ConfigureAwait(false), WorldJson.Options);
    }

    private static Task<IResult> AddRuleAsync(
        AdminRuleInput request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            "generation_rule_group:" + request.RuleGroupId.ToString(CultureInfo.InvariantCulture),
            request.Reason,
            audit,
            limits,
            () => edits.AddRuleAsync(request, cancellationToken),
            null);

    private static Task<IResult> UpdateRuleAsync(
        int ruleId,
        AdminRuleInput request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            "generation_rule:" + ruleId.ToString(CultureInfo.InvariantCulture),
            request.Reason,
            audit,
            limits,
            () => edits.UpdateRuleAsync(ruleId, request, cancellationToken),
            null);

    private static Task<IResult> UpdateRuleGroupAsync(
        int ruleGroupId,
        AdminRuleGroupUpdate request,
        WorldAdminEditService edits,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken) =>
        ApplyAsync(
            "generation_rule_group:" + ruleGroupId.ToString(CultureInfo.InvariantCulture),
            request.Reason,
            audit,
            limits,
            () => edits.UpdateRuleGroupAsync(ruleGroupId, request, cancellationToken),
            null);

    /// <summary>
    /// Runs a change of the admin panel: the reason is mandatory, the change records its old
    /// and new values with the reason for the journal, and a refusal says why (task 00009,
    /// decisions 3, 4 and 6).
    /// </summary>
    private static async Task<IResult> ApplyAsync(
        string target,
        string? reason,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        Func<Task<AdminChange>> change,
        Func<Task<IResult>>? read)
    {
        audit.Record(target, null, null);

        if (AdminReason.Check(reason, limits) is { } reasonError)
        {
            return Results.Json(
                new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["reason"] = [reasonError] } },
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            AdminChange applied = await change().ConfigureAwait(false);

            audit.Record(applied.Target, applied.OldValue, applied.NewValue);
            audit.RecordReason(reason!.Trim());

            return read is null
                ? Results.Json(new { id = applied.Id, version = applied.Version }, WorldJson.Options)
                : await read().ConfigureAwait(false);
        }
        catch (WorldAdminEditException exception)
        {
            return exception.Code switch
            {
                GameErrorCodes.NotFound => Results.Json(new { error = exception.Code }, statusCode: StatusCodes.Status404NotFound),
                WorldErrorCodes.VersionConflict => Results.Json(new { error = exception.Code }, statusCode: StatusCodes.Status409Conflict),
                WorldErrorCodes.ConfigInvalid => Results.Json(
                    new { error = exception.Code, details = new { problems = exception.Problems } },
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => Results.Json(new { error = exception.Code, details = exception.Fields }, statusCode: StatusCodes.Status400BadRequest),
            };
        }
    }

    private static IResult Card<T>(T? card)
        where T : class =>
        card is null
            ? Results.Json(new { error = GameErrorCodes.NotFound }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(card, WorldJson.Options);

    private static string FieldTarget(long fieldId) => "field:" + fieldId.ToString(CultureInfo.InvariantCulture);

    private static string BodyTarget(long planetId) => "planet:" + planetId.ToString(CultureInfo.InvariantCulture);

    private static string SystemTarget(long systemId) => "system:" + systemId.ToString(CultureInfo.InvariantCulture);

    private static async Task<IResult> EstimateAsync(
        [FromQuery(Name = "sector_count")] int? sectorCount,
        GenerationRunService runs,
        CancellationToken cancellationToken) =>
        Results.Json(await runs.EstimateAsync(sectorCount ?? 1, cancellationToken).ConfigureAwait(false), WorldJson.Options);

    private static async Task<IResult> StartAsync(
        StartGenerationRequest request,
        ClaimsPrincipal principal,
        GenerationRunService runs,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        string parameters = string.Create(
            CultureInfo.InvariantCulture,
            $"sector_count={request.SectorCount}; seed={request.Seed?.ToString(CultureInfo.InvariantCulture) ?? "-"}; rule_set={request.RuleSet ?? "-"}");

        try
        {
            GenerationRunDto run = await runs.StartAsync(request, AdminId(principal), cancellationToken).ConfigureAwait(false);

            audit.Record(RunTarget(run.Id), null, parameters);

            return Results.Json(run, WorldJson.Options, statusCode: StatusCodes.Status202Accepted);
        }
        catch (GameException exception)
        {
            audit.Record("generation_run", null, parameters);

            int statusCode = exception.Code == WorldErrorCodes.GenerationRunning
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;

            return Results.Json(new { error = exception.Code, details = exception.Details }, WorldJson.Options, statusCode: statusCode);
        }
    }

    private static async Task<IResult> ReadRunsAsync(
        [AsParameters] PageQuery query,
        [FromQuery(Name = "status")] string? status,
        GenerationRunService runs,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? page = query.ToRequest(limits);

        return page is null
            ? SearchTooLong()
            : Results.Json(await runs.ListAsync(page, status, cancellationToken).ConfigureAwait(false), WorldJson.Options);
    }

    private static async Task<IResult> ReadRunAsync(
        long runId,
        GenerationRunService runs,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        GenerationRunDto? run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);

        audit.Record(RunTarget(runId), null, null);

        return run is null
            ? Results.Json(new { error = GameErrorCodes.NotFound }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(run, WorldJson.Options);
    }

    private static string AdminId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(AdminRoles.AdminIdClaimType) ?? WorldGenerationService.SystemActor;

    private static string RunTarget(long runId) => "generation_run:" + runId.ToString(CultureInfo.InvariantCulture);

    private static IResult SearchTooLong() =>
        Results.Json(
            new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["search"] = [AdminPaging.SearchTooLong] } },
            statusCode: StatusCodes.Status400BadRequest);

    /// <summary>
    /// The paging parameters of the query string, in snake_case like the rest of the API.
    /// </summary>
    /// <param name="Page">The page number.</param>
    /// <param name="PageSize">The page size.</param>
    /// <param name="Search">The search text.</param>
    /// <param name="Sort">The sort column.</param>
    /// <param name="Direction">The direction.</param>
    public sealed record PageQuery(
        [FromQuery(Name = "page")] int? Page,
        [FromQuery(Name = "page_size")] int? PageSize,
        [FromQuery(Name = "search")] string? Search,
        [FromQuery(Name = "sort")] string? Sort,
        [FromQuery(Name = "direction")] string? Direction)
    {
        /// <summary>
        /// Builds the page request.
        /// </summary>
        /// <param name="limits">The limits from the configuration.</param>
        /// <returns>The request, or <see langword="null"/> when the search text is too long.</returns>
        public PageRequest? ToRequest(IAdminQueryLimits limits) =>
            AdminPaging.Create(Page, PageSize, Search, Sort, Direction, limits);
    }
}
