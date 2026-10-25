using System.Globalization;

namespace Aetheris.Core.Configuration;

/// <summary>
/// A parameter that the server knows about: its key, its starting value and its bounds.
/// </summary>
/// <param name="Key">The dotted parameter key.</param>
/// <param name="DefaultValue">The value written into a fresh database.</param>
/// <param name="Minimum">The smallest numeric value an administrator may set, or <see langword="null"/>.</param>
/// <param name="Maximum">The largest numeric value an administrator may set, or <see langword="null"/>.</param>
/// <param name="Description">What the parameter controls.</param>
/// <remarks>
/// The values are kept as text, exactly as they are stored in <c>config_parameter</c>.
/// </remarks>
public sealed record ConfigParameterDefinition(
    string Key,
    string DefaultValue,
    string? Minimum,
    string? Maximum,
    string Description)
{
    /// <summary>
    /// Checks that a value fits the bounds of the parameter.
    /// </summary>
    /// <param name="value">The raw value to check.</param>
    /// <returns><see langword="true"/> when the value is acceptable.</returns>
    public bool Accepts(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (Minimum is null && Maximum is null)
        {
            return true;
        }

        if (!TryParse(value, out double number))
        {
            return false;
        }

        return (Minimum is null || (TryParse(Minimum, out double minimum) && number >= minimum))
            && (Maximum is null || (TryParse(Maximum, out double maximum) && number <= maximum));
    }

    /// <summary>
    /// Brings a numeric value into the bounds of the parameter.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <returns>The value, moved to the nearest bound when it is outside.</returns>
    public double Clamp(double value)
    {
        if (Minimum is not null && TryParse(Minimum, out double minimum) && value < minimum)
        {
            return minimum;
        }

        if (Maximum is not null && TryParse(Maximum, out double maximum) && value > maximum)
        {
            return maximum;
        }

        return value;
    }

    private static bool TryParse(string text, out double number) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
}
