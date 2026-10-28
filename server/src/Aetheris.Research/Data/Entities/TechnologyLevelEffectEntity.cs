namespace Aetheris.Research.Data.Entities;

/// <summary>
/// An effect of a level of a technology (task 00012, section 2.4).
/// </summary>
/// <remarks>
/// <para>
/// The effect opens a subject or raises a parameter of a body. The kind is a logical code of
/// the program (documentation/tech/09, section 3.4), and the rows of the catalog say which
/// subject or which parameter it touches.
/// </para>
/// <para>
/// The value of the effect grows with the level of the technology, so the rows carry the
/// growth of the value and not the value of every level.
/// </para>
/// </remarks>
public sealed class TechnologyLevelEffectEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the level of the technology.</summary>
    public int TechnologyLevelId { get; set; }

    /// <summary>Gets or sets the kind of the effect, one of <see cref="Services.TechnologyEffectTypes"/>.</summary>
    public string EffectType { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of the subject the effect opens, one of <see cref="Services.TechnologyUnlockKinds"/>.</summary>
    public string? UnlockKind { get; set; }

    /// <summary>Gets or sets the subject the effect opens.</summary>
    public int? UnlockTargetId { get; set; }

    /// <summary>Gets or sets the value of the effect at the level.</summary>
    public decimal Value { get; set; }
}
