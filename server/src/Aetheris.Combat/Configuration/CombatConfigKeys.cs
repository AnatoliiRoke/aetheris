using Aetheris.Core.Configuration;

namespace Aetheris.Combat.Configuration;

/// <summary>
/// The configuration keys of the combat module with their starting values (task 00062).
/// </summary>
/// <remarks>
/// The host adds these definitions to its seed. The numbers live here as text, and
/// <see cref="CombatSettings"/> refuses a stored value that is outside the bounds.
/// </remarks>
public static class CombatConfigKeys
{
    /// <summary>How long the deployment phase lasts, in game seconds.</summary>
    public const string DeploySeconds = "combat.deploy_seconds";

    /// <summary>How long the pause for orders lasts, in game seconds.</summary>
    public const string PauseSeconds = "combat.pause_seconds";

    /// <summary>How long one segment the engine counts lasts, in game seconds.</summary>
    public const string SegmentSeconds = "combat.segment_seconds";

    /// <summary>The calculation step, in milliseconds.</summary>
    public const string TickMilliseconds = "combat.tick_ms";

    /// <summary>The step of the track points sent to the client, in game seconds.</summary>
    public const string TrackStepSeconds = "combat.track_step_seconds";

    /// <summary>The limit of the cycles.</summary>
    public const string MaxCycles = "combat.max_cycles";

    /// <summary>The share of the shield saturation restored at the end of a cycle.</summary>
    public const string ShieldRestoreShare = "combat.shield_restore_share";

    /// <summary>The share of the damage that goes into a module after the armor.</summary>
    public const string ModuleDamageShare = "combat.module_damage_share";

    /// <summary>The width of the field.</summary>
    public const string FieldWidth = "combat.field_width";

    /// <summary>The height of the field.</summary>
    public const string FieldHeight = "combat.field_height";

    /// <summary>The depth of the deployment zone.</summary>
    public const string DeployZoneDepth = "combat.deploy_zone_depth";

    /// <summary>The detection multiplier inside an asteroid field.</summary>
    public const string AsteroidDetectionMultiplier = "combat.asteroid_detection_mult";

    /// <summary>The hit multiplier inside an asteroid field.</summary>
    public const string AsteroidHitMultiplier = "combat.asteroid_hit_mult";

    /// <summary>The speed multiplier inside an asteroid field.</summary>
    public const string AsteroidSpeedMultiplier = "combat.asteroid_speed_mult";

    /// <summary>The accuracy used when a group has no row.</summary>
    public const string AccuracyDefault = "combat.accuracy_default";

    /// <summary>How much the accuracy falls at the edge of the range.</summary>
    public const string DistanceFalloff = "combat.distance_falloff";

    /// <summary>How much the accuracy falls with the speed of the target.</summary>
    public const string TargetSpeedFalloff = "combat.target_speed_falloff";

    /// <summary>How many squadrons one fleet may hold.</summary>
    public const string SquadronMax = "combat.squadron_max";

    /// <summary>How many formations one fleet may hold.</summary>
    public const string FormationMax = "combat.formation_max";

    /// <summary>The share of the resources of a destroyed hull that becomes debris.</summary>
    public const string DebrisShare = "combat.debris_share";

    /// <summary>The experience a commander gains for one engagement. Zero until the academy exists.</summary>
    public const string CommanderExperience = "combat.commander_xp_per_battle";

    /// <summary>The least time between two orders of one player, in game seconds.</summary>
    public const string OrderIntervalSeconds = "combat.order_interval_seconds";

    /// <summary>How many if-then rules one scenario may hold.</summary>
    public const string ScenarioRuleMax = "combat.scenario_rule_max";

    /// <summary>The weight of the damage in the strength of a side.</summary>
    public const string ForceDamageWeight = "combat.force_damage_weight";

    /// <summary>The weight of the defense in the strength of a side.</summary>
    public const string ForceDefenseWeight = "combat.force_defense_weight";

    /// <summary>
    /// The keys whose stored text is a fraction. The other keys are whole numbers.
    /// </summary>
    public static IReadOnlySet<string> FractionalKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ShieldRestoreShare,
        ModuleDamageShare,
        AsteroidDetectionMultiplier,
        AsteroidHitMultiplier,
        AsteroidSpeedMultiplier,
        AccuracyDefault,
        DistanceFalloff,
        TargetSpeedFalloff,
        DebrisShare,
        ForceDamageWeight,
        ForceDefenseWeight,
    };

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(DeploySeconds, "300", "30", "3600", "Тривалість фази розстановки, ігрові секунди."),
        new(PauseSeconds, "420", "60", "1800", "Тривалість паузи на накази, ігрові секунди."),
        new(SegmentSeconds, "60", "10", "300", "Тривалість відрізку, що рахує рушій, ігрові секунди."),
        new(TickMilliseconds, "250", "50", "1000", "Крок розрахунку, мілісекунди."),
        new(TrackStepSeconds, "10", "1", "60", "Крок точок сліду руху для клієнта, ігрові секунди."),
        new(MaxCycles, "6", "1", "50", "Ліміт циклів."),
        new(ShieldRestoreShare, "0.30", "0", "1", "Частка насичення щита, що повертається в кінці циклу."),
        new(ModuleDamageShare, "0.25", "0", "1", "Частка шкоди, що після броні йде в модуль."),
        new(FieldWidth, "20000", "2000", "200000", "Ширина поля."),
        new(FieldHeight, "12000", "2000", "200000", "Висота поля."),
        new(DeployZoneDepth, "3000", "100", "50000", "Глибина зони розстановки."),
        new(AsteroidDetectionMultiplier, "0.6", "0.05", "1", "Множник виявлення в астероїдному полі."),
        new(AsteroidHitMultiplier, "0.7", "0.05", "1", "Множник влучання в астероїдному полі."),
        new(AsteroidSpeedMultiplier, "0.5", "0.05", "1", "Множник швидкості в астероїдному полі."),
        new(AccuracyDefault, "0.5", "0", "1", "Влучність по групі без рядка."),
        new(DistanceFalloff, "0.5", "0", "1", "Спад влучності на межі дальності."),
        new(TargetSpeedFalloff, "0.5", "0", "1", "Спад влучності від швидкості цілі."),
        new(SquadronMax, "12", "1", "50", "Ескадрилей у флоті."),
        new(FormationMax, "5", "1", "20", "Формацій у флоті."),
        new(DebrisShare, "0.20", "0", "1", "Частка ресурсів знищеного корабля в уламках."),
        new(CommanderExperience, "0", "0", "1000", "Досвід командира за бій."),
        new(OrderIntervalSeconds, "2", "0", "60", "Мінімум між наказами одного гравця, ігрові секунди."),
        new(ScenarioRuleMax, "12", "1", "50", "Правил «якщо → то» у сценарії."),
        new(ForceDamageWeight, "1.0", "0", "100", "Вага шкоди в силі сторони."),
        new(ForceDefenseWeight, "1.0", "0", "100", "Вага захисту в силі сторони."),
    ];

    private static readonly Dictionary<string, ConfigParameterDefinition> DefinitionsByKey =
        Definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    /// <summary>
    /// Finds the definition of a key.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="InvalidOperationException">The key is not one of the combat parameters.</exception>
    public static ConfigParameterDefinition Require(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return DefinitionsByKey.TryGetValue(key, out ConfigParameterDefinition? definition)
            ? definition
            : throw new InvalidOperationException($"The configuration '{key}' is not a combat parameter.");
    }
}
