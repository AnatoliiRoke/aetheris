namespace Aetheris.Tests.World;

/// <summary>
/// The neutral codes of the starting data that the tests name (task 00022).
/// </summary>
/// <remarks>
/// The server knows no code of a game entity; only the tests, which check the starting data,
/// name some of its rows. The names here say what the row is in the starting data today.
/// </remarks>
public static class SeedCodes
{
    /// <summary>The first group of the starting data, the metals.</summary>
    public const string MetalGroup = "resource_group_1";

    /// <summary>The group of the starting data named «Мінерали».</summary>
    public const string MineralGroup = "resource_group_2";

    /// <summary>The group of the ether.</summary>
    public const string EtherGroup = "resource_group_4";

    /// <summary>The group named «Кристали», only in asteroid fields.</summary>
    public const string CrystalGroup = "resource_group_5";

    /// <summary>The first metal of the starting data.</summary>
    public const string Iron = "resource_1";

    /// <summary>The third metal of the starting data.</summary>
    public const string Copper = "resource_3";

    /// <summary>The first mineral of the starting data.</summary>
    public const string Quartzite = "resource_5";

    /// <summary>The ether.</summary>
    public const string Ether = "resource_11";

    /// <summary>The crystal of the asteroid fields.</summary>
    public const string Crystal = "resource_12";

    /// <summary>The second star type.</summary>
    public const string RedDwarf = "object_type_2";

    /// <summary>The start body type.</summary>
    public const string Terrestrial = "object_type_5";

    /// <summary>The fourth body type.</summary>
    public const string Desert = "object_type_8";

    /// <summary>The ordinary asteroid field.</summary>
    public const string OrdinaryField = "object_type_11";

    /// <summary>The rich asteroid field.</summary>
    public const string RichField = "object_type_12";

    /// <summary>The second race.</summary>
    public const string SecondRace = "race_2";

    /// <summary>The main building, the start building of every race.</summary>
    public const string AdminCenter = "building_1";

    /// <summary>The science center.</summary>
    public const string ScienceCenter = "building_2";

    /// <summary>The residential complex.</summary>
    public const string ResidentialComplex = "building_3";

    /// <summary>The power plant.</summary>
    public const string PowerPlant = "building_4";

    /// <summary>The mine of the first metal.</summary>
    public const string IronMine = "building_5";

    /// <summary>The mine of the third metal.</summary>
    public const string CopperMine = "building_6";

    /// <summary>The storage of the metals.</summary>
    public const string MetalStorage = "building_7";

    /// <summary>The storage of the minerals.</summary>
    public const string MineralStorage = "building_8";

    /// <summary>The fuel tank.</summary>
    public const string FuelStorage = "building_9";
}
