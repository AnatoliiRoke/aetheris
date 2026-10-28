using System.Text.Json;

namespace Aetheris.World.Data;

/// <summary>
/// The groups of the neutral codes of the reference rows of the world (task 00022, decision 1).
/// </summary>
/// <remarks>
/// A code is the group and a number given by the server: <c>resource_12</c>, <c>race_3</c>. The
/// group names the kind of the row, never its meaning, so a row keeps its code when it is renamed
/// or moved to another resource group.
/// </remarks>
public static class WorldCodeGroups
{
    /// <summary>The resource groups.</summary>
    public const string ResourceGroup = "resource_group";

    /// <summary>The resource types.</summary>
    public const string Resource = "resource";

    /// <summary>The celestial object types: stars, bodies, fields, nebulae.</summary>
    public const string ObjectType = "object_type";

    /// <summary>The size classes of the bodies.</summary>
    public const string SizeClass = "size_class";

    /// <summary>The generation rule groups.</summary>
    public const string RuleGroup = "rule_group";

    /// <summary>The races.</summary>
    public const string Race = "race";
}

/// <summary>
/// The localisation keys of the reference rows of the world: the kind of the row, its neutral
/// code and the part, <c>world.resource.resource_1.name</c> (task 00022, decision 2).
/// </summary>
public static class WorldTextKeys
{
    /// <summary>The prefix of the keys of the resource groups.</summary>
    public const string ResourceGroup = "world.resource_group";

    /// <summary>The prefix of the keys of the resource types.</summary>
    public const string Resource = "world.resource";

    /// <summary>The prefix of the keys of the object types.</summary>
    public const string ObjectType = "world.object_type";

    /// <summary>The prefix of the keys of the size classes.</summary>
    public const string SizeClass = "world.size_class";

    /// <summary>The prefix of the keys of the races.</summary>
    public const string Race = "world.race";

    /// <summary>The part of the key of a name.</summary>
    public const string NamePart = "name";

    /// <summary>The part of the key of a description.</summary>
    public const string DescriptionPart = "description";

    /// <summary>The part of the key of the mood of a race.</summary>
    public const string MoodPart = "mood";

    /// <summary>The part of the key of a strength of a race, followed by its number.</summary>
    public const string TraitPart = "trait_";

    /// <summary>
    /// Builds a key.
    /// </summary>
    /// <param name="prefix">The prefix of the kind of the row.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <param name="part">The part.</param>
    /// <returns>The key.</returns>
    public static string Of(string prefix, string code, string part) => prefix + "." + code + "." + part;

    /// <summary>
    /// Builds the key of a name.
    /// </summary>
    /// <param name="prefix">The prefix of the kind of the row.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <returns>The key.</returns>
    public static string Name(string prefix, string code) => Of(prefix, code, NamePart);

    /// <summary>
    /// Builds the key of a description.
    /// </summary>
    /// <param name="prefix">The prefix of the kind of the row.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <returns>The key.</returns>
    public static string Description(string prefix, string code) => Of(prefix, code, DescriptionPart);
}

/// <summary>
/// The table of the old codes of the reference rows and their neutral codes (task 00022,
/// section 3.1), kept with the migrations that applied it.
/// </summary>
/// <remarks>
/// The table maps every old code by the group of its table. The generation rule groups had codes
/// that repeated per object type; they are numbered in the order of their identifiers, so their
/// entries only name the group. The source scanners of the server and the client read the old
/// codes from here and refuse any of them in the code.
/// </remarks>
public static class NeutralCodeMap
{
    /// <summary>
    /// The name of the embedded table.
    /// </summary>
    public const string ResourceName = "Aetheris.World.neutral-code-map.json";

    /// <summary>
    /// Reads the table: the old code and the new one by the code group.
    /// </summary>
    /// <returns>The table.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load()
    {
        using Stream stream = typeof(NeutralCodeMap).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded table {ResourceName} is missing.");

        Dictionary<string, Dictionary<string, string>> table = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
            ?? throw new InvalidOperationException($"The embedded table {ResourceName} is empty.");

        return table.ToDictionary(
            group => group.Key,
            group => (IReadOnlyDictionary<string, string>)group.Value,
            StringComparer.Ordinal);
    }
}
