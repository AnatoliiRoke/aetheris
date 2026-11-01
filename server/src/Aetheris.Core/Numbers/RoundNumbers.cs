namespace Aetheris.Core.Numbers;

/// <summary>
/// Rounds a number to a few significant digits, so the values of the starting data read as round
/// numbers (task 00030): 1 234 becomes 1 200 and 56 789 becomes 57 000.
/// </summary>
public static class RoundNumbers
{
    private const double DecimalBase = 10;

    /// <summary>
    /// Rounds a value to a number of significant digits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="significantDigits">The number of significant digits; zero or less leaves the value as it is.</param>
    /// <returns>The rounded value; a value whose whole part has fewer digits is kept whole.</returns>
    public static double ToSignificant(double value, int significantDigits)
    {
        if (significantDigits <= 0 || value == 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            return value;
        }

        double magnitude = Math.Floor(Math.Log10(Math.Abs(value))) + 1;
        double scale = Math.Pow(DecimalBase, significantDigits - magnitude);

        // A value with fewer whole digits than asked for keeps its whole part: only the
        // digits left of the decimal point are rounded away.
        return scale >= 1
            ? Math.Round(value, MidpointRounding.AwayFromZero)
            : Math.Round(value * scale, MidpointRounding.AwayFromZero) / scale;
    }
}
