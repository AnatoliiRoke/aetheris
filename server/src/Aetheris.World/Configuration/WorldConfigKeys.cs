using Aetheris.Core.Configuration;

namespace Aetheris.World.Configuration;

/// <summary>
/// The configuration keys of the world and their starting values.
/// </summary>
/// <remarks>
/// Every number of the world generation lives in <c>config_parameter</c> (task 00004,
/// section 1). The values below are neutral starting values for a fresh database; the
/// simulator tunes them later. The host adds these definitions to its seed, so the bounds
/// are checked by the admin panel like every other parameter.
/// </remarks>
public static class WorldConfigKeys
{
    /// <summary>The seed of the galaxy; the seed of every sector is derived from it.</summary>
    public const string GalaxySeed = "world.galaxy.seed";

    /// <summary>The number of sectors created on the first start.</summary>
    public const string InitialSectorCount = "world.galaxy.initial_sector_count";

    /// <summary>The number of the first sectors that are marked for newcomers.</summary>
    public const string NewbieSectorCount = "world.galaxy.newbie_sector_count";

    /// <summary>The distance between the origins of neighbouring sectors.</summary>
    public const string SectorSpacing = "world.galaxy.sector_spacing";

    /// <summary>The width and the height of a sector.</summary>
    public const string SectorSize = "world.sector.size";

    /// <summary>The depth of a sector along z; zero keeps the map flat.</summary>
    public const string SectorDepth = "world.sector.depth";

    /// <summary>The smallest number of star systems in a sector.</summary>
    public const string SystemsMin = "world.sector.systems_min";

    /// <summary>The largest number of star systems in a sector.</summary>
    public const string SystemsMax = "world.sector.systems_max";

    /// <summary>The smallest distance between two star systems.</summary>
    public const string MinSystemDistance = "world.sector.min_system_distance";

    /// <summary>How many random positions are tried for one star system.</summary>
    public const string PlacementAttempts = "world.sector.placement_attempts";

    /// <summary>The number of nebulae in a sector.</summary>
    public const string NebulaCount = "world.sector.nebula_count";

    /// <summary>The smallest number of free fields created with a sector.</summary>
    public const string FreeFieldsMin = "world.sector.free_fields_min";

    /// <summary>The largest number of free fields created with a sector.</summary>
    public const string FreeFieldsMax = "world.sector.free_fields_max";

    /// <summary>The number of fields a sector never falls below for long.</summary>
    public const string MinFieldsPerSector = "world.sector.min_fields";

    /// <summary>The number of orbits of a star system.</summary>
    public const string OrbitCount = "world.system.orbit_count";

    /// <summary>The smallest number of bodies on the orbits of a system.</summary>
    public const string BodiesMin = "world.system.bodies_min";

    /// <summary>The largest number of bodies on the orbits of a system.</summary>
    public const string BodiesMax = "world.system.bodies_max";

    /// <summary>The probability that a system has gas giants at all.</summary>
    public const string GasGiantProbability = "world.system.gas_giant_probability";

    /// <summary>The largest number of gas giants in a system.</summary>
    public const string GasGiantMax = "world.system.gas_giant_max";

    /// <summary>The probability of a field on the outermost orbit of a new system.</summary>
    public const string SystemFieldProbability = "world.system.field_probability";

    /// <summary>The smallest radius of a nebula.</summary>
    public const string NebulaRadiusMin = "world.nebula.radius_min";

    /// <summary>The largest radius of a nebula.</summary>
    public const string NebulaRadiusMax = "world.nebula.radius_max";

    /// <summary>The multiplier of the radar range inside a nebula.</summary>
    public const string NebulaRadarMultiplier = "world.nebula.radar_multiplier";

    /// <summary>Whether a nebula forbids the jump drive.</summary>
    public const string NebulaBlocksJumpDrive = "world.nebula.blocks_jump_drive";

    /// <summary>The largest share of a field composition; the rest is always empty rock.</summary>
    public const string FieldMaxCompositionShare = "world.field.max_composition_share";

    /// <summary>The delay before a depleted field is replaced, in game seconds.</summary>
    public const string FieldRespawnDelaySeconds = "world.field.respawn_delay_seconds";

    /// <summary>The probability that a replacement field lies on the outermost orbit of a system.</summary>
    public const string FieldRespawnInSystemProbability = "world.field.respawn_in_system_probability";

    /// <summary>The largest number of stations on one gas giant.</summary>
    public const string GasGiantStationLimit = "world.gas_giant.station_limit";

    /// <summary>The fixed travel time inside a star system, in game seconds.</summary>
    public const string InSystemTravelSeconds = "world.travel.in_system_seconds";

    /// <summary>The code of the body type a newcomer starts on.</summary>
    public const string StartPlanetTypeCode = "world.start.planet_type_code";

    /// <summary>The largest number of newcomers who start in one star system.</summary>
    public const string MaxNewbiesPerSystem = "world.start.max_newbies_per_system";

    /// <summary>The development switch that shows the whole map to everyone.</summary>
    public const string RevealAll = "world.visibility.reveal_all";

    /// <summary>The largest number of free bodies returned at once.</summary>
    public const string FreeBodiesPageSize = "world.query.free_page_size";

    /// <summary>The name template of a sector.</summary>
    public const string SectorNameTemplate = "world.naming.sector_template";

    /// <summary>The name template of a star system.</summary>
    public const string SystemNameTemplate = "world.naming.system_template";

    /// <summary>The smallest length of the name a player gives the home body.</summary>
    public const string HomeNameMinLength = "world.civilization.home_name_min_length";

