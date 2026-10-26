using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.World;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// The account endpoints: registration, e-mail confirmation, sign-in, session refresh,
/// sign-out, password reset and the Google sign-in (task 00002, section 2.1).
/// </summary>
/// <remarks>
/// The answers never tell whether an e-mail address is registered: registration and the
/// reset request always answer <c>202</c>, a failed sign-in always answers
/// <c>invalid_credentials</c>.
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>
    /// The route prefix of the account endpoints.
    /// </summary>
    public const string Prefix = "/api/v1/auth";

    /// <summary>
    /// The error code of an address that registered too many accounts recently.
    /// </summary>
    public const string RegistrationLimitReached = "registration_limit_reached";

    /// <summary>
    /// The error code of a registration that needs the captcha.
    /// </summary>
    public const string CaptchaRequired = "captcha_required";

    /// <summary>
    /// The claim of the Google sign-in that confirms the e-mail address.
    /// </summary>
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>
    /// Maps the account endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup(Prefix)
            .RequireRateLimiting(RateLimitGroups.Auth)
            .WithValidation();

        group.MapGet("/csrf", GetCsrfToken);
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/resend-confirmation", ResendConfirmationAsync);
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(SecurityPolicies.Player);
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/reset-password", ResetPasswordAsync);
        group.MapGet("/me", GetMeAsync).RequireAuthorization(SecurityPolicies.Player);
        group.MapGet("/providers", GetProvidersAsync);
        group.MapGet("/google", StartGoogleAsync);
        group.MapGet("/google/complete", CompleteGoogleAsync);

        return endpoints;
    }

    private static IResult GetCsrfToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);

        return Results.Ok(new { token = tokens.RequestToken, header = AuthCookies.CsrfHeader });
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext httpContext,
        RegistrationService registration,
        SecurityEventLog securityLog)
    {
        RegistrationResult result = await registration.RegisterAsync(
            request,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.RequestAborted);

        switch (result.Outcome)
        {
            case RegistrationOutcome.Invalid:
                return ApiErrors.Validation(result.Errors);

            case RegistrationOutcome.LimitReached:
                await securityLog.WriteAsync(SecurityEventTypes.RateLimitExceeded, httpContext, null, "registration_ip_limit");
                return ApiErrors.Result(StatusCodes.Status429TooManyRequests, RegistrationLimitReached);

            case RegistrationOutcome.CaptchaRequired:
                return ApiErrors.Result(StatusCodes.Status400BadRequest, CaptchaRequired);
        }

        if (result.UserId is not null)
        {
            await securityLog.WriteAsync(SecurityEventTypes.AccountRegistered, httpContext, result.UserId);
        }

        return Accepted();
    }

    private static async Task<IResult> ResendConfirmationAsync(
        ResendConfirmationRequest request,
        UserManager<IdentityUser> userManager,
        ConfirmationMailer mailer,
        HttpContext httpContext)
    {
        IdentityUser? user = await userManager.FindByEmailAsync(request.Email!);

        // Only an account that still waits for its confirmation gets an e-mail, and not more
        // often than the configured pause; the answer is the same in every case, so it does not
        // tell which addresses are registered.
        if (user is { EmailConfirmed: false })
        {
            await mailer.SendAsync(user, httpContext.RequestAborted);
        }

        return Accepted();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        HttpContext httpContext,
        UserManager<IdentityUser> userManager,
        SecurityEventLog securityLog)
    {
        IdentityUser? user = await userManager.FindByIdAsync(request.UserId!);

        // The link works once: an address that is already confirmed refuses it again.
        if (user is null
            || user.EmailConfirmed
            || !(await userManager.ConfirmEmailAsync(user, request.Token!)).Succeeded)
        {
            return ApiErrors.Result(StatusCodes.Status400BadRequest, GameErrorCodes.InvalidToken);
        }

        await securityLog.WriteAsync(SecurityEventTypes.EmailConfirmed, httpContext, user.Id);

        return Results.Ok(new { status = "confirmed" });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        SignInChecker checker,
        SignInThrottle throttle,
        SessionIssuer sessions,
        AccountLifecycle lifecycle,
        AetherisDbContext dbContext,
        TimeProvider timeProvider,
        SecurityEventLog securityLog)
    {
        string login = request.Login!.Trim();
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        CancellationToken cancellationToken = httpContext.RequestAborted;

        // The lockout by the typed name and by the address answers the same for a registered
        // and an unknown name (task 00006, decision 4).
        if (await throttle.IsLockedAsync(login, ipAddress, cancellationToken))
        {
            await securityLog.WriteAsync(SecurityEventTypes.SignInFailed, httpContext, null, "throttled");

            return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.AccountLocked);
        }

        SignInCheckResult result = await checker.CheckAsync(
            login,
            request.Password!,
            httpContext,
            SecurityEventTypes.SignInFailed,
            requireConfirmedEmail: false);

        if (result.Outcome != SignInOutcome.Succeeded)
        {
            await throttle.RecordAsync(login, ipAddress, succeeded: false, cancellationToken);

            return result.Outcome == SignInOutcome.LockedOut
                ? ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.AccountLocked)
                : ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.InvalidCredentials);
        }

        IdentityUser user = result.User!;

        await throttle.RecordAsync(login, ipAddress, succeeded: true, cancellationToken);
        await checker.ResetFailuresAsync(user);

        // A sign-in within the delay of a requested deletion cancels it (task 00006, decision 10).
        bool deletionCancelled = await lifecycle.CancelDeletionAsync(user.Id, cancellationToken);

        dbContext.AccountAccessLog.Add(new AccountAccessLogEntity
        {
            UserId = user.Id,
            Kind = AccountAccessKinds.SignIn,
            IpAddress = ipAddress,
            At = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await sessions.StartAsync(httpContext, user, request.RememberMe);
        await securityLog.WriteAsync(SecurityEventTypes.SignInSucceeded, httpContext, user.Id, deletionCancelled ? "deletion_cancelled" : null);

        return Results.Ok(new { user_id = user.Id, deletion_cancelled = deletionCancelled });
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        RefreshTokenService refreshTokens,
        UserManager<IdentityUser> userManager,
        SessionIssuer sessions,
        SecurityEventLog securityLog)
    {
        if (!httpContext.Request.Cookies.TryGetValue(AuthCookies.Refresh, out string? token) || string.IsNullOrEmpty(token))
        {
            return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.InvalidToken);
        }

        RefreshResult result = await refreshTokens.RotateAsync(token, httpContext.RequestAborted);

        if (result.Reused)
        {
            await securityLog.WriteAsync(SecurityEventTypes.RefreshTokenReused, httpContext, result.UserId);
        }

        IdentityUser? user = result.UserId is null ? null : await userManager.FindByIdAsync(result.UserId);

        if (result.NewToken is null || user is null || await userManager.IsLockedOutAsync(user))
        {
            await sessions.EndAsync(httpContext);

            return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.InvalidToken);
        }

        await sessions.ContinueAsync(httpContext, user, result.NewToken, result.ExpiresAt, result.IsPersistent);
        await securityLog.WriteAsync(SecurityEventTypes.SessionRefreshed, httpContext, user.Id);

        return Results.Ok(new { userId = user.Id });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        SessionIssuer sessions,
        SecurityEventLog securityLog)
    {
        string? userId = httpContext.User.GetPlayerId();

        await sessions.EndAsync(httpContext);
        await securityLog.WriteAsync(SecurityEventTypes.SignedOut, httpContext, userId);

        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        HttpContext httpContext,
        UserManager<IdentityUser> userManager,
        IAccountEmailSender emailSender,
        SecurityEventLog securityLog)
    {
        IdentityUser? user = await userManager.FindByEmailAsync(request.Email!);

        if (user is { EmailConfirmed: true })
        {
            string token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emailSender.SendAsync(
                new AccountEmail(user.Id, user.Email!, AccountEmailKinds.PasswordReset, token),
                httpContext.RequestAborted);

            await securityLog.WriteAsync(SecurityEventTypes.PasswordResetRequested, httpContext, user.Id);
        }

        return Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        HttpContext httpContext,
        UserManager<IdentityUser> userManager,
        RefreshTokenService refreshTokens,
        SecurityEventLog securityLog)
    {
        IdentityUser? user = await userManager.FindByIdAsync(request.UserId!);

        if (user is null)
        {
            return ApiErrors.Result(StatusCodes.Status400BadRequest, GameErrorCodes.InvalidToken);
        }

        List<IdentityError> passwordErrors = await ValidatePasswordAsync(userManager, user, request.NewPassword!);

        if (passwordErrors.Count > 0)
        {
            return PasswordErrors(passwordErrors, nameof(ResetPasswordRequest.NewPassword));
        }

        IdentityResult reset = await userManager.ResetPasswordAsync(user, request.Token!, request.NewPassword!);

        if (!reset.Succeeded)
        {
            return ApiErrors.Result(StatusCodes.Status400BadRequest, GameErrorCodes.InvalidToken);
        }

        // A changed password ends every session, including one an attacker may hold.
        await refreshTokens.RevokeAllAsync(user.Id, httpContext.RequestAborted);
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        await securityLog.WriteAsync(SecurityEventTypes.PasswordChanged, httpContext, user.Id);

        return Results.Ok(new { status = "changed" });
    }

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        UserManager<IdentityUser> userManager,
        AetherisDbContext dbContext,
        IWorldModule world,
        HttpContext httpContext)
    {
        string? userId = principal.GetPlayerId();
        IdentityUser? user = userId is null ? null : await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.NotAuthenticated);
        }

        CancellationToken cancellationToken = httpContext.RequestAborted;
        PlayerProfileEntity? profile = await dbContext.PlayerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.UserId == user.Id, cancellationToken);

        // The account sees its own address; other players never get it (task 00006, decision 7).
        return Results.Ok(new
        {
            user_id = user.Id,
            nickname = user.UserName,
            email = user.Email,
            email_confirmed = user.EmailConfirmed,
            nickname_changed_at = profile?.NicknameChangedAt,
            civilization = await world.GetCivilizationAsync(user.Id, cancellationToken),
        });
    }

    private static async Task<IResult> GetProvidersAsync(IAuthenticationSchemeProvider schemes)
    {
        bool google = await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is not null;

        return Results.Ok(new { google });
    }

    private static async Task<IResult> StartGoogleAsync(
        string? returnUrl,
        IAuthenticationSchemeProvider schemes,
        SignInManager<IdentityUser> signInManager,
        ClientOrigins clientOrigins)
    {
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            return ApiErrors.Result(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
        }

        string redirectUri = $"{Prefix}/google/complete?returnUrl={Uri.EscapeDataString(clientOrigins.SafeReturnUrl(returnUrl))}";

        // The handler adds the "state" parameter and the correlation cookie, and checks
        // both when Google redirects back.
        AuthenticationProperties properties =
            signInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUri);

        return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> CompleteGoogleAsync(
        string? returnUrl,
        HttpContext httpContext,
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        SessionIssuer sessions,
        SecurityEventLog securityLog,
        ClientOrigins clientOrigins)
    {
        ExternalLoginInfo? info = await signInManager.GetExternalLoginInfoAsync();

        if (info is null)
        {
            return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.NotAuthenticated);
        }

        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        IdentityUser? user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        if (user is null)
        {
            string? email = info.Principal.FindFirstValue(ClaimTypes.Email);
            bool verified = string.Equals(info.Principal.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);

            // Only an address that Google confirmed may be linked to an account.
            if (string.IsNullOrEmpty(email) || !verified)
            {
                await securityLog.WriteAsync(SecurityEventTypes.SignInFailed, httpContext, null, "google_email_not_verified");

                return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.EmailNotConfirmed);
            }

            user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };

                if (!(await userManager.CreateAsync(user)).Succeeded)
                {
                    return ApiErrors.Result(StatusCodes.Status500InternalServerError, GameErrorCodes.InternalError);
                }

                await securityLog.WriteAsync(SecurityEventTypes.AccountRegistered, httpContext, user.Id, "google");
            }

            await userManager.AddLoginAsync(user, info);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.AccountLocked);
        }

        await sessions.StartAsync(httpContext, user);
        await securityLog.WriteAsync(SecurityEventTypes.SignInSucceeded, httpContext, user.Id, "google");

        return Results.Redirect(clientOrigins.SafeReturnUrl(returnUrl));
    }

    private static async Task<List<IdentityError>> ValidatePasswordAsync(
        UserManager<IdentityUser> userManager,
        IdentityUser user,
        string password)
    {
        List<IdentityError> errors = [];

        foreach (IPasswordValidator<IdentityUser> validator in userManager.PasswordValidators)
        {
            IdentityResult result = await validator.ValidateAsync(userManager, user, password);
            errors.AddRange(result.Errors);
        }

        return errors;
    }

    private static IResult PasswordErrors(IEnumerable<IdentityError> errors, string field) =>
        ApiErrors.Validation(new Dictionary<string, string[]>
        {
            [field] = errors.Select(error => error.Code).Distinct(StringComparer.Ordinal).ToArray(),
        });

    private static IResult Accepted() => Results.Accepted(value: new { status = "accepted" });
}
