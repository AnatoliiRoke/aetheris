using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Data;

/// <summary>
/// Fills the reference tables of a fresh database with the starting data.
/// </summary>
/// <remarks>
/// The starting data lives in the embedded file <c>Data/world-reference-seed.json</c>, not
/// in the code: resources, object types, size classes and generation rules are rows that a
/// designer changes, adds or removes (task 00004, decision 23). Every row has a neutral code
/// and its texts in every language (task 00022). The seed runs only while the reference tables
/// are empty, so it never overwrites or restores what was changed later.
/// </remarks>
public static class WorldReferenceSeed
{
    /// <summary>
    /// The name of the embedded starting data.
    /// </summary>
    public const string ResourceName = "Aetheris.World.reference-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static ReferenceSeedData Load()
    {
        using Stream stream = typeof(WorldReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        return JsonSerializer.Deserialize<ReferenceSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");
    }

    /// <summary>
    /// Writes the starting data when the reference tables are empty.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    /// <remarks>
    /// The rows of the starting data refer to each other by their neutral codes; the database
    /// links them by identifiers. The texts go to the table of texts, and the counters of the
    /// codes are raised above the codes of the starting data, so the next row created by an
    /// administrator gets a new code.
    /// </remarks>
    public static async Task<bool> SeedAsync(WorldDbContext dbContext, ILocalizationTexts texts, ICodeSequence codes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        if (await dbContext.ResourceGroups.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            // The tables are not empty, so the starting data is not written again; only the
            // resources of the starting data that this database does not have yet are added.
            return await SeedMissingResourcesAsync(dbContext, texts, codes, cancellationToken).ConfigureAwait(false);
        }

        ReferenceSeedData seed = Load();

        Dictionary<string, ResourceGroupEntity> groups = seed.ResourceGroups.ToDictionary(
            group => group.Code,
            group => new ResourceGroupEntity
            {
                Code = group.Code,
                NameKey = WorldTextKeys.Name(WorldTextKeys.ResourceGroup, group.Code),
                DescriptionKey = WorldTextKeys.Description(WorldTextKeys.ResourceGroup, group.Code),
                AllowedOnPlanet = group.AllowedOnPlanet,
            },
            StringComparer.Ordinal);

        dbContext.ResourceGroups.AddRange(groups.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, ResourceTypeEntity> resources = seed.Resources.ToDictionary(
            resource => resource.Code,
            resource => new ResourceTypeEntity
            {
                Code = resource.Code,
                NameKey = WorldTextKeys.Name(WorldTextKeys.Resource, resource.Code),
                DescriptionKey = WorldTextKeys.Description(WorldTextKeys.Resource, resource.Code),
                ImageKey = resource.ImageKey,
                ResourceGroupId = resource.Group is null ? null : groups[resource.Group].Id,
                Scope = resource.Scope,
                EarnsPopulationIncome = resource.EarnsPopulationIncome,
                Weight = resource.Weight ?? 1m,
            },
            StringComparer.Ordinal);

        dbContext.ResourceTypes.AddRange(resources.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, CelestialObjectTypeEntity> types = seed.ObjectTypes.ToDictionary(
            type => type.Code,
            type => new CelestialObjectTypeEntity
            {
                Code = type.Code,
                ObjectClass = type.ObjectClass,
                NameKey = WorldTextKeys.Name(WorldTextKeys.ObjectType, type.Code),
                DescriptionKey = WorldTextKeys.Description(WorldTextKeys.ObjectType, type.Code),
                ImageKey = type.ImageKey,
                IsColonizable = type.IsColonizable,
                IsStartDefault = type.IsStartDefault,
                GenerationWeight = type.GenerationWeight,
                MassMin = type.MassMin,
                MassMax = type.MassMax,
                HiddenResourceTypeId = type.HiddenResource is null ? null : resources[type.HiddenResource].Id,
                HiddenShareMin = type.HiddenShareMin,
                HiddenShareMax = type.HiddenShareMax,
            },
            StringComparer.Ordinal);

        dbContext.ObjectTypes.AddRange(types.Values);
        dbContext.SizeClasses.AddRange(seed.SizeClasses.Select(size => new PlanetSizeClassEntity
        {
            Code = size.Code,
            NameKey = WorldTextKeys.Name(WorldTextKeys.SizeClass, size.Code),
            DescriptionKey = WorldTextKeys.Description(WorldTextKeys.SizeClass, size.Code),
            GenerationWeight = size.GenerationWeight,
            SizeMin = size.SizeMin,
            SizeMax = size.SizeMax,
        }));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        dbContext.RuleGroups.AddRange(seed.RuleGroups.Select(group => new GenerationRuleGroupEntity
        {
            ObjectTypeId = types[group.ObjectType].Id,
            Code = group.Code,
            MinPicks = group.MinPicks,
            MaxPicks = group.MaxPicks,
            ExtraPickProbability = group.ExtraPickProbability,
            PicksAll = group.PicksAll,
            RuleIds =
            [
                .. group.Rules.Select(rule => new GenerationRuleEntity
                {
                    ResourceId = resources[rule.Resource].Id,
                    Weight = rule.Weight,
                    CoefficientMin = rule.CoefficientMin,
                    CoefficientMax = rule.CoefficientMax,
                    RichCoefficientMin = rule.RichCoefficientMin,
                    RichCoefficientMax = rule.RichCoefficientMax,
                }),
            ],
        }));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. seed.ResourceGroups.SelectMany(group => SeedTexts.Of(WorldTextKeys.ResourceGroup, group.Code, group.Name, group.Description)),
                .. seed.Resources.SelectMany(resource => SeedTexts.Of(WorldTextKeys.Resource, resource.Code, resource.Name, resource.Description)),
                .. seed.ObjectTypes.SelectMany(type => SeedTexts.Of(WorldTextKeys.ObjectType, type.Code, type.Name, type.Description)),
                .. seed.SizeClasses.SelectMany(size => SeedTexts.Of(WorldTextKeys.SizeClass, size.Code, size.Name, size.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await RaiseAsync(codes, WorldCodeGroups.ResourceGroup, seed.ResourceGroups.Select(group => group.Code), cancellationToken).ConfigureAwait(false);
        await RaiseAsync(codes, WorldCodeGroups.Resource, seed.Resources.Select(resource => resource.Code), cancellationToken).ConfigureAwait(false);
        await RaiseAsync(codes, WorldCodeGroups.ObjectType, seed.ObjectTypes.Select(type => type.Code), cancellationToken).ConfigureAwait(false);
        await RaiseAsync(codes, WorldCodeGroups.SizeClass, seed.SizeClasses.Select(size => size.Code), cancellationToken).ConfigureAwait(false);
        await RaiseAsync(codes, WorldCodeGroups.RuleGroup, seed.RuleGroups.Select(group => group.Code), cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Adds the resources of the starting data that a database does not have, by their codes.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a resource was added.</returns>
    /// <remarks>
    /// A database made before a resource joined the starting data (the credits of task 00015) still
    /// gets it, like the buildings of the bodies module do. A resource that is already there is not
    /// touched, so what an administrator changed stays; a resource whose group is not in the database
    /// is left out, since the group is not the business of this step.
    /// </remarks>
    private static async Task<bool> SeedMissingResourcesAsync(WorldDbContext dbContext, ILocalizationTexts texts, ICodeSequence codes, CancellationToken cancellationToken)
    {
        ReferenceSeedData seed = Load();
        HashSet<string> startingCodes = [.. seed.Resources.Select(resource => resource.Code)];

        HashSet<string> existingCodes = [.. await dbContext.ResourceTypes.AsNoTracking()
            .Where(resource => startingCodes.Contains(resource.Code))
            .Select(resource => resource.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

        Dictionary<string, int> groupIds = await dbContext.ResourceGroups.AsNoTracking()
            .ToDictionaryAsync(group => group.Code, group => group.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        List<SeedResource> missing =
        [
            .. seed.Resources.Where(resource =>
                !existingCodes.Contains(resource.Code)
                && (resource.Group is null || groupIds.ContainsKey(resource.Group))),
        ];

        if (missing.Count == 0)
        {
            return false;
        }

        dbContext.ResourceTypes.AddRange(missing.Select(resource => new ResourceTypeEntity
        {
            Code = resource.Code,
            NameKey = WorldTextKeys.Name(WorldTextKeys.Resource, resource.Code),
            DescriptionKey = WorldTextKeys.Description(WorldTextKeys.Resource, resource.Code),
            ImageKey = resource.ImageKey,
            ResourceGroupId = resource.Group is null ? null : groupIds[resource.Group],
            Scope = resource.Scope,
            EarnsPopulationIncome = resource.EarnsPopulationIncome,
            Weight = resource.Weight ?? 1m,
        }));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [.. missing.SelectMany(resource => SeedTexts.Of(WorldTextKeys.Resource, resource.Code, resource.Name, resource.Description))],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await RaiseAsync(codes, WorldCodeGroups.Resource, missing.Select(resource => resource.Code), cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Writes the starting races when the race table is empty.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the races were written.</returns>
    /// <remarks>
    /// The races came with task 00006, after the other reference tables, so a database that
    /// already has its resources still gets its races. Like the rest of the seed, it never
    /// overwrites what was changed later.
    /// </remarks>
    public static async Task<bool> SeedRacesAsync(WorldDbContext dbContext, ILocalizationTexts texts, ICodeSequence codes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        if (await dbContext.Races.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ReferenceSeedData seed = Load();
        IReadOnlyList<SeedRace> races = seed.Races ?? [];

        Dictionary<string, int> resourceIds = await dbContext.ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, int> typeIds = await dbContext.ObjectTypes.AsNoTracking()
            .ToDictionaryAsync(type => type.Code, type => type.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        dbContext.Races.AddRange(races.Select(race => new RaceEntity
        {
            Code = race.Code,
            NameKey = WorldTextKeys.Name(WorldTextKeys.Race, race.Code),
            DescriptionKey = WorldTextKeys.Description(WorldTextKeys.Race, race.Code),
            MoodKey = WorldTextKeys.Of(WorldTextKeys.Race, race.Code, WorldTextKeys.MoodPart),
            TraitKeys = [.. race.Traits.Select((_, index) => TraitKey(race.Code, index))],
            ImageKey = race.ImageKey,
            ColorKey = race.ColorKey,
            SortOrder = race.SortOrder,
            PreferredObjectTypeId = race.PreferredObjectType is null ? null : typeIds[race.PreferredObjectType],
            StartBuildingLevel = race.StartBuildingLevel,
            StartResourceIds =
            [
                .. race.StartResources.Select(resource => new RaceStartResourceEntity
                {
                    ResourceTypeId = resourceIds[resource.Resource],
                    Amount = resource.Amount,
                }),
            ],
        }));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. races.SelectMany(race => SeedTexts.Of(WorldTextKeys.Race, race.Code, race.Name, race.Description)),
                .. races.SelectMany(race => SeedTexts.Entries(WorldTextKeys.Of(WorldTextKeys.Race, race.Code, WorldTextKeys.MoodPart), race.Mood)),
                .. races.SelectMany(race => race.Traits.SelectMany((trait, index) => SeedTexts.Entries(TraitKey(race.Code, index), trait))),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await RaiseAsync(codes, WorldCodeGroups.Race, races.Select(race => race.Code), cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Raises the counter of a code group above the codes of the starting data.
    /// </summary>
    /// <param name="codes">The counters.</param>
    /// <param name="group">The code group.</param>
    /// <param name="seedCodes">The codes of the starting data of the group.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the counter is raised.</returns>
    public static Task RaiseAsync(ICodeSequence codes, string group, IEnumerable<string> seedCodes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(seedCodes);

        string prefix = group + "_";
        int taken = seedCodes
            .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .Select(code => int.TryParse(code.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return codes.RaiseAsync(group, taken, cancellationToken);
    }

    private static string TraitKey(string raceCode, int index) =>
        WorldTextKeys.Of(WorldTextKeys.Race, raceCode, WorldTextKeys.TraitPart + (index + 1).ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// The texts of the starting data.
/// </summary>
public static class SeedTexts
{
    /// <summary>
    /// The author of the texts of the starting data.
    /// </summary>
    public const string Author = "seed";

    /// <summary>
    /// Builds the texts of the name and the description of a row.
    /// </summary>
    /// <param name="prefix">The prefix of the keys of the kind of the row.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <param name="name">The name by language.</param>
    /// <param name="description">The description by language, if the row has one.</param>
    /// <returns>The texts.</returns>
    public static IEnumerable<LocalizationTextEntry> Of(
        string prefix,
        string code,
        IReadOnlyDictionary<string, string> name,
        IReadOnlyDictionary<string, string>? description) =>
        [
            .. Entries(WorldTextKeys.Name(prefix, code), name),
            .. Entries(WorldTextKeys.Description(prefix, code), description),
        ];

    /// <summary>
    /// Builds the texts of one key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="texts">The text by language, or <see langword="null"/>.</param>
    /// <returns>The texts.</returns>
    public static IEnumerable<LocalizationTextEntry> Entries(string key, IReadOnlyDictionary<string, string>? texts) =>
        texts is null ? [] : texts.Select(text => new LocalizationTextEntry(key, text.Key, text.Value));
}

/// <summary>The starting data of the reference tables.</summary>
/// <param name="ResourceGroups">The resource groups.</param>
/// <param name="Resources">The resource types.</param>
/// <param name="ObjectTypes">The object types.</param>
/// <param name="SizeClasses">The size classes.</param>
/// <param name="RuleGroups">The generation rule groups.</param>
/// <param name="Races">The races with their start packages.</param>
public sealed record ReferenceSeedData(
    IReadOnlyList<SeedResourceGroup> ResourceGroups,
    IReadOnlyList<SeedResource> Resources,
    IReadOnlyList<SeedObjectType> ObjectTypes,
    IReadOnlyList<SeedSizeClass> SizeClasses,
    IReadOnlyList<SeedRuleGroup> RuleGroups,
    IReadOnlyList<SeedRace>? Races = null);

/// <summary>A race of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="SortOrder">The order in the list.</param>
/// <param name="PreferredObjectType">The code of the preferred start body type, if any.</param>
/// <param name="StartBuildingLevel">The level of the start building.</param>
/// <param name="StartResources">The start resources.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
/// <param name="Mood">The mood by language.</param>
/// <param name="Traits">The strengths, each by language.</param>
/// <param name="ImageKey">The key of the picture.</param>
/// <param name="ColorKey">The key of the colour.</param>
public sealed record SeedRace(
    string Code,
    int SortOrder,
    string? PreferredObjectType,
    int StartBuildingLevel,
    IReadOnlyList<SeedRaceResource> StartResources,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    IReadOnlyDictionary<string, string>? Mood,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Traits,
    string? ImageKey,
    string? ColorKey);

/// <summary>A start resource of a race in the starting data.</summary>
/// <param name="Resource">The code of the resource.</param>
/// <param name="Amount">The amount.</param>
public sealed record SeedRaceResource(string Resource, long Amount);

/// <summary>A resource group of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="AllowedOnPlanet">Whether the group may lie on a body.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language, if any.</param>
public sealed record SeedResourceGroup(
    string Code,
    bool AllowedOnPlanet,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description = null);

/// <summary>A resource type of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="Group">The code of the group, if the resource has one.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language, if any.</param>
/// <param name="ImageKey">The key of the picture, if any.</param>
/// <param name="Scope">Where the resource lies: on a body or in the whole civilization (task 00015).</param>
/// <param name="EarnsPopulationIncome">Whether the population of a civilization brings this currency (task 00015).</param>
/// <param name="Weight">The weight of one unit of the resource in tonnes (task 00027).</param>
public sealed record SeedResource(
    string Code,
    string? Group,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description = null,
    string? ImageKey = null,
    string Scope = ResourceScopes.Planet,
    bool EarnsPopulationIncome = false,
    decimal? Weight = null);

/// <summary>An object type of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="ObjectClass">The class.</param>
/// <param name="IsColonizable">Whether a player may own the object.</param>
/// <param name="GenerationWeight">The weight within the class.</param>
/// <param name="Name">The name by language.</param>
/// <param name="MassMin">The smallest mass of a field.</param>
/// <param name="MassMax">The largest mass of a field.</param>
/// <param name="Description">The description by language, if any.</param>
/// <param name="ImageKey">The key of the picture, if any.</param>
/// <param name="IsStartDefault">Whether a newcomer without a preferred type starts on it.</param>
/// <param name="HiddenResource">The code of the resource a field hides, if any.</param>
/// <param name="HiddenShareMin">The smallest hidden share.</param>
/// <param name="HiddenShareMax">The largest hidden share.</param>
public sealed record SeedObjectType(
    string Code,
    string ObjectClass,
    bool IsColonizable,
    int GenerationWeight,
    IReadOnlyDictionary<string, string> Name,
    long? MassMin = null,
    long? MassMax = null,
    IReadOnlyDictionary<string, string>? Description = null,
    string? ImageKey = null,
    bool IsStartDefault = false,
    string? HiddenResource = null,
    decimal? HiddenShareMin = null,
    decimal? HiddenShareMax = null);

/// <summary>A size class of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="GenerationWeight">The weight.</param>
/// <param name="SizeMin">The smallest size.</param>
/// <param name="SizeMax">The largest size.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language, if any.</param>
public sealed record SeedSizeClass(
    string Code,
    int GenerationWeight,
    int SizeMin,
    int SizeMax,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description = null);

/// <summary>A generation rule group of the starting data.</summary>
/// <param name="ObjectType">The code of the object type.</param>
/// <param name="Code">The neutral code.</param>
/// <param name="MinPicks">The picks that always happen.</param>
/// <param name="MaxPicks">The largest number of picks.</param>
/// <param name="ExtraPickProbability">The probability of every further pick.</param>
/// <param name="Rules">The rules.</param>
/// <param name="PicksAll">Whether every rule is taken, whatever the picks are.</param>
public sealed record SeedRuleGroup(
    string ObjectType,
    string Code,
    int MinPicks,
    int MaxPicks,
    double ExtraPickProbability,
    IReadOnlyList<SeedRule> Rules,
    bool PicksAll = false);

/// <summary>A generation rule of the starting data.</summary>
/// <param name="Resource">The code of the resource.</param>
/// <param name="Weight">The weight.</param>
/// <param name="CoefficientMin">The smallest coefficient.</param>
/// <param name="CoefficientMax">The largest coefficient.</param>
/// <param name="RichCoefficientMin">The smallest coefficient on a rich body, if the rule has a rich range.</param>
/// <param name="RichCoefficientMax">The largest coefficient on a rich body, if the rule has a rich range.</param>
public sealed record SeedRule(
    string Resource,
    int Weight,
    decimal CoefficientMin,
    decimal CoefficientMax,
    decimal? RichCoefficientMin = null,
    decimal? RichCoefficientMax = null);
