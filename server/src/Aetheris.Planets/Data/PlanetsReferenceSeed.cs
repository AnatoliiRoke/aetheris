using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Data;

/// <summary>
/// Fills the building tables of a fresh database with the starting data (task 00011, open
/// question 2).
/// </summary>
/// <remarks>
/// The starting data lives in the embedded file <c>Data/planets-reference-seed.json</c>: every
/// building type with the start value and the growth of its time, costs and effects. The seed
/// turns them into one row per level, <c>round(base × growth^(level − 1) + step × (level − 1))</c>,
/// so the database holds a plain table that a designer or the simulator changes later. Every type
/// has a neutral code and its texts in every language (task 00022). The seed writes only the
/// rows whose codes are missing, so it never overwrites a change. The categories of the buildings
/// (task 00020, section 3.1) come from the same file, and every building names its category by
/// the code.
/// </remarks>
public static class PlanetsReferenceSeed
{
    /// <summary>
    /// The name of the embedded starting data.
    /// </summary>
    public const string ResourceName = "Aetheris.Planets.reference-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static PlanetsSeedData Load()
    {
        using Stream stream = typeof(PlanetsReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        return JsonSerializer.Deserialize<PlanetsSeedData>(stream, JsonOptions)
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
    /// Gets the value of an effect at a level: the value the starting data names for the level
    /// when it lists them, otherwise the value of the growth formula.
    /// </summary>
    /// <param name="effect">The effect of the starting data.</param>
    /// <param name="level">The level, from 1.</param>
    /// <returns>The value, with its fraction.</returns>
    /// <remarks>
    /// A list of values is for an effect that does not grow by a formula (a multiplier that rises
    /// by uneven steps): the value of every level is written in the data. A level past the end of
    /// the list keeps the last value.
    /// </remarks>
    public static decimal EffectValueAt(SeedEffect effect, int level)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (effect.Values is { Count: > 0 } values)
        {
            return values[Math.Min(level, values.Count) - 1];
        }

        return ValueAt(effect, level);
    }

    /// <summary>
    /// Writes the starting data when the building table is empty.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="requirements">The common requirements module, for the requirements of the buildings.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(
        PlanetsDbContext dbContext,
        ResourceCatalog resources,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(requirements);

        if (resources.Resources.Count == 0)
        {
            return false;
        }

        PlanetsSeedData seed = Load();

        // The seed writes only the buildings that are missing, so a database that already
        // holds some of them still gets the rest. The check is on the codes of the starting
        // data and not on the emptiness of the table: a building that someone added before
        // the starting data was written must not stop the seed (task 00014, decision 3.1).
        HashSet<string> startingCodes = [.. seed.BuildingTypes.Select(type => type.Code)];

        List<string> existingCodes = await dbContext.BuildingTypes
            .Where(type => startingCodes.Contains(type.Code))
            .Select(type => type.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SeedBuildingType> missingTypes = [.. seed.BuildingTypes.Where(type => !existingCodes.Contains(type.Code))];

        // The categories are written first: a building needs the identifier of its category.
        HashSet<string> categoryCodes = [.. seed.Categories.Select(category => category.Code)];

        List<string> existingCategoryCodes = await dbContext.BuildingCategories
            .Where(category => categoryCodes.Contains(category.Code))
            .Select(category => category.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SeedBuildingCategory> missingCategories = [.. seed.Categories.Where(category => !existingCategoryCodes.Contains(category.Code))];

        dbContext.BuildingCategories.AddRange(missingCategories.Select(category => new BuildingCategoryEntity
        {
            Code = category.Code,
            NameKey = PlanetsTextKeys.CategoryName(category.Code),
            DescriptionKey = PlanetsTextKeys.CategoryDescription(category.Code),
            SortOrder = category.SortOrder,
        }));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, int> categoryIds = await dbContext.BuildingCategories
            .Where(category => categoryCodes.Contains(category.Code))
            .ToDictionaryAsync(category => category.Code, category => category.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal);
        Dictionary<string, int> groupIds = resources.Groups.ToDictionary(group => group.Code, group => group.Id, StringComparer.Ordinal);

        Dictionary<string, BuildingTypeEntity> types = missingTypes.ToDictionary(
            type => type.Code,
            type => new BuildingTypeEntity
            {
                Code = type.Code,
                NameKey = PlanetsTextKeys.Name(type.Code),
                DescriptionKey = PlanetsTextKeys.Description(type.Code),
                SortOrder = type.SortOrder,
                IsStartBuilding = type.IsStartBuilding,
                BuildingClass = type.BuildingClass,
                CategoryId = categoryIds.TryGetValue(type.Category, out int categoryId)
                    ? categoryId
                    : throw new InvalidOperationException($"The starting data gives the building '{type.Code}' the unknown category '{type.Category}'."),
                LevelIds =
                [
                    .. Enumerable.Range(1, type.MaxLevel is > 0 ? type.MaxLevel.Value : seed.LevelCount).Select(level => new BuildingLevelEntity
                    {
                        Level = level,
                        DurationSeconds = ValueAt(type.DurationSeconds, level),
                        CostIds = [.. type.Costs.Where(cost => level >= cost.FromLevel).Select(cost => new BuildingLevelCostEntity { ResourceTypeId = resourceIds[cost.Resource], Amount = ValueAt(cost, level) })],
                        EffectIds =
                        [
                            .. type.Effects.Select(effect => new BuildingLevelEffectEntity
                            {
                                EffectType = effect.Type,
                                ResourceTypeId = effect.Resource is null ? null : resourceIds[effect.Resource],
                                ResourceGroupId = effect.Group is null ? null : groupIds[effect.Group],
                                Value = EffectValueAt(effect, level),
                            }),
                        ],
                    }),
                ],
            },
            StringComparer.Ordinal);

        dbContext.BuildingTypes.AddRange(types.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The requirements of a building may name another building of the starting data,
        // and that building may have been written by an earlier run, so the ids of all of
        // them are read from the table.
        Dictionary<string, int> typeIds = await dbContext.BuildingTypes
            .Where(type => startingCodes.Contains(type.Code))
            .ToDictionaryAsync(type => type.Code, type => type.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        // The requirements live in the common requirements module, so the seed adds them
        // through its contract and never through the tables of that module. The requirements
        // of one type are grouped by the level they apply from.
        foreach ((string code, SeedBuildingType type) in missingTypes.Select(type => (type.Code, type)))
        {
            if (type.Requirements.Count == 0)
            {
                continue;
            }

            // A technology requirement is resolved after the technologies exist. The research
            // seed writes it, and only for a technology inserted on that run (task 00028).
            foreach (IGrouping<int, SeedRequirement> group in type.Requirements
                .Where(requirement => requirement.Type != RequirementTypes.TechnologyLevel)
                .GroupBy(requirement => requirement.SubjectLevel ?? 1))
            {
                await requirements.AddAsync(
                    RequirementSubjectTypes.BuildingType,
                    typeIds[code],
                    group.Key,
                    [.. group.Select(requirement => new RequirementDefinition(
                        requirement.Type,
                        requirement.Type switch
                        {
                            RequirementTypes.BuildingLevel => typeIds[requirement.Target],
                            RequirementTypes.Deposit => resourceIds[requirement.Target],
                            _ => throw new InvalidOperationException($"The starting data names the requirement type '{requirement.Type}', which it cannot resolve."),
                        },
                        requirement.TargetLevel ?? 0))],
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await texts.AddMissingAsync(
            [
                .. seed.BuildingTypes.SelectMany(type => SeedTexts.Entries(PlanetsTextKeys.Name(type.Code), type.Name)),
                .. seed.BuildingTypes.SelectMany(type => SeedTexts.Entries(PlanetsTextKeys.Description(type.Code), type.Description)),
                .. seed.Categories.SelectMany(category => SeedTexts.Entries(PlanetsTextKeys.CategoryName(category.Code), category.Name)),
                .. seed.Categories.SelectMany(category => SeedTexts.Entries(PlanetsTextKeys.CategoryDescription(category.Code), category.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await WorldReferenceSeed.RaiseAsync(codes, PlanetsTextKeys.CodeGroup, seed.BuildingTypes.Select(type => type.Code), cancellationToken).ConfigureAwait(false);
        await WorldReferenceSeed.RaiseAsync(codes, PlanetsTextKeys.CategoryCodeGroup, seed.Categories.Select(category => category.Code), cancellationToken).ConfigureAwait(false);

        return missingTypes.Count > 0 || missingCategories.Count > 0;
    }
}

/// <summary>
/// The neutral codes and the localisation keys of the building types (task 00022):
/// <c>building_1</c>, <c>planets.building.building_1.name</c>.
/// </summary>
public static class PlanetsTextKeys
{
    /// <summary>The group of the neutral codes of the building types.</summary>
    public const string CodeGroup = "building";

    /// <summary>The prefix of the keys of the building types.</summary>
    public const string Building = "planets.building";

    /// <summary>
    /// Builds the key of the name of a building type.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string Name(string code) => WorldTextKeys.Name(Building, code);

    /// <summary>
    /// Builds the key of the description of a building type.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string Description(string code) => WorldTextKeys.Description(Building, code);

    /// <summary>The group of the neutral codes of the building categories: <c>group_1</c>.</summary>
    public const string CategoryCodeGroup = "group";

    /// <summary>The prefix of the keys of the building categories.</summary>
    public const string Category = "planets.building_category";

    /// <summary>
    /// Builds the key of the name of a building category.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string CategoryName(string code) => WorldTextKeys.Name(Category, code);

    /// <summary>
    /// Builds the key of the description of a building category.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string CategoryDescription(string code) => WorldTextKeys.Description(Category, code);
}

/// <summary>The starting data of the building tables.</summary>
/// <param name="LevelCount">The number of levels written for every type.</param>
/// <param name="Categories">The categories of the building types.</param>
/// <param name="BuildingTypes">The building types.</param>
public sealed record PlanetsSeedData(int LevelCount, IReadOnlyList<SeedBuildingCategory> Categories, IReadOnlyList<SeedBuildingType> BuildingTypes);

/// <summary>A building category of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="SortOrder">The order of the groups on the building page.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
public sealed record SeedBuildingCategory(
    string Code,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description);

/// <summary>A building type of the starting data.</summary>
/// <param name="Code">The neutral code.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
/// <param name="DurationSeconds">The growth of the construction time.</param>
/// <param name="Costs">The growth of the costs.</param>
/// <param name="Effects">The growth of the effects.</param>
/// <param name="Requirements">The requirements.</param>
/// <param name="IsStartBuilding">Whether a race without a start building gets this type.</param>
/// <param name="BuildingClass">The class of the type: what the building is for (task 00012).</param>
/// <param name="Category">The code of the category of the type (task 00020).</param>
public sealed record SeedBuildingType(
    string Code,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string>? Description,
    SeedGrowth DurationSeconds,
    IReadOnlyList<SeedCost> Costs,
    IReadOnlyList<SeedEffect> Effects,
    IReadOnlyList<SeedRequirement> Requirements,
    bool IsStartBuilding = false,
    string BuildingClass = Services.BuildingClasses.None,
    string Category = "",
    int? MaxLevel = null);

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

/// <summary>A cost of the starting data.</summary>
public sealed record SeedCost : SeedGrowth
{
    /// <summary>Gets the code of the resource.</summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>Gets the first level that pays this resource. Earlier levels omit the row.</summary>
    public int FromLevel { get; init; } = 1;
}

/// <summary>An effect of the starting data.</summary>
public sealed record SeedEffect : SeedGrowth
{
    /// <summary>Gets the effect type.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the code of the resource of a production effect.</summary>
    public string? Resource { get; init; }

    /// <summary>Gets the code of the resource group of a storage effect.</summary>
    public string? Group { get; init; }

    /// <summary>
    /// Gets the value of every level, from the first, for an effect that has no growth formula;
    /// the values have a fraction. When it is empty the value comes from the growth formula.
    /// </summary>
    public IReadOnlyList<decimal>? Values { get; init; }
}

/// <summary>A requirement of the starting data.</summary>
/// <param name="Type">The requirement type.</param>
/// <param name="Target">The code of the target: a building type or a resource.</param>
/// <param name="TargetLevel">The required level of a building.</param>
/// <param name="SubjectLevel">The first level of the building the requirement applies to.</param>
public sealed record SeedRequirement(string Type, string Target, int? TargetLevel = null, int? SubjectLevel = null);
