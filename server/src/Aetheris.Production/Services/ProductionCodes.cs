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

    /// <summary>The group of the neutral codes of the characteristics of the parts (task 00027).</summary>
    public const string CharacteristicCodeGroup = "characteristic";

    /// <summary>The prefix of the keys of the groups of the parts.</summary>
    public const string PartGroup = "production.part_group";

    /// <summary>The prefix of the keys of the types of the parts.</summary>
    public const string PartType = "production.part_type";

    /// <summary>The prefix of the keys of the recipes.</summary>
    public const string Recipe = "production.recipe";

    /// <summary>The prefix of the keys of the characteristics of the parts (task 00027).</summary>
    public const string Characteristic = "production.characteristic";

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

    /// <summary>
    /// Builds the key of the name of a characteristic of the parts (task 00027).
    /// </summary>
    /// <param name="code">The neutral code of the characteristic.</param>
    /// <returns>The key.</returns>
    public static string CharacteristicName(string code) => $"{Characteristic}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a characteristic of the parts (task 00027).
    /// </summary>
    /// <param name="code">The neutral code of the characteristic.</param>
    /// <returns>The key.</returns>
    public static string CharacteristicDescription(string code) => $"{Characteristic}.{code}.description";

    /// <summary>
    /// Builds the key of the unit of measure of a characteristic of the parts (task 00027).
    /// </summary>
    /// <param name="code">The neutral code of the characteristic.</param>
    /// <returns>The key.</returns>
    public static string CharacteristicUnit(string code) => $"{Characteristic}.{code}.unit";
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
    /// The mark of a capsule of inhabitants: a part with this attribute holds as many inhabitants
    /// as the number of the attribute and gives them to the population of a body when it is used
    /// (task 00031).
    /// </summary>
    public const string PopulationCapsule = "population_capsule";

    /// <summary>
    /// The race of a capsule of inhabitants, as the neutral code of the race: the inhabitants of
    /// the capsule belong to this race, so only a player of the race can use the capsule
    /// (task 00031).
    /// </summary>
    public const string CapsuleRace = "capsule_race";

    /// <summary>
    /// The role of the part: what it may be put on, one of <see cref="PartRoles"/>.
    /// </summary>
    public const string Role = "role";

    /// <summary>
    /// The role of the hull of the part: what the part is a hull of, one of
    /// <see cref="HullRoles"/>. It is set on a hull, so every hull of a group carries the same
    /// role (task 00038, section 3.1).
    /// </summary>
    public const string HullRole = "hull_role";

    /// <summary>
    /// The kind of the component the part is: the drive, the reactor or the life support, one
    /// of <see cref="PartComponents"/>. The constructor of the ships reads it to know which
    /// parts a ship must have (task 00038, decision 9).
    /// </summary>
    public const string Component = "component";

    /// <summary>
    /// The order of a flight plan the part opens for a fleet, one of <see cref="PartFlightOrders"/>: a fleet may be
    /// given the order only when a ship of it carries a part with this attribute (task 00058).
    /// </summary>
    public const string FlightOrder = "flight_order";

    /// <summary>The kind of target a scanning module detects.</summary>
    public const string ScanTarget = "scan_target";

    /// <summary>
    /// The mark of a part that is not a module of a ship: the ammunition and the capsules of
    /// inhabitants are carried in the hold or used on a body, so they are never fitted to a
    /// hull and never show in the constructor (the decision of the author, task 00038).
    /// </summary>
    public const string NotAShipModule = "not_a_ship_module";

    /// <summary>
    /// The class of the drive of the part, one of <see cref="PartEngineKinds"/> (task 00072). The
    /// pull of the drives is counted apart for every class, so a fleet may carry drives of many
    /// classes at once.
    /// </summary>
    public const string EngineKind = "engine_kind";

    /// <summary>
    /// The mark of a part that is only a cargo: it is never fitted to a hull and never shows in the
    /// constructor, but it is carried in the hold of a ship (task 00073).
    /// </summary>
    public const string CargoOnly = "cargo_only";

    /// <summary>
    /// The order of a flight plan a cargo opens for a fleet, one of <see cref="PartCargoOrders"/>:
    /// a fleet may be given the order only when a ship of it carries a part with this attribute
    /// (task 00073).
    /// </summary>
    public const string CargoOrder = "cargo_order";

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
        PopulationCapsule,
        CapsuleRace,
        Role,
        HullRole,
        Component,
        FlightOrder,
        ScanTarget,
        NotAShipModule,
        EngineKind,
        CargoOnly,
        CargoOrder,
    ];
}

