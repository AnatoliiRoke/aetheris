using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Combat.Engine;

/// <summary>
/// One writer for a segment. Two runs use it, so the text matches byte for byte when the
/// states match. Maps are written from a sorted dictionary.
/// </summary>
public static class BattleJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Writes a segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns>The text.</returns>
    public static string Write(SegmentResult segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        return JsonSerializer.Serialize(segment, Options);
    }

    /// <summary>Writes a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The text.</returns>
    public static string WriteState(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>Reads a state written by <see cref="WriteState"/>.</summary>
    /// <param name="json">The text.</param>
    /// <returns>The state.</returns>
    /// <exception cref="JsonException">The text is not a state.</exception>
    public static BattleState ReadState(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<BattleState>(json, Options)
            ?? throw new JsonException("The text has no state.");
    }
}
