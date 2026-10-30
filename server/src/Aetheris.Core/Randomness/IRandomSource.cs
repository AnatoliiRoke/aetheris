namespace Aetheris.Core.Randomness;

/// <summary>
/// The source of the random numbers of the game rules that roll a value (task 00014, section
/// 11): a module asks this interface and never <see cref="Random"/>, so a test replaces the
/// source with a scripted one and the roll is repeatable.
/// </summary>
/// <remarks>
/// The world generator has its own deterministic source seeded by the sector; this one is for
/// the rolls of the running game.
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Gets the next random number in <c>[0, 1)</c>.
    /// </summary>
    /// <returns>The number.</returns>
    double NextDouble();
}

/// <summary>
/// The random source of the running server: it asks the shared generator of the runtime.
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    /// <inheritdoc />
    public double NextDouble() => Random.Shared.NextDouble();
}
