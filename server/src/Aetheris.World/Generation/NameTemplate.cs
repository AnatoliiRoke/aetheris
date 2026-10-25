using System.Globalization;

namespace Aetheris.World.Generation;

/// <summary>
/// Fills the name templates of the configuration.
/// </summary>
/// <remarks>
/// A template holds placeholders like <c>{sector}</c> or <c>{number}</c>. The names are codes
/// of the map, not translated texts; the client shows them as they are.
/// </remarks>
public static class NameTemplate
{
    /// <summary>The placeholder of the sector name.</summary>
    public const string Sector = "sector";

    /// <summary>The placeholder of the system name.</summary>
    public const string System = "system";

    /// <summary>The placeholder of a number.</summary>
    public const string Number = "number";

    /// <summary>The placeholder of an index.</summary>
    public const string Index = "index";

    /// <summary>The placeholder of an orbit.</summary>
    public const string Orbit = "orbit";

    /// <summary>
    /// Fills a template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="values">The placeholder names and their values.</param>
    /// <returns>The name.</returns>
    public static string Format(string template, params (string Placeholder, object Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        string result = template;

        foreach ((string placeholder, object value) in values)
        {
            result = result.Replace(
                "{" + placeholder + "}",
                Convert.ToString(value, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        return result;
    }
}
