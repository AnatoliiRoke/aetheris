namespace Aetheris.Core.Time;

/// <summary>
/// Game clock driven by an anchor point and a multiplier.
/// </summary>
/// <remarks>
/// The anchor is <c>(real0, game0, k)</c>: at real time <c>real0</c> the game time is
/// <c>game0</c>, and one real second advances the game by <c>k</c> seconds.
/// Changing the multiplier re-anchors the clock at the current moment, which keeps all
/// already scheduled game timestamps valid (documentation/tech/07, section 6).
/// </remarks>
public sealed class GameClock : IGameClock
{
    private readonly Lock _anchorLock = new();
    private DateTimeOffset _realAnchor;
    private DateTimeOffset _gameAnchor;
    private double _multiplier;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameClock"/> class.
    /// </summary>
    /// <param name="realAnchor">The real time of the anchor.</param>
    /// <param name="gameAnchor">The game time of the anchor.</param>
    /// <param name="multiplier">The time multiplier. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiplier"/> is not positive.</exception>
    public GameClock(DateTimeOffset realAnchor, DateTimeOffset gameAnchor, double multiplier = 1d)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(multiplier, 0d);

        _realAnchor = realAnchor;
        _gameAnchor = gameAnchor;
        _multiplier = multiplier;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset GameNow => ToGameTime(DateTimeOffset.UtcNow);

    /// <inheritdoc />
    public double Multiplier
    {
        get
        {
            lock (_anchorLock)
            {
                return _multiplier;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset ToGameTime(DateTimeOffset realTime)
    {
        lock (_anchorLock)
        {
            double elapsedRealSeconds = (realTime - _realAnchor).TotalSeconds;
            return _gameAnchor.AddSeconds(elapsedRealSeconds * _multiplier);
        }
    }

    /// <inheritdoc />
    public DateTimeOffset ToRealTime(DateTimeOffset gameTime)
    {
        lock (_anchorLock)
        {
            double elapsedGameSeconds = (gameTime - _gameAnchor).TotalSeconds;
            return _realAnchor.AddSeconds(elapsedGameSeconds / _multiplier);
        }
    }

    /// <summary>
    /// Changes the time multiplier, re-anchoring the clock at the current real moment.
    /// </summary>
    /// <param name="multiplier">The new multiplier. Must be greater than zero.</param>
    /// <param name="now">The real time to anchor at. Defaults to the current real time.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiplier"/> is not positive.</exception>
    public void SetMultiplier(double multiplier, DateTimeOffset? now = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(multiplier, 0d);

        DateTimeOffset anchorReal = now ?? DateTimeOffset.UtcNow;

        lock (_anchorLock)
        {
            // Re-anchor so that the game time does not jump at the moment of the change.
            _gameAnchor = ToGameTimeUnlocked(anchorReal);
            _realAnchor = anchorReal;
            _multiplier = multiplier;
        }
    }

    private DateTimeOffset ToGameTimeUnlocked(DateTimeOffset realTime)
    {
        double elapsedRealSeconds = (realTime - _realAnchor).TotalSeconds;
        return _gameAnchor.AddSeconds(elapsedRealSeconds * _multiplier);
    }
}
