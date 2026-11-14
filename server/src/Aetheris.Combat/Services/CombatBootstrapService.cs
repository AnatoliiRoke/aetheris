using Aetheris.Combat.Api;
using Aetheris.Combat.Data;
using Aetheris.Combat.Data.Entities;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Combat.Services;

/// <summary>
/// Fills the target rules and the behaviors that the starting data names and the database
/// does not have yet (task 00062). A row that is already there keeps its logical code.
/// </summary>
public sealed class CombatBootstrapService
{
    private readonly CombatDbContext _dbContext;
    private readonly ILocalizationTexts _texts;
    private readonly ICodeSequence _codes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    public CombatBootstrapService(CombatDbContext dbContext, ILocalizationTexts texts, ICodeSequence codes)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        _dbContext = dbContext;
        _texts = texts;
        _codes = codes;
    }

    /// <summary>
    /// Adds the missing rows and the missing texts, and raises the counters above the file.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rows are in place.</returns>
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        CombatSeedFile seed = CombatReferenceSeed.Load();

        await AddMissingAsync(_dbContext.TargetRules, seed.TargetRules, CombatTextKeys.TargetRule, cancellationToken).ConfigureAwait(false);
        await AddMissingAsync(_dbContext.Behaviors, seed.Behaviors, CombatTextKeys.Behavior, cancellationToken).ConfigureAwait(false);

        await SeedCodeRules.RaiseAsync(_codes, CombatTextKeys.TargetRuleGroup, seed.TargetRules.Select(row => row.Code), cancellationToken).ConfigureAwait(false);
        await SeedCodeRules.RaiseAsync(_codes, CombatTextKeys.BehaviorGroup, seed.Behaviors.Select(row => row.Code), cancellationToken).ConfigureAwait(false);
    }

    private async Task AddMissingAsync<TRow>(
        DbSet<TRow> rows,
        IReadOnlyList<CombatSeedRule> seedRows,
        string prefix,
        CancellationToken cancellationToken)
        where TRow : class, ICombatRuleRow, new()
    {
        HashSet<string> existing = await rows.AsNoTracking()
            .Select(row => row.Code)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (CombatSeedRule seedRow in seedRows)
        {
            if (existing.Contains(seedRow.Code))
            {
                continue;
            }

            rows.Add(new TRow
            {
                Code = seedRow.Code,
                LogicCode = seedRow.LogicCode,
                NameKey = CombatTextKeys.Name(prefix, seedRow.Code),
                DescriptionKey = CombatTextKeys.Description(prefix, seedRow.Code),
                SortOrder = seedRow.SortOrder,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        List<LocalizationTextEntry> entries = [];

        foreach (CombatSeedRule seedRow in seedRows)
        {
            string nameKey = CombatTextKeys.Name(prefix, seedRow.Code);
            string descriptionKey = CombatTextKeys.Description(prefix, seedRow.Code);

            foreach (KeyValuePair<string, string> text in seedRow.Name)
            {
                entries.Add(new LocalizationTextEntry(nameKey, text.Key, text.Value));
            }

            foreach (KeyValuePair<string, string> text in seedRow.Description)
            {
                entries.Add(new LocalizationTextEntry(descriptionKey, text.Key, text.Value));
            }
        }

        await _texts.AddMissingAsync(entries, CombatReferenceSeed.SeedAuthor, cancellationToken).ConfigureAwait(false);
    }
}
