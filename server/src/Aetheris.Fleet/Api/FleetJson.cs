using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Fleet.Api;

/// <summary>
/// The settings of the JSON of the fleet module.
/// </summary>
public static class FleetJson
{
    /// <summary>
    /// Gets the serializer settings: snake_case names, as in the rest of the API
    /// (documentation/tech/08, rule 3).
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        // An empty field is sent as null, never left out: the client reads `null` as "nothing", and a
        // missing field would be read as a value that is not there.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
