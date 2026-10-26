using System.Text.Json;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Data;

/// <summary>
/// Fills the reference tables of a fresh database with the starting data.
/// </summary>
/// <remarks>
/// The starting data lives in the embedded file <c>Data/world-reference-seed.json</c>, not
/// in the code: resources, object types, size classes and generation rules are rows that a
/// designer changes, adds or removes (task 00004, decision 23). The seed runs only while the
/// reference tables are empty, so it never overwrites or restores what was changed later.
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
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(WorldDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (await dbContext.ResourceGroups.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ReferenceSeedData seed = Load();

        Dictionary<string, ResourceGroupEntity> groups = seed.ResourceGroups.ToDictionary(
            group => group.Code,
            group => new ResourceGroupEntity { Code = group.Code, NameKey = group.NameKey, AllowedOnPlanet = group.AllowedOnPlanet },
            StringComparer.Ordinal);

        dbContext.ResourceGroups.AddRange(groups.Values);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, ResourceTypeEntity> resources = seed.Resources.ToDictionary(
            resource => resource.Code,
            resource => new ResourceTypeEntity
            {
                Code = resource.Code,
                NameKey = resource.NameKey,
                ResourceGroupId = groups[resource.Group].Id,
            },
            StringComparer.Ordinal);

        Dictionary<string, CelestialObjectTypeEntity> types = seed.ObjectTypes.ToDictionary(
            type => type.Code,
            type => new CelestialObjectTypeEntity
            {
                Code = type.Code,
                ObjectClass = type.ObjectClass,
                NameKey = type.NameKey,
                IsColonizable = type.IsColonizable,
                GenerationWeight = type.GenerationWeight,
                MassMin = type.MassMin,
                MassMax = type.MassMax,
            },
            StringComparer.Ordinal);

        dbContext.ResourceTypes.AddRange(resources.Values);
        dbContext.ObjectTypes.AddRange(types.Values);
        dbContext.SizeClasses.AddRange(seed.SizeClasses.Select(size => new PlanetSizeClassEntity
        {
            Code = size.Code,
            NameKey = size.NameKey,
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

        return true;
    }

    /// <summary>
    /// Writes the starting races when the race table is empty.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the races were written.</returns>
    /// <remarks>
    /// The races came with task 00006, after the other reference tables, so a database that
    /// already has its resources still gets its races. Like the rest of the seed, it never
    /// overwrites what was changed later.
    /// </remarks>
    public static async Task<bool> SeedRacesAsync(WorldDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (await dbContext.Races.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ReferenceSeedData seed = Load();

        Dictionary<string, int> resourceIds = await dbContext.ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, int> typeIds = await dbContext.ObjectTypes.AsNoTracking()
            .ToDictionaryAsync(type => type.Code, type => type.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        dbContext.Races.AddRange((seed.Races ?? []).Select(race => new RaceEntity
        {
            Code = race.Code,
            NameKey = race.NameKey,
            SortOrder = race.SortOrder,
            PreferredObjectTypeId = race.PreferredObjectType is null ? null : typeIds[race.PreferredObjectType],
            StartBuildingTypeId = race.StartBuildingTypeId,
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

        return true;
    }
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
/// <param name="Code">The code.</param>
/// <param name="NameKey">The localisation key.</param>
/// <param name="SortOrder">The order in the list.</param>
/// <param name="PreferredObjectType">The code of the preferred start body type, if any.</param>
/// <param name="StartBuildingTypeId">The start building type, if the buildings module has one.</param>
/// <param name="StartBuildingLevel">The level of the start building.</param>
/// <param name="StartResources">The start resources.</param>
public sealed record SeedRace(
    string Code,
    string NameKey,
    int SortOrder,
    string? PreferredObjectType,
    int? StartBuildingTypeId,
    int StartBuildingLevel,
    IReadOnlyList<SeedRaceResource> StartResources);

/// <summary>A start resource of a race in the starting data.</summary>
/// <param name="Resource">The code of the resource.</param>
/// <param name="Amount">The amount.</param>
public sealed record SeedRaceResource(string Resource, long Amount);

/// <summary>A resource group of the starting data.</summary>
/// <param name="Code">The code.</param>
/// <param name="NameKey">The localisation key.</param>
/// <param name="AllowedOnPlanet">Whether the group may lie on a body.</param>
public sealed record SeedResourceGroup(string Code, string NameKey, bool AllowedOnPlanet);

/// <summary>A resource type of the starting data.</summary>
/// <param name="Code">The code.</param>
/// <param name="Group">The code of the group.</param>
/// <param name="NameKey">The localisation key.</param>
public sealed record SeedResource(string Code, string Group, string NameKey);

/// <summary>An object type of the starting data.</summary>
/// <param name="Code">The code.</param>
/// <param name="ObjectClass">The class.</param>
/// <param name="NameKey">The localisation key.</param>
/// <param name="IsColonizable">Whether a player may own the object.</param>
/// <param name="GenerationWeight">The weight within the class.</param>
/// <param name="MassMin">The smallest mass of a field.</param>
/// <param name="MassMax">The largest mass of a field.</param>
public sealed record SeedObjectType(
    string Code,
    string ObjectClass,
    string NameKey,
    bool IsColonizable,
    int GenerationWeight,
    long? MassMin,
    long? MassMax);

/// <summary>A size class of the starting data.</summary>
/// <param name="Code">The code.</param>
/// <param name="NameKey">The localisation key.</param>
/// <param name="GenerationWeight">The weight.</param>
/// <param name="SizeMin">The smallest size.</param>
/// <param name="SizeMax">The largest size.</param>
public sealed record SeedSizeClass(string Code, string NameKey, int GenerationWeight, int SizeMin, int SizeMax);

/// <summary>A generation rule group of the starting data.</summary>
/// <param name="ObjectType">The code of the object type.</param>
/// <param name="Code">The code.</param>
/// <param name="MinPicks">The picks that always happen.</param>
/// <param name="MaxPicks">The largest number of picks.</param>
/// <param name="ExtraPickProbability">The probability of every further pick.</param>
/// <param name="Rules">The rules.</param>
public sealed record SeedRuleGroup(
    string ObjectType,
    string Code,
    int MinPicks,
    int MaxPicks,
    double ExtraPickProbability,
    IReadOnlyList<SeedRule> Rules);

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
