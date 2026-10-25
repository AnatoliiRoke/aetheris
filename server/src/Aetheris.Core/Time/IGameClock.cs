namespace Aetheris.Core.Time;

/// <summary>
/// Converts real time into game time using an anchor and a speed multiplier.
/// </summary>
/// <remarks>
/// The anchor is the triple <c>(real0, game0, k)</c> described in
/// documentation/tech/07-planuvalnyk-podii.md, section 6. Changing the multiplier creates
/// a new anchor instead of rewriting existing timers, so scheduled events keep their
/// game-time stamps.
/// </remarks>
public interface IGameClock
{
    /// <summary>
    /// Gets the current real (wall clock) time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the current game time.
    /// </summary>
    DateTimeOffset GameNow { get; }

    /// <summary>
    /// Gets the current time multiplier. A value of 1 means real time, 10 means ten game
    /// hours per real hour.
    /// </summary>
    double Multiplier { get; }

    /// <summary>
    /// Converts a real timestamp into the corresponding game timestamp.
    /// </summary>
    /// <param name="realTime">The real timestamp to convert.</param>
    /// <returns>The corresponding game timestamp.</returns>
    DateTimeOffset ToGameTime(DateTimeOffset realTime);

    /// <summary>
    /// Converts a game timestamp into the corresponding real timestamp.
    /// </summary>
    /// <param name="gameTime">The game timestamp to convert.</param>
    /// <returns>The corresponding real timestamp.</returns>
    DateTimeOffset ToRealTime(DateTimeOffset gameTime);
}
