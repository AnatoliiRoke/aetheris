namespace Aetheris.Research.Services;

/// <summary>
/// The kinds of the effects of a level of a technology (task 00012, section 2.4).
/// </summary>
/// <remarks>
/// <para>
/// An effect opens a subject or raises a parameter of a body. The kinds are logical codes of
/// the program (documentation/tech/09, section 3.4), and the rows of the catalog say which
/// subject or which parameter the effect touches.
/// </para>
/// <para>
/// The subjects a technology opens are named by their kind and their identifier, like the
/// subjects of the common requirement mechanism.
/// </para>
/// </remarks>
public static class TechnologyEffectTypes
{
    /// <summary>The technology opens the subject of the kind and the identifier.</summary>
    public const string Unlocks = "unlocks";

    /// <summary>The technology raises the production of the metals.</summary>
    public const string MetalProduction = "metal_production";

    /// <summary>The technology raises the production of the crystals.</summary>
    public const string CrystalProduction = "crystal_production";

    /// <summary>The technology shortens the construction time.</summary>
    public const string ConstructionTime = "construction_time";

    /// <summary>The technology shortens the production time (task 00013, decision 14).</summary>
    public const string ProductionTime = "production_time";

    /// <summary>The technology raises the capacity of the storages.</summary>
    public const string StorageCapacity = "storage_capacity";

    /// <summary>The technology raises the energy the buildings produce.</summary>
    public const string EnergyOutput = "energy_output";

    /// <summary>
    /// The technology raises the largest population of the body. The three codes of the economy
    /// effects are the ones of the economy module, so the modules cannot differ on them.
    /// </summary>
    public const string PopulationMax = Aetheris.Economy.Services.EconomyCodes.PopulationMaxEffect;

    /// <summary>
    /// The technology raises the speed the population of the body grows with (task 00015).
    /// </summary>
    public const string PopulationGrowthBonus = Aetheris.Economy.Services.EconomyCodes.PopulationGrowthBonusEffect;

    /// <summary>
    /// The technology raises the income of the credits of the civilization (task 00015).
    /// </summary>
    public const string CreditIncomeBonus = Aetheris.Economy.Services.EconomyCodes.CreditIncomeBonusEffect;

    /// <summary>
    /// Gets every effect kind, in the order of the form of the admin panel (task 00023).
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Unlocks,
        MetalProduction,
        CrystalProduction,
        ConstructionTime,
        ProductionTime,
        StorageCapacity,
        EnergyOutput,
        PopulationMax,
        PopulationGrowthBonus,
        CreditIncomeBonus,
    ];
}

/// <summary>
/// The kinds of the subjects a technology can open (task 00012, section 2.4).
/// </summary>
public static class TechnologyUnlockKinds
{
    /// <summary>A building type of the bodies module.</summary>
    public const string BuildingType = "building_type";

    /// <summary>A module of a ship.</summary>
    public const string Module = "module";

    /// <summary>A recipe of the production.</summary>
    public const string Recipe = "recipe";

    /// <summary>A class of a ship.</summary>
    public const string ShipClass = "ship_class";

    /// <summary>
    /// Gets every kind of a subject a technology can open (task 00023).
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [BuildingType, Module, Recipe, ShipClass];
}
