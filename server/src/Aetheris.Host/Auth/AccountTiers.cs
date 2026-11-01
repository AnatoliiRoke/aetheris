namespace Aetheris.Host.Auth;

/// <summary>
/// The status of an account as the top bar of the game shows it (task 00033, the header of the
/// design): the code the server names, the client turns it into a word of the language.
/// </summary>
public static class AccountTiers
{
    /// <summary>The status every account has until a paid status exists.</summary>
    public const string Standard = "standard";

    /// <summary>The paid status; no account has it yet, the code is reserved for the monetization.</summary>
    public const string Premium = "premium";

    /// <summary>
    /// Gets the status of an account.
    /// </summary>
    /// <returns>The code of the status; every account is <see cref="Standard"/> until the paid status is made.</returns>
    public static string Of() => Standard;
}
