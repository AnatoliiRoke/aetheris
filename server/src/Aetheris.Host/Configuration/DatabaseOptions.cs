namespace Aetheris.Host.Configuration;

/// <summary>
/// Resolves the database connection string.
/// </summary>
/// <remarks>
/// The connection string is never committed to the repository. There are exactly two ways
/// to provide it:
/// <list type="number">
///   <item>
///     The <c>AETHERIS_DATABASE__CONNECTIONSTRING</c> environment variable. It wins over
///     everything else, so a deployment can override the file without editing it.
///   </item>
///   <item>
///     The <c>ConnectionStrings:Default</c> section of the configuration, which is where
///     the git-ignored <c>appsettings.Local.json</c> file puts it.
///   </item>
/// </list>
/// </remarks>
public static class DatabaseOptions
{
    /// <summary>
    /// The environment variable that overrides the connection string.
    /// </summary>
    public const string ConnectionStringEnvironmentVariable = "AETHERIS_DATABASE__CONNECTIONSTRING";

    /// <summary>
    /// The configuration key of the connection string inside the <c>ConnectionStrings</c> section.
    /// </summary>
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Resolves the effective connection string.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The connection string to use.</returns>
    /// <exception cref="InvalidOperationException">No connection string is configured.</exception>
    public static string GetConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? fromEnvironment = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        string? resolved = !string.IsNullOrWhiteSpace(fromEnvironment)
            ? fromEnvironment
            : configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException(
                $"No database connection string is configured. Set the " +
                $"{ConnectionStringEnvironmentVariable} environment variable, or add a " +
                $"\"ConnectionStrings\": {{ \"{ConnectionStringName}\": \"...\" }} section to " +
                $"the git-ignored appsettings.Local.json file.");
        }

        return resolved;
    }
}
