namespace Aetheris.Research.Services;

/// <summary>
/// The branches of the tree of the technologies (task 00012, decision 2).
/// </summary>
/// <remarks>
/// The branches are logical codes of the program, so they keep their meaning when the neutral
/// codes of the rows change (documentation/tech/09, section 3.4). The branches are data of
/// the seed, and a new branch arrives as a new row and a new code here.
/// </remarks>
public static class TechnologyBranches
{
    /// <summary>The branch of the production, the storage and the energy.</summary>
    public const string Economy = "economy";

    /// <summary>The branch of the ships and their weapons.</summary>
    public const string Fleet = "fleet";

    /// <summary>The branch of the engines and the logistics.</summary>
    public const string EnginesAndLogistics = "engines_and_logistics";

    /// <summary>The branch of the defence of the bodies.</summary>
    public const string Defence = "defence";

    /// <summary>The branch of the colonisation and the space.</summary>
    public const string Colonisation = "colonisation";

    /// <summary>The branch of the trade.</summary>
    public const string Trade = "trade";

    /// <summary>
    /// Gets every branch in the order of the tree; a branch without technologies is shown
    /// empty (task 00012, decision 11).
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Economy, Fleet, EnginesAndLogistics, Defence, Colonisation, Trade];
}

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

    /// <summary>The technology raises the capacity of the storages.</summary>
    public const string StorageCapacity = "storage_capacity";

    /// <summary>The technology raises the energy the buildings produce.</summary>
    public const string EnergyOutput = "energy_output";

    /// <summary>The technology raises the largest population of the body.</summary>
    public const string PopulationMax = "population_max";
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
}
