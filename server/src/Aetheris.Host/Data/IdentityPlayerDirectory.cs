using Aetheris.Core.Players;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// Gives the nicknames of the players to the modules from the accounts of the host.
/// </summary>
public sealed class IdentityPlayerDirectory : IPlayerDirectory
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityPlayerDirectory"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the host.</param>
    public IdentityPlayerDirectory(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetNicknamesAsync(IReadOnlyCollection<string> playerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        if (playerIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _dbContext.Users.AsNoTracking()
            .Where(user => playerIds.Contains(user.Id) && user.UserName != null)
            .ToDictionaryAsync(user => user.Id, user => user.UserName!, cancellationToken)
            .ConfigureAwait(false);
    }
}
