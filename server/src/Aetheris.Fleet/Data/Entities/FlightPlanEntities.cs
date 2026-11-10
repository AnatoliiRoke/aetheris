namespace Aetheris.Fleet.Data.Entities;

/// <summary>
/// A flight plan of a fleet: the ordered list of the orders the fleet carries out one after
/// another (task 00050).
/// </summary>
/// <remarks>
/// <para>
/// The plan belongs to the player and to the fleet at once: the fleet it is made for is written
/// in <see cref="FleetId"/> and the owner in <see cref="PlayerId"/>, so a plan of another player
/// is never read and never changed. The fleet has one plan at most, so the table keeps one plan
/// per fleet and the database refuses a second one.
/// </para>
/// <para>
/// The plan is written once and read until it is carried out. A carried out plan is deleted, so
/// the table holds the plans that are still waiting or that wait for the player. The orders
/// themselves live in the table of the orders of the plan.
/// </para>
/// </remarks>
public sealed class FlightPlanEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owner of the plan.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the fleet the plan is made for.</summary>
    public long FleetId { get; set; }

    /// <summary>Gets or sets the state of the plan, one of <see cref="FlightPlanStates"/>.</summary>
    public string StateCode { get; set; } = FlightPlanStates.Draft;

    /// <summary>Gets or sets the moment of game time the plan was created at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the moment of game time the plan was changed at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the orders of the plan, in the order they are carried out in.</summary>
    public List<FlightPlanOrderEntity> OrderIds { get; set; } = [];
}

/// <summary>
/// An order of a flight plan: what the fleet does at one step of its route (task 00050).
/// </summary>
/// <remarks>
/// <para>
/// The type of the order is a logical code, one of <see cref="FlightOrderTypes"/>, and the
/// target of the order is written by its kind: a system, an object of a system, a point of a
/// sector or a body. The coordinates are three axes, as the space of the game is
/// (documentation/tech/08, rule 4).
/// </para>
/// <para>
/// The parameters of the order, like the list of the resources of a load, are kept as a
/// document: the orders of the next tasks add their own parameters, and the table of the orders
/// does not grow a column for every kind.
/// </para>
/// </remarks>
public sealed class FlightPlanOrderEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the plan the order belongs to.</summary>
    public long FlightPlanId { get; set; }

    /// <summary>Gets or sets the position of the order in the plan, from one.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the type of the order, one of <see cref="FlightOrderTypes"/>.</summary>
    public string TypeCode { get; set; } = FlightOrderTypes.Fly;

    /// <summary>Gets or sets the kind of the target, one of <see cref="FlightTargetKinds"/>.</summary>
    public string TargetKind { get; set; } = FlightTargetKinds.None;

    /// <summary>Gets or sets the sector of the target.</summary>
    public int TargetSectorId { get; set; }

    /// <summary>Gets or sets the system of the target.</summary>
    public long TargetSystemId { get; set; }

    /// <summary>Gets or sets the object of the target.</summary>
    public long TargetObjectId { get; set; }

    /// <summary>Gets or sets the first axis of the target.</summary>
    public decimal TargetX { get; set; }

    /// <summary>Gets or sets the second axis of the target.</summary>
    public decimal TargetY { get; set; }

    /// <summary>Gets or sets the third axis of the target.</summary>
    public decimal TargetZ { get; set; }

    /// <summary>Gets or sets how long the order lasts, in game seconds. Zero when it has no time.</summary>
    public long DurationSeconds { get; set; }

    /// <summary>Gets or sets the parameters of the order as a document.</summary>
    public string Parameters { get; set; } = "{}";
}

/// <summary>
/// The states of a flight plan. They are logical codes, not numbers of the balance.
/// </summary>
public static class FlightPlanStates
{
    /// <summary>The plan is written and waits for the player to carry it out.</summary>
    public const string Draft = "draft";

    /// <summary>The plan is carried out.</summary>
    public const string Active = "active";

    /// <summary>
    /// The plan is stopped: the fleet waits where it is, and the player may carry the plan out
    /// again or make a new one.
    /// </summary>
    public const string Paused = "paused";
}

/// <summary>
/// The types of an order of a flight plan. They are logical codes, not numbers of the balance.
/// </summary>
public static class FlightOrderTypes
{
    /// <summary>Every type of an order, so a request that names another one is refused.</summary>
    public static readonly IReadOnlyList<string> All = [Fly, Hyperfly, Jump, Mine, Scan, Load, Unload, Wait, Return];

    /// <summary>The fleet flies to a target.</summary>
    public const string Fly = "fly";

    /// <summary>The fleet flies to a target faster than a plain flight.</summary>
    public const string Hyperfly = "hyperfly";

    /// <summary>The fleet jumps to a target without the flight of the space between.</summary>
    public const string Jump = "jump";

    /// <summary>The fleet takes resources from a target.</summary>
    public const string Mine = "mine";

    /// <summary>The fleet looks at a target.</summary>
    public const string Scan = "scan";

    /// <summary>The fleet takes resources and parts into its hold.</summary>
    public const string Load = "load";

    /// <summary>The fleet puts resources and parts out of its hold.</summary>
    public const string Unload = "unload";

    /// <summary>The fleet waits for a time.</summary>
    public const string Wait = "wait";

    /// <summary>The fleet flies back to its home body.</summary>
    public const string Return = "return";
}

/// <summary>
/// The kinds of a target of an order of a flight plan. They are logical codes, not numbers of the
/// balance.
/// </summary>
public static class FlightTargetKinds
{
    /// <summary>Every kind of a target, so a request that names another one is refused.</summary>
    public static readonly IReadOnlyList<string> All = [None, System, Object, Point, Planet];

    /// <summary>The order has no target.</summary>
    public const string None = "none";

    /// <summary>The target is a system.</summary>
    public const string System = "system";

    /// <summary>The target is an object of a system.</summary>
    public const string Object = "object";

    /// <summary>The target is a point of a sector.</summary>
    public const string Point = "point";

    /// <summary>The target is a body of the player.</summary>
    public const string Planet = "planet";
}
