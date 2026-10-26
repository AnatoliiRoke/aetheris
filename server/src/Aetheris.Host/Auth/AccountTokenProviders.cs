using Aetheris.Host.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Aetheris.Host.Auth;

/// <summary>
/// The options of the e-mail confirmation tokens.
/// </summary>
public sealed class ConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
}

/// <summary>
/// The options of the password reset tokens.
/// </summary>
public sealed class ResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
}

/// <summary>
/// Issues the e-mail confirmation tokens; they live <c>security.account.confirmation_hours</c>
/// (task 00006, decision 2).
/// </summary>
public sealed class ConfirmationTokenProvider : DataProtectorTokenProvider<IdentityUser>
{
    /// <summary>
    /// The name of the provider.
    /// </summary>
    public const string ProviderName = "AetherisConfirmation";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationTokenProvider"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    public ConfirmationTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ConfirmationTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<IdentityUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}

/// <summary>
/// Issues the password reset tokens; they live <c>security.account.reset_minutes</c>
/// (task 00006, decision 5). A reset changes the security stamp, so a token works once.
/// </summary>
public sealed class ResetTokenProvider : DataProtectorTokenProvider<IdentityUser>
{
    /// <summary>
    /// The name of the provider.
    /// </summary>
    public const string ProviderName = "AetherisReset";

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetTokenProvider"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    public ResetTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ResetTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<IdentityUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}

/// <summary>
/// Reads the lifetimes of the account tokens from the database when the options are first
/// used, after the configuration was loaded at start.
/// </summary>
public sealed class ConfigureAccountTokens :
    IConfigureOptions<ConfirmationTokenProviderOptions>,
    IConfigureOptions<ResetTokenProviderOptions>
{
    private readonly SecuritySettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureAccountTokens"/> class.
    /// </summary>
    /// <param name="settings">The security settings.</param>
    public ConfigureAccountTokens(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <inheritdoc />
    public void Configure(ConfirmationTokenProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Name = ConfirmationTokenProvider.ProviderName;
        options.TokenLifespan = _settings.ConfirmationLifetime;
    }

    /// <inheritdoc />
    public void Configure(ResetTokenProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Name = ResetTokenProvider.ProviderName;
        options.TokenLifespan = _settings.ResetLifetime;
    }
}
