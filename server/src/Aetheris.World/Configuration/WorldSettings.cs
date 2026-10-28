using System.Globalization;
using Aetheris.Core.Configuration;
using Aetheris.Core.Text;

namespace Aetheris.World.Configuration;

/// <summary>
/// The numeric parameters of the world, read at once from the configuration.
/// </summary>
/// <remarks>
/// A missing or unreadable value falls back to the starting value of its definition in
/// <see cref="WorldConfigKeys.Definitions"/>, so the code never carries its own number.
/// </remarks>
public sealed record WorldSettings
{
    /// <summary>Gets the seed of the galaxy.</summary>
    public long GalaxySeed { get; init; }

    /// <summary>Gets the number of sectors created on the first start.</summary>
    public int InitialSectorCount { get; init; }

    /// <summary>Gets the number of the first sectors marked for newcomers.</summary>
    public int NewbieSectorCount { get; init; }

    /// <summary>Gets the distance between the origins of neighbouring sectors.</summary>
    public double SectorSpacing { get; init; }

    /// <summary>Gets the width and the height of a sector.</summary>
    public double SectorSize { get; init; }

    /// <summary>Gets the depth of a sector along z.</summary>
    public double SectorDepth { get; init; }

    /// <summary>Gets the smallest number of star systems in a sector.</summary>
    public int SystemsMin { get; init; }

    /// <summary>Gets the largest number of star systems in a sector.</summary>
    public int SystemsMax { get; init; }

    /// <summary>Gets the smallest distance between two star systems.</summary>
    public double MinSystemDistance { get; init; }

    /// <summary>Gets how many random positions are tried for one star system.</summary>
    public int PlacementAttempts { get; init; }

    /// <summary>Gets the number of nebulae in a sector.</summary>
    public int NebulaCount { get; init; }

    /// <summary>Gets the smallest number of free fields of a new sector.</summary>
    public int FreeFieldsMin { get; init; }

    /// <summary>Gets the largest number of free fields of a new sector.</summary>
    public int FreeFieldsMax { get; init; }

    /// <summary>Gets the number of fields a sector never falls below for long.</summary>
    public int MinFieldsPerSector { get; init; }

    /// <summary>Gets the number of orbits of a star system.</summary>
    public int OrbitCount { get; init; }

    /// <summary>Gets the smallest number of bodies of a system.</summary>
    public int BodiesMin { get; init; }

    /// <summary>Gets the largest number of bodies of a system.</summary>
    public int BodiesMax { get; init; }

    /// <summary>Gets the probability that a system has gas giants.</summary>
    public double GasGiantProbability { get; init; }

    /// <summary>Gets the largest number of gas giants in a system.</summary>
    public int GasGiantMax { get; init; }

    /// <summary>Gets the probability of a field on the outermost orbit of a new system.</summary>
    public double SystemFieldProbability { get; init; }

    /// <summary>Gets the smallest radius of a nebula.</summary>
    public double NebulaRadiusMin { get; init; }

    /// <summary>Gets the largest radius of a nebula.</summary>
    public double NebulaRadiusMax { get; init; }

    /// <summary>Gets the multiplier of the radar range inside a nebula.</summary>
    public double NebulaRadarMultiplier { get; init; }

    /// <summary>Gets a value indicating whether a nebula forbids the jump drive.</summary>
    public bool NebulaBlocksJumpDrive { get; init; }

    /// <summary>Gets the largest share of a field composition.</summary>
    public decimal FieldMaxCompositionShare { get; init; }

    /// <summary>Gets the probability that a new planet is rich.</summary>
    public double RichPlanetProbability { get; init; }

    /// <summary>Gets the smallest coefficient of a deposit of a body.</summary>
    public decimal DepositCoefficientMin { get; init; }

    /// <summary>Gets the largest coefficient of a deposit of a body.</summary>
    public decimal DepositCoefficientMax { get; init; }

    /// <summary>Gets the delay before a depleted field is replaced.</summary>
    public TimeSpan FieldRespawnDelay { get; init; }

