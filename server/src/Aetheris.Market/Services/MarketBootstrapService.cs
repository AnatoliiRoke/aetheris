using Aetheris.Core.Text;
using Aetheris.Market.Data;

namespace Aetheris.Market.Services;

/// <summary>
/// Prepares the module: fills the tables of the market with the starting data (task 00024,
/// section 4.1).
/// </summary>
/// <remarks>
/// Every step changes nothing on a repeated start. The seed runs only while the tables are
/// empty, so it never overwrites a change of the admin panel (task 00023, decision 7).
/// </remarks>
public sealed class MarketBootstrapService
{
    private readonly MarketDbContext _dbContext;
    private readonly ILocalizationTexts _texts;
    private readonly Core.Data.ICodeSequence _codes;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    public MarketBootstrapService(
        MarketDbContext dbContext,
        ILocalizationTexts texts,
        Core.Data.ICodeSequence codes)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        _dbContext = dbContext;
        _texts = texts;
        _codes = codes;
    }

    /// <summary>
    /// Runs the steps.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    public async Task<MarketBootstrapResult> EnsureAsync(CancellationToken cancellationToken)
    {
        bool seeded = await MarketReferenceSeed
            .SeedAsync(_dbContext, _texts, _codes, cancellationToken)
            .ConfigureAwait(false);

        return new MarketBootstrapResult(seeded);
    }
}

/// <summary>
/// What the preparation of the module did (task 00024).
/// </summary>
/// <param name="Seeded">Whether the starting data was written.</param>
public sealed record MarketBootstrapResult(bool Seeded);
