using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Core.Admin;

/// <summary>
/// The endpoints of a reference kind on the secured admin group: the list, the record, the
/// creation, the change, the archiving and the return from the archive (task 00023,
/// section 4.1). There is no delete.
/// </summary>
/// <remarks>
/// The module that owns the kind maps them with its own route and action codes, so the module
/// decides about its reference and the host only plugs the group in. The host checks the
/// session and the permission of the action code and writes the audit record; the endpoints
/// state the changed row, its old and new values and the reason.
/// </remarks>
public static class AdminReferenceEndpoints
{
    /// <summary>
    /// Maps the endpoints of a kind.
    /// </summary>
    /// <typeparam name="TService">The service of the module that owns the kind.</typeparam>
    /// <param name="secured">The secured admin group.</param>
    /// <param name="descriptor">The kind.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapAdminReference<TService>(this RouteGroupBuilder secured, AdminReferenceDescriptor descriptor)
        where TService : IAdminReferenceService
    {
        ArgumentNullException.ThrowIfNull(secured);
        ArgumentNullException.ThrowIfNull(descriptor);

        string kind = descriptor.Kind;
        string route = descriptor.Route;
        AdminReferencePermissions permissions = descriptor.Permissions;

        secured.MapGet(route, (
            [FromQuery(Name = "page")] int? page,
            [FromQuery(Name = "page_size")] int? pageSize,
            [FromQuery(Name = "search")] string? search,
            [FromQuery(Name = "archived")] bool? archived,
            [FromQuery(Name = "all")] bool? all,
            TService service,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
            ReadAsync(service, kind, AdminPaging.Create(page, pageSize, search, null, null, limits), all == true ? null : archived ?? false, cancellationToken))
            .WithName(permissions.ListRead);

        secured.MapGet(route + "/{id:int}", async (int id, TService service, IAdminAuditRecorder audit, CancellationToken cancellationToken) =>
        {
            audit.Record(Target(kind, id), null, null);

            AdminReferenceRecord? record = await service.ReadOneAsync(kind, id, cancellationToken).ConfigureAwait(false);

            return record is null ? NotFound() : Results.Json(record, AdminReferenceJson.Options);
        })
            .WithName(permissions.RecordRead);

        secured.MapPost(route, (
            JsonElement input,
            ClaimsPrincipal principal,
            TService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
            ApplyAsync(kind, kind, AdminReferenceInput.Parse(input), audit, limits, parsed => service.CreateAsync(kind, parsed, AdminId(principal), cancellationToken)))
            .WithName(permissions.Added);

        secured.MapPut(route + "/{id:int}", (
            int id,
            JsonElement input,
            ClaimsPrincipal principal,
            TService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
            ApplyAsync(kind, Target(kind, id), AdminReferenceInput.Parse(input), audit, limits, parsed => service.UpdateAsync(kind, id, parsed, AdminId(principal), cancellationToken)))
            .WithName(permissions.Updated);

        secured.MapPut(route + "/{id:int}/archive", (
            int id,
            JsonElement input,
            TService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
            ApplyAsync(kind, Target(kind, id), AdminReferenceInput.Parse(input), audit, limits, parsed => service.SetArchivedAsync(kind, id, true, parsed, cancellationToken)))
            .WithName(permissions.Archived);

        secured.MapPut(route + "/{id:int}/restore", (
            int id,
            JsonElement input,
            TService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
            ApplyAsync(kind, Target(kind, id), AdminReferenceInput.Parse(input), audit, limits, parsed => service.SetArchivedAsync(kind, id, false, parsed, cancellationToken)))
            .WithName(permissions.Restored);

        return secured;
    }

    /// <summary>
    /// Builds the journal target of a row.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The row.</param>
    /// <returns>The target, for example <c>race:12</c>.</returns>
    public static string Target(string kind, int id) => kind + ":" + id.ToString(CultureInfo.InvariantCulture);

    private static async Task<IResult> ReadAsync(
        IAdminReferenceService service,
        string kind,
        PageRequest? page,
        bool? archived,
        CancellationToken cancellationToken)
    {
        if (page is null)
        {
            return Validation(StatusCodes.Status400BadRequest, new Dictionary<string, string[]> { ["search"] = [AdminPaging.SearchTooLong] });
        }

        PageResult<AdminReferenceRow> rows = await service.ReadAsync(kind, new AdminReferenceQuery(page, archived), cancellationToken).ConfigureAwait(false);

        return Results.Json(rows, AdminReferenceJson.Options);
    }

    /// <summary>
    /// Runs a change: the reason is mandatory, the change records its old and new values with the
    /// reason for the journal, and a refusal says why.
    /// </summary>
    private static async Task<IResult> ApplyAsync(
        string kind,
        string target,
        AdminReferenceInput input,
        IAdminAuditRecorder audit,
        IAdminQueryLimits limits,
        Func<AdminReferenceInput, Task<AdminReferenceChange>> change)
    {
        audit.Record(target, null, null);

        if (AdminReason.Check(input.Reason, limits) is { } reasonError)
        {
            return Validation(StatusCodes.Status400BadRequest, new Dictionary<string, string[]> { ["reason"] = [reasonError] });
        }

        try
        {
            AdminReferenceChange applied = await change(input).ConfigureAwait(false);

            audit.Record(applied.Target, applied.OldValue, applied.NewValue);
            audit.RecordReason(input.Reason!.Trim());

            return Results.Json(new { id = applied.Id, version = applied.Version, kind }, AdminReferenceJson.Options);
        }
        catch (AdminReferenceException exception)
        {
            return exception.Code switch
            {
                GameErrorCodes.NotFound => NotFound(),
                AdminReferenceErrors.VersionConflict => Results.Json(new { error = exception.Code }, statusCode: StatusCodes.Status409Conflict),
                _ => Validation(
                    exception.Fields.Values.SelectMany(codes => codes).Any(AdminReferenceErrors.RuleCodes.Contains)
                        ? StatusCodes.Status422UnprocessableEntity
                        : StatusCodes.Status400BadRequest,
                    exception.Fields),
            };
        }
    }

    private static IResult NotFound() =>
        Results.Json(new { error = GameErrorCodes.NotFound }, statusCode: StatusCodes.Status404NotFound);

    private static IResult Validation(int statusCode, IReadOnlyDictionary<string, string[]> fields) =>
        Results.Json(new { error = GameErrorCodes.ValidationFailed, details = fields }, statusCode: statusCode);

    private static string AdminId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(AdminRoles.AdminIdClaimType)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no administrator.");
}
