using Aetheris.World;

namespace Aetheris.Economy.Services;

/// <summary>
/// The costs of a subject, split by the scope of the resources they name (task 00015,
/// decision 5).
/// </summary>
/// <remarks>
/// The queues of the modules pay their costs through this service, so the code of a queue
/// does not know what a credit is: it gives the costs and the service says which of them
/// come from the balance of the body and which from the balance of the civilization.
/// </remarks>
public sealed class CostSplitter
{
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="CostSplitter"/> class.
    /// </summary>
    /// <param name="world">The world, for the resources and their scope.</param>
    public CostSplitter(IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <summary>
    /// Splits the costs by the scope of the resources they name.
    /// </summary>
    /// <param name="costs">The costs by resource.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The costs of the body and the costs of the civilization.</returns>
    public async Task<SplitCosts> SplitAsync(
        IReadOnlyDictionary<int, long> costs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costs);

        if (costs.Count == 0)
        {
            return new SplitCosts(new Dictionary<int, long>(), new Dictionary<int, long>());
        }

        World.Api.ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<int, long> planet = [];
        Dictionary<int, long> currencies = [];

        foreach ((int resourceTypeId, long amount) in costs)
        {
            World.Api.ResourceTypeFact? resource = catalog.Resources
                .FirstOrDefault(entry => entry.Id == resourceTypeId);

            // A resource that is not in the catalog lies on a body: the scope of a resource is
            // known from the reference table, and a resource that is not there is a planet one.
            if (resource is { IsCivilizationScope: true })
            {
                currencies[resourceTypeId] = amount;
            }
            else
            {
                planet[resourceTypeId] = amount;
            }
        }

        return new SplitCosts(planet, currencies);
    }
}
