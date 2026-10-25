namespace Aetheris.Host.Configuration;

/// <summary>
/// Database connection settings, bound from the <c>Database</c> configuration section.
/// </summary>
/// <remarks>
/// The connection string is never committed to the repository. It comes from the
/// <c>AETHERIS_DATABASE__CONNECTIONSTRING</c> environment variable, from user secrets, or
/// from the git-ignored <c>appsettings.Local.json</c> file
/// (task 00001, sections 2.3 and 2.6).
/// </remarks>
public sealed class DatabaseOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// The environment variable that overrides the connection string.
    /// </summary>
    public const string ConnectionStringEnvironmentVariable = "AETHERIS_DATABASE__CONNECTIONSTRING";

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Resolves the effective connection string.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The connection string to use.</returns>
    /// <exception cref="InvalidOperationException">No connection string is configured.</exception>
    public string GetConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? fromEnvironment = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        string? resolved = !string.IsNullOrWhiteSpace(fromEnvironment)
            ? fromEnvironment
            : configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException(
                $"No database connection string is configured. Set the " +
                $"{ConnectionStringEnvironmentVariable} environment variable, or add a " +
                $"\"ConnectionStrings\": {{ \"Default\": \"...\" }} section to the " +
                $"git-ignored appsettings.Local.json file.");
        }

        return resolved;
    }
}
