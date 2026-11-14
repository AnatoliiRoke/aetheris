namespace Aetheris.Combat.Engine;

/// <summary>The whole field at the start or the end of a segment.</summary>
/// <param name="BattleId">The engagement.</param>
/// <param name="Cycle">The cycle this segment belongs to. The run does not advance it.</param>
/// <param name="Seed">The seed of the engagement. A segment mixes it with the cycle.</param>
/// <param name="Field">The size of the field and the circles on it.</param>
/// <param name="Sides">The sides, in the order of their identifiers.</param>
/// <param name="Squadrons">The squadrons.</param>
/// <param name="Ships">The ships.</param>
/// <param name="Drones">The drones that are already out, or on the armor.</param>
/// <param name="Debris">The debris left by destroyed ships.</param>
public sealed record BattleState(
    long BattleId,
    int Cycle,
    ulong Seed,
    FieldState Field,
    IReadOnlyList<SideState> Sides,
    IReadOnlyList<SquadronState> Squadrons,
    IReadOnlyList<ShipState> Ships,
    IReadOnlyList<DroneState> Drones,
    IReadOnlyList<DebrisState> Debris);

/// <summary>The rectangle of the field and the circles that stand on it.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Objects">Planets and asteroid fields.</param>
public sealed record FieldState(
    double Width,
    double Height,
    IReadOnlyList<FieldObject> Objects);

/// <summary>A circle on the field.</summary>
/// <param name="Kind">One of <see cref="FieldObjectKinds"/>.</param>
/// <param name="Center">The center.</param>
/// <param name="Radius">The radius.</param>
public sealed record FieldObject(string Kind, Vec3 Center, double Radius);

/// <summary>One side of the engagement.</summary>
/// <param name="SideId">The side.</param>
/// <param name="Attacker">Whether this side opened the engagement.</param>
public sealed record SideState(int SideId, bool Attacker);

/// <summary>A squadron and the order it already has. The run does not read orders from anywhere else.</summary>
/// <param name="SquadronId">The squadron.</param>
/// <param name="SideId">The side.</param>
/// <param name="Name">The name kept for the report. It does not affect the numbers.</param>
/// <param name="ShipIds">The ships, in the order they were given.</param>
/// <param name="Order">The order in force for this segment.</param>
public sealed record SquadronState(
    int SquadronId,
    int SideId,
    string Name,
    IReadOnlyList<long> ShipIds,
    SquadronOrder Order);

/// <summary>The order a squadron carries into the segment.</summary>
/// <param name="Route">Points the center walks, in order, before the behavior applies.</param>
/// <param name="BehaviorLogic">One of <see cref="BehaviorLogics"/>.</param>
/// <param name="TargetRuleLogic">One of <see cref="TargetRuleLogics"/>.</param>
/// <param name="FocusShipId">A ship to shoot first when it is visible and in range.</param>
/// <param name="FocusSquadronId">A squadron to shoot first when one of its ships is visible and in range.</param>
/// <param name="DroneCommand">What to do with the drones at the start of the segment.</param>
/// <param name="Retreat">Whether the squadron leaves toward the nearest edge.</param>
/// <param name="EscortSquadronId">The squadron an escort follows.</param>
public sealed record SquadronOrder(
    IReadOnlyList<Vec3> Route,
    string BehaviorLogic,
    string TargetRuleLogic,
    long? FocusShipId,
    long? FocusSquadronId,
    DroneCommand DroneCommand,
    bool Retreat,
    int? EscortSquadronId);

/// <summary>Launch, recall or hold, and an optional part the order names.</summary>
/// <param name="Logic">One of <see cref="DroneCommandLogics"/>. A missing logic means hold.</param>
/// <param name="PartTypeId">The part the order names, or none.</param>
public readonly record struct DroneCommand(string? Logic, int? PartTypeId);

/// <summary>The numbers of one module that the formulas read. Empty means the module adds nothing.</summary>
public readonly record struct ModuleFacts(
    double EnergyUse,
    double Power,
    double Speed,
    double Detection,
    double Damage,
    double Rate,
    double Range,
    double FlightSpeed,
    string DamageKind,
    int AmmoPartTypeId,
    double EnergyBlock,
    double KineticBlock,
    double ShieldCapacity,
    double ShieldRestore,
    double Repair,
    double AntiMissileChance,
    double Missiles,
    double LauncherSize,
    double Magazine);

/// <summary>One row of the accuracy of a weapon against a hull group.</summary>
/// <param name="HullGroupId">The group.</param>
/// <param name="Chance">The chance before the falloff of the shot.</param>
public readonly record struct AccuracyEntry(int HullGroupId, double Chance);

/// <summary>One fitted module.</summary>
/// <param name="ModuleId">The module. Ties in the shutdown order prefer the larger identifier.</param>
/// <param name="PartTypeId">The part.</param>
/// <param name="Kind">One of <see cref="ModuleKinds"/>.</param>
/// <param name="Integrity">The integrity left.</param>
/// <param name="IntegrityMax">The integrity when the module is whole.</param>
/// <param name="PowerPriority">The shutdown order. A larger number is turned off sooner. Zero is a real order.</param>
/// <param name="Mass">The mass. A destroyed module is picked for damage with this weight.</param>
/// <param name="On">Whether the module draws power and does its work.</param>
/// <param name="CooldownSeconds">The time left before the weapon may fire. Zero fires on the first tick.</param>
/// <param name="Facts">The numbers of the module.</param>
/// <param name="Accuracy">The rows of accuracy. Missing rows use the default of the settings.</param>
public sealed record ModuleState(
    long ModuleId,
    int PartTypeId,
    string Kind,
    double Integrity,
    double IntegrityMax,
    int PowerPriority,
    double Mass,
    bool On,
    double CooldownSeconds,
    ModuleFacts Facts,
    IReadOnlyList<AccuracyEntry> Accuracy);

