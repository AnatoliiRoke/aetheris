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
    /// The configuration key of the optional connection string used for the migrations.
    /// </summary>
    /// <remarks>
    /// Set it through the <c>Database__MigrationConnectionString</c> environment variable or
    /// the git-ignored local file when the server runs with a database account that has no
    /// right to change the schema.
    /// </remarks>
    public const string MigrationConnectionStringKey = "Database:MigrationConnectionString";

    /// <summary>
    /// Resolves the connection string of the migrations.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The connection string, or <see langword="null"/> when the main one is used for the migrations too.</returns>
    public static string? GetMigrationConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? value = configuration[MigrationConnectionStringKey];

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

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
