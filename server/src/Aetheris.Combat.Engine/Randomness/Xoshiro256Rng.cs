namespace Aetheris.Combat.Engine;

/// <summary>
/// A deterministic generator. The same seed yields the same stream. One stream is used
/// for a segment, and every draw has a fixed place in that stream.
/// </summary>
public sealed class Xoshiro256Rng
{
    private const ulong SplitMixFirst = 0xBF58476D1CE4E5B9UL;
    private const ulong SplitMixSecond = 0x94D049BB133111EBUL;
    private const ulong StarFirst = 5;
    private const int StarRotation = 7;
    private const ulong StarSecond = 9;
    private const int MixShift = 17;
    private const int ThirdRotation = 45;
    private const int DoubleShift = 11;
    private const int WordBits = 64;
    private const double UnitIntervalScale = 1.0 / 9007199254740992.0;

    private ulong _state0;
    private ulong _state1;
    private ulong _state2;
    private ulong _state3;

    /// <summary>
    /// Initializes a new instance of the <see cref="Xoshiro256Rng"/> class from one seed.
    /// </summary>
    /// <param name="seed">The seed. It is expanded so the four words are not all zero.</param>
    public Xoshiro256Rng(ulong seed)
    {
        _state0 = SplitMix64(seed);
        _state1 = SplitMix64(_state0);
        _state2 = SplitMix64(_state1);
        _state3 = SplitMix64(_state2);

        if ((_state0 | _state1 | _state2 | _state3) == 0)
        {
            _state0 = 1;
        }
    }

    /// <summary>
    /// Mixes one word. The seed of a segment is this mix of the engagement seed and the cycle.
    /// </summary>
    /// <param name="value">The word to mix.</param>
    /// <returns>The mixed word.</returns>
    public static ulong SplitMix64(ulong value)
    {
        ulong mixed = value + SplitMixFirst;

        mixed = (mixed ^ (mixed >> 30)) * SplitMixFirst;
        mixed = (mixed ^ (mixed >> 27)) * SplitMixSecond;

        return mixed ^ (mixed >> 31);
    }

    /// <summary>Draws the next word of the stream.</summary>
    /// <returns>The next word.</returns>
    public ulong NextUlong()
    {
        ulong result = Rotl(_state1 * StarFirst, StarRotation) * StarSecond;
        ulong mixed = _state1 << MixShift;

        _state2 ^= _state0;
        _state3 ^= _state1;
        _state1 ^= _state2;
        _state0 ^= _state3;
        _state2 ^= mixed;
        _state3 = Rotl(_state3, ThirdRotation);

        return result;
    }

    /// <summary>Draws a number in the half-open unit interval.</summary>
    /// <returns>A number from zero inclusive to one exclusive.</returns>
    public double NextDouble() => (NextUlong() >> DoubleShift) * UnitIntervalScale;

    private static ulong Rotl(ulong value, int bits) => (value << bits) | (value >> (WordBits - bits));
}
