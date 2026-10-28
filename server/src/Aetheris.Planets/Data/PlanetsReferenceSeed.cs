using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
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
/// has a neutral code and its texts in every language (task 00022). The seed runs only while the
/// building table is empty, so it never overwrites a change.
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
    /// Writes the starting data when the building table is empty.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(
        PlanetsDbContext dbContext,
        ResourceCatalog resources,
        ILocalizationTexts texts,
        ICodeSequence codes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        if (resources.Resources.Count == 0 || await dbContext.BuildingTypes.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        PlanetsSeedData seed = Load();
        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal);
        Dictionary<string, int> groupIds = resources.Groups.ToDictionary(group => group.Code, group => group.Id, StringComparer.Ordinal);

        Dictionary<string, BuildingTypeEntity> types = seed.BuildingTypes.ToDictionary(
            type => type.Code,
            type => new BuildingTypeEntity
            {
                Code = type.Code,
                NameKey = PlanetsTextKeys.Name(type.Code),
                DescriptionKey = PlanetsTextKeys.Description(type.Code),
                SortOrder = type.SortOrder,
                IsStartBuilding = type.IsStartBuilding,
                BuildingClass = type.BuildingClass,
                LevelIds =
                [
                    .. Enumerable.Range(1, seed.LevelCount).Select(level => new BuildingLevelEntity
                    {
                        Level = level,
                        DurationSeconds = ValueAt(type.DurationSeconds, level),
                        CostIds = [.. type.Costs.Select(cost => new BuildingLevelCostEntity { ResourceTypeId = resourceIds[cost.Resource], Amount = ValueAt(cost, level) })],
                        EffectIds =
                        [
                            .. type.Effects.Select(effect => new BuildingLevelEffectEntity
                            {
                                EffectType = effect.Type,
                                ResourceTypeId = effect.Resource is null ? null : resourceIds[effect.Resource],
                                ResourceGroupId = effect.Group is null ? null : groupIds[effect.Group],
                                Value = ValueAt(effect, level),
                            }),
                        ],
                    }),
                ],
            },
            StringComparer.Ordinal);

        dbContext.BuildingTypes.AddRange(types.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        dbContext.Requirements.AddRange(seed.BuildingTypes.SelectMany(type => type.Requirements.Select(requirement => new RequirementEntity
        {
            SubjectType = RequirementSubjectTypes.BuildingType,
            SubjectId = types[type.Code].Id,
            SubjectLevel = requirement.SubjectLevel ?? 1,
            RequirementType = requirement.Type,
            TargetId = requirement.Type switch
            {
                RequirementTypes.BuildingLevel => types[requirement.Target].Id,
                RequirementTypes.Deposit => resourceIds[requirement.Target],
                _ => throw new InvalidOperationException($"The starting data names the requirement type '{requirement.Type}', which it cannot resolve."),
            },
            TargetLevel = requirement.TargetLevel ?? 0,
        })));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. seed.BuildingTypes.SelectMany(type => SeedTexts.Entries(PlanetsTextKeys.Name(type.Code), type.Name)),
                .. seed.BuildingTypes.SelectMany(type => SeedTexts.Entries(PlanetsTextKeys.Description(type.Code), type.Description)),
            ],
            SeedTexts.Author,
            cancellationToken).ConfigureAwait(false);

        await WorldReferenceSeed.RaiseAsync(codes, PlanetsTextKeys.CodeGroup, seed.BuildingTypes.Select(type => type.Code), cancellationToken).ConfigureAwait(false);

        return true;
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
}

/// <summary>The starting data of the building tables.</summary>
/// <param name="LevelCount">The number of levels written for every type.</param>
/// <param name="BuildingTypes">The building types.</param>
public sealed record PlanetsSeedData(int LevelCount, IReadOnlyList<SeedBuildingType> BuildingTypes);

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
    string BuildingClass = Services.BuildingClasses.None);

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
}

/// <summary>A requirement of the starting data.</summary>
/// <param name="Type">The requirement type.</param>
/// <param name="Target">The code of the target: a building type or a resource.</param>
/// <param name="TargetLevel">The required level of a building.</param>
/// <param name="SubjectLevel">The first level of the building the requirement applies to.</param>
public sealed record SeedRequirement(string Type, string Target, int? TargetLevel = null, int? SubjectLevel = null);
