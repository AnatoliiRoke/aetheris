using Aetheris.Core.Errors;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Extraction;
using Aetheris.World.Generation;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The extraction from a nebula (task 00050, task 00053).
/// </summary>
/// <remarks>
/// A nebula has no mass, only an area, and it never runs out: it is an endless source of ether and
/// of dark matter. The extractors of ether and of matter take the yield of the cycle by the shares
/// of the composition of the nebula, as «the yield of the cycle times the share of the resource»,
/// and nothing is taken away from the nebula itself, so the service only reads.
/// </remarks>
public sealed class NebulaExtractionService
{
    private readonly WorldDbContext _dbContext;
    private readonly ResourceCatalogCache _resourceCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="NebulaExtractionService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="resourceCatalog">The catalog of the resources, for the weight of the yield in the hold.</param>
    public NebulaExtractionService(WorldDbContext dbContext, ResourceCatalogCache resourceCatalog)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resourceCatalog);

        _dbContext = dbContext;
        _resourceCatalog = resourceCatalog;
    }

    /// <summary>
    /// Processes one cycle on a nebula: the yield is the processed mass split by the shares of the
    /// composition, and a full hold limits it. The nebula keeps everything it had.
    /// </summary>
    /// <param name="nebulaId">The nebula.</param>
    /// <param name="processingMass">The mass the module processes per cycle.</param>
    /// <param name="freeHold">The free room of the hold.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The yield of the cycle; the remaining mass is zero because the nebula has none.</returns>
    public async Task<ExtractionResult> ExtractAsync(long nebulaId, long processingMass, long freeHold, CancellationToken cancellationToken = default)
    {
        bool exists = await _dbContext.Nebulae.AsNoTracking()
            .AnyAsync(nebula => nebula.Id == nebulaId, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            throw new GameException(GameErrorCodes.NotFound, $"The nebula {nebulaId} does not exist.");
        }

        List<ResourceShare> composition = await _dbContext.NebulaCompositions.AsNoTracking()
            .Where(share => share.NebulaId == nebulaId)
            .OrderBy(share => share.Id)
            .Select(share => new ResourceShare(share.ResourceId, share.Coefficient))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The nebula is endless, so the «mass of the place» is exactly what the cycle processes: the
        // rule of the field gives the yield by the shares and stops at a full hold, and its
        // remainder is thrown away because the nebula keeps no mass.
        ResourceCatalog catalog = await _resourceCatalog.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        ExtractionResult result = FieldExtraction.Extract(
            processingMass,
            composition,
            processingMass,
            freeHold,
            catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.Weight));

        return result with { RemainingMass = 0, Depleted = false };
    }
}