    /// <summary>Gets the probability that a replacement field lies on an outer orbit.</summary>
    public double FieldRespawnInSystemProbability { get; init; }

    /// <summary>Gets the largest number of stations on one gas giant.</summary>
    public int GasGiantStationLimit { get; init; }

    /// <summary>Gets the fixed travel time inside a star system.</summary>
    public TimeSpan InSystemTravelTime { get; init; }

    /// <summary>Gets the largest number of newcomers in one star system.</summary>
    public int MaxNewbiesPerSystem { get; init; }

    /// <summary>Gets the smallest length of the name of a home body.</summary>
    public int HomeNameMinLength { get; init; }

    /// <summary>Gets the largest length of the name of a home body.</summary>
    public int HomeNameMaxLength { get; init; }

    /// <summary>Gets the characters the name of a home body may contain.</summary>
    public AllowedCharacters HomeNameCharacters { get; init; } = AllowedCharacters.Parse(null);

    /// <summary>Gets the capacity of the storage of a new home body, per resource.</summary>
    public long StartStorageCapacity { get; init; }

    /// <summary>Gets a value indicating whether every player sees the whole map.</summary>
    public bool RevealAll { get; init; }

    /// <summary>Gets the largest number of free bodies returned at once.</summary>
    public int FreeBodiesPageSize { get; init; }

    /// <summary>Gets the largest number of sectors of one generation run.</summary>
    public int MaxSectorsPerRun { get; init; }

    /// <summary>Gets the largest expected number of star systems of one generation run.</summary>
    public int MaxSystemsPerRun { get; init; }

    /// <summary>Gets how often the admin panel asks for the status of a running generation.</summary>
    public int StatusPollSeconds { get; init; }

