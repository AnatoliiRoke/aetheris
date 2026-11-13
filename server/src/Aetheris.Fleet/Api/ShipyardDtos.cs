namespace Aetheris.Fleet.Api;

/// <summary>
/// A shipyard of a body with its queue, the projects the player may assemble and the ships
/// that stand on the body (task 00039).
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="PlanetBuildingId">The shipyard, or <see langword="null"/> when the body has none.</param>
/// <param name="Level">The level of the shipyard.</param>
/// <param name="Used">How many records the queue holds.</param>
/// <param name="Total">How many records the queue may hold.</param>
/// <param name="GameNow">The game time of the answer.</param>
/// <param name="TimeMultiplier">How many game seconds pass in a real second.</param>
/// <param name="Orders">The queue, the one under way first.</param>
/// <param name="Designs">The projects of the player with the reason each one can or cannot be assembled.</param>
/// <param name="Ships">The finished ships that stand on the body.</param>
public sealed record ShipyardViewDto(
    long PlanetId,
    long? PlanetBuildingId,
    int Level,
    int Used,
    int Total,
    DateTimeOffset GameNow,
    double TimeMultiplier,
    IReadOnlyList<ShipyardOrderDto> Orders,
    IReadOnlyList<ShipyardDesignDto> Designs,
    IReadOnlyList<ShipDto> Ships);

/// <summary>
/// A record of a shipyard queue.
/// </summary>
/// <param name="Id">The record.</param>
/// <param name="DesignId">The project.</param>
/// <param name="Title">The name of the project.</param>
/// <param name="Position">The position in the queue, from one.</param>
/// <param name="UnderWay">Whether the assembly has started.</param>
/// <param name="FinishesAt">The moment the assembly finishes, or <see langword="null"/> while it waits.</param>
/// <param name="DurationSeconds">The duration the player waits, in game seconds. One ship while the record is under way, the ships still left while it waits.</param>
/// <param name="Costs">The resources the record paid for, for the whole batch.</param>
/// <param name="Quantity">How many ships the record assembles.</param>
/// <param name="ProducedQuantity">How many ships of the record are already finished.</param>
public sealed record ShipyardOrderDto(
    long Id,
    long DesignId,
    string Title,
    int Position,
    bool UnderWay,
    DateTimeOffset? FinishesAt,
    long DurationSeconds,
    IReadOnlyList<ShipyardCostDto> Costs,
    long Quantity,
    long ProducedQuantity);

/// <summary>
/// A project as the shipyard shows it: the cost, the time and why it can or cannot be queued.
/// </summary>
/// <param name="DesignId">The project.</param>
/// <param name="Name">The name of the project.</param>
/// <param name="HullPartTypeId">The hull.</param>
/// <param name="HullNameKey">The localisation key of the name of the hull.</param>
/// <param name="CanEnqueue">Whether the project can be queued now.</param>
/// <param name="ReasonCode">Why it cannot, or <see langword="null"/> when it can.</param>
/// <param name="RequirementNameKey">The localisation key of the name of an unmet requirement.</param>
/// <param name="RequirementLevel">The level an unmet requirement asks for.</param>
/// <param name="Crew">The crew the assembly takes.</param>
/// <param name="Credits">The credits the assembly takes.</param>
/// <param name="DurationSeconds">The duration the assembly would take if it started now.</param>
/// <param name="Costs">The resources the assembly takes, including the crew and the credits.</param>
/// <param name="Parts">The parts the assembly takes and how many the body has.</param>
/// <param name="UpkeepPerHour">The credits one finished ship of the project costs per hour. It is the configured share of the credit price of the fitted modules.</param>
/// <param name="Warnings">The problems of the project that do not stop the assembly (task 00072): the same warnings the constructor shows.</param>
public sealed record ShipyardDesignDto(
    long DesignId,
    string Name,
    int HullPartTypeId,
    string HullNameKey,
    bool CanEnqueue,
    string? ReasonCode,
    string? RequirementNameKey,
    int? RequirementLevel,
    long Crew,
    long Credits,
    long DurationSeconds,
    IReadOnlyList<ShipyardCostDto> Costs,
    IReadOnlyList<ShipyardPartDto> Parts,
    decimal UpkeepPerHour = 0m,
    IReadOnlyList<ShipDesignWarningDto>? Warnings = null);

/// <summary>
/// A resource of an assembly.
/// </summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Amount">The amount the assembly takes.</param>
/// <param name="NameKey">The localisation key of the name of the resource.</param>
/// <param name="Available">
/// How much the player has: inhabitants of the body, the currency of the civilization, or the stock
/// of the body. <see langword="null"/> on a paid queue record, which already took the amount.
/// </param>
public sealed record ShipyardCostDto(int ResourceTypeId, long Amount, string? NameKey, long? Available = null);

/// <summary>
/// A stack of parts an assembly takes, or a stack on a finished ship.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="NameKey">The localisation key of the name of the part.</param>
/// <param name="MarkLevel">The mark of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="Quantity">How many parts the assembly takes, or the ship carries.</param>
/// <param name="Available">How many such parts the body has. Zero on a finished ship.</param>
/// <param name="IsHull">Whether the stack is the hull.</param>
public sealed record ShipyardPartDto(
    int PartTypeId,
    string NameKey,
    int MarkLevel,
    bool HasRaceBonus,
    long Quantity,
    long Available,
    bool IsHull);

/// <summary>
/// A finished ship.
/// </summary>
/// <param name="Id">The ship.</param>
/// <param name="PlanetId">The body the ship stands on.</param>
/// <param name="DesignId">The project the ship was built from.</param>
/// <param name="Name">The name of the project.</param>
/// <param name="HullNameKey">The localisation key of the name of the hull.</param>
/// <param name="ClassNameKey">The localisation key of the class of the hull: the group of the hull.</param>
/// <param name="StateCode">The state of the ship.</param>
/// <param name="Crew">The crew assigned to the ship.</param>
/// <param name="UpkeepPerHour">The credits the ship costs per hour. Zero until the modelling fills the characteristic.</param>
/// <param name="Modules">The parts of the ship.</param>
/// <param name="FleetId">The fleet the ship is in, or <see langword="null"/> when the ship is free (task 00040).</param>
public sealed record ShipDto(
    long Id,
    long PlanetId,
    long DesignId,
    string Name,
    string HullNameKey,
    string ClassNameKey,
    string StateCode,
    long Crew,
    decimal UpkeepPerHour,
    IReadOnlyList<ShipyardPartDto> Modules,
    long? FleetId = null);
