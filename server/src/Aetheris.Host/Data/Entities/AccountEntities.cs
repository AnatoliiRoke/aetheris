namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>player_profile</c> table: what the account keeps next to the Identity
/// user (task 00006).
/// </summary>
/// <remarks>
/// The nickname itself is the Identity user name, so it is unique without regard to case
/// through the normalized name, and the sign-in finds an account by it.
/// </remarks>
public sealed class PlayerProfileEntity
{
    /// <summary>Gets or sets the account, the primary key.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC moment of the registration.</summary>
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>Gets or sets the UTC moment of the last nickname change, or <see langword="null"/> before the first one.</summary>
    public DateTimeOffset? NicknameChangedAt { get; set; }

    /// <summary>Gets or sets the UTC moment the last confirmation e-mail was sent.</summary>
    public DateTimeOffset? ConfirmationSentAt { get; set; }

    /// <summary>Gets or sets the UTC moment of the deletion request, or <see langword="null"/> when none is pending.</summary>
    public DateTimeOffset? DeletionRequestedAt { get; set; }

    /// <summary>Gets or sets the UTC moment the account is deleted, or <see langword="null"/> when no deletion is pending.</summary>
    public DateTimeOffset? DeletionDueAt { get; set; }
}

/// <summary>
/// The kinds of the entries of <c>account_access_log</c>.
/// </summary>
public static class AccountAccessKinds
{
    /// <summary>The registration of the account.</summary>
    public const string Registration = "registration";

    /// <summary>A successful sign-in.</summary>
    public const string SignIn = "sign_in";
}

/// <summary>
/// Entity for the <c>account_access_log</c> table: the IP address and the moment of the
/// registration and of every sign-in, for the analysis of multiple accounts
/// (documentation/tech/05, section 3). Only the admin panel reads it.
/// </summary>
public sealed class AccountAccessLogEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind, one of <see cref="AccountAccessKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the remote IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the UTC moment.</summary>
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// Entity for the <c>sign_in_attempt</c> table: one sign-in attempt, counted by the name
/// that was typed and by the IP address (task 00006, decision 4).
/// </summary>
/// <remarks>
/// The typed e-mail address or nickname is kept only as a SHA-256 hash: the lockout works the
/// same for a registered and for an unknown name, and the table holds no address.
/// </remarks>
public sealed class SignInAttemptEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the hash of the typed name, hex encoded.</summary>
    public string LoginHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the remote IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets a value indicating whether the attempt succeeded.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets the UTC moment.</summary>
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// Entity for the <c>blocked_email_domain</c> table: a domain of temporary mailboxes that
/// may not register (task 00006, decision 8).
/// </summary>
public sealed class BlockedEmailDomainEntity
{
    /// <summary>Gets or sets the domain in lower case, the primary key.</summary>
    public string Domain { get; set; } = string.Empty;
}

/// <summary>
/// The kinds of the entries of <c>restricted_word</c>.
/// </summary>
public static class RestrictedWordKinds
{
    /// <summary>An offensive word: a name that contains it is refused.</summary>
    public const string Offensive = "offensive";

    /// <summary>A reserved word, such as the name of the administration: a name made of it is refused.</summary>
    public const string Reserved = "reserved";
}

/// <summary>
/// Entity for the <c>restricted_word</c> table: a word that a visible name may not
/// contain (documentation/ui-art/04, task 00006, decision 7).
/// </summary>
public sealed class RestrictedWordEntity
{
    /// <summary>Gets or sets the word in lower case, the primary key.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind, one of <see cref="RestrictedWordKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;
}

/// <summary>
/// Entity for the <c>unconfirmed_allowed_action</c> table: an action code that an account
/// may run before its e-mail address is confirmed (task 00006, decision 2).
/// </summary>
/// <remarks>
/// Every other command needs a confirmed address. The list is read for every command of an
/// unconfirmed account, so a change takes effect at once.
/// </remarks>
public sealed class UnconfirmedAllowedActionEntity
{
    /// <summary>Gets or sets the action code, the primary key.</summary>
    public string ActionCode { get; set; } = string.Empty;
}
