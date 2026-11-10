using Aetheris.Core.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Market.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Data;

/// <summary>
/// Tells the command <c>reconcile-seed-codes</c> which tables of the market module the starting
/// data fills and how the keys of their texts are built from the code (task 00052).
/// </summary>
public sealed class MarketSeedCodeSource : ISeedCodeSource
{
    private static readonly string[] TextColumns = ["name_key", "description_key"];

    private readonly MarketDbContext _dbContext;
    private readonly MarketUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketSeedCodeSource"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    public MarketSeedCodeSource(MarketDbContext dbContext, MarketUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public IReadOnlyList<ISeedCodeTable> Tables =>
    [
        new SeedCodeTable<MarketLevelEntity>(
            "market_level",
            MarketCodeGroups.MarketLevel,
            [.. MarketReferenceSeed.Load().MarketLevels.Select(level => level.Code)],
            TextColumns,
            code => [MarketTextKeys.Name(MarketTextKeys.MarketLevel, code), MarketTextKeys.Description(MarketTextKeys.MarketLevel, code)],
            cancellationToken => _dbContext.MarketLevels.OrderBy(row => row.Id).ToListAsync(cancellationToken),
            _unitOfWork.RunAsync,
            row => row.Id,
            row => row.Code,
            row => [row.NameKey, row.DescriptionKey],
            (row, code, keys) =>
            {
                row.Code = code;
                row.NameKey = keys[0] ?? row.NameKey;
                row.DescriptionKey = keys[1] ?? row.DescriptionKey;
            }),
    ];
}
