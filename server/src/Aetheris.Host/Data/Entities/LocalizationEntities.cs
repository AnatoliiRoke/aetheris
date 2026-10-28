namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>localization_text</c> table: the text of a localisation key in one
/// language (task 00022, decision 2).
/// </summary>
public sealed class LocalizationTextEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the localisation key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the language, a two-letter code.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets the text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the version, raised by every change; a change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the UTC time of the last change.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets who changed the text last: an administrator or <c>seed</c>.</summary>
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Entity for the <c>code_sequence</c> table: the last number given in a group of neutral
/// codes (task 00022, decision 1).
/// </summary>
public sealed class CodeSequenceEntity
{
    /// <summary>Gets or sets the group of the codes, the primary key.</summary>
    public string CodeGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the last given number; it never goes down.</summary>
    public int LastNumber { get; set; }
}
