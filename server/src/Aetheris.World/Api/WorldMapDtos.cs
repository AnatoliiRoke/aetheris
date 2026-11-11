namespace Aetheris.World.Api;

/// <summary>
/// The states of an object or a star on the map: what the player knows about it (task 00034).
/// </summary>
public static class MapStates
{
    /// <summary>The player sees the object with its details (it is open by a radar or is the player's own).</summary>
    public const string Open = "open";

    /// <summary>The player knows only that it is there: the place without a name and details.</summary>
    public const string Known = "known";

    /// <summary>The player knows nothing of the place (a point in empty or unexplored space).</summary>
    public const string Unknown = "unknown";
}

/// <summary>The kinds of the target of a flight a player points at on the map (task 00034).</summary>
public static class MapTargetKinds
{
    /// <summary>A star system.</summary>
    public const string System = "system";

    /// <summary>A celestial object: a body, a field.</summary>
    public const string Object = "object";

    /// <summary>A free point of space.</summary>
    public const string Point = "point";
}

/// <summary>The codes of what forbids a target on the map (task 00034).</summary>
public static class MapTargetErrors
{
    /// <summary>The point lies outside the sector the player pointed in.</summary>
    public const string OutOfSector = "map_target_out_of_sector";

    /// <summary>The target is the place the fleet starts from.</summary>
    public const string SamePlace = "map_target_same_place";
}

/// <summary>A sector on the map of the galaxy.</summary>
/// <param name="Id">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x coordinate of the origin.</param>
/// <param name="Y">The y coordinate of the origin.</param>
/// <param name="Z">The z coordinate of the origin.</param>
/// <param name="IsNewbie">Whether newcomers start there.</param>
/// <param name="SystemCount">How many star systems the sector has.</param>
/// <param name="OwnPlanetCount">How many bodies of the player lie in the sector.</param>
/// <param name="State">One of <see cref="MapStates"/>: what the player knows of the sector.</param>
public sealed record MapSectorDto(int Id, string Name, double X, double Y, double Z, bool IsNewbie, int SystemCount, int OwnPlanetCount, string State);

/// <summary>The map of the galaxy: the sectors with what the player has in them.</summary>
/// <param name="Sectors">The sectors.</param>
/// <param name="SectorSize">The width and the height of a sector, so the client can draw it.</param>
/// <param name="SeesOnlyOwn">Whether the player sees nothing but their own bodies (no radar has opened anything).</param>
public sealed record MapGalaxyDto(IReadOnlyList<MapSectorDto> Sectors, double SectorSize, bool SeesOnlyOwn);

/// <summary>A star system on the map of a sector.</summary>
/// <param name="Id">The system.</param>
/// <param name="Number">The number within the sector.</param>
/// <param name="Name">The name; <see langword="null"/> for a star the player only knows of.</param>
/// <param name="X">The x coordinate within the sector.</param>
/// <param name="Y">The y coordinate within the sector.</param>
/// <param name="Z">The z coordinate within the sector.</param>
/// <param name="InNebula">Whether the system lies inside a nebula.</param>
/// <param name="State">One of <see cref="MapStates"/>.</param>
/// <param name="ObjectCount">How many objects of the system the player sees.</param>
/// <param name="OwnPlanetCount">How many bodies of the player lie in the system.</param>
/// <param name="Distance">The distance from the body the player flies from, or <see langword="null"/>.</param>
/// <param name="TravelSeconds">The travel time from that body, in game seconds, or <see langword="null"/>.</param>
public sealed record MapSystemDto(
    long Id,
    int Number,
    string? Name,
    double X,
    double Y,
    double Z,
    bool InNebula,
    string State,
    int ObjectCount,
    int OwnPlanetCount,
    double? Distance,
    long? TravelSeconds);

