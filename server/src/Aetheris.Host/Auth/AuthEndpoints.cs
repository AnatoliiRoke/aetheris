using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;

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
        group.MapPost("/confirm-email", ConfirmEmailAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization(SecurityPolicies.Player);
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/reset-password", ResetPasswordAsync);
        group.MapGet("/me", GetMe).RequireAuthorization(SecurityPolicies.Player);
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
        UserManager<IdentityUser> userManager,
        IAccountEmailSender emailSender,
        SecurityEventLog securityLog)
    {
        string email = request.Email!;
        IdentityUser user = new() { UserName = email, Email = email };

        // The password is checked before the address, so a weak password gets the same
        // answer for a new and for a registered address.
        List<IdentityError> passwordErrors = await ValidatePasswordAsync(userManager, user, request.Password!);

        if (passwordErrors.Count > 0)
        {
            return PasswordErrors(passwordErrors, nameof(RegisterRequest.Password));
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Accepted();
        }

        IdentityResult created = await userManager.CreateAsync(user, request.Password!);

        if (!created.Succeeded)
        {
            return PasswordErrors([.. created.Errors], nameof(RegisterRequest.Password));
        }

        string token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendAsync(
            new AccountEmail(user.Id, email, AccountEmailKinds.Confirmation, token),
            httpContext.RequestAborted);

        await securityLog.WriteAsync(SecurityEventTypes.AccountRegistered, httpContext, user.Id);

        return Accepted();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        HttpContext httpContext,
        UserManager<IdentityUser> userManager,
        SecurityEventLog securityLog)
    {
        IdentityUser? user = await userManager.FindByIdAsync(request.UserId!);

        if (user is null || !(await userManager.ConfirmEmailAsync(user, request.Token!)).Succeeded)
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
        SessionIssuer sessions,
        SecurityEventLog securityLog)
    {
        SignInCheckResult result = await checker.CheckAsync(
            request.Email!,
            request.Password!,
            httpContext,
            SecurityEventTypes.SignInFailed);

        switch (result.Outcome)
        {
            case SignInOutcome.LockedOut:
                return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.AccountLocked);

            case SignInOutcome.EmailNotConfirmed:
                return ApiErrors.Result(StatusCodes.Status403Forbidden, GameErrorCodes.EmailNotConfirmed);

            case SignInOutcome.InvalidCredentials:
                return ApiErrors.Result(StatusCodes.Status401Unauthorized, GameErrorCodes.InvalidCredentials);
        }

        IdentityUser user = result.User!;

        await checker.ResetFailuresAsync(user);
        await sessions.StartAsync(httpContext, user);
        await securityLog.WriteAsync(SecurityEventTypes.SignInSucceeded, httpContext, user.Id);

        return Results.Ok(new { userId = user.Id });
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

        await sessions.ContinueAsync(httpContext, user, result.NewToken, result.ExpiresAt);
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

    private static IResult GetMe(ClaimsPrincipal principal) =>
        Results.Ok(new { userId = principal.GetPlayerId() });

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
