using Aetheris.Core.Configuration;

namespace Aetheris.Host.Configuration;

/// <summary>
/// Loads game configuration parameters from the database.
/// </summary>
public interface IConfigParameterStore
{
    /// <summary>
    /// Reads all configuration parameters.
    /// </summary>
    /// <returns>The parameters currently stored in the database.</returns>
    Task<IReadOnlyList<KeyValuePair<string, ConfigParameter>>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
