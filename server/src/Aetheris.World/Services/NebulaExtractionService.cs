using Aetheris.Core.Errors;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Extraction;
using Aetheris.World.Generation;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The extraction from a nebula (task 00050).
/// </summary>
/// <remarks>
/// A nebula has a mass and a composition, like a field has. The extractors of ether and of matter
/// take a part of the mass every cycle and the yield goes to the hold of the fleet by the shares of
/// the composition. A nebula does not run out to the end: the mass of a nebula is the mass of a
/// zone of space, and the game keeps a minimum of it.
/// </remarks>
public sealed class NebulaExtractionService
{
    /// <summary>
    /// The share of the mass of a nebula that never leaves it: a nebula is a zone of space and not
    /// a heap of ore, so it never runs out to the end.
    /// </summary>
    private const decimal RemainingShare = 0.1m;

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="NebulaExtractionService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    public NebulaExtractionService(WorldDbContext dbContext, WorldUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Processes one cycle on a nebula.
    /// </summary>
    /// <param name="nebulaId">The nebula.</param>
    /// <param name="processingMass">The mass the module processes per cycle.</param>
    /// <param name="freeHold">The free room of the hold.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The yield of the cycle.</returns>
    public Task<ExtractionResult> ExtractAsync(long nebulaId, long processingMass, long freeHold, CancellationToken cancellationToken = default) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                NebulaEntity nebula = (await _dbContext.Nebulae
                    .FromSql($"SELECT * FROM nebula WHERE id = {nebulaId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new GameException(GameErrorCodes.NotFound, $"The nebula {nebulaId} does not exist.");

                List<ResourceShare> composition = await _dbContext.NebulaCompositions.AsNoTracking()
                    .Where(share => share.NebulaId == nebulaId)
                    .OrderBy(share => share.Id)
                    .Select(share => new ResourceShare(share.ResourceId, share.Coefficient))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                // The mass of a nebula never falls below the tenth of what it was: the zone of space
                // stays, the sources of it come back.
                long floor = (long)(nebula.InitialMass * RemainingShare);
                long available = Math.Max(0, nebula.Mass - floor);
                ExtractionResult result = FieldExtraction.Extract(available, composition, processingMass, freeHold);

                nebula.Mass = nebula.Mass - result.ProcessedMass;

                return result with { RemainingMass = nebula.Mass, Depleted = false };
            },
            cancellationToken);
}
