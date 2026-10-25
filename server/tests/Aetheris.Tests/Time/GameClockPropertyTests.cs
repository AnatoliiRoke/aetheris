using Aetheris.Core.Time;
using FsCheck;
using FsCheck.Fluent;

namespace Aetheris.Tests.Time;

/// <summary>
/// Property based tests for the game clock.
/// </summary>
/// <remarks>
/// The properties check invariants that must hold for any input, not for a few fixed
/// examples. This is the "unit tests (boundaries)" level described in
/// documentation/tech/06-testuvannia-ta-symuliatory.md, section 2.
/// FsCheck 3 uses the fluent API (<c>Prop.ForAll</c>) and throws on a failed property,
/// so no explicit assertion is needed.
/// </remarks>
public sealed class GameClockPropertyTests
{
    /// <summary>
    /// The configuration used for the property runs. It throws as soon as a property fails.
    /// </summary>
    private static Config TestConfig => Config.QuickThrowOnFailure;

    /// <summary>
    /// Converting a real timestamp to game time and back returns the original instant,
    /// for any anchor, any multiplier and any elapsed time.
    /// </summary>
    [Fact]
    public void Round_trip_conversion_preserves_the_instant()
    {
        Property property = Prop.ForAll(
            Arb.From(Gen.Choose(2000, 2099)),
            anchorYear => Prop.ForAll(
                Arb.From(Gen.Choose(1, 1000)),
                multiplierPercent => Prop.ForAll(
                    Arb.From(Gen.Choose(0, 100_000)),
                    elapsedMinutes =>
                    {
                        var anchor = new DateTimeOffset(anchorYear, 6, 15, 0, 0, 0, TimeSpan.Zero);
                        double multiplier = multiplierPercent / 100d;
                        var elapsed = TimeSpan.FromMinutes(elapsedMinutes);

                        GameClock clock = new(anchor, anchor, multiplier);

                        DateTimeOffset realTime = anchor + elapsed;
                        DateTimeOffset gameTime = clock.ToGameTime(realTime);
                        DateTimeOffset backToReal = clock.ToRealTime(gameTime);

                        double toleranceMilliseconds = 1d;

                        return Math.Abs((backToReal - realTime).TotalMilliseconds) <= toleranceMilliseconds;
                    })));

        property.Check(TestConfig);
    }

    /// <summary>
    /// The game time never goes backwards when the real time moves forward.
    /// </summary>
    [Fact]
    public void Game_time_never_goes_backwards()
    {
        Property property = Prop.ForAll(
            Arb.From(Gen.Choose(2000, 2099)),
            anchorYear => Prop.ForAll(
                Arb.From(Gen.Choose(1, 1000)),
                multiplierPercent => Prop.ForAll(
                    Arb.From(Gen.Choose(0, 100_000)),
                    elapsedMinutes =>
                    {
                        var anchor = new DateTimeOffset(anchorYear, 6, 15, 0, 0, 0, TimeSpan.Zero);
                        double multiplier = multiplierPercent / 100d;

                        GameClock clock = new(anchor, anchor, multiplier);

                        DateTimeOffset earlier = anchor;
                        DateTimeOffset later = anchor + TimeSpan.FromMinutes(elapsedMinutes);

                        return clock.ToGameTime(later) >= clock.ToGameTime(earlier);
                    })));

        property.Check(TestConfig);
    }

    /// <summary>
    /// The game clock advances faster than real time for any multiplier above one.
    /// </summary>
    [Fact]
    public void The_clock_advances_faster_than_real_time()
    {
        Property property = Prop.ForAll(
            Arb.From(Gen.Choose(101, 1000)),
            multiplierPercent => Prop.ForAll(
                Arb.From(Gen.Choose(1, 100_000)),
                elapsedMinutes =>
                {
                    double multiplier = multiplierPercent / 100d;
                    var elapsed = TimeSpan.FromMinutes(elapsedMinutes);

                    var anchor = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
                    GameClock clock = new(anchor, anchor, multiplier);

                    double gameElapsed = (clock.ToGameTime(anchor + elapsed) - anchor).TotalSeconds;
                    double realElapsed = elapsed.TotalSeconds;

                    return gameElapsed > realElapsed;
                }));

        property.Check(TestConfig);
    }
}
