using Aetheris.Core.Time;

namespace Aetheris.Tests.Time;

/// <summary>
/// Tests for the game clock: the anchor <c>(real0, game0, k)</c> and the effect of changing
/// the multiplier.
/// </summary>
/// <remarks>
/// See documentation/tech/07-planuvalnyk-podii.md, section 6.
/// </remarks>
public sealed class GameClockTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// With a multiplier of 1, game time advances exactly like real time.
    /// </summary>
    [Fact]
    public void Multiplier_of_one_advances_game_time_like_real_time()
    {
        GameClock clock = new(Anchor, Anchor);

        DateTimeOffset later = Anchor.AddHours(3);

        Assert.Equal(later, clock.ToGameTime(later));
    }

    /// <summary>
    /// With a multiplier of 10, one real hour is ten game hours.
    /// </summary>
    [Fact]
    public void Multiplier_of_ten_advances_game_time_ten_times_faster()
    {
        GameClock clock = new(Anchor, Anchor, 10d);

        DateTimeOffset later = Anchor.AddHours(1);

        Assert.Equal(Anchor.AddHours(10), clock.ToGameTime(later));
    }

    /// <summary>
    /// Converting a game timestamp back to real time returns the original instant.
    /// </summary>
    [Fact]
    public void Round_trip_conversion_returns_the_original_instant()
    {
        GameClock clock = new(Anchor, Anchor, 7d);

        DateTimeOffset realTime = Anchor.AddMinutes(37);

        DateTimeOffset gameTime = clock.ToGameTime(realTime);
        DateTimeOffset backToReal = clock.ToRealTime(gameTime);

        Assert.Equal(realTime, backToReal);
    }

    /// <summary>
    /// Changing the multiplier re-anchors the clock: the game time does not jump at the
    /// moment of the change.
    /// </summary>
    [Fact]
    public void Changing_the_multiplier_does_not_jump_the_game_time()
    {
        GameClock clock = new(Anchor, Anchor);

        DateTimeOffset changeMoment = Anchor.AddHours(1);
        DateTimeOffset gameTimeBefore = clock.ToGameTime(changeMoment);

        clock.SetMultiplier(100d, changeMoment);

        DateTimeOffset gameTimeAfter = clock.ToGameTime(changeMoment);

        Assert.Equal(gameTimeBefore, gameTimeAfter);
        Assert.Equal(100d, clock.Multiplier);
    }

    /// <summary>
    /// After the multiplier changes, new elapsed time uses the new multiplier.
    /// </summary>
    [Fact]
    public void Elapsed_time_after_the_change_uses_the_new_multiplier()
    {
        GameClock clock = new(Anchor, Anchor);

        DateTimeOffset changeMoment = Anchor.AddHours(1);
        clock.SetMultiplier(10d, changeMoment);

        DateTimeOffset later = changeMoment.AddHours(1);

        Assert.Equal(clock.ToGameTime(changeMoment).AddHours(10), clock.ToGameTime(later));
    }

    /// <summary>
    /// A multiplier of zero or less is rejected.
    /// </summary>
    /// <param name="multiplier">The invalid multiplier.</param>
    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void Non_positive_multiplier_is_rejected(double multiplier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(Anchor, Anchor, multiplier));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(Anchor, Anchor).SetMultiplier(multiplier));
    }
}
