namespace Aetheris.World.Data.Entities;

/// <summary>
/// The shapes and the tones of the icons of the resource groups (code audit of 04.11.2026).
/// </summary>
/// <remarks>
/// They are the names of the drawings and the colours of the design, not game entities: the
/// group says which of them it uses, so the client never maps a group to an icon by its code.
/// </remarks>
public static class ResourceGroupIcons
{
    /// <summary>
    /// The shapes an icon of a group may have.
    /// </summary>
    public static IReadOnlyList<string> Shapes { get; } = ["square", "gem", "drop", "spark", "diamond", "hexagon", "coin"];

    /// <summary>
    /// The tones an icon of a group may have.
    /// </summary>
    public static IReadOnlyList<string> Tones { get; } = ["metal", "gem", "flame", "made", "credits"];

    /// <summary>
    /// Tells whether a shape is known; no shape at all is allowed and gives the neutral icon.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <returns><see langword="true"/> when the shape is empty or known.</returns>
    public static bool IsKnownShape(string? shape) =>
        string.IsNullOrEmpty(shape) || Shapes.Contains(shape, StringComparer.Ordinal);

    /// <summary>
    /// Tells whether a tone is known; no tone at all is allowed and gives the neutral colour.
    /// </summary>
    /// <param name="tone">The tone.</param>
    /// <returns><see langword="true"/> when the tone is empty or known.</returns>
    public static bool IsKnownTone(string? tone) =>
        string.IsNullOrEmpty(tone) || Tones.Contains(tone, StringComparer.Ordinal);
}
