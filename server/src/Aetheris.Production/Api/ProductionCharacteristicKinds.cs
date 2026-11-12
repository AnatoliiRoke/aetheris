namespace Aetheris.Production.Api;

/// <summary>
/// The kinds of the characteristics the program reads (task 00038).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program, so the constructor of the ships reads the same
/// kinds whatever the characteristic is. A characteristic without a kind is one the program
/// does not read by its kind.
/// </remarks>
public static class CharacteristicKinds
{
    /// <summary>The number of the slots of a hull.</summary>
    public const string HullSlots = "hull_slots";

    /// <summary>The extra slots the race of the player gives a hull.</summary>
    public const string HullRaceSlot = "hull_race_slot";

    /// <summary>The capacity of the hold of a hull.</summary>
    public const string HullCapacity = "hull_capacity";

    /// <summary>The strength of a hull.</summary>
    public const string Integrity = "integrity";

    /// <summary>The energy a module takes.</summary>
    public const string EnergyUse = "energy_use";

    /// <summary>The power a reactor gives.</summary>
    public const string Power = "power";

    /// <summary>The crew a module needs.</summary>
    public const string Crew = "crew";

    /// <summary>The push a drive gives the hull.</summary>
    public const string DrivePush = "drive_push";

    /// <summary>The damage a weapon makes.</summary>
    public const string Damage = "damage";

    /// <summary>The capacity of a shield.</summary>
    public const string ShieldCapacity = "shield_capacity";

    /// <summary>The cargo a hold module adds to the hold of the hull.</summary>
    public const string Cargo = "cargo";

    /// <summary>How fast a shield recovers.</summary>
    public const string ShieldRegen = "shield_regen";

    /// <summary>How many shield units a module restores over time.</summary>
    public const string ShieldRestore = "shield_restore";

    /// <summary>The shots a weapon makes in one round.</summary>
    public const string Rate = "rate";

    /// <summary>The size of the launcher a weapon is, or the size an ammunition round fits.</summary>
    public const string LauncherSize = "launcher_size";

    /// <summary>How much a module extracts over time.</summary>
    public const string Yield = "yield";

    /// <summary>The radius of a geological scan.</summary>
    public const string ScanRadius = "scan_radius";

    /// <summary>How fast a scan proceeds.</summary>
    public const string ScanSpeed = "scan_speed";

    /// <summary>The duration of a scan, in game seconds.</summary>
    public const string ScanDuration = "scan_duration";

    /// <summary>The distance at which a module sees a target.</summary>
    public const string Detection = "detection";

    /// <summary>How fast an excavation proceeds.</summary>
    public const string DigSpeed = "dig_speed";

    /// <summary>What a module adds to the speed of the hull.</summary>
    public const string Speed = "speed";

    /// <summary>How fast a module collects debris.</summary>
    public const string CollectSpeed = "collect_speed";

    /// <summary>The fuel a module uses.</summary>
    public const string FuelUse = "fuel_use";

    /// <summary>How many missiles a module stops over time.</summary>
    public const string Missiles = "missiles";

    /// <summary>How fast a transfer proceeds.</summary>
    public const string TransferSpeed = "transfer_speed";

    /// <summary>How much a module sends at once.</summary>
    public const string TransferVolume = "transfer_volume";

    /// <summary>How long a deployment takes.</summary>
    public const string DeployTime = "deploy_time";

    /// <summary>How many colonists a module carries.</summary>
    public const string Colonists = "colonists";

    /// <summary>How much population a module holds.</summary>
    public const string Population = "population";

    /// <summary>How much a warehouse module holds.</summary>
    public const string Storage = "storage";

    /// <summary>How fast a repair proceeds.</summary>
    public const string RepairSpeed = "repair_speed";

    /// <summary>How fast training proceeds.</summary>
    public const string TrainingSpeed = "training_speed";

    /// <summary>How many market lots a module opens.</summary>
    public const string TradeLots = "trade_lots";

    /// <summary>How long a jump takes to prepare.</summary>
    public const string JumpPrep = "jump_prep";

    /// <summary>How many credits a hull or a module costs per hour (task 00039). Zero until the modelling fills the values.</summary>
    public const string Upkeep = "upkeep";

    /// <summary>
    /// The base seconds to assemble a ship of this hull, without a technology (task 00039).
    /// It is not the time to manufacture the hull part. Zero until the directory fills the value.
    /// </summary>
    public const string AssemblyTime = "assembly_time";

    /// <summary>
    /// Gets every kind, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        HullSlots,
        HullRaceSlot,
        HullCapacity,
        Integrity,
        EnergyUse,
        Power,
        Crew,
        DrivePush,
        Damage,
        ShieldCapacity,
        Cargo,
        ShieldRegen,
        ShieldRestore,
        Rate,
        LauncherSize,
        Yield,
        ScanRadius,
        ScanSpeed,
        ScanDuration,
        Detection,
        DigSpeed,
        Speed,
        CollectSpeed,
        FuelUse,
        Missiles,
        TransferSpeed,
        TransferVolume,
        DeployTime,
        Colonists,
        Population,
        Storage,
        RepairSpeed,
        TrainingSpeed,
        TradeLots,
        JumpPrep,
        Upkeep,
        AssemblyTime,
    ];

    /// <summary>
    /// The kinds a project plaque adds up. The kinds the plaque already shows on their own
    /// row, and the kinds that only choose ammunition, stay out of this set.
    /// </summary>
    public static IReadOnlySet<string> PlaqueFacts { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Yield,
        ScanRadius,
        ScanSpeed,
        ScanDuration,
        Detection,
        DigSpeed,
        Speed,
        CollectSpeed,
        FuelUse,
        Missiles,
        TransferSpeed,
        TransferVolume,
        DeployTime,
        Colonists,
        Population,
        Storage,
        RepairSpeed,
        TrainingSpeed,
        TradeLots,
        JumpPrep,
    };

    /// <summary>
    /// Tells whether the kind is added up on the plaque of a project.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the plaque adds the kind up.</returns>
    public static bool IsPlaqueFact(string? kind) =>
        kind is not null && PlaqueFacts.Contains(kind);

    /// <summary>
    /// Tells whether the kind is known to the program.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the kind is known.</returns>
    public static bool IsKnown(string? kind) =>
        kind is not null && All.Contains(kind, StringComparer.Ordinal);
}
