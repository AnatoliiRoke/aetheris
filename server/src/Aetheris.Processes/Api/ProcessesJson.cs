using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Processes.Api;

/// <summary>
/// The settings of the JSON of the processes module.
/// </summary>
public static class ProcessesJson
{
    /// <summary>
    /// Gets the serializer settings: snake_case names, as in the rest of the API
    /// (documentation/tech/08, rule 3).
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
