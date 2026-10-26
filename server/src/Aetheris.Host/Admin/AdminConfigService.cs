using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Admin;

/// <summary>
/// The value types of the configuration parameters, as the admin panel shows and checks them.
/// </summary>
public static class ConfigValueTypes
{
    /// <summary>A whole number.</summary>
    public const string Integer = "integer";

    /// <summary>A number with a fraction.</summary>
    public const string Decimal = "decimal";

    /// <summary><c>true</c> or <c>false</c>.</summary>
    public const string Boolean = "boolean";

    /// <summary>Any text.</summary>
    public const string Text = "text";

    /// <summary>
    /// Derives the type of a parameter from its definition: a starting value of <c>true</c> or
    /// <c>false</c> is a switch, bounds make a number, and the rest is text.
    /// </summary>
    /// <param name="definition">The definition, or <see langword="null"/> for an unknown parameter.</param>
    /// <returns>One of the types of this class.</returns>
    public static string Of(ConfigParameterDefinition? definition)
    {
        if (definition is null)
        {
            return Text;
        }

        if (bool.TryParse(definition.DefaultValue, out _))
        {
            return Boolean;
        }

        if (definition.Minimum is null && definition.Maximum is null)
        {
            return Text;
        }

        return IsWhole(definition.DefaultValue) && (definition.Minimum is null || IsWhole(definition.Minimum))
            && (definition.Maximum is null || IsWhole(definition.Maximum))
            ? Integer
            : Decimal;
    }

    /// <summary>
    /// Checks a value against the type and the bounds of its parameter.
    /// </summary>
    /// <param name="definition">The definition, or <see langword="null"/> for an unknown parameter.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The normalized value, or <see langword="null"/> when it is refused.</returns>
    public static string? Normalize(ConfigParameterDefinition? definition, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string text = value.Trim();

        switch (Of(definition))
        {
            case Boolean:
                return bool.TryParse(text, out bool flag) ? (flag ? "true" : "false") : null;

            case Integer:
                return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole) && definition!.Accepts(text)
                    ? whole.ToString(CultureInfo.InvariantCulture)
                    : null;

            case Decimal:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    && double.IsFinite(number) && definition!.Accepts(text)
                    ? text
                    : null;

            default:
                return value;
        }
    }

    private static bool IsWhole(string text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
}

/// <summary>
/// A configuration parameter as the admin panel shows it.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The current value.</param>
/// <param name="Type">The value type, one of <see cref="ConfigValueTypes"/>.</param>
/// <param name="Minimum">The smallest value, or <see langword="null"/>.</param>
/// <param name="Maximum">The largest value, or <see langword="null"/>.</param>
/// <param name="DefaultValue">The starting value, or <see langword="null"/> for an unknown parameter.</param>
/// <param name="Description">What the parameter controls.</param>
/// <param name="ReadOnly">Whether the admin panel only shows the parameter.</param>
/// <param name="Version">The version of the value.</param>
/// <param name="UpdatedBy">Who changed the value last.</param>
/// <param name="UpdatedAt">When the value was changed last.</param>
public sealed record AdminConfigParameter(
    string Key,
    string Value,
    string Type,
    string? Minimum,
    string? Maximum,
    string? DefaultValue,
    string Description,
    bool ReadOnly,
    int Version,
    string? UpdatedBy,
    DateTimeOffset UpdatedAt);

/// <summary>
/// One change of a configuration parameter.
/// </summary>
/// <param name="Id">The change.</param>
/// <param name="Key">The key of the parameter.</param>
/// <param name="OldValue">The value before.</param>
/// <param name="NewValue">The value after.</param>
/// <param name="Version">The version after the change.</param>
/// <param name="Reason">The reason.</param>
/// <param name="RolledBackChangeId">The change this one rolled back.</param>
/// <param name="AdminName">Who made the change.</param>
/// <param name="ChangedAt">When.</param>
public sealed record AdminConfigChange(
    long Id,
    string Key,
    string OldValue,
    string NewValue,
    int Version,
    string Reason,
    long? RolledBackChangeId,
    string AdminName,
    DateTimeOffset ChangedAt);

/// <summary>
/// The outcome of a change of a configuration parameter.
/// </summary>
public enum ConfigChangeOutcome
{
    /// <summary>The change is staged.</summary>
    Staged,

    /// <summary>The parameter does not exist.</summary>
    NotFound,

