namespace Aetheris.World;

/// <summary>
/// The settings of the host that concern the World module at start.
/// </summary>
public static class WorldStartup
{
    /// <summary>
    /// The key of the application setting that creates the galaxy at start when it is
    /// missing. It is on by default; the test host turns it off and creates the world itself.
    /// </summary>
    public const string GenerateOnStartupKey = "World:GenerateOnStartup";
}