/// <summary>A nebula on the map of a sector.</summary>
/// <param name="Id">The nebula.</param>
/// <param name="ObjectId">Its celestial object.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x coordinate of the centre.</param>
/// <param name="Y">The y coordinate of the centre.</param>
/// <param name="Z">The z coordinate of the centre.</param>
/// <param name="Radius">The radius.</param>
/// <param name="BlocksJumpDrive">Whether a jump drive does not work inside.</param>
/// <param name="State">One of <see cref="MapStates"/>: <c>open</c> when a radar of the player reaches the nebula, so the player may name it as a target of a flight.</param>
public sealed record MapNebulaDto(long Id, long ObjectId, string Name, double X, double Y, double Z, double Radius, bool BlocksJumpDrive, string State);

/// <summary>A free asteroid field the player sees in the space of a sector.</summary>
/// <param name="ObjectId">The celestial object.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x coordinate within the sector.</param>
/// <param name="Y">The y coordinate within the sector.</param>
/// <param name="Z">The z coordinate within the sector.</param>
/// <param name="Mass">The mass left in the field.</param>
/// <param name="Distance">The distance from the body the player flies from, or <see langword="null"/>.</param>
/// <param name="TravelSeconds">The travel time from that body, or <see langword="null"/>.</param>
public sealed record MapFieldDto(long ObjectId, int ObjectTypeId, string Name, double X, double Y, double Z, long Mass, double? Distance, long? TravelSeconds);

/// <summary>The range of the radar of a body of the player; none exists until the radar is built.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="X">The x coordinate within the sector.</param>
/// <param name="Y">The y coordinate within the sector.</param>
/// <param name="Z">The z coordinate within the sector.</param>
/// <param name="Radius">The radius of the range.</param>
public sealed record MapRadarDto(long PlanetId, double X, double Y, double Z, double Radius);

/// <summary>The map of a sector.</summary>
/// <param name="SectorId">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="SectorSize">The width and the height of the sector.</param>
/// <param name="Systems">Every star system of the sector, open or known.</param>
/// <param name="Nebulas">The nebulas.</param>
/// <param name="Fields">The free fields the player sees.</param>
/// <param name="Radars">The ranges of the radars of the player in the sector.</param>
/// <param name="SeesOnlyOwn">Whether the player sees nothing but their own bodies.</param>
public sealed record MapSectorViewDto(
    int SectorId,
    string Name,
    double SectorSize,
    IReadOnlyList<MapSystemDto> Systems,
    IReadOnlyList<MapNebulaDto> Nebulas,
    IReadOnlyList<MapFieldDto> Fields,
    IReadOnlyList<MapRadarDto> Radars,
    bool SeesOnlyOwn);

/// <summary>The owner of a body as the map shows them.</summary>
/// <param name="Nickname">The nickname.</param>
/// <param name="RaceCode">The neutral code of the race.</param>
/// <param name="RaceNameKey">The localisation key of the name of the race.</param>
/// <param name="RaceColorKey">The colour key of the race, or <see langword="null"/>.</param>
public sealed record MapOwnerDto(string Nickname, string RaceCode, string RaceNameKey, string? RaceColorKey);

/// <summary>A resource share of a field or of a deposit of a body on the card of an object.</summary>
/// <param name="ResourceId">The resource.</param>
/// <param name="Coefficient">The share or the coefficient.</param>
public sealed record MapResourceShareDto(int ResourceId, decimal Coefficient);