    /// <summary>The parameter is read-only in the admin panel.</summary>
    ReadOnly,

    /// <summary>The value does not fit the type or the bounds.</summary>
    Invalid,
}

/// <summary>
/// Reads and changes the configuration parameters for the admin panel (task 00007, decision 7).
/// </summary>
/// <remarks>
/// A change is only staged in the database context: the audit filter saves it together with
/// its history row, the new configuration version and the audit record, and the configuration
/// is reloaded once that is committed, so the change takes effect without a restart.
/// </remarks>
public sealed class AdminConfigService
{
    /// <summary>
    /// The sort column of the parameter list by the time of the last change.
    /// </summary>
    public const string SortByUpdatedAt = "updated_at";

    private readonly AetherisDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminConfigService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="timeProvider">The clock.</param>
    public AdminConfigService(AetherisDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Lists the parameters. The table is small, so the filters run in memory.
    /// </summary>
    /// <param name="page">The page, the search text and the order.</param>
    /// <param name="group">The first part of the key, for example <c>security</c>, or <see langword="null"/>.</param>
    /// <param name="readOnly">Only the read-only or only the editable parameters, or both.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminConfigParameter>> ListAsync(
        PageRequest page,
        string? group,
        bool? readOnly,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        List<ConfigParameterEntity> parameters = await _dbContext.ConfigParameters.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        HashSet<string> readOnlyKeys = await ReadOnlyKeysAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> names = await AdminNamesAsync(parameters.Select(parameter => parameter.UpdatedBy), cancellationToken).ConfigureAwait(false);

        IEnumerable<AdminConfigParameter> rows = parameters.Select(parameter => ToRow(parameter, readOnlyKeys, names));

        if (!string.IsNullOrEmpty(group))
        {
            rows = rows.Where(row => row.Key.StartsWith(group + ".", StringComparison.Ordinal));
        }

        if (readOnly is not null)
        {
            rows = rows.Where(row => row.ReadOnly == readOnly.Value);
        }

        if (page.Search is { } search)
        {
            rows = rows.Where(row => row.Key.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        rows = string.Equals(page.Sort, SortByUpdatedAt, StringComparison.Ordinal)
            ? (page.Descending ? rows.OrderByDescending(row => row.UpdatedAt) : rows.OrderBy(row => row.UpdatedAt))
            : (page.Descending ? rows.OrderByDescending(row => row.Key, StringComparer.Ordinal) : rows.OrderBy(row => row.Key, StringComparer.Ordinal));

        List<AdminConfigParameter> filtered = rows.ToList();

        return new PageResult<AdminConfigParameter>([.. filtered.Skip(page.Skip).Take(page.PageSize)], filtered.Count, page.Page, page.PageSize);
    }

    /// <summary>
    /// Lists the changes of one parameter, newest first.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminConfigChange>> HistoryAsync(string key, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<ConfigParameterChangeEntity> query = _dbContext.ConfigParameterChanges.AsNoTracking().Where(change => change.Key == key);

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ConfigParameterChangeEntity> changes = await query
            .OrderByDescending(change => change.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> names = await AdminNamesAsync(changes.Select(change => change.AdminAccountId), cancellationToken).ConfigureAwait(false);

        return new PageResult<AdminConfigChange>(
            [.. changes.Select(change => new AdminConfigChange(
                change.Id,
                change.Key,
                change.OldValue,
                change.NewValue,
                change.Version,
                change.Reason,
                change.RolledBackChangeId,
                names.GetValueOrDefault(change.AdminAccountId, change.AdminAccountId),
                change.ChangedAt))],
            total,
            page.Page,
            page.PageSize);
    }

    /// <summary>
    /// Finds a change of a parameter, for a rollback.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="changeId">The change.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    public Task<ConfigParameterChangeEntity?> FindChangeAsync(string key, long changeId, CancellationToken cancellationToken) =>
        _dbContext.ConfigParameterChanges.AsNoTracking()
            .SingleOrDefaultAsync(change => change.Id == changeId && change.Key == key, cancellationToken);

    /// <summary>
    /// Stages a new value of a parameter with its history row and a new configuration version.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The new value.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="adminId">The administrator.</param>
    /// <param name="rolledBackChangeId">The change that is rolled back, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome, the old and the new value, and the new version.</returns>
    public async Task<(ConfigChangeOutcome Outcome, string? OldValue, string? NewValue, int Version)> StageChangeAsync(
        string key,
        string value,
        string reason,
        string adminId,
        long? rolledBackChangeId,
        CancellationToken cancellationToken)
    {
        ConfigParameterEntity? parameter = await _dbContext.ConfigParameters
            .SingleOrDefaultAsync(entry => entry.Key == key, cancellationToken).ConfigureAwait(false);

        if (parameter is null)
        {
            return (ConfigChangeOutcome.NotFound, null, null, 0);
        }

        if ((await ReadOnlyKeysAsync(cancellationToken).ConfigureAwait(false)).Contains(key))
        {
            return (ConfigChangeOutcome.ReadOnly, null, null, parameter.Version);
        }

        // A value outside the type or the bounds of its definition is refused
        // (documentation/tech/05, section 8): a typo must not switch a protection off.
        string? normalized = ConfigValueTypes.Normalize(ConfigSeed.Find(key), value);

        if (normalized is null)
        {
            return (ConfigChangeOutcome.Invalid, null, null, parameter.Version);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        string oldValue = parameter.Value;

        parameter.Value = normalized;
        parameter.Version++;
        parameter.UpdatedAt = now;
        parameter.UpdatedBy = adminId;

        int lastVersion = await _dbContext.ConfigVersions.MaxAsync(version => (int?)version.Version, cancellationToken).ConfigureAwait(false) ?? 0;
        _dbContext.ConfigVersions.Add(new ConfigVersionEntity { Version = lastVersion + 1, AppliedAt = now, AppliedBy = adminId });

        _dbContext.ConfigParameterChanges.Add(new ConfigParameterChangeEntity
        {
            Key = key,
            OldValue = oldValue,
            NewValue = normalized,
            Version = parameter.Version,
            Reason = reason,
            RolledBackChangeId = rolledBackChangeId,
            AdminAccountId = adminId,
            ChangedAt = now,
        });

        return (ConfigChangeOutcome.Staged, oldValue, normalized, parameter.Version);
    }

    /// <summary>
    /// Maps administrator identifiers to the names shown in the admin panel. An identifier that
    /// is not an administrator, such as <c>seed</c>, stays as it is.
    /// </summary>
    /// <param name="adminIds">The identifiers.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The names by identifier.</returns>
    public async Task<Dictionary<string, string>> AdminNamesAsync(IEnumerable<string?> adminIds, CancellationToken cancellationToken)
    {
        List<string> ids = adminIds.OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        List<AdminAccountEntity> accounts = await _dbContext.AdminAccounts.AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return accounts.ToDictionary(account => account.Id, account => AdminNames.Display(account.DisplayName), StringComparer.Ordinal);
    }

    private async Task<HashSet<string>> ReadOnlyKeysAsync(CancellationToken cancellationToken) =>
        (await _dbContext.ReadOnlyConfigParameters.AsNoTracking()
            .Select(entry => entry.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
        .ToHashSet(StringComparer.Ordinal);

    private static AdminConfigParameter ToRow(ConfigParameterEntity parameter, HashSet<string> readOnlyKeys, Dictionary<string, string> names)
    {
        ConfigParameterDefinition? definition = ConfigSeed.Find(parameter.Key);

        return new AdminConfigParameter(
            parameter.Key,
            parameter.Value,
            ConfigValueTypes.Of(definition),
            definition?.Minimum,
            definition?.Maximum,
            definition?.DefaultValue,
            definition?.Description ?? string.Empty,
            readOnlyKeys.Contains(parameter.Key),
            parameter.Version,
            parameter.UpdatedBy is null ? null : names.GetValueOrDefault(parameter.UpdatedBy, parameter.UpdatedBy),
            parameter.UpdatedAt);
    }
}

/// <summary>
/// The names of the administrators as the admin panel shows them.
/// </summary>
public static class AdminNames
{
    /// <summary>
    /// Shows a name without a full e-mail address: an address keeps its first character and
    /// its domain, <c>j***@example.com</c> (task 00002, section 2.7).
    /// </summary>
    /// <param name="name">The stored name.</param>
    /// <returns>The name to show.</returns>
    public static string Display(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        int at = name.IndexOf('@', StringComparison.Ordinal);

        if (at < 0)
        {
            return name;
        }

        return at == 0 ? "***" : $"{name[0]}***{name[at..]}";
    }
}
