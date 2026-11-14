namespace Aetheris.Combat.Engine;

/// <summary>
/// The numbers a segment needs. They come from the configuration of the server.
/// This library checks only that the numbers can be used: a share stays between zero and one,
/// a size is positive, and the segment divides into ticks. It does not store starting values.
/// </summary>
public sealed class CombatSettings
{
    /// <summary>How many milliseconds are in one second. The tick count is built from this.</summary>
    public const int MillisecondsPerSecond = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatSettings"/> class.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A number cannot be used.</exception>
    public CombatSettings(
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
        EnsureValid();
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

    /// <summary>Gets the share of the hull damage that reaches one module.</summary>
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

    /// <summary>The fixed step of the segment, in seconds.</summary>
    public double TickSeconds => TickMilliseconds / (double)MillisecondsPerSecond;

    /// <summary>
    /// Counts the ticks of a segment. The count has to be a whole number.
    /// </summary>
    /// <param name="segmentSeconds">The length of the segment, in game seconds.</param>
    /// <returns>The number of ticks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The length does not divide into ticks.</exception>
    public int CountTicks(int segmentSeconds)
    {
        if (!Divides(segmentSeconds, TickMilliseconds))
        {
            throw new ArgumentOutOfRangeException(nameof(segmentSeconds), "The segment does not divide into ticks.");
        }

        return segmentSeconds * MillisecondsPerSecond / TickMilliseconds;
    }

    /// <summary>Rejects numbers that the formulas cannot use.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A number cannot be used.</exception>
    public void EnsureValid()
    {
        RequirePositive(DeploySeconds, nameof(DeploySeconds));
        RequirePositive(PauseSeconds, nameof(PauseSeconds));
        RequirePositive(SegmentSeconds, nameof(SegmentSeconds));
        RequirePositive(TickMilliseconds, nameof(TickMilliseconds));
        RequirePositive(TrackStepSeconds, nameof(TrackStepSeconds));
        RequirePositive(MaxCycles, nameof(MaxCycles));
        RequirePositive(FieldWidth, nameof(FieldWidth));
        RequirePositive(FieldHeight, nameof(FieldHeight));
        RequirePositive(DeployZoneDepth, nameof(DeployZoneDepth));
        RequirePositive(SquadronMax, nameof(SquadronMax));
        RequirePositive(FormationMax, nameof(FormationMax));
        RequirePositive(ScenarioRuleMax, nameof(ScenarioRuleMax));
        RequireShare(ShieldRestoreShare, nameof(ShieldRestoreShare));
        RequireShare(ModuleDamageShare, nameof(ModuleDamageShare));
        RequireShare(AccuracyDefault, nameof(AccuracyDefault));
        RequireShare(DistanceFalloff, nameof(DistanceFalloff));
        RequireShare(TargetSpeedFalloff, nameof(TargetSpeedFalloff));
        RequireShare(DebrisShare, nameof(DebrisShare));
        RequireOpenShare(AsteroidDetectionMultiplier, nameof(AsteroidDetectionMultiplier));
        RequireOpenShare(AsteroidHitMultiplier, nameof(AsteroidHitMultiplier));
        RequireOpenShare(AsteroidSpeedMultiplier, nameof(AsteroidSpeedMultiplier));
        RequireNotNegative(CommanderExperience, nameof(CommanderExperience));
        RequireNotNegative(OrderIntervalSeconds, nameof(OrderIntervalSeconds));
        RequireFiniteNotNegative(ForceDamageWeight, nameof(ForceDamageWeight));
        RequireFiniteNotNegative(ForceDefenseWeight, nameof(ForceDefenseWeight));

        if (!Divides(SegmentSeconds, TickMilliseconds))
        {
            throw new ArgumentOutOfRangeException(nameof(SegmentSeconds), "The segment does not divide into ticks.");
        }
    }

    private static bool Divides(int segmentSeconds, int tickMilliseconds) =>
        segmentSeconds > 0
        && tickMilliseconds > 0
        && (segmentSeconds * MillisecondsPerSecond) % tickMilliseconds == 0;

    private static void RequirePositive(int number, string name)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireNotNegative(int number, string name)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireShare(double number, string name)
    {
        if (!double.IsFinite(number) || number < 0 || number > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireOpenShare(double number, string name)
    {
        if (!double.IsFinite(number) || number <= 0 || number > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireFiniteNotNegative(double number, string name)
    {
        if (!double.IsFinite(number) || number < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
