namespace Aetheris.Core.Players;

/// <summary>
/// What the modules may know about a player they show to other players: the nickname.
/// </summary>
/// <remarks>
/// The accounts live in the host, so a module reads the nickname through this contract and never
/// through the tables of the accounts (documentation/tech/08, rule 5).
/// </remarks>
public interface IPlayerDirectory
{
    /// <summary>
    /// Gets the nicknames of several players in one read.
    /// </summary>
    /// <param name="playerIds">The players.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The nickname by player; a player that is unknown is not in the answer.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNicknamesAsync(IReadOnlyCollection<string> playerIds, CancellationToken cancellationToken = default);
}
