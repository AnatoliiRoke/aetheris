using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Core.Time;
using Aetheris.Host.Auth;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Localization;
using Aetheris.Host.Security;
using Aetheris.Planets;
using Aetheris.Research;
using Aetheris.World.Api;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// The sign-in of an administrator.
/// </summary>
/// <param name="Email">The e-mail address.</param>
/// <param name="Password">The password.</param>
/// <param name="Code">The one-time code of the authenticator app, needed when <c>admin.require_totp</c> is on.</param>
public sealed record AdminLoginRequest(string? Email, string? Password, string? Code);

/// <summary>
/// A new value of a configuration parameter.
/// </summary>
/// <param name="Value">The new value.</param>
/// <param name="Reason">Why the value is changed; written to the journal.</param>
public sealed record ConfigChangeRequest(string? Value, string? Reason);

/// <summary>
/// A rollback of a configuration parameter to the value before one of its changes.
/// </summary>
/// <param name="ChangeId">The change whose old value comes back.</param>
/// <param name="Reason">Why the value is rolled back; written to the journal.</param>
public sealed record ConfigRollbackRequest(
    [property: JsonPropertyName("change_id")] long ChangeId,
    string? Reason);

/// <summary>
/// A new time multiplier.
/// </summary>
/// <param name="Multiplier">The new multiplier.</param>
public sealed record ClockChangeRequest(double Multiplier);

/// <summary>Validates <see cref="AdminLoginRequest"/>.</summary>
public sealed class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
{
    /// <summary>
    /// The largest length of a one-time code. The code of an authenticator app has six digits.
    /// </summary>
    public const int CodeMaxLength = 8;

    /// <summary>Initializes a new instance of the <see cref="AdminLoginRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public AdminLoginRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Email).NotEmpty().MaximumLength(settings.EmailMaxLength);
        RuleFor(request => request.Password).AccountPassword(settings);
        RuleFor(request => request.Code).MaximumLength(CodeMaxLength);
    }
}

/// <summary>Validates <see cref="ConfigChangeRequest"/>.</summary>
public sealed class ConfigChangeRequestValidator : AbstractValidator<ConfigChangeRequest>
{
    /// <summary>
    /// The length of the value column of <c>config_parameter</c>.
    /// </summary>
    public const int ValueMaxLength = 4096;

    /// <summary>Initializes a new instance of the <see cref="ConfigChangeRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public ConfigChangeRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.Value).NotNull().MaximumLength(ValueMaxLength);
        RuleFor(request => request.Reason).AdminReason(settings);
    }
}

/// <summary>Validates <see cref="ConfigRollbackRequest"/>.</summary>
public sealed class ConfigRollbackRequestValidator : AbstractValidator<ConfigRollbackRequest>
{
    /// <summary>Initializes a new instance of the <see cref="ConfigRollbackRequestValidator"/> class.</summary>
    /// <param name="settings">The security settings.</param>
    public ConfigRollbackRequestValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RuleFor(request => request.ChangeId).GreaterThan(0);
        RuleFor(request => request.Reason).AdminReason(settings);
    }
}

/// <summary>
/// The validation rules shared by the admin requests.
/// </summary>
public static class AdminValidationRules
{
    /// <summary>
    /// The reason of an administrator change: mandatory, with its length from the configuration.
    /// </summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="rule">The rule of the reason property.</param>
    /// <param name="settings">The security settings.</param>
    /// <returns>The same rule.</returns>
    public static IRuleBuilderOptions<T, string?> AdminReason<T>(this IRuleBuilder<T, string?> rule, SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(settings);

        return rule
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithErrorCode("required")
            .Must(reason => reason is null || reason.Trim().Length >= settings.AdminReasonMinLength).WithErrorCode("too_short")
            .Must(reason => reason is null || reason.Trim().Length <= settings.AdminReasonMaxLength).WithErrorCode("too_long");
    }
}

