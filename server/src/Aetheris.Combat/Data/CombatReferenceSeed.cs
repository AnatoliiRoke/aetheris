using System.Text.Json;
using Aetheris.Combat.Api;
using Aetheris.Core.Data;

namespace Aetheris.Combat.Data;

/// <summary>
/// The starting rows of the target rules and the behaviors (task 00062).
/// </summary>
public static class CombatReferenceSeed
{
    /// <summary>The name of the embedded starting data.</summary>
    public const string ResourceName = "Aetheris.Combat.combat-reference-seed.json";

    /// <summary>The author of the texts of the starting data.</summary>
    public const string SeedAuthor = "combat_seed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads and checks the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    /// <exception cref="InvalidOperationException">A code is missing, malformed or repeated.</exception>
    public static CombatSeedFile Load()
    {
        using Stream stream = typeof(CombatReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        CombatSeedFile seed = JsonSerializer.Deserialize<CombatSeedFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");

        Validate(seed);

        return seed;
    }

    /// <summary>
    /// Checks the codes and the logical codes of the file.
    /// </summary>
    /// <param name="seed">The starting data.</param>
    /// <exception cref="InvalidOperationException">A code is missing, malformed or repeated.</exception>
    public static void Validate(CombatSeedFile seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        SeedCodeRules.Validate(seed.TargetRules.Select(row => row.Code), "target rule");
        SeedCodeRules.Validate(seed.Behaviors.Select(row => row.Code), "behavior");
        CheckLogic(seed.TargetRules, "target rule");
        CheckLogic(seed.Behaviors, "behavior");
    }

    private static void CheckLogic(IReadOnlyList<CombatSeedRule> rows, string kind)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (CombatSeedRule row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.LogicCode) || !CombatLogicCodes.Accepts(row.LogicCode) || row.LogicCode.Length > CombatLogicCodes.MaxLength)
            {
                throw new InvalidOperationException($"The logical code '{row.LogicCode}' of a {kind} is not acceptable.");
            }

            if (!seen.Add(row.LogicCode))
            {
                throw new InvalidOperationException($"The starting data gives the {kind} logical code '{row.LogicCode}' twice.");
            }
        }
    }
}

/// <summary>The starting data of the combat module.</summary>
/// <param name="TargetRules">The target rules.</param>
/// <param name="Behaviors">The behaviors.</param>
public sealed record CombatSeedFile(
    IReadOnlyList<CombatSeedRule> TargetRules,
    IReadOnlyList<CombatSeedRule> Behaviors);

/// <summary>One reference row of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="LogicCode">The logical code.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
public sealed record CombatSeedRule(
    string Code,
    string LogicCode,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description);
