using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aetheris.Host.Observability;

/// <summary>
/// Turns a captured JSON body into a line of the log with the secret fields masked.
/// </summary>
/// <remarks>
/// A body that was captured whole is parsed and every field whose name contains one of the
/// configured parts is replaced, at any depth. A body that was cut at the limit, or that is not
/// valid JSON, is masked by a pattern over the text instead; the pattern hides more rather than
/// less. The enricher of the log masks e-mail addresses and long tokens in the values that are
/// left, as it does for every log event.
/// </remarks>
public static class BodyMasker
{
    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Masks a captured body.
    /// </summary>
    /// <param name="captured">The captured bytes.</param>
    /// <param name="totalBytes">The number of bytes the body had in all.</param>
    /// <param name="settings">The parameters of the observability.</param>
    /// <returns>The text for the log: the masked body, with a note when it was cut.</returns>
    public static string Mask(ReadOnlySpan<byte> captured, long totalBytes, ObservabilitySnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string text = Encoding.UTF8.GetString(captured);
        bool cut = totalBytes > captured.Length;

        string masked = cut ? MaskByPattern(text, settings) : MaskWhole(text, settings);

        return cut
            ? masked + string.Create(CultureInfo.InvariantCulture, $"...[cut, {totalBytes} bytes]")
            : masked;
    }

    private static string MaskWhole(string text, ObservabilitySnapshot settings)
    {
        JsonNode? node;

        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return MaskByPattern(text, settings);
        }

        MaskNode(node, settings.MaskedFields);

        return node?.ToJsonString(WriteOptions) ?? "null";
    }

    private static void MaskNode(JsonNode? node, string[] maskedFields)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (string name in jsonObject.Select(property => property.Key).ToList())
                {
                    if (IsMasked(name, maskedFields))
                    {
                        jsonObject[name] = SensitiveDataMaskingEnricher.Mask;
                    }
                    else
                    {
                        MaskNode(jsonObject[name], maskedFields);
                    }
                }

                break;

            case JsonArray jsonArray:
                foreach (JsonNode? element in jsonArray)
                {
                    MaskNode(element, maskedFields);
                }

                break;
        }
    }

    private static bool IsMasked(string name, string[] maskedFields) =>
        maskedFields.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static string MaskByPattern(string text, ObservabilitySnapshot settings)
    {
        if (settings.MaskedFieldPattern is null)
        {
            return text;
        }

        try
        {
            return settings.MaskedFieldPattern.Replace(text, "\"${name}\":\"" + SensitiveDataMaskingEnricher.Mask + "\"");
        }
        catch (RegexMatchTimeoutException)
        {
            // A body that the pattern cannot finish in time is not written at all.
            return SensitiveDataMaskingEnricher.Mask;
        }
    }
}
