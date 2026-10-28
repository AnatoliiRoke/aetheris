using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Research.Data.Entities;
using Aetheris.Research.Services;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Data;

/// <summary>
/// Fills the technology tables of a fresh database with the starting data (task 00012,
/// section 2.7).
/// </summary>
/// <remarks>
/// <para>
/// The starting data lives in the embedded file <c>Data/research-reference-seed.json</c>: every
/// technology with the start value and the growth of its time, its costs and its effects. The
/// seed turns them into one row per level, <c>base × growth^(level − 1) + step × (level − 1)</c>,
/// so the database holds a plain table that a designer or the simulator changes later.
/// </para>
/// <para>
/// Every technology has a neutral code and its texts in every language (task 00022). The seed
/// runs only while the technology table is empty, so it never overwrites a change.
/// </para>
/// </remarks>
public static class ResearchReferenceSeed
{
    /// <summary>
    /// The name of the embedded starting data.
    /// </summary>
    public const string ResourceName = "Aetheris.Research.reference-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static ResearchSeedData Load()
    {
        using Stream stream = typeof(ResearchReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        return JsonSerializer.Deserialize<ResearchSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");
    }

    /// <summary>
    /// Gets the value of a growth formula at a level.
    /// </summary>
    /// <param name="formula">The formula.</param>
    /// <param name="level">The level, from 1.</param>
    /// <returns>The value.</returns>
    public static long ValueAt(SeedGrowth formula, int level)
    {
        ArgumentNullException.ThrowIfNull(formula);

        int steps = level - 1;

        return (long)Math.Round((formula.Base * Math.Pow(formula.Growth, steps)) + (formula.Step * steps), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Gets the value of a growth formula of a multiplier at a level.
    /// </summary>
    /// <param name="formula">The formula.</param>
    /// <param name="level">The level, from 1.</param>
    /// <returns>The value.</returns>
    public static decimal MultiplierAt(SeedGrowth formula, int level)
    {
        ArgumentNullException.ThrowIfNull(formula);

        int steps = level - 1;

        return (decimal)(formula.Base * Math.Pow(formula.Growth, steps) + (formula.Step * steps));
    }

    /// <summary>
    /// Writes the starting data when the technology table is empty.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(
        ResearchDbContext dbContext,
        ResourceCatalog resources,
        ILocalizationTexts texts,
        ICodeSequence codes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        ResearchSeedData seed = Load();

        // The texts of the branches arrive with the module, even when the technologies are
        // already there, so a branch added later always has its name.
        await texts.AddMissingAsync(
            [
                .. seed.Branches.SelectMany(branch => SeedTexts.Entries(ResearchBranchTextKeys.Name(branch.Branch), branch.Name)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        if (resources.Resources.Count == 0 || await dbContext.Technologies.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal);

        Dictionary<string, TechnologyEntity> technologies = seed.Technologies.ToDictionary(
            technology => technology.Code,
            technology => new TechnologyEntity
            {
                Code = technology.Code,
                NameKey = ResearchTextKeys.Name(technology.Code),
                DescriptionKey = ResearchTextKeys.Description(technology.Code),
                Branch = technology.Branch,
                SortOrder = technology.SortOrder,
                MaxLevel = technology.MaxLevel,
                ResearchCenterLevel = technology.ResearchCenterLevel,
                LevelIds =
                [
                    .. Enumerable.Range(1, technology.MaxLevel).Select(level => new TechnologyLevelEntity
                    {
                        Level = level,
                        DurationSeconds = ValueAt(technology.DurationSeconds, level),
                        CostIds = [.. technology.Costs.Select(cost => new TechnologyLevelCostEntity { ResourceTypeId = resourceIds[cost.Resource], Amount = ValueAt(cost.Growth, level) })],
                        EffectIds =
                        [
                            .. technology.Effects.Select(effect => new TechnologyLevelEffectEntity
                            {
                                EffectType = effect.Type,
                                UnlockKind = effect.UnlockKind,
                                UnlockTargetId = effect.UnlockTargetId,
                                Value = MultiplierAt(effect.Growth ?? new SeedGrowth { Base = 1 }, level),
                            }),
                        ],
                    }),
                ],
            },
            StringComparer.Ordinal);

        dbContext.Technologies.AddRange(technologies.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. seed.Technologies.SelectMany(technology => SeedTexts.Entries(ResearchTextKeys.Name(technology.Code), technology.Name)),
                .. seed.Technologies.SelectMany(technology => SeedTexts.Entries(ResearchTextKeys.Description(technology.Code), technology.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await WorldReferenceSeed.RaiseAsync(codes, ResearchTextKeys.CodeGroup, seed.Technologies.Select(technology => technology.Code), cancellationToken).ConfigureAwait(false);

        return true;
    }
}

/// <summary>
/// The neutral codes and the localisation keys of the technologies (task 00012, decision 8):
/// <c>tech_1</c>, <c>research.technology.tech_1.name</c>.
/// </summary>
public static class ResearchTextKeys
{
    /// <summary>The group of the neutral codes of the technologies.</summary>
    public const string CodeGroup = "tech";

    /// <summary>The prefix of the keys of the technologies.</summary>
    public const string Technology = "research.technology";

    /// <summary>
    /// Builds the key of the name of a technology.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string Name(string code) => WorldTextKeys.Name(Technology, code);

    /// <summary>
    /// Builds the key of the description of a technology.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string Description(string code) => WorldTextKeys.Description(Technology, code);
}

/// <summary>The starting data of the technology tables.</summary>
/// <param name="LevelCount">The number of levels written for every technology.</param>
/// <param name="Branches">The branches of the tree.</param>
/// <param name="Technologies">The technologies.</param>
public sealed record ResearchSeedData(
    int LevelCount,
    IReadOnlyList<SeedBranch> Branches,
    IReadOnlyList<SeedTechnology> Technologies);

/// <summary>A branch of the tree of the starting data.</summary>
/// <param name="Branch">The logical code of the branch.</param>
/// <param name="Name">The name by language.</param>
public sealed record SeedBranch(string Branch, IReadOnlyDictionary<string, string> Name);

/// <summary>A technology of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="Branch">The branch of the tree.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="MaxLevel">The largest level of the technology.</param>
/// <param name="ResearchCenterLevel">The level of the science centre the research needs.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
/// <param name="DurationSeconds">The growth of the time of the research.</param>
/// <param name="Costs">The growth of the costs.</param>
/// <param name="Effects">The growth of the effects.</param>
public sealed record SeedTechnology(
    string Code,
    string Branch,
    int SortOrder,
    int MaxLevel,
    int ResearchCenterLevel,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    SeedGrowth DurationSeconds,
    IReadOnlyList<SeedTechnologyCost> Costs,
    IReadOnlyList<SeedTechnologyEffect> Effects);

/// <summary>The cost of a level of a technology of the starting data.</summary>
/// <param name="Resource">The neutral code of the resource.</param>
/// <param name="Base">The amount at the first level.</param>
/// <param name="Growth">The multiplier per level.</param>
public sealed record SeedTechnologyCost(string Resource, SeedGrowth Growth);

/// <summary>An effect of a level of a technology of the starting data.</summary>
/// <param name="Type">The kind of the effect.</param>
/// <param name="UnlockKind">The kind of the subject the effect opens.</param>
/// <param name="UnlockTargetId">The subject the effect opens.</param>
/// <param name="Base">The value at the first level.</param>
/// <param name="Growth">The multiplier per level.</param>
/// <param name="Step">The amount added per level.</param>
public sealed record SeedTechnologyEffect(
    string Type,
    string? UnlockKind = null,
    int? UnlockTargetId = null,
    SeedGrowth? Growth = null);

/// <summary>The growth of a value with the level.</summary>
/// <remarks>The value at a level is <c>base × growth^(level − 1) + step × (level − 1)</c>.</remarks>
public record SeedGrowth
{
    /// <summary>Gets the value at the first level.</summary>
    public double Base { get; init; }

    /// <summary>Gets the multiplier per level.</summary>
    public double Growth { get; init; }

    /// <summary>Gets the amount added per level.</summary>
    public double Step { get; init; }
}
