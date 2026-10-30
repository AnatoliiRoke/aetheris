namespace Aetheris.Production.Services;

/// <summary>
/// The codes of the reference rows of the production module and the keys of their texts
/// (task 00013).
/// </summary>
/// <remarks>
/// The codes of the rows are neutral and given by the server, so a new part or a new
/// recipe arrives as a row and not as code (documentation/tech/09, section 3.1).
/// </remarks>
public static class ProductionTextKeys
{
    /// <summary>The group of the neutral codes of the groups of the parts.</summary>
    public const string PartGroupCodeGroup = "part_group";

    /// <summary>The group of the neutral codes of the types of the parts.</summary>
    public const string PartTypeCodeGroup = "part";

    /// <summary>The group of the neutral codes of the recipes.</summary>
    public const string RecipeCodeGroup = "recipe";

    /// <summary>The prefix of the keys of the groups of the parts.</summary>
    public const string PartGroup = "production.part_group";

    /// <summary>The prefix of the keys of the types of the parts.</summary>
    public const string PartType = "production.part_type";

    /// <summary>The prefix of the keys of the recipes.</summary>
    public const string Recipe = "production.recipe";

    /// <summary>
    /// Builds the key of the name of a group of the parts.
    /// </summary>
    /// <param name="code">The neutral code of the group.</param>
    /// <returns>The key.</returns>
    public static string GroupName(string code) => $"{PartGroup}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a group of the parts.
    /// </summary>
    /// <param name="code">The neutral code of the group.</param>
    /// <returns>The key.</returns>
    public static string GroupDescription(string code) => $"{PartGroup}.{code}.description";

    /// <summary>
    /// Builds the key of the name of a type of a part.
    /// </summary>
    /// <param name="code">The neutral code of the type.</param>
    /// <returns>The key.</returns>
    public static string PartName(string code) => $"{PartType}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a type of a part.
    /// </summary>
    /// <param name="code">The neutral code of the type.</param>
    /// <returns>The key.</returns>
    public static string PartDescription(string code) => $"{PartType}.{code}.description";

    /// <summary>
    /// Builds the key of the name of a recipe.
    /// </summary>
    /// <param name="code">The neutral code of the recipe.</param>
    /// <returns>The key.</returns>
    public static string RecipeName(string code) => $"{Recipe}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a recipe.
    /// </summary>
    /// <param name="code">The neutral code of the recipe.</param>
    /// <returns>The key.</returns>
    public static string RecipeDescription(string code) => $"{Recipe}.{code}.description";
}

/// <summary>
/// The mark levels of the parts (task 00013, decision 2.6).
/// </summary>
/// <remarks>
/// The levels are the rules of the game, so they live in the code as logical codes and the
/// rows of the catalog name them.
/// </remarks>
public static class PartMarkLevels
{
    /// <summary>The first mark level.</summary>
    public const int First = 1;

    /// <summary>The second mark level.</summary>
    public const int Second = 2;

    /// <summary>The third mark level.</summary>
    public const int Third = 3;

    /// <summary>The largest mark level of a part.</summary>
    public const int Max = Third;
}

/// <summary>
/// The keys of the attributes of the parts (task 00013, decision 2.11).
/// </summary>
/// <remarks>
/// The keys are logical codes of the program, so the constructor of the ships reads the same
/// codes whatever the part is, and a new parameter is a new code with one reader.
/// </remarks>
public static class PartAttributeKeys
{
    /// <summary>The energy the part takes, or gives for a generator.</summary>
    public const string Energy = "energy";

    /// <summary>The crew the part needs.</summary>
    public const string Crew = "crew";

    /// <summary>The damage the part makes.</summary>
    public const string Damage = "damage";

    /// <summary>The kind of the damage the part makes, one of <see cref="PartDamageKinds"/>.</summary>
    public const string DamageKind = "damage_kind";

    /// <summary>The range of the part, in whatever the part measures it.</summary>
    public const string Range = "range";

    /// <summary>The size of the slot the part takes, one of <see cref="PartSlotSizes"/>.</summary>
    public const string SlotSize = "slot_size";

    /// <summary>The mass of the part.</summary>
    public const string Mass = "mass";

    /// <summary>
    /// The mark of a one-shot device of the terraforming: a part with this attribute is such a
    /// device, and the attribute holds no value. The multiplier of the deposits is rolled by the
    /// world for every deposit in the range of the configuration, so the part does not give it
    /// (task 00014, section 11).
    /// </summary>
    public const string TerraformingDevice = "terraforming_device";

    /// <summary>
    /// Gets every key of an attribute, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Energy,
        Crew,
        Damage,
        DamageKind,
        Range,
        SlotSize,
        Mass,
        TerraformingDevice,
    ];
}

/// <summary>
/// The kinds of the damage a part makes (task 00013, decision 2.11).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program, so the battle reads them whatever the part is.
/// Their words differ from the old codes of the game entities, so a part attribute never
/// looks like a resource or a class of size (task 00022).
/// </remarks>
public static class PartDamageKinds
{
    /// <summary>The energy damage of the lasers.</summary>
    public const string Energy = "energy";

    /// <summary>The thermal damage of the incinerators.</summary>
    public const string Thermal = "thermal";

    /// <summary>The kinetic damage of the guns.</summary>
    public const string Kinetic = "kinetic";

    /// <summary>The ion damage.</summary>
    public const string Ion = "ion";

    /// <summary>The explosive damage of the rockets and the torpedoes.</summary>
    public const string Explosive = "explosive";

    /// <summary>
    /// Gets every kind, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Energy, Thermal, Kinetic, Ion, Explosive];
}

/// <summary>
/// The sizes of the slots of a ship (task 00013, decision 2.11).
/// </summary>
/// <remarks>
/// The sizes are logical codes of the program, so the constructor of the ships reads them
/// whatever the part is. Their words differ from the old codes of the game entities, so a
/// part attribute never looks like a class of size (task 00022).
/// </remarks>
public static class PartSlotSizes
{
    /// <summary>The compact slot.</summary>
    public const string Compact = "compact";

    /// <summary>The standard slot.</summary>
    public const string Standard = "standard";

    /// <summary>The heavy slot.</summary>
    public const string Heavy = "heavy";

    /// <summary>
    /// Gets every size, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Compact, Standard, Heavy];
}
