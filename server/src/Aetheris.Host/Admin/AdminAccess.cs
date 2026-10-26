using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// The JSON settings of the admin panel: snake_case names, as in the rest of the API
/// (documentation/tech/08, rule 3).
/// </summary>
public static class AdminJson
{
    /// <summary>
    /// Gets the serializer settings.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Writes a value with the settings of the admin panel.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static IResult Ok(object? value) => Results.Json(value, Options);
}

/// <summary>
/// The error codes of the admin panel.
/// </summary>
public static class AdminErrorCodes
{
    /// <summary>The administrator session has ended or does not exist.</summary>
    public const string SessionExpired = "session_expired";

    /// <summary>The endpoint has no action code, so no permission can allow it.</summary>
    public const string ActionCodeMissing = "action_code_missing";

    /// <summary>The parameter is shown in the admin panel but never changed there.</summary>
    public const string ReadOnlyParameter = "read_only_parameter";
}

/// <summary>
/// The roles and the permissions of the administrators (task 00007, decision 4).
/// </summary>
/// <remarks>
/// Every check reads the database, so a role or a permission changed in the tables takes
/// effect on the next request, without a new sign-in.
/// </remarks>
public sealed class AdminAccessService
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAccessService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public AdminAccessService(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <summary>
    /// Checks whether one of the roles of an administrator grants a permission.
    /// </summary>
    /// <param name="adminId">The administrator account.</param>
    /// <param name="actionCode">The action code of the endpoint.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the action is allowed.</returns>
    public Task<bool> HasPermissionAsync(string adminId, string actionCode, CancellationToken cancellationToken) =>
        _dbContext.AdminAccountRoles
            .Where(accountRole => accountRole.AdminAccountId == adminId)
            .Join(_dbContext.AdminRolePermissions, accountRole => accountRole.AdminRoleId, grant => grant.AdminRoleId, (accountRole, grant) => grant.AdminPermissionId)
            .Join(_dbContext.AdminPermissions, permissionId => permissionId, permission => permission.Id, (permissionId, permission) => permission.Code)
            .AnyAsync(code => code == actionCode, cancellationToken);

    /// <summary>
    /// Lists the permissions of an administrator, for the client that hides what is not allowed.
    /// </summary>
    /// <param name="adminId">The administrator account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The action codes.</returns>
    public Task<List<string>> GetPermissionCodesAsync(string adminId, CancellationToken cancellationToken) =>
        _dbContext.AdminAccountRoles
            .Where(accountRole => accountRole.AdminAccountId == adminId)
            .Join(_dbContext.AdminRolePermissions, accountRole => accountRole.AdminRoleId, grant => grant.AdminRoleId, (accountRole, grant) => grant.AdminPermissionId)
            .Join(_dbContext.AdminPermissions, permissionId => permissionId, permission => permission.Id, (permissionId, permission) => permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Lists the role codes of an administrator.
    /// </summary>
    /// <param name="adminId">The administrator account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The role codes.</returns>
    public Task<List<string>> GetRoleCodesAsync(string adminId, CancellationToken cancellationToken) =>
        _dbContext.AdminAccountRoles
            .Where(accountRole => accountRole.AdminAccountId == adminId)
            .Join(_dbContext.AdminRoles, accountRole => accountRole.AdminRoleId, role => role.Id, (accountRole, role) => role)
            .OrderBy(role => role.SortOrder)
            .Select(role => role.Code)
            .ToListAsync(cancellationToken);
}

/// <summary>
/// The limits of the admin tables, from the security parameters.
/// </summary>
public sealed class AdminQueryLimits : IAdminQueryLimits
{
    private readonly SecuritySettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminQueryLimits"/> class.
    /// </summary>
    /// <param name="settings">The security settings.</param>
    public AdminQueryLimits(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <inheritdoc />
    public int MaxPageSize => _settings.AdminPageSize;

    /// <inheritdoc />
    public int SearchMaxLength => _settings.AdminSearchMaxLength;

    /// <inheritdoc />
    public int ReasonMinLength => _settings.AdminReasonMinLength;

    /// <inheritdoc />
    public int ReasonMaxLength => _settings.AdminReasonMaxLength;
}

/// <summary>
/// Checks every request of the secured admin group: the session is alive, the second factor
/// is there when it is required, and a role of the administrator grants the action code of
/// the endpoint (task 00007, decisions 4–6).
/// </summary>
/// <remarks>
/// The filter runs inside <see cref="AdminAuditFilter"/>, so a refused request is audited too,
/// and the time of the last activity is saved together with the audit record. An endpoint
/// without an action code is refused: no admin action can be added without a permission.
/// </remarks>
public sealed class AdminAccessFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        HttpContext httpContext = context.HttpContext;
        IServiceProvider services = httpContext.RequestServices;
        CancellationToken cancellationToken = httpContext.RequestAborted;

        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        SecuritySettings settings = services.GetRequiredService<SecuritySettings>();
        DateTimeOffset now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        string? adminId = httpContext.User.GetAdminId();
        string? sessionId = httpContext.User.FindFirst(AetherisClaims.AdminSessionId)?.Value;

        AdminSessionEntity? session = adminId is null || sessionId is null
            ? null
            : await dbContext.AdminSessions
                .SingleOrDefaultAsync(entry => entry.Id == sessionId && entry.AdminAccountId == adminId, cancellationToken)
                .ConfigureAwait(false);

        if (session is null || session.EndedAt is not null)
        {
            await httpContext.SignOutAsync(AuthSchemes.Admin).ConfigureAwait(false);

            return ApiErrors.Result(StatusCodes.Status401Unauthorized, AdminErrorCodes.SessionExpired);
        }

        string? endReason = now - session.LastActivityAt > settings.AdminSessionIdleTimeout ? AdminSessionEndReasons.Idle
            : now - session.CreatedAt > settings.AdminSessionMaxLifetime ? AdminSessionEndReasons.Expired
            : settings.AdminRequireTotp && session.SecondFactor != AetherisClaims.Totp ? AdminSessionEndReasons.SecondFactorRequired
            : null;

        if (endReason is not null)
        {
            session.EndedAt = now;
            session.EndReason = endReason;
            await httpContext.SignOutAsync(AuthSchemes.Admin).ConfigureAwait(false);

            return ApiErrors.Result(
                StatusCodes.Status401Unauthorized,
                endReason == AdminSessionEndReasons.SecondFactorRequired ? GameErrorCodes.SecondFactorRequired : AdminErrorCodes.SessionExpired);
        }

        string? actionCode = httpContext.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;

        if (string.IsNullOrEmpty(actionCode))
        {
            return ApiErrors.Result(StatusCodes.Status403Forbidden, AdminErrorCodes.ActionCodeMissing);
        }

        if (!await services.GetRequiredService<AdminAccessService>().HasPermissionAsync(session.AdminAccountId, actionCode, cancellationToken).ConfigureAwait(false))
        {
            return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.AccessDenied);
        }

        session.LastActivityAt = now;

        return await next(context).ConfigureAwait(false);
    }
}
