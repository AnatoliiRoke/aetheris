using System.Globalization;
using Aetheris.Core.Configuration;
using EngineCombatSettings = Aetheris.Combat.Engine.CombatSettings;

namespace Aetheris.Combat.Configuration;

/// <summary>
/// The combat parameters read from the configuration (task 00062).
/// </summary>
/// <remarks>
/// Every key is read. A missing key uses the starting value of its definition, and a stored
/// value outside the bounds of the definition is refused. The numbers of the game are not
/// written here.
/// </remarks>
public sealed class CombatSettings
{
    /// <summary>How many milliseconds are in one second. The tick count is built from this.</summary>
    private const int MillisecondsPerSecond = 1000;

    private CombatSettings(
        int deploySeconds,
        int pauseSeconds,
        int segmentSeconds,
        int tickMilliseconds,
        int trackStepSeconds,
        int maxCycles,
        double shieldRestoreShare,
        double moduleDamageShare,
        int fieldWidth,
        int fieldHeight,
        int deployZoneDepth,
        double asteroidDetectionMultiplier,
        double asteroidHitMultiplier,
        double asteroidSpeedMultiplier,
        double accuracyDefault,
        double distanceFalloff,
        double targetSpeedFalloff,
        int squadronMax,
        int formationMax,
        double debrisShare,
        int commanderExperience,
        int orderIntervalSeconds,
        int scenarioRuleMax,
        double forceDamageWeight,
        double forceDefenseWeight)
    {
        DeploySeconds = deploySeconds;
        PauseSeconds = pauseSeconds;
        SegmentSeconds = segmentSeconds;
        TickMilliseconds = tickMilliseconds;
        TrackStepSeconds = trackStepSeconds;
        MaxCycles = maxCycles;
        ShieldRestoreShare = shieldRestoreShare;
        ModuleDamageShare = moduleDamageShare;
        FieldWidth = fieldWidth;
        FieldHeight = fieldHeight;
        DeployZoneDepth = deployZoneDepth;
        AsteroidDetectionMultiplier = asteroidDetectionMultiplier;
        AsteroidHitMultiplier = asteroidHitMultiplier;
        AsteroidSpeedMultiplier = asteroidSpeedMultiplier;
        AccuracyDefault = accuracyDefault;
        DistanceFalloff = distanceFalloff;
        TargetSpeedFalloff = targetSpeedFalloff;
        SquadronMax = squadronMax;
        FormationMax = formationMax;
        DebrisShare = debrisShare;
        CommanderExperience = commanderExperience;
        OrderIntervalSeconds = orderIntervalSeconds;
        ScenarioRuleMax = scenarioRuleMax;
        ForceDamageWeight = forceDamageWeight;
        ForceDefenseWeight = forceDefenseWeight;
    }

    /// <summary>Gets how long the deployment phase lasts, in game seconds.</summary>
    public int DeploySeconds { get; }

    /// <summary>Gets how long the pause for orders lasts, in game seconds.</summary>
    public int PauseSeconds { get; }

    /// <summary>Gets how long one counted segment lasts, in game seconds.</summary>
    public int SegmentSeconds { get; }

    /// <summary>Gets the calculation step, in milliseconds.</summary>
    public int TickMilliseconds { get; }

    /// <summary>Gets the step of the track points, in game seconds.</summary>
    public int TrackStepSeconds { get; }

    /// <summary>Gets the limit of the cycles.</summary>
    public int MaxCycles { get; }

    /// <summary>Gets the share of the shield saturation restored at the end of a cycle.</summary>
    public double ShieldRestoreShare { get; }

    /// <summary>Gets the share of the damage that goes into a module after the armor.</summary>
    public double ModuleDamageShare { get; }

    /// <summary>Gets the width of the field.</summary>
    public int FieldWidth { get; }

    /// <summary>Gets the height of the field.</summary>
    public int FieldHeight { get; }

    /// <summary>Gets the depth of the deployment zone.</summary>
    public int DeployZoneDepth { get; }

    /// <summary>Gets the detection multiplier inside an asteroid field.</summary>
    public double AsteroidDetectionMultiplier { get; }

    /// <summary>Gets the hit multiplier inside an asteroid field.</summary>
    public double AsteroidHitMultiplier { get; }

    /// <summary>Gets the speed multiplier inside an asteroid field.</summary>
    public double AsteroidSpeedMultiplier { get; }

    /// <summary>Gets the accuracy used when a group has no row.</summary>
    public double AccuracyDefault { get; }

    /// <summary>Gets how much the accuracy falls at the edge of the range.</summary>
    public double DistanceFalloff { get; }

    /// <summary>Gets how much the accuracy falls with the speed of the target.</summary>
    public double TargetSpeedFalloff { get; }

