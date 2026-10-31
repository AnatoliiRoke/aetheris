using System.Text.Json;
using Aetheris.Core.Text;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Data;

/// <summary>
/// The starting data of the market module: the levels of the market and the reference prices
/// (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// Every row takes the next code of the counter of the server, and the keys of its texts are
/// built from that code (documentation/tech/09, sections 3.2 and 4.1). The seed writes only
/// while the tables are empty, so it never overwrites a change of the admin panel
/// (task 00023, decision 7).
/// </remarks>
public static class MarketReferenceSeed
{
    /// <summary>The name of the embedded starting data.</summary>
    public const string ResourceName = "Aetheris.Market.reference-seed.json";

    /// <summary>The author of the texts of the starting data.</summary>
    public const string SeedAuthor = "market_seed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static MarketSeedData Load()
    {
        using Stream stream = typeof(MarketReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        return JsonSerializer.Deserialize<MarketSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");
    }

    /// <summary>
    /// Writes the starting data into the tables of the module.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether anything was written.</returns>
    public static async Task<bool> SeedAsync(
        MarketDbContext dbContext,
        ILocalizationTexts texts,
        Core.Data.ICodeSequence codes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        if (await dbContext.MarketLevels.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            // The tables are not empty, so the starting data is not written again.
            return false;
        }

        MarketSeedData seed = Load();

        foreach (SeedMarketLevel level in seed.MarketLevels)
        {
            string code = await codes.NextAsync(MarketCodeGroups.MarketLevel, cancellationToken).ConfigureAwait(false);

            MarketLevelEntity entity = new()
            {
                Code = code,
                Visibility = level.Visibility,
                CommissionPercent = level.CommissionPercent,
                IsActive = level.IsActive,
                SortOrder = level.SortOrder,
                NameKey = MarketTextKeys.Name(MarketTextKeys.MarketLevel, code),
                DescriptionKey = MarketTextKeys.Description(MarketTextKeys.MarketLevel, code),
            };

            dbContext.MarketLevels.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await texts.AddMissingAsync(
                [
                    .. MarketSeedTexts.Entries(entity.NameKey, level.Name),
                    .. MarketSeedTexts.Entries(entity.DescriptionKey, level.Description),
                ],
                SeedAuthor,
                cancellationToken).ConfigureAwait(false);
        }

        await codes.RaiseAsync(MarketCodeGroups.MarketLevel, seed.MarketLevels.Count, cancellationToken).ConfigureAwait(false);

        return true;
    }
}

/// <summary>The starting data of the market module.</summary>
/// <param name="MarketLevels">The levels of the market.</param>
/// <param name="ReferencePrices">The reference prices of the items.</param>
public sealed record MarketSeedData(
    IReadOnlyList<SeedMarketLevel> MarketLevels,
    IReadOnlyList<SeedReferencePrice> ReferencePrices);

/// <summary>A level of the market of the starting data.</summary>
/// <param name="Code">The neutral code of the level in the file.</param>
/// <param name="Visibility">The scope of the level.</param>
/// <param name="CommissionPercent">The commission of the level in per cent.</param>
/// <param name="IsActive">Whether the level takes lots.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name of the level.</param>
/// <param name="Description">The description of the level.</param>
public sealed record SeedMarketLevel(
    string Code,
    string Visibility,
    decimal CommissionPercent,
    bool IsActive,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description);

/// <summary>A reference price of the starting data.</summary>
/// <param name="Resource">The neutral code of the resource, or <see langword="null"/>.</param>
/// <param name="Part">The neutral code of the part, or <see langword="null"/>.</param>
/// <param name="Price">The price of one unit of the item.</param>
/// <param name="SortOrder">The order in the lists.</param>
public sealed record SeedReferencePrice(string? Resource, string? Part, long Price, int SortOrder);

/// <summary>The texts of the starting data of the market module.</summary>
public static class MarketSeedTexts
{
    /// <summary>
    /// Builds the texts of one key.
    /// </summary>
    /// <param name="key">The key, or <see langword="null"/> when the row has no such text.</param>
    /// <param name="texts">The text by language, or <see langword="null"/>.</param>
    /// <returns>The texts.</returns>
    public static IEnumerable<LocalizationTextEntry> Entries(string? key, IReadOnlyDictionary<string, string>? texts) =>
        key is null || texts is null
            ? []
            : texts.Select(text => new LocalizationTextEntry(key, text.Key, text.Value));
}
