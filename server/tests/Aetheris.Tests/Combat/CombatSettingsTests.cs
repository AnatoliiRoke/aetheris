using System.Globalization;
using Aetheris.Combat.Api;
using Aetheris.Combat.Configuration;
using Aetheris.Core.Configuration;

namespace Aetheris.Tests.Combat;

/// <summary>
/// Tests of the combat parameters and of the logical codes (task 00062).
/// </summary>
public sealed class CombatSettingsTests
{
    /// <summary>An empty configuration uses the starting value of every key.</summary>
    [Fact]
    public void An_empty_configuration_uses_the_starting_values()
    {
        CombatSettings settings = CombatSettings.FromConfig(new StoredConfig(null));

        Assert.Equal(Whole(CombatConfigKeys.DeploySeconds), settings.DeploySeconds);
        Assert.Equal(Whole(CombatConfigKeys.PauseSeconds), settings.PauseSeconds);
        Assert.Equal(Whole(CombatConfigKeys.SegmentSeconds), settings.SegmentSeconds);
        Assert.Equal(Whole(CombatConfigKeys.TickMilliseconds), settings.TickMilliseconds);
        Assert.Equal(Whole(CombatConfigKeys.TrackStepSeconds), settings.TrackStepSeconds);
        Assert.Equal(Whole(CombatConfigKeys.MaxCycles), settings.MaxCycles);
        Assert.Equal(Fraction(CombatConfigKeys.ShieldRestoreShare), settings.ShieldRestoreShare);
        Assert.Equal(Fraction(CombatConfigKeys.ModuleDamageShare), settings.ModuleDamageShare);
        Assert.Equal(Whole(CombatConfigKeys.FieldWidth), settings.FieldWidth);
        Assert.Equal(Whole(CombatConfigKeys.FieldHeight), settings.FieldHeight);
        Assert.Equal(Whole(CombatConfigKeys.DeployZoneDepth), settings.DeployZoneDepth);
        Assert.Equal(Fraction(CombatConfigKeys.AsteroidDetectionMultiplier), settings.AsteroidDetectionMultiplier);
        Assert.Equal(Fraction(CombatConfigKeys.AsteroidHitMultiplier), settings.AsteroidHitMultiplier);
        Assert.Equal(Fraction(CombatConfigKeys.AsteroidSpeedMultiplier), settings.AsteroidSpeedMultiplier);
        Assert.Equal(Fraction(CombatConfigKeys.AccuracyDefault), settings.AccuracyDefault);
        Assert.Equal(Fraction(CombatConfigKeys.DistanceFalloff), settings.DistanceFalloff);
        Assert.Equal(Fraction(CombatConfigKeys.TargetSpeedFalloff), settings.TargetSpeedFalloff);
        Assert.Equal(Whole(CombatConfigKeys.SquadronMax), settings.SquadronMax);
        Assert.Equal(Whole(CombatConfigKeys.FormationMax), settings.FormationMax);
        Assert.Equal(Fraction(CombatConfigKeys.DebrisShare), settings.DebrisShare);
        Assert.Equal(Whole(CombatConfigKeys.CommanderExperience), settings.CommanderExperience);
        Assert.Equal(Whole(CombatConfigKeys.OrderIntervalSeconds), settings.OrderIntervalSeconds);
        Assert.Equal(Whole(CombatConfigKeys.ScenarioRuleMax), settings.ScenarioRuleMax);
        Assert.Equal(Fraction(CombatConfigKeys.ForceDamageWeight), settings.ForceDamageWeight);
        Assert.Equal(Fraction(CombatConfigKeys.ForceDefenseWeight), settings.ForceDefenseWeight);
    }

    /// <summary>A stored value outside the bounds of one key is refused, and every key is read.</summary>
    [Fact]
    public void A_stored_value_outside_the_bounds_is_refused()
    {
        foreach (ConfigParameterDefinition definition in CombatConfigKeys.Definitions)
        {
            double upper = double.Parse(definition.Maximum!, CultureInfo.InvariantCulture);
            string outside = (upper + 1).ToString(CultureInfo.InvariantCulture);

            Assert.Throws<InvalidOperationException>(() =>
                CombatSettings.FromConfig(new StoredConfig(new Dictionary<string, string> { [definition.Key] = outside })));
        }
    }

    /// <summary>A segment length that does not divide into ticks is refused.</summary>
    [Fact]
    public void A_segment_that_does_not_divide_into_ticks_is_refused()
    {
        Dictionary<string, string> stored = new()
        {
            [CombatConfigKeys.SegmentSeconds] = "10",
            [CombatConfigKeys.TickMilliseconds] = "300",
        };

        Assert.Throws<InvalidOperationException>(() => CombatSettings.FromConfig(new StoredConfig(stored)));
    }

    /// <summary>The server parameters cross into the library that runs a segment.</summary>
    [Fact]
    public void The_parameters_cross_into_the_library()
    {
        CombatSettings settings = CombatSettings.FromConfig(new StoredConfig(null));
        Aetheris.Combat.Engine.CombatSettings engine = settings.ToEngine();

        Assert.Equal(settings.SegmentSeconds, engine.SegmentSeconds);
        Assert.Equal(settings.TickMilliseconds, engine.TickMilliseconds);
        Assert.Equal(settings.ModuleDamageShare, engine.ModuleDamageShare);
        Assert.Equal(settings.ShieldRestoreShare, engine.ShieldRestoreShare);
    }

    /// <summary>A logical code is a lowercase word. It is not a closed list.</summary>
    [Fact]
    public void A_logical_code_is_a_lowercase_word()
    {
        Assert.False(CombatLogicCodes.Accepts("Nearest"));
        Assert.True(CombatLogicCodes.Accepts("nearest"));
        Assert.True(CombatLogicCodes.Accepts("keep_distance"));
        Assert.False(CombatLogicCodes.Accepts("1abc"));
    }

    private static int Whole(string key) =>
        int.Parse(CombatConfigKeys.Require(key).DefaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static double Fraction(string key) =>
        double.Parse(CombatConfigKeys.Require(key).DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture);

    private sealed class StoredConfig : IGameConfig
    {
        private readonly Dictionary<string, string> _stored;

        public StoredConfig(Dictionary<string, string>? stored)
        {
            _stored = stored ?? [];
        }

        public string? GetRaw(string key) => _stored.TryGetValue(key, out string? stored) ? stored : null;

        public long GetInt64(string key, long defaultValue = 0) => defaultValue;

        public double GetDouble(string key, double defaultValue = 0d) => defaultValue;

        public bool GetBoolean(string key, bool defaultValue = false) => defaultValue;

        public IReadOnlyDictionary<string, ConfigParameter> Snapshot() => new Dictionary<string, ConfigParameter>();
    }
}
