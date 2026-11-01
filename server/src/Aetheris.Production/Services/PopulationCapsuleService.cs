using Aetheris.Core.Errors;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Requirements;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Production.Services;

/// <summary>
/// The use of a capsule of inhabitants (task 00031): one capsule leaves the storage of the parts
/// of a body and its inhabitants join the population of the body.
/// </summary>
/// <remarks>
/// <para>
/// A capsule is a part whose attribute <see cref="PartAttributeKeys.PopulationCapsule"/> holds
/// the number of its inhabitants, so the number is data and not a constant of the code. The
/// capsule is spent for good. The inhabitants are given the way a returning crew is given: a
/// population above the largest population is not refused but dies down, so a capsule used on a
/// full body is wasted and the player is told so before in the client.
/// </para>
/// <para>
/// The storage of the parts is locked first and the population after it, as everywhere in the
/// game, so two uses never take the same capsule.
/// </para>
/// </remarks>
public sealed class PopulationCapsuleService
{
    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly IRequirementsModule _requirements;
    private readonly PartStockService _stock;
    private readonly IWorldModule _world;
    private readonly IEconomyModule _economy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopulationCapsuleService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="stock">The storage of the parts.</param>
    /// <param name="world">The world, for the owner and the sector of the body.</param>
    /// <param name="economy">The economy, for the population of the body.</param>
    public PopulationCapsuleService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        ProductionCatalogCache catalogCache,
        IRequirementsModule requirements,
        PartStockService stock,
        IWorldModule world,
        IEconomyModule economy)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(economy);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _requirements = requirements;
        _stock = stock;
        _world = world;
        _economy = economy;
    }

    /// <summary>
    /// Uses one capsule of a body: takes it from the storage and gives its inhabitants to the
    /// population of the body.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="partTypeId">The kind of the capsule.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many inhabitants the capsule gave and the population of the body after that.</returns>
    /// <exception cref="GameException">The body is not a colony of the player, the part is not a capsule, or the body has no such capsule.</exception>
    public Task<CapsuleUseDto> UseAsync(string playerId, long planetId, int partTypeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

                if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
                {
                    throw new GameException(ProductionErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
                }

                ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
                PartTypeInfo? part = catalog.FindPart(partTypeId);
                long inhabitants = part is not null && part.Attributes.TryGetValue(PartAttributeKeys.PopulationCapsule, out PartAttributeInfo? mark)
                    ? (long)(mark.NumberValue ?? 0)
                    : 0;

                if (inhabitants <= 0)
                {
                    throw new GameException(ProductionErrorCodes.NotACapsule, "The part is not a capsule of inhabitants.");
                }

                await _stock.LockAsync(planetId, facts.SectorId, cancellationToken).ConfigureAwait(false);
                _ = await _stock.TakeStackAsync(planetId, partTypeId, PartCondition.Full, 1, cancellationToken).ConfigureAwait(false);
                await _economy.GivePopulationAsync(playerId, planetId, inhabitants, cancellationToken).ConfigureAwait(false);

                PopulationFacts? population = await _economy.GetPopulationAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                return new CapsuleUseDto(planetId, partTypeId, inhabitants, population?.Amount ?? 0);
            },
            cancellationToken);
    }
}
