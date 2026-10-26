using System.Reflection;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Identity;

namespace Aetheris.Host.Auth;

/// <summary>
/// Checks a new password against the limits from the configuration and against the list of
/// common passwords (task 00002, section 2.1).
/// </summary>
/// <remarks>
/// The rules: a minimal length, a maximal length, enough different characters, a digit and
/// an upper-case letter (each one a switch in the database, task 00006, decision 6), not a
/// common password and not the e-mail address.
/// </remarks>
public sealed class ConfigPasswordValidator : IPasswordValidator<IdentityUser>
{
    /// <summary>The error code of a too short password.</summary>
    public const string TooShort = "password_too_short";

    /// <summary>The error code of a too long password.</summary>
    public const string TooLong = "password_too_long";

    /// <summary>The error code of a password with too few different characters.</summary>
    public const string TooFewUniqueCharacters = "password_too_few_unique_chars";

    /// <summary>The error code of a password without a digit.</summary>
    public const string NoDigit = "password_no_digit";

    /// <summary>The error code of a password without an upper-case letter.</summary>
    public const string NoUppercase = "password_no_uppercase";

    /// <summary>The error code of a common password.</summary>
    public const string Common = "password_too_common";

    /// <summary>The error code of a password that contains the e-mail address.</summary>
    public const string ContainsEmail = "password_contains_email";

    private static readonly Lazy<HashSet<string>> CommonPasswords = new(LoadCommonPasswords);

    private readonly SecuritySettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigPasswordValidator"/> class.
    /// </summary>
    /// <param name="settings">The security settings.</param>
    public ConfigPasswordValidator(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <inheritdoc />
    public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);

        List<IdentityError> errors = [];
        string value = password ?? string.Empty;

        if (value.Length < _settings.PasswordMinLength)
        {
            errors.Add(Error(TooShort));
        }

        if (value.Length > _settings.PasswordMaxLength)
        {
            errors.Add(Error(TooLong));
        }

        if (value.Distinct().Count() < _settings.PasswordMinUniqueCharacters)
        {
            errors.Add(Error(TooFewUniqueCharacters));
        }

        if (_settings.PasswordRequireDigit && !value.Any(char.IsDigit))
        {
            errors.Add(Error(NoDigit));
        }

        if (_settings.PasswordRequireUppercase && !value.Any(char.IsUpper))
        {
            errors.Add(Error(NoUppercase));
        }

        if (CommonPasswords.Value.Contains(value))
        {
            errors.Add(Error(Common));
        }

        string? localPart = user.Email?.Split('@')[0];

        if (!string.IsNullOrEmpty(localPart) && value.Contains(localPart, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(Error(ContainsEmail));
        }

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }

    private static IdentityError Error(string code) => new() { Code = code, Description = code };

    private static HashSet<string> LoadCommonPasswords()
    {
        Assembly assembly = typeof(ConfigPasswordValidator).Assembly;
        string listName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith("common-passwords.txt", StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(listName)
            ?? throw new InvalidOperationException("The list of common passwords is missing.");
        using StreamReader reader = new(stream);

        HashSet<string> passwords = new(StringComparer.OrdinalIgnoreCase);

        while (reader.ReadLine() is { } line)
        {
            string entry = line.Trim();

            if (entry.Length > 0 && !entry.StartsWith('#'))
            {
                passwords.Add(entry);
            }
        }

        return passwords;
    }
}