    /// <summary>The largest length of the name a player gives the home body.</summary>
    public const string HomeNameMaxLength = "world.civilization.home_name_max_length";

    /// <summary>The characters the name of the home body may contain, as an <see cref="Core.Text.AllowedCharacters"/> definition.</summary>
    public const string HomeNameCharacters = "world.civilization.home_name_characters";

    /// <summary>The capacity of the storage of a new home body, per resource.</summary>
    public const string StartStorageCapacity = "world.civilization.start_storage_capacity";

    /// <summary>The name template of a body on an orbit.</summary>
    public const string BodyNameTemplate = "world.naming.body_template";

    /// <summary>The name template of an asteroid field.</summary>
    public const string FieldNameTemplate = "world.naming.field_template";

    /// <summary>The name template of a nebula.</summary>
    public const string NebulaNameTemplate = "world.naming.nebula_template";

    /// <summary>
    /// Gets the definitions of the world parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(GalaxySeed, "20261025", "0", "9007199254740991", "Seed of the galaxy; the sector seeds are derived from it."),
        new(InitialSectorCount, "3", "1", "1000", "Sectors created on the first start."),
        new(NewbieSectorCount, "1", "0", "1000", "First sectors that are marked for newcomers."),
        new(SectorSpacing, "1500", "1", "1000000", "Distance between the origins of neighbouring sectors."),
        new(SectorSize, "1000", "1", "1000000", "Width and height of a sector."),
        new(SectorDepth, "0", "0", "1000000", "Depth of a sector along z; zero keeps the map flat."),
        new(SystemsMin, "50", "1", "10000", "Smallest number of star systems in a sector."),
        new(SystemsMax, "150", "1", "10000", "Largest number of star systems in a sector."),
        new(MinSystemDistance, "20", "0", "1000000", "Smallest distance between two star systems."),
        new(PlacementAttempts, "50", "1", "10000", "Random positions tried for one star system."),
        new(NebulaCount, "1", "0", "100", "Nebulae in a sector."),
        new(FreeFieldsMin, "5", "0", "10000", "Smallest number of free fields created with a sector."),
        new(FreeFieldsMax, "10", "0", "10000", "Largest number of free fields created with a sector."),
        new(MinFieldsPerSector, "20", "0", "10000", "Fields a sector never falls below for long."),
        new(OrbitCount, "12", "2", "100", "Orbits of a star system; the outermost one is kept for a field."),
        new(BodiesMin, "3", "0", "100", "Smallest number of bodies on the orbits of a system."),
        new(BodiesMax, "9", "0", "100", "Largest number of bodies on the orbits of a system."),
        new(GasGiantProbability, "0.4", "0", "1", "Probability that a system has gas giants."),
        new(GasGiantMax, "2", "0", "100", "Largest number of gas giants in a system."),
        new(SystemFieldProbability, "0.15", "0", "1", "Probability of a field on the outermost orbit of a new system."),
        new(NebulaRadiusMin, "40", "0", "1000000", "Smallest radius of a nebula."),
        new(NebulaRadiusMax, "120", "0", "1000000", "Largest radius of a nebula."),
        new(NebulaRadarMultiplier, "0.5", "0", "10", "Multiplier of the radar range inside a nebula."),
        new(NebulaBlocksJumpDrive, "true", null, null, "Whether a nebula forbids the jump drive."),
        new(FieldMaxCompositionShare, "0.95", "0", "1", "Largest share of a field composition; the rest is empty rock."),
        new(FieldRespawnDelaySeconds, "3600", "0", "31536000", "Delay before a depleted field is replaced, in game seconds."),
        new(FieldRespawnInSystemProbability, "0.3", "0", "1", "Probability that a replacement field lies on the outermost orbit of a system."),
        new(GasGiantStationLimit, "5", "0", "1000", "Largest number of stations on one gas giant."),
        new(InSystemTravelSeconds, "600", "0", "86400", "Fixed travel time inside a star system, in game seconds."),
        new(StartPlanetTypeCode, "terrestrial", null, null, "Code of the body type a newcomer starts on."),
        new(MaxNewbiesPerSystem, "3", "1", "100", "Largest number of newcomers who start in one star system."),
        new(RevealAll, "false", null, null, "Development switch: every player sees the whole map."),
        new(FreeBodiesPageSize, "100", "1", "10000", "Largest number of free bodies returned at once."),
        new(HomeNameMinLength, "3", "1", "64", "Smallest length of the name a player gives the home body."),
        new(HomeNameMaxLength, "20", "1", "64", "Largest length of the name a player gives the home body."),
        new(HomeNameCharacters, "A-Za-zЀ-ӿ0-9 _'’-", null, null, "Characters the name of the home body may contain: listed as they are, a-z is a range, a dash at the end is the dash itself."),
        new(StartStorageCapacity, "10000", "0", "1000000000", "Capacity of the storage of a new home body, per resource; the start resources never exceed it."),
        new(SectorNameTemplate, "S{index}", null, null, "Name template of a sector: {index}."),
        new(SystemNameTemplate, "{sector}-{number}", null, null, "Name template of a star system: {sector}, {number}."),
        new(BodyNameTemplate, "{system}-{orbit}", null, null, "Name template of a body: {system}, {orbit}."),
        new(FieldNameTemplate, "{sector}-F{number}", null, null, "Name template of an asteroid field: {sector}, {number}."),
        new(NebulaNameTemplate, "{sector}-N{number}", null, null, "Name template of a nebula: {sector}, {number}."),
    ];
}