    /// <summary>Gets the name templates.</summary>
    public WorldNameTemplates Names { get; init; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// Reads the parameters from the configuration.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <returns>The parameters.</returns>
    public static WorldSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new WorldSettings
        {
            GalaxySeed = ReadInt64(config, WorldConfigKeys.GalaxySeed),
            InitialSectorCount = ReadInt32(config, WorldConfigKeys.InitialSectorCount),
            NewbieSectorCount = ReadInt32(config, WorldConfigKeys.NewbieSectorCount),
            SectorSpacing = ReadDouble(config, WorldConfigKeys.SectorSpacing),
            SectorSize = ReadDouble(config, WorldConfigKeys.SectorSize),
            SectorDepth = ReadDouble(config, WorldConfigKeys.SectorDepth),
            SystemsMin = ReadInt32(config, WorldConfigKeys.SystemsMin),
            SystemsMax = ReadInt32(config, WorldConfigKeys.SystemsMax),
            MinSystemDistance = ReadDouble(config, WorldConfigKeys.MinSystemDistance),
            PlacementAttempts = ReadInt32(config, WorldConfigKeys.PlacementAttempts),
            NebulaCount = ReadInt32(config, WorldConfigKeys.NebulaCount),
            FreeFieldsMin = ReadInt32(config, WorldConfigKeys.FreeFieldsMin),
            FreeFieldsMax = ReadInt32(config, WorldConfigKeys.FreeFieldsMax),
            MinFieldsPerSector = ReadInt32(config, WorldConfigKeys.MinFieldsPerSector),
            OrbitCount = ReadInt32(config, WorldConfigKeys.OrbitCount),
            BodiesMin = ReadInt32(config, WorldConfigKeys.BodiesMin),
            BodiesMax = ReadInt32(config, WorldConfigKeys.BodiesMax),
            GasGiantProbability = ReadDouble(config, WorldConfigKeys.GasGiantProbability),
            GasGiantMax = ReadInt32(config, WorldConfigKeys.GasGiantMax),
            SystemFieldProbability = ReadDouble(config, WorldConfigKeys.SystemFieldProbability),
            NebulaRadiusMin = ReadDouble(config, WorldConfigKeys.NebulaRadiusMin),
            NebulaRadiusMax = ReadDouble(config, WorldConfigKeys.NebulaRadiusMax),
            NebulaRadarMultiplier = ReadDouble(config, WorldConfigKeys.NebulaRadarMultiplier),
            NebulaBlocksJumpDrive = ReadBoolean(config, WorldConfigKeys.NebulaBlocksJumpDrive),
            FieldMaxCompositionShare = (decimal)ReadDouble(config, WorldConfigKeys.FieldMaxCompositionShare),
            RichPlanetProbability = ReadDouble(config, WorldConfigKeys.RichPlanetProbability),
            DepositCoefficientMin = (decimal)ReadDouble(config, WorldConfigKeys.DepositCoefficientMin),
            DepositCoefficientMax = (decimal)ReadDouble(config, WorldConfigKeys.DepositCoefficientMax),
            FieldRespawnDelay = TimeSpan.FromSeconds(ReadDouble(config, WorldConfigKeys.FieldRespawnDelaySeconds)),
            FieldRespawnInSystemProbability = ReadDouble(config, WorldConfigKeys.FieldRespawnInSystemProbability),
            GasGiantStationLimit = ReadInt32(config, WorldConfigKeys.GasGiantStationLimit),
            InSystemTravelTime = TimeSpan.FromSeconds(ReadDouble(config, WorldConfigKeys.InSystemTravelSeconds)),
            MaxNewbiesPerSystem = ReadInt32(config, WorldConfigKeys.MaxNewbiesPerSystem),
            HomeNameMinLength = ReadInt32(config, WorldConfigKeys.HomeNameMinLength),
            HomeNameMaxLength = ReadInt32(config, WorldConfigKeys.HomeNameMaxLength),
            HomeNameCharacters = AllowedCharacters.Parse(ReadText(config, WorldConfigKeys.HomeNameCharacters)),
            StartStorageCapacity = ReadInt64(config, WorldConfigKeys.StartStorageCapacity),
            RevealAll = ReadBoolean(config, WorldConfigKeys.RevealAll),
            FreeBodiesPageSize = ReadInt32(config, WorldConfigKeys.FreeBodiesPageSize),
            MaxSectorsPerRun = ReadInt32(config, WorldConfigKeys.MaxSectorsPerRun),
            MaxSystemsPerRun = ReadInt32(config, WorldConfigKeys.MaxSystemsPerRun),
            StatusPollSeconds = ReadInt32(config, WorldConfigKeys.StatusPollSeconds),
            Names = new WorldNameTemplates(
                ReadText(config, WorldConfigKeys.SectorNameTemplate),
                ReadText(config, WorldConfigKeys.SystemNameTemplate),
                ReadText(config, WorldConfigKeys.BodyNameTemplate),
                ReadText(config, WorldConfigKeys.FieldNameTemplate),
                ReadText(config, WorldConfigKeys.NebulaNameTemplate)),
        };
    }

    private static string ReadText(IGameConfig config, string key) =>
        config.GetRaw(key) ?? Default(key);

    private static long ReadInt64(IGameConfig config, string key) =>
        long.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : long.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) => (int)ReadInt64(config, key);

    private static double ReadDouble(IGameConfig config, string key) =>
        double.TryParse(config.GetRaw(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : double.Parse(Default(key), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool ReadBoolean(IGameConfig config, string key) =>
        bool.TryParse(config.GetRaw(key), out bool value) ? value : bool.Parse(Default(key));

    private static string Default(string key) =>
        WorldConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

/// <summary>
/// The name templates of the world objects.
/// </summary>
/// <param name="Sector">The template of a sector name.</param>
/// <param name="System">The template of a star system name.</param>
/// <param name="Body">The template of a body name.</param>
/// <param name="Field">The template of an asteroid field name.</param>
/// <param name="Nebula">The template of a nebula name.</param>
public sealed record WorldNameTemplates(string Sector, string System, string Body, string Field, string Nebula);