/// <summary>
/// The admin endpoints, separate from the game API (documentation/tech/04, section 6).
/// </summary>
/// <remarks>
/// Every endpoint except the sign-in needs an open administrator session and a role that
/// grants the action code of the endpoint (its name), and writes an audit record. The routes
/// have their own rate limit groups, stricter for the sign-in than for the players.
/// </remarks>
public static class AdminEndpoints
{
    /// <summary>
    /// The route prefix of the admin endpoints.
    /// </summary>
    public const string Prefix = "/api/admin";

    /// <summary>
    /// The action code of the sign-in, which is written by the sign-in itself.
    /// </summary>
    public const string SignInAction = "admin.sign_in";

    /// <summary>
    /// The number of random bytes of a session identifier.
    /// </summary>
    private const int SessionIdBytes = 32;

    /// <summary>
    /// Maps the admin endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder admin = endpoints.MapGroup(Prefix).WithValidation();

        admin.MapPost("/auth/login", SignInAsync)
            .WithName(SignInAction)
            .RequireRateLimiting(RateLimitGroups.AdminAuth);

        MapSecuredEndpoints(admin.MapGroup(string.Empty).WithAdminAccess());

        return endpoints;
    }

    /// <summary>
    /// Puts a group behind the administrator session, the admin rate limit, the audit record
    /// and the permission check of every request.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder WithAdminAccess(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // The audit filter is added first, so it wraps the access check and audits a refusal too.
        return group
            .RequireAuthorization(SecurityPolicies.Admin)
            .RequireRateLimiting(RateLimitGroups.Admin)
            .AddEndpointFilter<AdminAuditFilter>()
            .AddEndpointFilter<AdminAccessFilter>();
    }

    private static void MapSecuredEndpoints(RouteGroupBuilder secured)
    {
        secured.MapGet("/ping", () => Results.Ok(new { status = "ok" }))
            .WithName("admin.ping");

        secured.MapGet("/auth/me", ReadMeAsync)
            .WithName("admin.me");

        secured.MapPost("/auth/logout", SignOutAsync)
            .WithName("admin.sign_out");

        secured.MapGet("/config", ReadConfigAsync)
            .WithName("admin.config_read");

        secured.MapGet("/config/{key}/history", ReadConfigHistoryAsync)
            .WithName("admin.config_history_read");

        secured.MapPut("/config/{key}", ChangeConfigAsync)
            .WithName("admin.config_changed");

        secured.MapPost("/config/{key}/rollback", RollBackConfigAsync)
            .WithName("admin.config_rolled_back");

        secured.MapGet("/clock", ReadClock)
            .WithName("admin.clock_read");

        secured.MapPut("/clock/multiplier", ChangeClock)
            .WithName("admin.clock_changed");

        secured.MapGet("/audit", ReadAuditAsync)
            .WithName("admin.audit_read");

        // The actions of the modules share the same group, so they get the session, the
        // permission check, the rate limit and the audit record like the actions above.
        secured.MapWorldAdminEndpoints();
        secured.MapTextAdminEndpoints();
        secured.MapPlayerAdminEndpoints();
        secured.MapReferenceKindEndpoints();
        secured.MapReferenceImageAdminEndpoints();
        PlanetsModule.MapAdminEndpoints(secured);
        ResearchServiceCollectionExtensions.MapAdminEndpoints(secured);
    }

    private static async Task<IResult> SignInAsync(
        AdminLoginRequest request,
        HttpContext httpContext,
        SignInChecker checker,
        UserManager<IdentityUser> userManager,
        AetherisDbContext dbContext,
        SecuritySettings settings,
        SecurityEventLog securityLog,
        TimeProvider timeProvider)
    {
        SignInLimits limits = new(settings.AdminLoginMaxFailedAttempts, settings.AdminLoginLockout);

        SignInCheckResult result = await checker.CheckAsync(
            request.Email!,
            request.Password!,
            httpContext,
            SecurityEventTypes.AdminSignInFailed,
            limits: limits);

        if (result.Outcome == SignInOutcome.LockedOut)
        {
            return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.AccountLocked);
        }

        IdentityUser? user = result.User;
        AdminAccountEntity? account = result.Outcome == SignInOutcome.Succeeded && user is not null
            ? await dbContext.AdminAccounts.AsNoTracking().SingleOrDefaultAsync(entry => entry.IdentityUserId == user.Id)
            : null;

        if (user is null || account is null)
        {
            if (result.Outcome == SignInOutcome.Succeeded)
            {
                await securityLog.WriteAsync(SecurityEventTypes.AdminSignInFailed, httpContext, user?.Id, "not_an_administrator");
            }

            return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.InvalidCredentials);
        }

        string secondFactor = AetherisClaims.NoSecondFactor;

        // The second factor is a switch (task 00007, decision 6). When it is on, an account
        // without it, or a request without a code, never gets an administrator session.
        if (settings.AdminRequireTotp)
        {
            if (!account.TwoFactorEnabled || string.IsNullOrWhiteSpace(request.Code))
            {
                await securityLog.WriteAsync(SecurityEventTypes.AdminSignInFailed, httpContext, user.Id, "second_factor_missing");

                return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.SecondFactorRequired);
            }

            bool codeIsValid = await userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                request.Code);

            if (!codeIsValid)
            {
                await checker.RegisterFailureAsync(user, httpContext, SecurityEventTypes.AdminSignInFailed, "wrong_second_factor", limits);

                return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.SecondFactorRequired);
            }

            secondFactor = AetherisClaims.Totp;
        }

        await checker.ResetFailuresAsync(user);

        DateTimeOffset now = timeProvider.GetUtcNow();
        AdminSessionEntity session = new()
        {
            Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SessionIdBytes)),
            AdminAccountId = account.Id,
            SecondFactor = secondFactor,
            CreatedAt = now,
            LastActivityAt = now,
        };

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id),
            new(AetherisClaims.AdminId, account.Id),
            new(AetherisClaims.AdminSessionId, session.Id),
            new(AetherisClaims.SecondFactor, secondFactor),
        ];

        // The cookie outlives the idle timeout; the server ends the session itself
        // (AdminAccessFilter), and "remember me" does not exist for an administrator.
        await httpContext.SignInAsync(
            AuthSchemes.Admin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Admin)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = now + settings.AdminSessionMaxLifetime });

        dbContext.AdminSessions.Add(session);
        dbContext.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            AdminId = account.Id,
            Action = SignInAction,
            NewValue = secondFactor,
            At = now,
        });

        await dbContext.SaveChangesAsync(httpContext.RequestAborted);
        await securityLog.WriteAsync(SecurityEventTypes.AdminSignInSucceeded, httpContext, user.Id);

        return AdminJson.Ok(new { admin_id = account.Id });
    }

    private static async Task<IResult> SignOutAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        AetherisDbContext dbContext,
        TimeProvider timeProvider)
    {
        string? sessionId = principal.FindFirstValue(AetherisClaims.AdminSessionId);
        AdminSessionEntity? session = await dbContext.AdminSessions.SingleOrDefaultAsync(entry => entry.Id == sessionId);

        if (session is not null)
        {
            session.EndedAt = timeProvider.GetUtcNow();
            session.EndReason = AdminSessionEndReasons.SignedOut;
        }

        await httpContext.SignOutAsync(AuthSchemes.Admin);

        return Results.NoContent();
    }

    private static async Task<IResult> ReadMeAsync(
        ClaimsPrincipal principal,
        AetherisDbContext dbContext,
        AdminAccessService access,
        SecuritySettings settings,
        CancellationToken cancellationToken)
    {
        string adminId = principal.GetAdminId()!;
        AdminAccountEntity account = await dbContext.AdminAccounts.AsNoTracking()
            .SingleAsync(entry => entry.Id == adminId, cancellationToken);

        return AdminJson.Ok(new
        {
            admin_id = account.Id,
            display_name = AdminNames.Display(account.DisplayName),
            role_codes = await access.GetRoleCodesAsync(adminId, cancellationToken),
            permission_codes = await access.GetPermissionCodesAsync(adminId, cancellationToken),
            idle_minutes = settings.AdminSessionIdleTimeout.TotalMinutes,
            second_factor = principal.FindFirstValue(AetherisClaims.SecondFactor),
            require_totp = settings.AdminRequireTotp,
        });
    }

    private static async Task<IResult> ReadConfigAsync(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "sort")] string? sort,
        [FromQuery(Name = "direction")] string? direction,
        [FromQuery(Name = "group")] string? group,
        [FromQuery(Name = "read_only")] bool? readOnly,
        AdminConfigService configs,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? request = AdminPaging.Create(page, pageSize, search, sort, direction, limits);

        return request is null
            ? SearchTooLong()
            : AdminJson.Ok(await configs.ListAsync(request, group, readOnly, cancellationToken));
    }

    private static async Task<IResult> ReadConfigHistoryAsync(
        string key,
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        AdminConfigService configs,
        IAdminQueryLimits limits,
        AdminAuditScope audit,
        CancellationToken cancellationToken)
    {
        PageRequest request = AdminPaging.Create(page, pageSize, null, null, null, limits)!;

        audit.Record(key, null, null);

        return AdminJson.Ok(await configs.HistoryAsync(key, request, cancellationToken));
    }

    private static async Task<IResult> ChangeConfigAsync(
        string key,
        ConfigChangeRequest request,
        ClaimsPrincipal principal,
        AdminConfigService configs,
        AdminAuditScope audit,
        ConfigReloader reloader,
        CancellationToken cancellationToken)
    {
        string reason = request.Reason!.Trim();
        (ConfigChangeOutcome outcome, string? oldValue, string? newValue, int version) = await configs.StageChangeAsync(
            key,
            request.Value!,
            reason,
            principal.GetAdminId()!,
            null,
            cancellationToken);

        audit.Record(key, oldValue, newValue);
        audit.RecordReason(reason);

        return ChangeResult(outcome, key, newValue, version, audit, reloader, nameof(ConfigChangeRequest.Value));
    }

    private static async Task<IResult> RollBackConfigAsync(
        string key,
        ConfigRollbackRequest request,
        ClaimsPrincipal principal,
        AdminConfigService configs,
        AdminAuditScope audit,
        ConfigReloader reloader,
        CancellationToken cancellationToken)
    {
        ConfigParameterChangeEntity? change = await configs.FindChangeAsync(key, request.ChangeId, cancellationToken);

        if (change is null)
        {
            audit.Record(key, null, null);

            return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
        }

        string reason = request.Reason!.Trim();
        (ConfigChangeOutcome outcome, string? oldValue, string? newValue, int version) = await configs.StageChangeAsync(
            key,
            change.OldValue,
            reason,
            principal.GetAdminId()!,
            change.Id,
            cancellationToken);

        audit.Record(key, oldValue, newValue);
        audit.RecordReason(reason);

        return ChangeResult(outcome, key, newValue, version, audit, reloader, nameof(ConfigRollbackRequest.ChangeId));
    }

    private static IResult ChangeResult(
        ConfigChangeOutcome outcome,
        string key,
        string? value,
        int version,
        AdminAuditScope audit,
        ConfigReloader reloader,
        string field)
    {
        switch (outcome)
        {
            case ConfigChangeOutcome.NotFound:
                return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);

            case ConfigChangeOutcome.ReadOnly:
                return ApiErrors.Result(StatusCodes.Status403Forbidden, AdminErrorCodes.ReadOnlyParameter);

            case ConfigChangeOutcome.Invalid:
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [field] = ["out_of_bounds"],
                });

            default:
                audit.OnCommitted(() => reloader.ReloadAsync());

                return AdminJson.Ok(new { key, value, version });
        }
    }

    private static IResult ReadClock(GameClock clock, SecuritySettings settings) =>
        AdminJson.Ok(new
        {
            multiplier = clock.Multiplier,
            max_multiplier = settings.AdminMaxClockMultiplier,
            read_only = true,
        });

    private static IResult ChangeClock(
        ClockChangeRequest request,
        GameClock clock,
        SecuritySettings settings,
        AdminAuditScope audit)
    {
        if (request.Multiplier <= 0 || request.Multiplier > settings.AdminMaxClockMultiplier)
        {
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(ClockChangeRequest.Multiplier)] = ["out_of_bounds"],
            });
        }

        string oldValue = clock.Multiplier.ToString(CultureInfo.InvariantCulture);
        string newValue = request.Multiplier.ToString(CultureInfo.InvariantCulture);

        audit.Record("clock.multiplier", oldValue, newValue);
        audit.OnCommitted(() =>
        {
            clock.SetMultiplier(request.Multiplier);
            return Task.CompletedTask;
        });

        return Results.Ok(new { multiplier = request.Multiplier });
    }

    private static async Task<IResult> ReadAuditAsync(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "action")] string? action,
        [FromQuery(Name = "admin_id")] string? adminId,
        [FromQuery(Name = "changes_only")] bool? changesOnly,
        AetherisDbContext dbContext,
        AdminConfigService configs,
        IAdminQueryLimits limits,
        CancellationToken cancellationToken)
    {
        PageRequest? request = AdminPaging.Create(page, pageSize ?? limit, search, null, null, limits);

        if (request is null)
        {
            return SearchTooLong();
        }

        IQueryable<AdminAuditLogEntity> query = dbContext.AdminAuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(record => record.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(adminId))
        {
            query = query.Where(record => record.AdminId == adminId);
        }

        // The reads of the screens are audited too; "changes only" leaves the actions, their
        // refusals and the account events that have no endpoint.
        if (changesOnly == true)
        {
            List<string> actions = await dbContext.AdminPermissions.AsNoTracking()
                .Where(permission => permission.Kind == AdminPermissionKinds.Action)
                .Select(permission => permission.Code)
                .ToListAsync(cancellationToken);

            List<string> rejected = actions.Select(code => code + AdminAuditFilter.RejectedSuffix).ToList();
            actions.AddRange(rejected);
            actions.Add(SignInAction);
            actions.Add(AdminAccountService.AccountCreatedAction);

            query = query.Where(record => actions.Contains(record.Action));
        }

        if (request.Search is { } text)
        {
            string pattern = AdminPaging.ContainsPattern(text);

            query = query.Where(record => EF.Functions.ILike(record.Action, pattern, AdminPaging.LikeEscape)
                || (record.Target != null && EF.Functions.ILike(record.Target, pattern, AdminPaging.LikeEscape))
                || (record.Reason != null && EF.Functions.ILike(record.Reason, pattern, AdminPaging.LikeEscape)));
        }

        int total = await query.CountAsync(cancellationToken);
        List<AdminAuditLogEntity> records = await query
            .OrderByDescending(record => record.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        Dictionary<string, string> names = await configs.AdminNamesAsync(records.Select(record => record.AdminId), cancellationToken);

        return AdminJson.Ok(new PageResult<object>(
            [.. records.Select(record => (object)new
            {
                id = record.Id,
                admin_id = record.AdminId,
                admin_name = names.GetValueOrDefault(record.AdminId, record.AdminId),
                action = record.Action,
                target = record.Target,
                old_value = record.OldValue,
                new_value = record.NewValue,
                reason = record.Reason,
                at = record.At,
            })],
            total,
            request.Page,
            request.PageSize));
    }

    private static IResult SearchTooLong() =>
        ApiErrors.Validation(new Dictionary<string, string[]>
        {
            ["search"] = [AdminPaging.SearchTooLong],
        });
}