    /// <summary>Gets how many squadrons one fleet may hold.</summary>
    public int SquadronMax { get; }

    /// <summary>Gets how many formations one fleet may hold.</summary>
    public int FormationMax { get; }

    /// <summary>Gets the share of a destroyed hull that becomes debris.</summary>
    public double DebrisShare { get; }

    /// <summary>Gets the experience a commander gains for one engagement.</summary>
    public int CommanderExperience { get; }

    /// <summary>Gets the least time between two orders of one player, in game seconds.</summary>
    public int OrderIntervalSeconds { get; }

    /// <summary>Gets how many if-then rules one scenario may hold.</summary>
    public int ScenarioRuleMax { get; }

    /// <summary>Gets the weight of the damage in the strength of a side.</summary>
    public double ForceDamageWeight { get; }

    /// <summary>Gets the weight of the defense in the strength of a side.</summary>
    public double ForceDefenseWeight { get; }

    /// <summary>
    /// Reads every combat parameter.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>The parameters.</returns>
    /// <exception cref="InvalidOperationException">A stored value is outside its bounds.</exception>
    public static CombatSettings FromConfig(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        CombatSettings settings = new(
            Whole(config, CombatConfigKeys.DeploySeconds),
            Whole(config, CombatConfigKeys.PauseSeconds),
            Whole(config, CombatConfigKeys.SegmentSeconds),
            Whole(config, CombatConfigKeys.TickMilliseconds),
            Whole(config, CombatConfigKeys.TrackStepSeconds),
            Whole(config, CombatConfigKeys.MaxCycles),
            Fraction(config, CombatConfigKeys.ShieldRestoreShare),
            Fraction(config, CombatConfigKeys.ModuleDamageShare),
            Whole(config, CombatConfigKeys.FieldWidth),
            Whole(config, CombatConfigKeys.FieldHeight),
            Whole(config, CombatConfigKeys.DeployZoneDepth),
            Fraction(config, CombatConfigKeys.AsteroidDetectionMultiplier),
            Fraction(config, CombatConfigKeys.AsteroidHitMultiplier),
            Fraction(config, CombatConfigKeys.AsteroidSpeedMultiplier),
            Fraction(config, CombatConfigKeys.AccuracyDefault),
            Fraction(config, CombatConfigKeys.DistanceFalloff),
            Fraction(config, CombatConfigKeys.TargetSpeedFalloff),
            Whole(config, CombatConfigKeys.SquadronMax),
            Whole(config, CombatConfigKeys.FormationMax),
            Fraction(config, CombatConfigKeys.DebrisShare),
            Whole(config, CombatConfigKeys.CommanderExperience),
            Whole(config, CombatConfigKeys.OrderIntervalSeconds),
            Whole(config, CombatConfigKeys.ScenarioRuleMax),
            Fraction(config, CombatConfigKeys.ForceDamageWeight),
            Fraction(config, CombatConfigKeys.ForceDefenseWeight));

        if (settings.TickMilliseconds <= 0
            || (long)settings.SegmentSeconds * MillisecondsPerSecond % settings.TickMilliseconds != 0)
        {
            throw new InvalidOperationException("The segment does not divide into ticks.");
        }

        return settings;
    }

    /// <summary>
    /// Copies the parameters into the library that runs a segment.
    /// </summary>
    /// <returns>The parameters of the library.</returns>
    public EngineCombatSettings ToEngine() =>
        new(
            DeploySeconds,
            PauseSeconds,
            SegmentSeconds,
            TickMilliseconds,
            TrackStepSeconds,
            MaxCycles,
            ShieldRestoreShare,
            ModuleDamageShare,
            FieldWidth,
            FieldHeight,
            DeployZoneDepth,
            AsteroidDetectionMultiplier,
            AsteroidHitMultiplier,
            AsteroidSpeedMultiplier,
            AccuracyDefault,
            DistanceFalloff,
            TargetSpeedFalloff,
            SquadronMax,
            FormationMax,
            DebrisShare,
            CommanderExperience,
            OrderIntervalSeconds,
            ScenarioRuleMax,
            ForceDamageWeight,
            ForceDefenseWeight);

    private static int Whole(IGameConfig config, string key) =>
        int.Parse(Accepted(config, key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static double Fraction(IGameConfig config, string key) =>
        double.Parse(Accepted(config, key), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string Accepted(IGameConfig config, string key)
    {
        ConfigParameterDefinition definition = CombatConfigKeys.Require(key);
        string raw = config.GetRaw(key) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = definition.DefaultValue;
        }

        if (!definition.Accepts(raw))
        {
            throw new InvalidOperationException($"The configuration '{key}' is outside its bounds.");
        }

        return raw;
    }
}
