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

    /// <summary>The group named «Кристали». It may lie on a planet.</summary>
    public const string CrystalGroup = "resource_group_4";

    /// <summary>The group of manufactured resources. It does not lie on a body.</summary>
    public const string ManufacturedGroup = "resource_group_5";

    /// <summary>The first ore of the starting data.</summary>
    public const string Iron = "resource_1";

    /// <summary>The third ore of the starting data.</summary>
    public const string Copper = "resource_3";

    /// <summary>The first mineral of the starting data.</summary>
    public const string Quartzite = "resource_8";

    /// <summary>Helium-3. It is mined in space, not on a planet.</summary>
    public const string Helium = "resource_13";

    /// <summary>Dark matter. It is mined in space, not on a body.</summary>
    public const string DarkMatter = "resource_14";

    /// <summary>The ether. It is the hidden resource of an asteroid field.</summary>
    public const string Ether = "resource_15";

    /// <summary>The first crystal of the starting data.</summary>
    public const string Crystal = "resource_17";

    /// <summary>The first manufactured resource. A body does not have a deposit of it.</summary>
    public const string Manufactured = "resource_20";

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

    /// <summary>The housing.</summary>
    public const string ResidentialComplex = "building_2";

    /// <summary>The warehouse. One building stores every resource group and the parts.</summary>
    public const string MetalStorage = "building_7";

    /// <summary>The science center.</summary>
    public const string ScienceCenter = "building_9";

    /// <summary>The mine of the first ore.</summary>
    public const string IronMine = "building_12";

    /// <summary>The mine of the titanium. Its second level needs a technology.</summary>
    public const string TitaniumMine = "building_13";

    /// <summary>The mine of the third ore.</summary>
    public const string CopperMine = "building_14";

    /// <summary>The solar power plant.</summary>
    public const string PowerPlant = "building_27";

    /// <summary>The first factory of a manufactured resource.</summary>
    public const string Factory = "building_31";
}
