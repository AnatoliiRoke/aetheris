using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Aetheris.Host.Observability;

/// <summary>
/// Masks passwords, tokens and e-mail addresses in every log event before it reaches a
/// sink (task 00002, section 2.7).
/// </summary>
/// <remarks>
/// <para>
/// The code names accounts by their identifier only, so this is the second line of defence:
/// a property whose name says it holds a secret is replaced as a whole, and any text value
/// that looks like an e-mail address or a long token is replaced as well.
/// </para>
/// <para>
/// Message templates are constants, so the values only ever travel in properties, where
/// this enricher sees them.
/// </para>
/// </remarks>
public sealed partial class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    /// <summary>
    /// The text that replaces a masked value.
    /// </summary>
    public const string Mask = "***";

    private static readonly string[] SensitiveNameParts =
    [
        "password", "token", "secret", "email", "cookie", "authorization", "authenticator", "totp",
    ];

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties.ToList())
        {
            LogEventPropertyValue masked = IsSensitiveName(property.Key)
                ? new ScalarValue(Mask)
                : MaskValue(property.Value);

            if (!ReferenceEquals(masked, property.Value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, masked));
            }
        }
    }

    private static bool IsSensitiveName(string name) =>
        SensitiveNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static LogEventPropertyValue MaskValue(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue { Value: string text }:
                string maskedText = TokenPattern().Replace(EmailPattern().Replace(text, Mask), Mask);
                return string.Equals(maskedText, text, StringComparison.Ordinal) ? value : new ScalarValue(maskedText);

            case StructureValue structure:
                return new StructureValue(
                    structure.Properties.Select(property => new LogEventProperty(
                        property.Name,
                        IsSensitiveName(property.Name) ? new ScalarValue(Mask) : MaskValue(property.Value))),
                    structure.TypeTag);

            case SequenceValue sequence:
                return new SequenceValue(sequence.Elements.Select(MaskValue));

            case DictionaryValue dictionary:
                return new DictionaryValue(dictionary.Elements.Select(entry =>
                    new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        entry.Key,
                        entry.Key.Value is string key && IsSensitiveName(key) ? new ScalarValue(Mask) : MaskValue(entry.Value))));

            default:
                return value;
        }
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    /// <summary>
    /// A long run of token characters: confirmation and reset tokens, refresh tokens,
    /// session cookies. Identifiers such as GUIDs are shorter and contain dashes only.
    /// </summary>
    [GeneratedRegex(@"[A-Za-z0-9_\-+/=]{40,}")]
    private static partial Regex TokenPattern();
}
