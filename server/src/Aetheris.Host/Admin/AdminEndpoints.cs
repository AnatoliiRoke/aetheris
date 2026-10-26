using System.Globalization;
using System.Security.Claims;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Core.Time;
using Aetheris.Host.Auth;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.World.Api;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// The sign-in of an administrator.
/// </summary>
/// <param name="Email">The e-mail address.</param>
/// <param name="Password">The password.</param>
/// <param name="Code">The one-time code of the authenticator app.</param>
public sealed record AdminLoginRequest(string? Email, string? Password, string? Code);

/// <summary>
/// A new value of a configuration parameter.
/// </summary>
/// <param name="Value">The new value.</param>
public sealed record ConfigChangeRequest(string? Value);

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
    public ConfigChangeRequestValidator()
    {
        RuleFor(request => request.Value).NotNull().MaximumLength(ValueMaxLength);
    }
}

/// <summary>
/// The admin endpoints, separate from the game API (documentation/tech/04, section 6).
/// </summary>
/// <remarks>
/// Every endpoint except the sign-in needs an administrator session that passed the second
/// factor, and writes an audit record. The routes have their own rate limit group.
/// </remarks>
public static class AdminEndpoints
{
    /// <summary>
    /// The route prefix of the admin endpoints.
    /// </summary>
    public const string Prefix = "/api/admin";

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
            .WithName("admin.sign_in")
            .RequireRateLimiting(RateLimitGroups.Auth);

        RouteGroupBuilder secured = admin.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.Admin)
            .RequireRateLimiting(RateLimitGroups.Admin)
            .AddEndpointFilter<AdminAuditFilter>();

        secured.MapGet("/ping", () => Results.Ok(new { status = "ok" }))
            .WithName("admin.ping");

        secured.MapPut("/config/{key}", ChangeConfigAsync)
            .WithName("admin.config_changed")
            .RequireAuthorization(SecurityPolicies.AdminBalance);

        secured.MapPut("/clock/multiplier", ChangeClock)
            .WithName("admin.clock_changed")
            .RequireAuthorization(SecurityPolicies.AdminOwner);

        secured.MapGet("/audit", ReadAuditAsync)
            .WithName("admin.audit_read")
            .RequireAuthorization(SecurityPolicies.AdminSupport);

        // The actions of the modules share the same group, so they get the second factor,
        // the rate limit and the audit record like the actions above.
        secured.MapWorldAdminEndpoints();
        secured.MapPlayerAdminEndpoints();

        return endpoints;
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
        SignInCheckResult result = await checker.CheckAsync(
            request.Email!,
            request.Password!,
            httpContext,
            SecurityEventTypes.AdminSignInFailed);

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

        // The second factor is mandatory: an account without it, or a request without a
        // code, never gets an administrator session.
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
            await checker.RegisterFailureAsync(user, httpContext, SecurityEventTypes.AdminSignInFailed, "wrong_second_factor");

            return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.SecondFactorRequired);
        }

        await checker.ResetFailuresAsync(user);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id),
            new(AetherisClaims.AdminId, account.Id),
            new(AetherisClaims.SecondFactor, AetherisClaims.Totp),
        ];

        claims.AddRange(account.Roles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => new Claim(AdminRoles.ClaimType, role)));

        DateTimeOffset now = timeProvider.GetUtcNow();

        await httpContext.SignInAsync(
            AuthSchemes.Admin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Admin)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = now + settings.AdminSessionLifetime });

        dbContext.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            AdminId = account.Id,
            Action = "admin.sign_in",
            At = now,
        });

        await dbContext.SaveChangesAsync(httpContext.RequestAborted);
        await securityLog.WriteAsync(SecurityEventTypes.AdminSignInSucceeded, httpContext, user.Id);

        return Results.Ok(new { adminId = account.Id });
    }

    private static async Task<IResult> ChangeConfigAsync(
        string key,
        ConfigChangeRequest request,
        ClaimsPrincipal principal,
        AetherisDbContext dbContext,
        AdminAuditScope audit,
        ConfigReloader reloader,
        TimeProvider timeProvider)
    {
        ConfigParameterEntity? parameter = await dbContext.ConfigParameters.SingleOrDefaultAsync(entry => entry.Key == key);

        if (parameter is null)
        {
            return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
        }

        string value = request.Value!;
        ConfigParameterDefinition? definition = ConfigSeed.Find(key);

        // A value outside the bounds of its definition is refused (documentation/tech/05,
        // section 8): a typo must not switch a protection off.
        if (definition is not null && !definition.Accepts(value))
        {
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(ConfigChangeRequest.Value)] = ["out_of_bounds"],
            });
        }

        string adminId = principal.GetAdminId()!;
        DateTimeOffset now = timeProvider.GetUtcNow();
        string oldValue = parameter.Value;

        parameter.Value = value;
        parameter.Version++;
        parameter.UpdatedAt = now;
        parameter.UpdatedBy = adminId;

        int lastVersion = await dbContext.ConfigVersions.MaxAsync(version => (int?)version.Version) ?? 0;
        dbContext.ConfigVersions.Add(new ConfigVersionEntity { Version = lastVersion + 1, AppliedAt = now, AppliedBy = adminId });

        audit.Record(key, oldValue, value);
        audit.OnCommitted(() => reloader.ReloadAsync());

        return Results.Ok(new { key, value, version = parameter.Version });
    }

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

    private static async Task<IResult> ReadAuditAsync(int? limit, AetherisDbContext dbContext, SecuritySettings settings)
    {
        int pageSize = Math.Clamp(limit ?? settings.AdminAuditPageSize, 1, settings.AdminAuditPageSize);

        List<AdminAuditLogEntity> records = await dbContext.AdminAuditLogs
            .AsNoTracking()
            .OrderByDescending(record => record.Id)
            .Take(pageSize)
            .ToListAsync();

        return Results.Ok(records.Select(record => new
        {
            record.Id,
            record.AdminId,
            record.Action,
            record.Target,
            record.OldValue,
            record.NewValue,
            record.At,
        }));
    }
}
