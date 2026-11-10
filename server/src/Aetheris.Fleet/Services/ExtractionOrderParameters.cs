using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The parameters of an order of extraction (task 00050).
/// </summary>
/// <remarks>
/// The order of extraction lasts as long as the player asked for, so the moment it began is kept
/// in the document of the parameters of the order. The document is the place for the parameters of
/// the orders of the next tasks too, so the table of the orders does not grow a column for every
/// kind.
/// </remarks>
public static class ExtractionOrderParameters
{
    /// <summary>
    /// The name of the parameter that holds the moment the extraction began.
    /// </summary>
    public const string StartedAtName = "started_at";

    /// <summary>
    /// Writes the moment the extraction began into the document of the parameters of an order.
    /// </summary>
    /// <param name="parameters">The document of the parameters of the order.</param>
    /// <param name="startedAt">The moment the extraction began, in game time.</param>
    /// <returns>The new document of the parameters.</returns>
    public static string WriteStartedAt(string? parameters, DateTimeOffset startedAt)
    {
        JsonObject document = Read(parameters);
        document[StartedAtName] = startedAt.ToString("O", CultureInfo.InvariantCulture);

        return document.ToJsonString();
    }

    /// <summary>
    /// Reads the moment the extraction began out of the document of the parameters of an order.
    /// </summary>
    /// <param name="parameters">The document of the parameters of the order.</param>
    /// <returns>The moment the extraction began, or <see langword="null"/> when the order has none.</returns>
    public static DateTimeOffset? ReadStartedAt(string? parameters)
    {
        JsonObject document = Read(parameters);

        if (!document.TryGetPropertyValue(StartedAtName, out JsonNode? node) || node is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            node.GetValue<string>(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset startedAt)
            ? startedAt
            : null;
    }

    /// <summary>
    /// Reads a document of parameters, or an empty one when the text is not a document.
    /// </summary>
    /// <param name="parameters">The text of the document.</param>
    /// <returns>The document.</returns>
    private static JsonObject Read(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(parameters) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}
