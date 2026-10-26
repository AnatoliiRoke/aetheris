namespace Aetheris.Core.Text;

/// <summary>
/// Checks a name that a player chooses and that other players see.
/// </summary>
/// <remarks>
/// The list of restricted words lives in the database (task 00006, decision 7); the host
/// implements this interface, and a module that accepts a visible name asks it, so every
/// name of the game is checked against the same list.
/// </remarks>
public interface ITextModeration
{
    /// <summary>
    /// Checks a name.
    /// </summary>
    /// <param name="text">The name.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the name contains no restricted word.</returns>
    Task<bool> IsAllowedAsync(string text, CancellationToken cancellationToken);
}