/// <summary>How many drones of one part a ship may launch, and what those drones are.</summary>
public sealed record DronePattern(
    int PartTypeId,
    int Count,
    double Hull,
    double Speed,
    double Signature,
    double EwMalus,
    double Repair,
    bool RepairDrone,
    double LeashRadius,
    int HullGroupId,
    ModuleFacts Weapon,
    IReadOnlyList<AccuracyEntry> Accuracy);

/// <summary>One ship.</summary>
public sealed record ShipState(
    long ShipId,
    int SideId,
    int SquadronId,
    int HullGroupId,
    Vec3 Position,
    Vec3 Velocity,
    double Heading,
    double Hull,
    double HullMax,
    double Armor,
    double ArmorMax,
    double ShieldNow,
    double ShieldCapacity,
    double Signature,
    double SignatureCut,
    IReadOnlyList<ModuleState> Modules,
    IReadOnlyDictionary<int, int> Ammo,
    IReadOnlyDictionary<int, int> HoldDrones,
    bool Retreating,
    bool Destroyed,
    bool Left = false,
    Vec3 FormationOffset = default,
    double LoadFactor = 1,
    double RadarBonus = 0,
    double EnergyBlock = 0,
    double KineticBlock = 0,
    double ArmorShare = 1,
    double Cost = 0,
    bool IsCarrier = false,
    IReadOnlyList<DronePattern>? DronePatterns = null);

/// <summary>One drone, either flying or sitting on the armor of its carrier.</summary>
public sealed record DroneState(
    long DroneId,
    long CarrierShipId,
    int SideId,
    int PartTypeId,
    int HullGroupId,
    Vec3 Position,
    Vec3 Velocity,
    double Hull,
    double HullMax,
    double Speed,
    double Signature,
    double EwMalus,
    double EwRadius,
    double Repair,
    double LeashRadius,
    bool RepairDrone,
    bool OnArmor,
    bool Destroyed,
    double CooldownSeconds,
    ModuleFacts Weapon,
    IReadOnlyList<AccuracyEntry> Accuracy);

/// <summary>Debris left where a ship was destroyed. Picking it up is a later task.</summary>
/// <param name="DebrisId">The debris.</param>
/// <param name="Position">Where it lies.</param>
/// <param name="Quantity">How much was left, from the cost of the ship and the configured share.</param>
/// <param name="Collected">Whether it has been picked up.</param>
/// <param name="SourceShipId">The ship that left it.</param>
public sealed record DebrisState(
    long DebrisId,
    Vec3 Position,
    double Quantity,
    bool Collected,
    long SourceShipId);

/// <summary>The points of one squadron, taken on the track step. The start of the segment is not a point.</summary>
/// <param name="SquadronId">The squadron.</param>
/// <param name="Points">The centers, in order.</param>
public sealed record SquadronTrack(int SquadronId, IReadOnlyList<Vec3> Points);

/// <summary>One line of the log. Unused numbers are zero so two runs write the same text.</summary>
public sealed record BattleLogEntry(
    int Tick,
    string Type,
    int FromSquadronId,
    int ToSquadronId,
    long ShipId,
    long ModuleId,
    long DroneId,
    long DebrisId,
    int Shots,
    int Hits,
    double Shield,
    double Armor,
    double Hull);

/// <summary>How the segment ended.</summary>
/// <param name="Kind">One of <see cref="OutcomeKinds"/>.</param>
/// <param name="SideId">The side that remains, or zero when the kind is not a win.</param>
public sealed record BattleOutcome(string Kind, int SideId)
{
    /// <summary>Both sides remain and the limit is not reached.</summary>
    public static BattleOutcome Continue { get; } = new(OutcomeKinds.Continue, 0);

    /// <summary>Nobody remains, or the limit was reached with both sides present.</summary>
    public static BattleOutcome Draw { get; } = new(OutcomeKinds.Draw, 0);

    /// <summary>Only one side still has a ship on the field.</summary>
    /// <param name="sideId">The side.</param>
    /// <returns>The outcome.</returns>
    public static BattleOutcome Won(int sideId) => new(OutcomeKinds.SideWon, sideId);
}

/// <summary>How long the segment to run is. Tests may set a length other than the one in the settings.</summary>
/// <param name="SegmentSeconds">The length, in game seconds. It has to divide into ticks.</param>
public sealed record SegmentInput(int SegmentSeconds);

/// <summary>The state after the segment, the tracks, the log and the outcome.</summary>
public sealed record SegmentResult(
    BattleState NewState,
    IReadOnlyList<SquadronTrack> Tracks,
    IReadOnlyList<BattleLogEntry> Log,
    BattleOutcome Outcome);
