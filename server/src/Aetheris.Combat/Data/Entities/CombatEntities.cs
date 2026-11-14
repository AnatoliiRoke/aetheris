namespace Aetheris.Combat.Data.Entities;

/// <summary>
/// The columns shared by a target rule and a behavior (task 00062).
/// </summary>
public interface ICombatRuleRow
{
    /// <summary>Gets or sets the primary key.</summary>
    int Id { get; set; }

    /// <summary>Gets or sets the neutral code, given by the server.</summary>
    string Code { get; set; }

    /// <summary>Gets or sets the logical code an administrator types. It is required and unique.</summary>
    string LogicCode { get; set; }

    /// <summary>Gets or sets the localisation key of the name.</summary>
    string NameKey { get; set; }

    /// <summary>Gets or sets the localisation key of the description.</summary>
    string DescriptionKey { get; set; }

    /// <summary>Gets or sets the key of the picture, or <see langword="null"/> when the row has none.</summary>
    string? ImageKey { get; set; }

    /// <summary>Gets or sets the order of the row in the lists.</summary>
    int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether the row is archived.</summary>
    bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change.</summary>
    int Version { get; set; }
}

/// <summary>
/// A rule that chooses a target (task 00062). The name lives in the texts.
/// </summary>
public sealed class TargetRuleEntity : ICombatRuleRow
{
    /// <inheritdoc />
    public int Id { get; set; }

    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public string LogicCode { get; set; } = string.Empty;

    /// <inheritdoc />
    public string NameKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string DescriptionKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? ImageKey { get; set; }

    /// <inheritdoc />
    public int SortOrder { get; set; }

    /// <inheritdoc />
    public bool IsArchived { get; set; }

    /// <inheritdoc />
    public int Version { get; set; } = 1;
}

/// <summary>
/// A behavior of a formation (task 00062). The name lives in the texts.
/// </summary>
public sealed class BehaviorEntity : ICombatRuleRow
{
    /// <inheritdoc />
    public int Id { get; set; }

    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public string LogicCode { get; set; } = string.Empty;

    /// <inheritdoc />
    public string NameKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string DescriptionKey { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? ImageKey { get; set; }

    /// <inheritdoc />
    public int SortOrder { get; set; }

    /// <inheritdoc />
    public bool IsArchived { get; set; }

    /// <inheritdoc />
    public int Version { get; set; } = 1;
}
