using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Requirements;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The reads of the characteristics of the parts for the client and the simulator (task 00027,
/// section 3.2).
/// </summary>
/// <remarks>
/// The catalog of the module keeps the characteristics in memory, so the read costs no query
/// after the first one and the cache is dropped when the admin panel changes a row.
/// </remarks>
public sealed class CharacteristicQueryService
{
    private readonly ProductionDbContext _dbContext;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacteristicQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="requirements">The common requirements module, for the catalog.</param>
    public CharacteristicQueryService(
        ProductionDbContext dbContext,
        ProductionCatalogCache catalogCache,
        IRequirementsModule requirements)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(requirements);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
        _requirements = requirements;
    }

    /// <summary>
    /// Gets the characteristics of the parts with the groups that have them.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The characteristics in the order of the lists.</returns>
    public async Task<IReadOnlyList<CharacteristicDto>> ReadAsync(CancellationToken cancellationToken)
    {
        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        Dictionary<int, PartGroupInfo> groupsById = catalog.Groups.ToDictionary(group => group.Id);

        return
        [
            .. catalog.Characteristics.Select(characteristic => new CharacteristicDto(
                characteristic.Id,
                characteristic.Code,
                characteristic.NameKey,
                characteristic.DescriptionKey,
                characteristic.UnitKey,
                characteristic.ImageKey,
                characteristic.SortOrder,
                characteristic.IsArchived,
                [
                    .. catalog.Groups
                        .Where(group => group.Characteristics.Any(link => link.CharacteristicId == characteristic.Id))
                        .Select(group =>
                        {
                            PartGroupCharacteristicInfo link = group.Characteristics.First(entry => entry.CharacteristicId == characteristic.Id);

                            return new CharacteristicGroupDto(
                                group.Id,
                                group.Code,
                                group.NameKey,
                                link.SortOrder,
                                link.IsRequired);
                        })
                        .OrderBy(group => group.SortOrder)
                        .ThenBy(group => group.PartGroupId),
                ])),
        ];
    }

    /// <summary>
    /// Gets the weight and the characteristics of every part.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The parts in the order of the lists.</returns>
    public async Task<IReadOnlyList<PartCharacteristicsDto>> ReadPartsAsync(CancellationToken cancellationToken)
    {
        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        return
        [
            .. catalog.Parts.Select(part => new PartCharacteristicsDto(
                part.Id,
                part.Code,
                part.NameKey,
                part.PartGroupId,
                part.Weight,
                [
                    .. part.Characteristics.Values.Select(value => new PartCharacteristicValueDto(
                        value.CharacteristicId,
                        value.NumberValue,
                        value.Levels)),
                ])),
        ];
    }
}
