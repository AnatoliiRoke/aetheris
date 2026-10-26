using System.Text.RegularExpressions;
using Aetheris.Core.Text;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Identity;

namespace Aetheris.Host.Auth;

/// <summary>
/// The rules of a nickname (task 00006, decision 7).
/// </summary>
/// <remarks>
/// The length limits come from the database. The allowed characters are the rule of the
/// author: Latin and Cyrillic letters, digits, the space, <c>_</c> and <c>-</c>. The name
/// is unique without regard to case, because it is the Identity user name, compared through
/// its normalized form.
/// </remarks>
public sealed partial class NicknameRules
{
    /// <summary>The error code of a too short nickname.</summary>
    public const string TooShort = "nickname_too_short";

    /// <summary>The error code of a too long nickname.</summary>
    public const string TooLong = "nickname_too_long";

    /// <summary>The error code of a nickname with characters that are not allowed.</summary>
    public const string InvalidCharacters = "nickname_invalid_characters";

    /// <summary>The error code of a nickname with a restricted word.</summary>
    public const string Restricted = "nickname_restricted";

    /// <summary>The error code of a nickname that another account has.</summary>
    public const string Taken = "nickname_taken";

    private readonly SecuritySettings _settings;
    private readonly ITextModeration _moderation;
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NicknameRules"/> class.
    /// </summary>
    /// <param name="settings">The security settings.</param>
    /// <param name="moderation">The restricted words check.</param>
    /// <param name="userManager">The account manager.</param>
    public NicknameRules(SecuritySettings settings, ITextModeration moderation, UserManager<IdentityUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(moderation);
        ArgumentNullException.ThrowIfNull(userManager);

        _settings = settings;
        _moderation = moderation;
        _userManager = userManager;
    }

    /// <summary>
    /// Brings a typed nickname to its stored form: without the spaces around it.
    /// </summary>
    /// <param name="nickname">The typed nickname.</param>
    /// <returns>The stored form.</returns>
    public static string Normalize(string? nickname) => (nickname ?? string.Empty).Trim();

    /// <summary>
    /// Checks a nickname.
    /// </summary>
    /// <param name="nickname">The nickname in its stored form.</param>
    /// <param name="ownerId">The account that may already have this nickname, or <see langword="null"/> for a new account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The error codes; empty when the nickname is accepted.</returns>
    public async Task<List<string>> ValidateAsync(string nickname, string? ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nickname);

        List<string> errors = [];

        if (nickname.Length < _settings.NicknameMinLength)
        {
            errors.Add(TooShort);
        }

        if (nickname.Length > _settings.NicknameMaxLength)
        {
            errors.Add(TooLong);
        }

        if (nickname.Length > 0 && !AllowedPattern().IsMatch(nickname))
        {
            errors.Add(InvalidCharacters);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (!await _moderation.IsAllowedAsync(nickname, cancellationToken).ConfigureAwait(false))
        {
            errors.Add(Restricted);
            return errors;
        }

        IdentityUser? holder = await _userManager.FindByNameAsync(nickname).ConfigureAwait(false);

        if (holder is not null && !string.Equals(holder.Id, ownerId, StringComparison.Ordinal))
        {
            errors.Add(Taken);
        }

        return errors;
    }

    /// <summary>
    /// Latin and Cyrillic letters, digits, the space, the underscore and the dash.
    /// </summary>
    [GeneratedRegex("^[A-Za-zЀ-ӿ0-9 _-]+$")]
    private static partial Regex AllowedPattern();
}
