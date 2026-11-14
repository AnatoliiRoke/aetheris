namespace Aetheris.Combat;

/// <summary>
/// The public contract of the Combat module (task 00062).
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces. This module keeps its own reference
/// rows and does not read another module.
/// </remarks>
public interface ICombatModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Writes the reference rows that are still missing. A repeated call leaves a row that is
    /// already there.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rows are in place.</returns>
    Task EnsureAsync(CancellationToken cancellationToken = default);
}
