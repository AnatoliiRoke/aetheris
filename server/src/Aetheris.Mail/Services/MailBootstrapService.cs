using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Text;
using Aetheris.Core.Time;
using Aetheris.Mail.Data;
using Aetheris.Mail.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Mail.Services;

/// <summary>
/// Writes the categories of the letters and their texts, once, from the seed of the categories
/// (task 00060).
/// </summary>
/// <remarks>
/// <para>
/// A category that is already there is left as it is: an administrator may have renamed it or
/// archived it, and the seed does not touch it. A category that was removed is written again on
/// the next start, because the letters of the old games still name it.
/// </para>
/// <para>
/// The texts of the categories join the table of the texts of the game, so the client reads the
/// name of a category from the same pack as the names of the game data
/// (documentation/tech/09, section 4).
/// </para>
/// </remarks>
public sealed class MailBootstrapService
{
    private const string SeedFileName = "Aetheris.Mail.mail-categories.json";

    private readonly MailDbContext _dbContext;
    private readonly ILocalizationTexts _texts;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="texts">The texts of the game.</param>
    /// <param name="clock">The clock of the game.</param>
    public MailBootstrapService(MailDbContext dbContext, ILocalizationTexts texts, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _texts = texts;
        _clock = clock;
    }

    /// <summary>
    /// Adds the missing rows of the categories and their texts. A repeated call changes nothing
    /// that is already there.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many categories were added, and how many texts were written.</returns>
    public async Task<MailBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default)
    {
        CategorySeed seed = Load();
        string[] codes = [.. seed.Categories.Select(category => category.Code)];
        Dictionary<string, MailCategoryEntity> existing = await _dbContext.MailCategories.AsNoTracking()
            .Where(category => codes.Contains(category.Code))
            .ToDictionaryAsync(category => category.Code, cancellationToken)
            .ConfigureAwait(false);

        int added = 0;

        foreach (CategoryEntry entry in seed.Categories)
        {
            if (existing.ContainsKey(entry.Code))
            {
                continue;
            }

            _dbContext.MailCategories.Add(new MailCategoryEntity
            {
                Code = entry.Code,
                NameKey = $"mail.category.{entry.Code}.name",
                DescriptionKey = $"mail.category.{entry.Code}.description",
                ImageKey = entry.ImageKey,
                SortOrder = entry.SortOrder,
                IsVisible = true,
                IsArchived = false,
                CreatedAt = _clock.GameNow,
            });

            added++;
        }

        if (added > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        List<LocalizationTextEntry> texts = [];

        foreach (CategoryEntry entry in seed.Categories)
        {
            texts.Add(new LocalizationTextEntry($"mail.category.{entry.Code}.name", "uk", entry.Name.Uk));
            texts.Add(new LocalizationTextEntry($"mail.category.{entry.Code}.name", "en", entry.Name.En));
            texts.Add(new LocalizationTextEntry($"mail.category.{entry.Code}.description", "uk", entry.Description.Uk));
            texts.Add(new LocalizationTextEntry($"mail.category.{entry.Code}.description", "en", entry.Description.En));
        }

        int written = await _texts.AddMissingAsync(texts, "seed", cancellationToken).ConfigureAwait(false);

        return new MailBootstrapResult(added, written);
    }

    private static CategorySeed Load()
    {
        using Stream stream = typeof(MailBootstrapService).Assembly.GetManifestResourceStream(SeedFileName)
            ?? throw new InvalidOperationException($"The assembly has no file '{SeedFileName}'.");

        return JsonSerializer.Deserialize<CategorySeed>(stream)
            ?? throw new InvalidOperationException($"The file '{SeedFileName}' is empty.");
    }

    private sealed class CategorySeed
    {
        [JsonPropertyName("categories")]
        public List<CategoryEntry> Categories { get; set; } = [];
    }

    private sealed class CategoryEntry
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("sort_order")]
        public int SortOrder { get; set; }

        [JsonPropertyName("image_key")]
        public string? ImageKey { get; set; }

        [JsonPropertyName("name")]
        public TextEntry Name { get; set; } = new();

        [JsonPropertyName("description")]
        public TextEntry Description { get; set; } = new();
    }

    private sealed class TextEntry
    {
        [JsonPropertyName("uk")]
        public string Uk { get; set; } = string.Empty;

        [JsonPropertyName("en")]
        public string En { get; set; } = string.Empty;
    }
}

/// <summary>
/// What the preparation of the mail wrote (task 00060).
/// </summary>
/// <param name="CategoriesAdded">How many categories were added.</param>
/// <param name="TextsWritten">How many texts were written.</param>
public sealed record MailBootstrapResult(int CategoriesAdded, int TextsWritten);
