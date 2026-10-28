using System.Text.Json;

namespace Aetheris.Research.Api;

/// <summary>
/// The JSON settings of the research API: snake_case names (documentation/tech/08, rule 3).
/// </summary>
public static class ResearchJson
{
    /// <summary>
    /// Gets the serializer settings.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
