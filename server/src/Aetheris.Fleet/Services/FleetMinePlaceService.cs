using Aetheris.Core.Configuration;
using Aetheris.Fleet.Configuration;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Fleet.Services;

/// <summary>
/// Finds the place an order of extraction takes from: the place where the fleet stands (task 00054).
/// </summary>
/// <remarks>
/// <para>
/// An order of extraction names no target: the player gives only the time. The place comes from the
/// position of the fleet at the moment the order begins. A fleet that stands at an object its modules take
/// from (the kinds of the places come from the rows of the modules, task 00055) is at that place. Otherwise the three
/// coordinates of the fleet are looked up in the world: a free asteroid field whose point is within the
/// tolerance of the configuration, or a nebula whose sphere holds the point; when several places fit,
/// the nearest wins.
/// </para>
/// <para>
/// The service reads and changes nothing.
/// </para>
/// </remarks>
public sealed class FleetMinePlaceService
{
    private readonly IWorldModule _world;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetMinePlaceService"/> class.
    /// </summary>
    /// <param name="world">The world module.</param>
    /// <param name="config">The game configuration.</param>
    public FleetMinePlaceService(IWorldModule world, IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(config);

        _world = world;
        _config = config;
    }

    /// <summary>
    /// Finds the place of the extraction for a fleet that stands at a position.
    /// </summary>
    /// <param name="position">The position of the fleet.</param>
    /// <param name="placeKinds">The kinds of the places the modules of the fleet take from, the classes of their objects.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place the fleet takes from, or <see langword="null"/> when the position holds none.</returns>
    public async Task<WorldPlaceDto?> FindAsync(
        WorldPlaceDto position,
        IReadOnlySet<string> placeKinds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(placeKinds);

        // A fleet that stands at an object it can take from is at that place; a depleted field is gone
        // from the map and falls through to the search by the coordinates.
        if (position.ObjectId is { } objectId)
        {
            WorldPlaceDto? standing = position.ObjectClass.Length > 0
                ? position
                : await _world.GetObjectPlaceAsync(objectId, cancellationToken).ConfigureAwait(false);

            if (standing is not null && placeKinds.Contains(standing.ObjectClass))
            {
                return standing;
            }
        }

        double tolerance = FleetSettings.From(_config).ExtractionPlaceTolerance;

        return await _world
            .FindExtractionPlaceAsync(position.X, position.Y, position.Z, tolerance, cancellationToken)
            .ConfigureAwait(false);
    }
}
