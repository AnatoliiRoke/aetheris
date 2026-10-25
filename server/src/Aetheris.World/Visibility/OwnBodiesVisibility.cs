using Aetheris.Core.Configuration;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Visibility;

/// <summary>
/// The visibility before the radar: a player sees only their own bodies; the development
/// switch <see cref="WorldConfigKeys.RevealAll"/> opens the whole map.
/// </summary>
public sealed class OwnBodiesVisibility : IWorldVisibility
{
    private readonly WorldDbContext _dbContext;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnBodiesVisibility"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="config">The game configuration.</param>
    public OwnBodiesVisibility(WorldDbContext dbContext, IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _config = config;
    }

    /// <inheritdoc />
    public async Task<VisibilityScope> GetScopeAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (WorldSettings.From(_config).RevealAll)
        {
            return new VisibilityScope(true, new HashSet<long>(), new HashSet<long>());
        }

        var owned = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.OwnerPlayerId == playerId)
            .Select(planet => new { planet.CelestialObjectId, planet.SystemId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new VisibilityScope(
            false,
            owned.Select(entry => entry.CelestialObjectId).ToHashSet(),
            owned.Select(entry => entry.SystemId).ToHashSet());
    }
}
