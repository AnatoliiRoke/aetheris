using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Numbers;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
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
/// Every technology and every branch has a neutral code and its texts in every language
/// (task 00022, task 00023). The seed adds the technologies that are missing by their codes
/// (task 00015) and does not rewrite their costs. It does put every starting technology into
/// the branch and the order the starting data names, renames those four buttons, and archives
/// the two old buttons the migration left behind (<c>branch_5</c>, <c>branch_6</c>).
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

        ResearchSeedData seed = JsonSerializer.Deserialize<ResearchSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");

        // The code of the file is the code of the row (task 00052): every row has one, in the
        // form the keys of the texts allow, and no two rows of a kind share it.
        SeedCodeRules.Validate(seed.Branches.Select(branch => branch.Code), "technology branch");
        SeedCodeRules.Validate(seed.Technologies.Select(technology => technology.Code), "technology");

        return seed;
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

        // A listed cost or time is already a whole number for every level: the prices balanced by
        // the steps of the game do not follow one multiplier
        // (documentation/references/balance/tsiny-budivel-i-nauk.html). A level past the end of the
        // list keeps the last value.
        if (formula.Values is { Count: > 0 } values)
        {
            int index = Math.Clamp(level, 1, values.Count) - 1;

            return values[index];
        }

        int steps = level - 1;

        double value = (formula.Base * Math.Pow(formula.Growth, steps)) + (formula.Step * steps);

        return (long)Math.Round(RoundNumbers.ToSignificant(value, formula.Significant), MidpointRounding.AwayFromZero);
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
    /// Writes the technologies of the starting data that the database does not have.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="planets">The bodies module, for the buildings a technology opens.</param>
    /// <param name="requirements">The common requirements module, for the conditions of the technologies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(
        ResearchDbContext dbContext,
        ResourceCatalog resources,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IPlanetsModule planets,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(requirements);

        ResearchSeedData seed = Load();

        // The branches are rows of their own table (task 00023, decision 8); the migration that
        // made the table wrote the branches of an existing database, and a fresh one gets them
        // here. Their texts arrive with the module and never overwrite a change.
        if (!await dbContext.TechnologyBranches.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            dbContext.TechnologyBranches.AddRange(seed.Branches.Select(branch => new TechnologyBranchEntity
            {
                Code = branch.Code,
                NameKey = ResearchBranchTextKeys.Name(branch.Code),
                DescriptionKey = ResearchBranchTextKeys.Description(branch.Code),
                SortOrder = branch.SortOrder,
            }));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await texts.AddMissingAsync(
            [
                .. seed.Branches.SelectMany(branch => SeedTexts.Entries(ResearchBranchTextKeys.Name(branch.Code), branch.Name)),
                .. seed.Branches.SelectMany(branch => SeedTexts.Entries(ResearchBranchTextKeys.Description(branch.Code), branch.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await WorldReferenceSeed.RaiseAsync(
            codes,
            ResearchBranchTextKeys.CodeGroup,
            await dbContext.TechnologyBranches.Select(branch => branch.Code).ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (resources.Resources.Count == 0)
        {
            return false;
        }

        // The seed writes only the technologies that are missing, by their codes, so a database
        // made before a technology joined the starting data still gets it, with its levels and
        // effects. A technology that is already there is not touched, so what an administrator
        // changed stays. A technology whose branch is not in the database is left out: the branch
        // is not the business of this step.
        HashSet<string> startingCodes = [.. seed.Technologies.Select(technology => technology.Code)];

        HashSet<string> existingCodes = [.. await dbContext.Technologies.AsNoTracking()
            .Where(technology => startingCodes.Contains(technology.Code))
            .Select(technology => technology.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        Dictionary<string, int> branchIds = await dbContext.TechnologyBranches
            .ToDictionaryAsync(branch => branch.Code, branch => branch.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        List<SeedTechnology> missingTechnologies =
        [
            .. seed.Technologies.Where(technology => !existingCodes.Contains(technology.Code) && branchIds.ContainsKey(technology.Branch)),
        ];

        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal);

        // The subjects a technology opens are named by their neutral code in the starting data,
        // so the codes are looked up before the rows are written.
        Dictionary<string, int> unlockIds = [];

        foreach (string code in missingTechnologies
            .SelectMany(technology => technology.Effects)
            .Where(effect => effect.UnlockKind == TechnologyUnlockKinds.BuildingType)
            .Select(effect => effect.UnlockTarget)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal))
        {
            int? buildingTypeId = await planets.FindBuildingTypeIdAsync(code, cancellationToken).ConfigureAwait(false);

            if (buildingTypeId is null)
            {
                throw new InvalidOperationException(
                    $"The starting data opens the building '{code}', which the bodies module does not have.");
            }

            unlockIds[code] = buildingTypeId.Value;
        }

        Dictionary<string, TechnologyEntity> technologies = missingTechnologies.ToDictionary(
            technology => technology.Code,
            technology => new TechnologyEntity
            {
                Code = technology.Code,
                NameKey = ResearchTextKeys.Name(technology.Code),
                DescriptionKey = ResearchTextKeys.Description(technology.Code),
                BranchId = branchIds[technology.Branch],
                SortOrder = technology.SortOrder,
                MaxLevel = technology.MaxLevel,
                ResearchCenterLevel = technology.ResearchCenterLevel,
                ImageKey = technology.ImageKey,
                LevelIds =
                [
                    .. Enumerable.Range(1, technology.MaxLevel).Select(level => new TechnologyLevelEntity
                    {
                        Level = level,
                        DurationSeconds = ValueAt(technology.DurationSeconds, level),
                        CostIds = [.. technology.Costs.Where(cost => level >= cost.FromLevel).Select(cost => new TechnologyLevelCostEntity { ResourceTypeId = resourceIds[cost.Resource], Amount = ValueAt(cost.Growth, level - cost.FromLevel + 1) })],
                        EffectIds =
                        [
                            .. technology.Effects.Where(effect => effect.Level is null || effect.Level == level).Select(effect => new TechnologyLevelEffectEntity
                            {
                                EffectType = effect.Type,
                                UnlockKind = effect.UnlockKind,
                                UnlockTargetId = unlockIds.TryGetValue(effect.UnlockTarget ?? string.Empty, out int unlockId) ? unlockId : null,
                                Value = MultiplierAt(effect.Growth ?? new SeedGrowth { Base = 1 }, level),
                            }),
                        ],
                    }),
                ],
            },
            StringComparer.Ordinal);

        dbContext.Technologies.AddRange(technologies.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // An unlock effect names what the technology opens. It does not close the building at
        // level 1: the chosen file leaves level 1 open, and the requirement rows below say the
        // level that actually waits for the technology (task 00028).
        if (missingTechnologies.Count > 0)
        {
            await WriteInsertedRequirementsAsync(missingTechnologies, planets, requirements, dbContext, cancellationToken).ConfigureAwait(false);
        }

        bool effectsAdded = await AddMissingEffectsAsync(dbContext, seed, existingCodes, cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. seed.Technologies.SelectMany(technology => SeedTexts.Entries(ResearchTextKeys.Name(technology.Code), technology.Name)),
                .. seed.Technologies.SelectMany(technology => SeedTexts.Entries(ResearchTextKeys.Description(technology.Code), technology.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await WorldReferenceSeed.RaiseAsync(codes, ResearchTextKeys.CodeGroup, seed.Technologies.Select(technology => technology.Code), cancellationToken).ConfigureAwait(false);

        bool groupsAligned = await AlignGroupsAsync(dbContext, texts, seed, cancellationToken).ConfigureAwait(false);

        return missingTechnologies.Count > 0 || groupsAligned || effectsAdded;
    }

    /// <summary>
    /// Adds to the technologies that are already in the database the effects of the starting data
    /// they have no row of at all (task 00073: the limit of the planets of the colonial
    /// administration joined the starting data after the databases were made).
    /// </summary>
    /// <remarks>
    /// Only an effect type a technology has no row of is written, for every level the starting data
    /// gives it, so an effect an administrator changed or removed on some levels stays as it is. The
    /// effects that open a subject are left out: they are written with the technology itself.
    /// </remarks>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="seed">The starting data.</param>
    /// <param name="existingCodes">The codes of the technologies of the starting data that are in the database.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether a row was written.</returns>
    private static async Task<bool> AddMissingEffectsAsync(
        ResearchDbContext dbContext,
        ResearchSeedData seed,
        IReadOnlySet<string> existingCodes,
        CancellationToken cancellationToken)
    {
        List<SeedTechnology> technologies =
        [
            .. seed.Technologies.Where(technology => existingCodes.Contains(technology.Code) && technology.Effects.Any(effect => effect.UnlockKind is null)),
        ];

        if (technologies.Count == 0)
        {
            return false;
        }

        string[] codes = [.. technologies.Select(technology => technology.Code)];

        // The levels of the technologies and the effect types they already have, read at once.
        var levels = await (
                from level in dbContext.TechnologyLevels.AsNoTracking()
                join technology in dbContext.Technologies.AsNoTracking() on level.TechnologyId equals technology.Id
                where codes.Contains(technology.Code)
                select new { level.Id, level.Level, technology.Code })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var present = await (
                from effect in dbContext.TechnologyLevelEffects.AsNoTracking()
                join level in dbContext.TechnologyLevels.AsNoTracking() on effect.TechnologyLevelId equals level.Id
                join technology in dbContext.Technologies.AsNoTracking() on level.TechnologyId equals technology.Id
                where codes.Contains(technology.Code)
                select new { technology.Code, effect.EffectType })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<(string Code, string EffectType)> known = [.. present.Select(row => (row.Code, row.EffectType))];
        bool added = false;

        foreach (SeedTechnology technology in technologies)
        {
            foreach (IGrouping<string, SeedTechnologyEffect> type in technology.Effects.Where(effect => effect.UnlockKind is null).GroupBy(effect => effect.Type))
            {
                if (known.Contains((technology.Code, type.Key)))
                {
                    continue;
                }

                foreach (var level in levels.Where(entry => entry.Code == technology.Code))
                {
                    foreach (SeedTechnologyEffect effect in type.Where(effect => effect.Level is null || effect.Level == level.Level))
                    {
                        dbContext.TechnologyLevelEffects.Add(new TechnologyLevelEffectEntity
                        {
                            TechnologyLevelId = level.Id,
                            EffectType = effect.Type,
                            Value = MultiplierAt(effect.Growth ?? new SeedGrowth { Base = 1 }, level.Level),
                        });
                        added = true;
                    }
                }
            }
        }

        if (added)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return added;
    }

    /// <summary>
    /// Adds the branches of the starting data that the database does not have yet, with their
    /// texts. A branch, a technology or a text that exists is never changed: the seed never
    /// overwrites what the administrator may have edited (documentation/tech/09, section 10a; code
    /// audit of 04.11.2026). The one-time move of the technologies into the branches of
    /// 02.11.2026 has run on every database started since then, and a fresh database gets the
    /// branches and the technologies in their places from the seed itself.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="seed">The starting data.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a branch was added.</returns>
    private static async Task<bool> AlignGroupsAsync(
        ResearchDbContext dbContext,
        ILocalizationTexts texts,
        ResearchSeedData seed,
        CancellationToken cancellationToken)
    {
        HashSet<string> known = [.. await dbContext.TechnologyBranches.AsNoTracking()
            .Select(branch => branch.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        List<SeedBranch> missing = [.. seed.Branches.Where(branch => !known.Contains(branch.Code))];

        if (missing.Count == 0)
        {
            return false;
        }

        dbContext.TechnologyBranches.AddRange(missing.Select(branch => new TechnologyBranchEntity
        {
            Code = branch.Code,
            NameKey = ResearchBranchTextKeys.Name(branch.Code),
            DescriptionKey = ResearchBranchTextKeys.Description(branch.Code),
            SortOrder = branch.SortOrder,
        }));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. missing.SelectMany(branch => SeedTexts.Entries(ResearchBranchTextKeys.Name(branch.Code), branch.Name)),
                .. missing.SelectMany(branch => SeedTexts.Entries(ResearchBranchTextKeys.Description(branch.Code), branch.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Writes the requirements of the technologies inserted on this run, and the technology
    /// requirements of buildings whose target was inserted on this run.
    /// </summary>
    private static async Task WriteInsertedRequirementsAsync(
        List<SeedTechnology> missingTechnologies,
        IPlanetsModule planets,
        IRequirementsModule requirements,
        ResearchDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Dictionary<string, int> technologyIds = await dbContext.Technologies.AsNoTracking()
            .ToDictionaryAsync(technology => technology.Code, technology => technology.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> insertedCodes = missingTechnologies.Select(technology => technology.Code).ToHashSet(StringComparer.Ordinal);

        foreach (SeedTechnology seedTechnology in missingTechnologies)
        {
            if (seedTechnology.Requirements is not { Count: > 0 })
            {
                continue;
            }

            foreach (IGrouping<int, Aetheris.Planets.SeedRequirement> group in seedTechnology.Requirements.GroupBy(requirement => requirement.SubjectLevel ?? 1))
            {
                List<RequirementDefinition> definitions = [];

                foreach (Aetheris.Planets.SeedRequirement requirement in group)
                {
                    int targetId = requirement.Type switch
                    {
                        RequirementTypes.BuildingLevel => await planets.FindBuildingTypeIdAsync(requirement.Target, cancellationToken).ConfigureAwait(false)
                            ?? throw new InvalidOperationException($"The starting data requires the building '{requirement.Target}', which the bodies module does not have."),
                        RequirementTypes.TechnologyLevel => technologyIds.TryGetValue(requirement.Target, out int technologyId)
                            ? technologyId
                            : throw new InvalidOperationException($"The starting data requires the technology '{requirement.Target}', which does not exist."),
                        _ => throw new InvalidOperationException($"The starting data names the requirement type '{requirement.Type}', which it cannot resolve."),
                    };

                    definitions.Add(new RequirementDefinition(requirement.Type, targetId, requirement.TargetLevel ?? 0));
                }

                await requirements.AddAsync(
                    RequirementSubjectTypes.Technology,
                    technologyIds[seedTechnology.Code],
                    group.Key,
                    definitions,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        Aetheris.Planets.PlanetsSeedData planetsSeed = Aetheris.Planets.PlanetsReferenceSeed.Load();

        foreach (Aetheris.Planets.SeedBuildingType building in planetsSeed.BuildingTypes)
        {
            List<Aetheris.Planets.SeedRequirement> technologyRequirements =
            [
                .. building.Requirements.Where(requirement => requirement.Type == RequirementTypes.TechnologyLevel && insertedCodes.Contains(requirement.Target)),
            ];

            if (technologyRequirements.Count == 0)
            {
                continue;
            }

            int buildingTypeId = await planets.FindBuildingTypeIdAsync(building.Code, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The starting data requires a technology for the building '{building.Code}', which the bodies module does not have.");

            foreach (IGrouping<int, Aetheris.Planets.SeedRequirement> group in technologyRequirements.GroupBy(requirement => requirement.SubjectLevel ?? 1))
            {
                await requirements.AddAsync(
                    RequirementSubjectTypes.BuildingType,
                    buildingTypeId,
                    group.Key,
                    [
                        .. group.Select(requirement => new RequirementDefinition(
                            RequirementTypes.TechnologyLevel,
                            technologyIds[requirement.Target],
                            requirement.TargetLevel ?? 1)),
                    ],
                    cancellationToken).ConfigureAwait(false);
            }
        }
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
/// <param name="Code">The neutral code of the branch.</param>
/// <param name="SortOrder">The order of the branch in the tree.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
public sealed record SeedBranch(
    string Code,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description);

/// <summary>A technology of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="Branch">The neutral code of the branch of the tree.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="MaxLevel">The largest level of the technology.</param>
/// <param name="ResearchCenterLevel">The level of the science centre the research needs.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
/// <param name="DurationSeconds">The growth of the time of the research.</param>
/// <param name="Costs">The growth of the costs.</param>
/// <param name="Effects">The growth of the effects.</param>
/// <param name="ImageKey">The key of the picture of the technology, or <see langword="null"/> for the placeholder.</param>
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
    IReadOnlyList<SeedTechnologyEffect> Effects,
    IReadOnlyList<Aetheris.Planets.SeedRequirement>? Requirements = null,
    string? ImageKey = null);

/// <summary>The cost of a level of a technology of the starting data.</summary>
/// <param name="Resource">The neutral code of the resource.</param>
/// <param name="Base">The amount at the first level.</param>
/// <param name="Growth">The multiplier per level.</param>
public sealed record SeedTechnologyCost(string Resource, SeedGrowth Growth, int FromLevel = 1);

/// <summary>An effect of a level of a technology of the starting data.</summary>
/// <param name="Type">The kind of the effect.</param>
/// <param name="UnlockKind">The kind of the subject the effect opens.</param>
/// <param name="UnlockTarget">The neutral code of the subject the effect opens.</param>
/// <param name="Growth">The growth of the value of the effect with the level.</param>
/// <param name="Level">The only level the effect belongs to, or <see langword="null"/> for every level. An effect that opens a subject belongs to the minimal level that opens it, and that level is the requirement of the subject.</param>
public sealed record SeedTechnologyEffect(
    string Type,
    string? UnlockKind = null,
    string? UnlockTarget = null,
    SeedGrowth? Growth = null,
    int? Level = null);

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

    /// <summary>Gets the number of significant digits the value is rounded to, so the numbers read round; zero keeps the value as it is.</summary>
    public int Significant { get; init; }

    /// <summary>
    /// Gets the value of every level, from the first level the formula counts from, when it does
    /// not grow by one multiplier. When it is empty the value comes from the growth formula. Only
    /// whole values (costs and times) are listed; a multiplier of an effect keeps its formula.
    /// </summary>
    public IReadOnlyList<long>? Values { get; init; }
}
