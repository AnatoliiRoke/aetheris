using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Production.Api;

/// <summary>
/// The settings of the JSON of the production module.
/// </summary>
public static class ProductionJson
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
