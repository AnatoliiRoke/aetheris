namespace Aetheris.World.Generation;

/// <summary>
/// The deterministic random source of the generator.
/// </summary>
/// <remarks>
/// The generator must give the same world for the same seed on every machine and every
/// runtime version, so it does not use <see cref="Random"/>, whose algorithm may change.
/// This is xoshiro256** seeded through SplitMix64; the constants below are the constants of
/// those published algorithms, not rules of the game.
/// </remarks>
public sealed class WorldRandom
{
    private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;
    private const ulong SplitMixMultiplierFirst = 0xBF58476D1CE4E5B9UL;
    private const ulong SplitMixMultiplierSecond = 0x94D049BB133111EBUL;
    private const int SplitMixShiftFirst = 30;
    private const int SplitMixShiftSecond = 27;
    private const int SplitMixShiftThird = 31;
    private const int ScramblerRotation = 7;
    private const ulong ScramblerMultiplierFirst = 5UL;
    private const ulong ScramblerMultiplierSecond = 9UL;
    private const int StateShift = 17;
    private const int StateRotation = 45;
    private const int DoubleMantissaShift = 11;
    private const double DoubleUnit = 1.0 / (1UL << 53);

    private ulong _state0;
    private ulong _state1;
    private ulong _state2;
    private ulong _state3;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldRandom"/> class.
    /// </summary>
    /// <param name="seed">The seed.</param>
    public WorldRandom(long seed)
    {
        ulong mix = unchecked((ulong)seed);

        _state0 = SplitMix(ref mix);
        _state1 = SplitMix(ref mix);
        _state2 = SplitMix(ref mix);
        _state3 = SplitMix(ref mix);
    }

    /// <summary>
    /// Derives the seed of a child, for example a sector of the galaxy.
    /// </summary>
    /// <param name="parentSeed">The seed of the parent.</param>
    /// <param name="childIndex">The index of the child.</param>
    /// <returns>The seed of the child, never negative.</returns>
    public static long DeriveSeed(long parentSeed, long childIndex)
    {
        ulong mix = unchecked((ulong)parentSeed ^ ((ulong)childIndex * SplitMixIncrement));

        return (long)(SplitMix(ref mix) >> 1);
    }

    /// <summary>
    /// Gets the next random 64-bit value.
    /// </summary>
    /// <returns>The value.</returns>
    public ulong NextUInt64()
    {
        ulong result = BitRotate(_state1 * ScramblerMultiplierFirst, ScramblerRotation) * ScramblerMultiplierSecond;
        ulong shifted = _state1 << StateShift;

        _state2 ^= _state0;
        _state3 ^= _state1;
        _state1 ^= _state2;
        _state0 ^= _state3;
        _state2 ^= shifted;
        _state3 = BitRotate(_state3, StateRotation);

        return result;
    }

    /// <summary>
    /// Gets a random number in <c>[0, 1)</c>.
    /// </summary>
    /// <returns>The number.</returns>
    public double NextDouble() => (NextUInt64() >> DoubleMantissaShift) * DoubleUnit;

    /// <summary>
    /// Gets a random number in <c>[minimum, maximum)</c>.
    /// </summary>
    /// <param name="minimum">The lower bound.</param>
    /// <param name="maximum">The upper bound.</param>
    /// <returns>The number.</returns>
    public double NextDouble(double minimum, double maximum) => minimum + (NextDouble() * (maximum - minimum));

    /// <summary>
    /// Gets a random whole number in <c>[minimum, maximum]</c>, both bounds included.
    /// </summary>
    /// <param name="minimum">The lower bound.</param>
    /// <param name="maximum">The upper bound.</param>
    /// <returns>The number.</returns>
    public long NextInclusive(long minimum, long maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        ulong range = (ulong)(maximum - minimum) + 1;

        return minimum + (long)(NextUInt64() % range);
    }

    /// <summary>
    /// Gets a random whole number in <c>[minimum, maximum]</c>, both bounds included.
    /// </summary>
    /// <param name="minimum">The lower bound.</param>
    /// <param name="maximum">The upper bound.</param>
    /// <returns>The number.</returns>
    public int NextInclusive(int minimum, int maximum) => (int)NextInclusive((long)minimum, maximum);

    /// <summary>
    /// Returns <see langword="true"/> with the given probability.
    /// </summary>
    /// <param name="probability">The probability, from 0 to 1.</param>
    /// <returns>The outcome.</returns>
    public bool Chance(double probability) => NextDouble() < probability;

    private static ulong SplitMix(ref ulong state)
    {
        state += SplitMixIncrement;

        ulong value = state;
        value = (value ^ (value >> SplitMixShiftFirst)) * SplitMixMultiplierFirst;
        value = (value ^ (value >> SplitMixShiftSecond)) * SplitMixMultiplierSecond;

        return value ^ (value >> SplitMixShiftThird);
    }

    private static ulong BitRotate(ulong value, int count) => System.Numerics.BitOperations.RotateLeft(value, count);
}
