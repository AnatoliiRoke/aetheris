using Aetheris.Core.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// Entity for the <c>config_parameter</c> table: a single tunable game parameter.
/// </summary>
public sealed class ConfigParameterEntity
{
    /// <summary>
    /// Gets or sets the dotted parameter key, for example <c>test.hello_multiplier</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw parameter value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version, incremented on every change.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the administrator who made the last change.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Converts the entity into the domain model used by the configuration module.
    /// </summary>
    /// <returns>The domain configuration parameter.</returns>
    public ConfigParameter ToDomain() => new(Key, Value, Version);
}
