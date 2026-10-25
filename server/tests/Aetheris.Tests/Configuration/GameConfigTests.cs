using Aetheris.Core.Configuration;

namespace Aetheris.Tests.Configuration;

/// <summary>
/// Tests for the in-memory configuration container itself, without a database.
/// </summary>
public sealed class GameConfigTests
{
    /// <summary>
    /// Replacing the values makes the new values visible immediately.
    /// </summary>
    [Fact]
    public void Replacing_the_values_publishes_them_at_once()
    {
        GameConfig config = new();

        Assert.Equal(0, config.GetInt64("missing"));

        config.Replace([new KeyValuePair<string, ConfigParameter>(
            "a.b", new ConfigParameter("a.b", "5", 1))]);

        Assert.Equal(5, config.GetInt64("a.b"));
        Assert.Equal(1, config.Version);
    }

    /// <summary>
    /// A parameter that cannot be parsed falls back to the default value.
    /// </summary>
    [Fact]
    public void An_unparseable_value_falls_back_to_the_default()
    {
        GameConfig config = new();

        config.Replace([new KeyValuePair<string, ConfigParameter>(
            "not.a.number", new ConfigParameter("not.a.number", "abc", 1))]);

        Assert.Equal(11, config.GetInt64("not.a.number", 11));
        Assert.Equal(2.5, config.GetDouble("not.a.number", 2.5));
        Assert.True(config.GetBoolean("not.a.number", true));
    }
}
