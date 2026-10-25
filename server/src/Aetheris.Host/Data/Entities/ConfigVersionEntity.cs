namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>config_version</c> table: tracks the applied configuration set.
/// </summary>
public sealed class ConfigVersionEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the configuration version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this version was applied.
    /// </summary>
    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>
    /// Gets or sets the administrator who applied this version.
    /// </summary>
    public string? AppliedBy { get; set; }
}