/// <summary>
/// The classes of the drives of the ships (task 00072).
/// </summary>
/// <remarks>
/// <para>
/// The classes are logical codes of the program, so the fleet module reads the same codes whatever
/// the drive is. A drive of the class <see cref="Normal"/> flies inside a star system; a drive of
/// the class <see cref="Hyper"/> flies between the stars; a drive of the class <see cref="Jump"/>
/// jumps between them.
/// </para>
/// <para>
/// A ship with a drive of a class other than <see cref="Normal"/> carries one of the normal
/// drives too, because the drive of its own class works only between the stars.
/// </para>
/// </remarks>
public static class PartEngineKinds
{
    /// <summary>The ordinary drive: it flies inside a star system and around it.</summary>
    public const string Normal = "normal";

    /// <summary>
    /// The gravitational drive (task 00072): it works only near a star, so it gives no push and
    /// burns no fuel in open space.
    /// </summary>
    public const string Gravity = "gravity";

    /// <summary>The drive of the hyperflight: it flies between the stars much faster.</summary>
    public const string Hyper = "hyper";

    /// <summary>The drive of the jump: it jumps between the stars after a preparation.</summary>
    public const string Jump = "jump";

    /// <summary>
    /// Gets every class of a drive, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Normal, Gravity, Hyper, Jump];
}

/// <summary>The supported targets of a scanning module.</summary>
public static class PartScanTargets
{
    /// <summary>Star systems and their objects.</summary>
    public const string Systems = "systems";

    /// <summary>Fleets.</summary>
    public const string Fleets = "fleets";

    /// <summary>Gets the available scan targets.</summary>
    public static IReadOnlyList<string> All { get; } = [Systems, Fleets];
}

/// <summary>
/// The orders of a flight plan that a part opens (task 00058). They are logical codes; the plan names the same
/// codes (<c>FlightOrderTypes</c> of the fleet module).
/// </summary>
public static class PartFlightOrders
{
    /// <summary>The fleet may fly faster than a plain flight.</summary>
    public const string Hyperfly = "hyperfly";

    /// <summary>The fleet may jump to a target without the flight of the space between.</summary>
    public const string Jump = "jump";

    /// <summary>The fleet may scan.</summary>
    public const string Scan = "scan";

    /// <summary>
    /// Gets every order a part may open, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Hyperfly, Jump, Scan];
}

/// <summary>
/// The orders of a flight plan that a cargo opens (task 00073). They are logical codes; the plan
/// names the same codes (<c>FlightOrderTypes</c> of the fleet module).
/// </summary>
public static class PartCargoOrders
{
    /// <summary>The fleet may colonize a planet with the cargo in its hold.</summary>
    public const string Colonize = "colonize";

    /// <summary>
    /// Gets every order a cargo may open, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Colonize];
}

/// <summary>
/// The kinds of the component a part is (task 00038, decision 9).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program, so the constructor of the ships reads the same
/// codes whatever the part is.
/// </remarks>
public static class PartComponents
{
    /// <summary>
    /// The drive of the ship: it gives the ship its movement. A ship without a drive does not
    /// fly.
    /// </summary>
    public const string Drive = "drive";

    /// <summary>
    /// The reactor of the ship: it gives the energy the modules take. A ship without a reactor
    /// has no energy.
    /// </summary>
    public const string Reactor = "reactor";

    /// <summary>
    /// Gets every kind, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Drive, Reactor];

    /// <summary>
    /// Tells whether the kind is known to the program.
    /// </summary>
    /// <param name="component">The kind.</param>
    /// <returns><see langword="true"/> when the kind is known.</returns>
    public static bool IsKnown(string? component) =>
        component is not null && All.Contains(component, StringComparer.Ordinal);
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
