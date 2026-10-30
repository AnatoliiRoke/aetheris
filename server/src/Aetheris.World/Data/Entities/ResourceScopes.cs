namespace Aetheris.World.Data.Entities;

/// <summary>
/// The scopes of a resource: where a resource lies (task 00015, decision 3).
/// </summary>
/// <remarks>
/// The scope is a logical code, because it is read by the program and not changed by the
/// author: a resource of a planet is stored on the body and is spent there, a resource of a
/// civilization belongs to the whole civilization and is spent anywhere of it.
/// </remarks>
public static class ResourceScopes
{
    /// <summary>The resource lies on a body and is spent on it.</summary>
    public const string Planet = "planet";

    /// <summary>The resource belongs to the whole civilization and is spent anywhere of it.</summary>
    public const string Civilization = "civilization";

    /// <summary>
    /// The scopes a resource may have.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Planet, Civilization];

    /// <summary>
    /// Tells whether a scope is known.
    /// </summary>
    /// <param name="scope">The scope to check.</param>
    /// <returns><see langword="true"/> when the scope is known.</returns>
    public static bool IsKnown(string scope) =>
        string.Equals(scope, Planet, StringComparison.Ordinal)
        || string.Equals(scope, Civilization, StringComparison.Ordinal);
}