/// <summary>An object of a star system on the map.</summary>
/// <param name="Id">The celestial object.</param>
/// <param name="State">One of <see cref="MapStates"/>.</param>
/// <param name="Orbit">The orbit, if any.</param>
/// <param name="ObjectClass">The class of the type, or <c>unknown</c> for an object the player only knows of.</param>
/// <param name="ObjectTypeId">The type, or <see langword="null"/> for an object the player only knows of.</param>
/// <param name="Name">The name, or <see langword="null"/> for an object the player only knows of.</param>
/// <param name="PlanetId">The body row of a planet or a gas giant.</param>
/// <param name="SizeClassId">The size class of a body.</param>
/// <param name="Size">The size of a body.</param>
/// <param name="IsOwn">Whether the body is the player's.</param>
/// <param name="IsFree">Whether the body is free and may be colonized.</param>
/// <param name="IsTerraformed">Whether the body is terraformed.</param>
/// <param name="Owner">The owner of the body, if it has one.</param>
/// <param name="StationLimit">The largest number of stations on a gas giant.</param>
/// <param name="Mass">The mass left in a field.</param>
/// <param name="Resources">The deposits of a body or the composition of a field.</param>
/// <param name="Distance">The distance from the body the player flies from, or <see langword="null"/>.</param>
/// <param name="TravelSeconds">The travel time from that body, or <see langword="null"/>.</param>
public sealed record MapObjectDto(
    long Id,
    string State,
    int? Orbit,
    string ObjectClass,
    int? ObjectTypeId,
    string? Name,
    long? PlanetId,
    int? SizeClassId,
    int? Size,
    bool IsOwn,
    bool IsFree,
    bool IsTerraformed,
    MapOwnerDto? Owner,
    int? StationLimit,
    long? Mass,
    IReadOnlyList<MapResourceShareDto> Resources,
    double? Distance,
    long? TravelSeconds);

/// <summary>The map of a star system.</summary>
/// <param name="System">The system.</param>
/// <param name="SectorName">The name of the sector.</param>
/// <param name="OrbitCount">How many orbits the system has.</param>
/// <param name="Objects">The objects of the system: the open ones with details, the others as places.</param>
public sealed record MapSystemViewDto(MapSystemDto System, string SectorName, int OrbitCount, IReadOnlyList<MapObjectDto> Objects);

/// <summary>A result of the search on the map.</summary>
/// <param name="Kind">What was found: <c>system</c>, <c>object</c> or <c>player</c>.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SystemId">The system, if any.</param>
/// <param name="ObjectId">The object, if any.</param>
/// <param name="ObjectTypeId">The type of an object.</param>
/// <param name="Name">What was found: the name of the system, the object or the nickname of the player.</param>
/// <param name="SystemName">The name of the system the result lies in.</param>
/// <param name="Orbit">The orbit of an object.</param>
public sealed record MapSearchResultDto(string Kind, int SectorId, long? SystemId, long? ObjectId, int? ObjectTypeId, string Name, string? SystemName, int? Orbit);

/// <summary>The target of a flight the player pointed at on the map (task 00034).</summary>
/// <param name="Kind">One of <see cref="MapTargetKinds"/>.</param>
/// <param name="TargetId">The system or the object the point snapped to, or <see langword="null"/> for a free point.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="X">The x coordinate within the sector.</param>
/// <param name="Y">The y coordinate within the sector.</param>
/// <param name="Z">The z coordinate within the sector.</param>
/// <param name="Name">The name of the system or the object the point snapped to.</param>
/// <param name="NearSystemId">The star system nearest to a free point.</param>
/// <param name="NearSystemName">The name of that system, or <see langword="null"/> when the player does not know it.</param>
/// <param name="InNebula">Whether the point lies inside a nebula.</param>
/// <param name="JumpForbidden">Whether a jump drive does not work at the point.</param>
/// <param name="State">One of <see cref="MapStates"/>.</param>
/// <param name="Distance">The distance from the body the player flies from, or <see langword="null"/>.</param>
/// <param name="TravelSeconds">The travel time from that body, or <see langword="null"/>.</param>
/// <param name="Errors">The codes of what forbids the target, one of <see cref="MapTargetErrors"/>.</param>
public sealed record MapTargetDto(
    string Kind,
    long? TargetId,
    int SectorId,
    double X,
    double Y,
    double Z,
    string? Name,
    long? NearSystemId,
    string? NearSystemName,
    bool InNebula,
    bool JumpForbidden,
    string State,
    double? Distance,
    long? TravelSeconds,
    IReadOnlyList<string> Errors);
