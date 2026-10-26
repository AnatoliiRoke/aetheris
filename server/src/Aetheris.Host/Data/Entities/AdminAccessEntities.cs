namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>admin_role</c> table: a role of the administrators (task 00007, decision 4).
/// </summary>
/// <remarks>
/// A role is data: a new role, for example the support or the moderation, is a new row and
/// its rows in <c>admin_role_permission</c>, without a change of the code.
/// </remarks>
public sealed class AdminRoleEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the stable code of the role.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localization key of the role name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the position of the role in lists.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Entity for the <c>admin_permission</c> table: the right to open a screen or to run an
/// action of the admin panel.
/// </summary>
/// <remarks>
/// The code of a permission is the action code of an admin endpoint, its endpoint name, so
/// every admin request is checked against this table.
/// </remarks>
public sealed class AdminPermissionEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the action code, for example <c>admin.config_changed</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind, one of <see cref="AdminPermissionKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;
}

/// <summary>
/// The kinds of the administrator permissions.
/// </summary>
public static class AdminPermissionKinds
{
    /// <summary>Reads data for a screen.</summary>
    public const string Screen = "screen";

    /// <summary>Changes something.</summary>
    public const string Action = "action";
}

/// <summary>
/// Entity for the <c>admin_role_permission</c> table: a permission granted to a role.
/// </summary>
public sealed class AdminRolePermissionEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the role.</summary>
    public int AdminRoleId { get; set; }

    /// <summary>Gets or sets the permission.</summary>
    public int AdminPermissionId { get; set; }
}

/// <summary>
/// Entity for the <c>admin_account_role</c> table: a role of an administrator account.
/// </summary>
public sealed class AdminAccountRoleEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the administrator account.</summary>
    public string AdminAccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets the role.</summary>
    public int AdminRoleId { get; set; }
}

/// <summary>
/// Entity for the <c>admin_session</c> table: an open session of an administrator.
/// </summary>
/// <remarks>
/// The cookie only names the session; the server decides whether it is still alive, so the
/// session ends after a time without activity (task 00007, decision 5) and at the sign-out,
/// whatever the cookie says.
/// </remarks>
public sealed class AdminSessionEntity
{
    /// <summary>Gets or sets the primary key, a random identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the administrator account.</summary>
    public string AdminAccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets the second factor of the sign-in, one of the values of <c>aetheris:mfa</c>.</summary>
    public string SecondFactor { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC moment of the sign-in.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC moment of the last request.</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>Gets or sets the UTC moment the session ended, or <see langword="null"/> while it is open.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Gets or sets why the session ended, one of <see cref="AdminSessionEndReasons"/>.</summary>
    public string? EndReason { get; set; }
}

/// <summary>
/// Why an administrator session ended.
/// </summary>
public static class AdminSessionEndReasons
{
    /// <summary>The administrator signed out.</summary>
    public const string SignedOut = "signed_out";

    /// <summary>No request came for longer than the idle timeout.</summary>
    public const string Idle = "idle";

    /// <summary>The session outlived its longest life.</summary>
    public const string Expired = "expired";

    /// <summary>The second factor became mandatory after the sign-in.</summary>
    public const string SecondFactorRequired = "second_factor_required";
}

/// <summary>
/// Entity for the <c>config_parameter_change</c> table: the history of the values of a
/// configuration parameter changed from the admin panel.
/// </summary>
public sealed class ConfigParameterChangeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the key of the parameter.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the value before the change.</summary>
    public string OldValue { get; set; } = string.Empty;

    /// <summary>Gets or sets the value after the change.</summary>
    public string NewValue { get; set; } = string.Empty;

    /// <summary>Gets or sets the version of the parameter after the change.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the reason the administrator gave.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the change this one rolled back, or <see langword="null"/>.</summary>
    public long? RolledBackChangeId { get; set; }

    /// <summary>Gets or sets the administrator account.</summary>
    public string AdminAccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC moment of the change.</summary>
    public DateTimeOffset ChangedAt { get; set; }
}

/// <summary>
/// Entity for the <c>config_parameter_read_only</c> table: a dangerous parameter that the
/// admin panel shows but never changes (task 00007, decision 7).
/// </summary>
public sealed class ReadOnlyConfigParameterEntity
{
    /// <summary>Gets or sets the key of the parameter.</summary>
    public string Key { get; set; } = string.Empty;
}
