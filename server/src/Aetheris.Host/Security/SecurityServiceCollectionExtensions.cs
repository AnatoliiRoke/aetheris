using Aetheris.Core.Admin;
using Aetheris.Core.Commands;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Security;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Auth;
using Aetheris.Host.Commands;
using Aetheris.Host.Data;
using Aetheris.Host.Realtime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Aetheris.Host.Security;

/// <summary>
/// Registers and orders the protection mechanisms of the core (task 00002).
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section of the Google sign-in. The client identifier and the
    /// secret come from the environment or the git-ignored local file, never from the
    /// repository.
    /// </summary>
    public const string GoogleSection = "Authentication:Google";

    /// <summary>
    /// Registers the identity, the authorization policies, the rate limits, the command
    /// pipeline and the rest of the protection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddAetherisSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SecuritySettings>();
        services.AddSingleton<SecurityEventLog>();
        services.AddSingleton<ClientOrigins>();

        AddIdentity(services);
        AddAuthentication(services, configuration);
        AddAuthorizationPolicies(services);

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AuthCookies.CsrfHeader;
            options.Cookie.Name = AuthCookies.Csrf;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // The anti-forgery service refuses to work over plain HTTP when its cookie must be
            // secure. The development server runs on http://localhost, so there the cookie
            // follows the request; everywhere else HTTPS is enforced and the cookie is secure.
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        services.AddCors();
        services.AddAetherisRateLimiting();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();

        // The HSTS lifetime and the SignalR message limit are read from the database when
        // the options are first used, after the configuration was loaded at start.
        services.AddHsts(_ => { });
        services.AddSingleton<IConfigureOptions<HstsOptions>, ConfigureHsts>();
        services.AddSingleton<IConfigureOptions<HubOptions>, ConfigureHubLimits>();
        services.AddSingleton<IConfigureOptions<PasswordHasherOptions>, ConfigurePasswordHasher>();

        // The command pipeline and its extension points.
        services.AddScoped<CommandExecutionScope>();
        services.AddScoped<IChangeJournal, EfChangeJournal>();
        services.AddScoped<OwnershipChecker>();
        services.AddOwnedObjectType<PlayerObjectResolver>();
        services.AddCommand<EchoCommand>();
        services.AddSingleton<IdempotencyCleanupService>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<IdempotencyCleanupService>());

        // The account flows and the admin panel.
        services.AddScoped<SignInChecker>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<SignInThrottle>();
        services.AddScoped<NicknameRules>();
        services.AddScoped<ITextModeration, TextModeration>();
        services.AddScoped<ConfirmationMailer>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<AccountLifecycle>();
        services.AddScoped<AccountGate>();
        services.AddSingleton<ICaptchaVerifier, UnconfiguredCaptchaVerifier>();
        services.AddScoped<IScheduledEventHandler, UnconfirmedAccountExpiryHandler>();
        services.AddScoped<IScheduledEventHandler, AccountDeletionHandler>();
        services.AddHttpContextAccessor();

        // The account settings of task 00006 go through the command pipeline.
        services.AddCommand<ResendConfirmationCommand>();
        services.AddCommand<ChangePasswordCommand>();
        services.AddCommand<ChangeNicknameCommand>();
        services.AddCommand<SignOutEverywhereCommand>();
        services.AddCommand<DeleteAccountCommand>();

        // The development server writes the account e-mails into a local mailbox folder, so
        // the links can be opened by hand; elsewhere the placeholder logs them until the mail
        // delivery task.
        if (environment.IsDevelopment())
        {
            services.AddSingleton<IAccountEmailSender, DevelopmentMailboxEmailSender>();
        }
        else
        {
            services.AddSingleton<IAccountEmailSender, LoggingAccountEmailSender>();
        }
        services.AddScoped<AdminAccountService>();
        services.AddScoped<AdminAuditScope>();
        services.AddScoped<IAdminAuditRecorder>(serviceProvider => serviceProvider.GetRequiredService<AdminAuditScope>());

        return services;
    }

    /// <summary>
    /// Adds the protection middleware in the order it has to run.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseAetherisSecurity(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages(WriteEmptyStatusAsync);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLimitsMiddleware>();
        app.UseMiddleware<HubOriginMiddleware>(new PathString(GameHub.RoutePrefix));

        app.UseRouting();

        ClientOrigins origins = app.Services.GetRequiredService<ClientOrigins>();
        app.UseCors(policy => policy
            .WithOrigins([.. origins.Origins])
            .AllowCredentials()
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
            .WithHeaders("Content-Type", AuthCookies.CsrfHeader, AuthCookies.IdempotencyHeader));

        app.UseAuthentication();
        app.UseMiddleware<CsrfProtectionMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                // The password rules and the lockout come from the database, through
                // ConfigPasswordValidator and SignInChecker. Identity's own rules are switched
                // off so that they cannot contradict the configured values.
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = int.MaxValue;
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;

                // The user name is the nickname; its characters are checked by NicknameRules
                // against the rule of the author, so Identity's own list is switched off.
                options.User.AllowedUserNameCharacters = string.Empty;

                // A player signs in before the confirmation; the command gate limits what the
                // account may do until then (task 00006, decision 2).
                options.SignIn.RequireConfirmedEmail = false;
                options.Tokens.EmailConfirmationTokenProvider = ConfirmationTokenProvider.ProviderName;
                options.Tokens.PasswordResetTokenProvider = ResetTokenProvider.ProviderName;
            })
            .AddEntityFrameworkStores<AetherisDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<ConfirmationTokenProvider>(ConfirmationTokenProvider.ProviderName)
            .AddTokenProvider<ResetTokenProvider>(ResetTokenProvider.ProviderName)
            .AddPasswordValidator<ConfigPasswordValidator>();

        services.AddSingleton<IConfigureOptions<ConfirmationTokenProviderOptions>, ConfigureAccountTokens>();
        services.AddSingleton<IConfigureOptions<ResetTokenProviderOptions>, ConfigureAccountTokens>();
        services.AddSingleton<IConfigureOptions<SecurityStampValidatorOptions>, ConfigureStampCheck>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookies.Session;
            ConfigureApiCookie(options);
        });

        services.ConfigureExternalCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
    }

    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        AuthenticationBuilder authentication = services.AddAuthentication()
            .AddCookie(AuthSchemes.Admin, options =>
            {
                options.Cookie.Name = AuthCookies.Admin;
                options.Cookie.Path = AuthCookies.AdminPath;
                ConfigureApiCookie(options);
            });

        string? clientId = configuration[$"{GoogleSection}:ClientId"];
        string? clientSecret = configuration[$"{GoogleSection}:ClientSecret"];

        // Without credentials the Google sign-in is simply not offered; the e-mail sign-in
        // keeps working.
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            authentication.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = AuthEndpoints.Prefix + "/google/callback";
                options.ClaimActions.MapJsonKey(AuthEndpoints.EmailVerifiedClaim, "email_verified");
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.HttpOnly = true;
            });
        }
    }

    private static void AddAuthorizationPolicies(IServiceCollection services)
    {
        string[] schemes = [IdentityConstants.ApplicationScheme, AuthSchemes.Admin];

        services.AddAuthorizationBuilder()
            .AddPolicy(SecurityPolicies.Player, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser())
            .AddPolicy(SecurityPolicies.Admin, policy => policy
                .AddAuthenticationSchemes(schemes)
                .RequireAuthenticatedUser()
                .RequireClaim(AetherisClaims.AdminId)
                .RequireClaim(AetherisClaims.SecondFactor, AetherisClaims.Totp))
            .AddPolicy(SecurityPolicies.AdminOwner, policy => AdminRole(policy, schemes))
            .AddPolicy(SecurityPolicies.AdminBalance, policy => AdminRole(policy, schemes, AdminRoles.Balance))
            .AddPolicy(SecurityPolicies.AdminSupport, policy => AdminRole(policy, schemes, AdminRoles.Support))
            .AddPolicy(SecurityPolicies.AdminModeration, policy => AdminRole(policy, schemes, AdminRoles.Moderation));
    }

    /// <summary>
    /// An administrator policy for a role. The owner role passes every one of them.
    /// </summary>
    private static void AdminRole(
        Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder policy,
        string[] schemes,
        params string[] roles)
    {
        policy
            .AddAuthenticationSchemes(schemes)
            .RequireAuthenticatedUser()
            .RequireClaim(AetherisClaims.SecondFactor, AetherisClaims.Totp)
            .RequireClaim(AdminRoles.ClaimType, [AdminRoles.Owner, .. roles]);
    }

    private static void ConfigureApiCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.SlidingExpiration = false;

        // An API answers with a status code; it never redirects to a sign-in page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    private static async Task WriteEmptyStatusAsync(StatusCodeContext context)
    {
        HttpResponse response = context.HttpContext.Response;

        if (response.HasStarted || response.ContentLength > 0)
        {
            return;
        }

        await response.WriteAsJsonAsync(new ApiError(ApiErrors.CodeFor(response.StatusCode))).ConfigureAwait(false);
    }

    private sealed class ConfigureHsts : IConfigureOptions<HstsOptions>
    {
        private readonly SecuritySettings _settings;

        public ConfigureHsts(SecuritySettings settings)
        {
            _settings = settings;
        }

        public void Configure(HstsOptions options)
        {
            options.MaxAge = _settings.HstsMaxAge;
            options.IncludeSubDomains = true;
        }
    }

    private sealed class ConfigureHubLimits : IConfigureOptions<HubOptions>
    {
        private readonly SecuritySettings _settings;

        public ConfigureHubLimits(SecuritySettings settings)
        {
            _settings = settings;
        }

        public void Configure(HubOptions options) => options.MaximumReceiveMessageSize = _settings.SignalRMaxMessageBytes;
    }

    private sealed class ConfigureStampCheck : IConfigureOptions<SecurityStampValidatorOptions>
    {
        private readonly SecuritySettings _settings;

        public ConfigureStampCheck(SecuritySettings settings)
        {
            _settings = settings;
        }

        // A changed password, a sign-out everywhere or a deletion request renew the security
        // stamp; the session cookies of the account stop working at the next check.
        public void Configure(SecurityStampValidatorOptions options) => options.ValidationInterval = _settings.SessionStampCheckInterval;
    }

    private sealed class ConfigurePasswordHasher : IConfigureOptions<PasswordHasherOptions>
    {
        private readonly SecuritySettings _settings;

        public ConfigurePasswordHasher(SecuritySettings settings)
        {
            _settings = settings;
        }

        public void Configure(PasswordHasherOptions options) => options.IterationCount = _settings.PasswordHashIterations;
    }
}
