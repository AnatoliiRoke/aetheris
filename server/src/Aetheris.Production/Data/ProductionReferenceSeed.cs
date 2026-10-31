using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Data;

/// <summary>
/// Fills the tables of the production with the starting data (task 00013, decision 2.12).
/// </summary>
/// <remarks>
/// The starting data lives in the embedded file <c>Data/production-reference-seed.json</c>:
/// the groups of the parts, the parts with their attributes, the recipes with their
/// ingredients and the bonuses of the races. Every row has a neutral code and its texts in
/// every language (task 00022). The seed runs only while the tables are empty, so it never
/// overwrites a change of the admin panel (task 00023, decision 7).
/// </remarks>
public static class ProductionReferenceSeed
{
    /// <summary>
    /// The name of the embedded starting data.
    /// </summary>
    public const string ResourceName = "Aetheris.Production.reference-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>The empty set of the values of the characteristics of a part (task 00027).</summary>
    private static readonly IReadOnlyDictionary<string, SeedCharacteristicValue> EmptyCharacteristics =
        new Dictionary<string, SeedCharacteristicValue>();

    /// <summary>The empty set of the values of a characteristic by the level of a part (task 00027).</summary>
    private static readonly IReadOnlyDictionary<int, decimal> EmptyLevels = new Dictionary<int, decimal>();

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static ProductionSeedData Load()
    {
        using Stream stream = typeof(ProductionReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        return JsonSerializer.Deserialize<ProductionSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");
    }

    /// <summary>
    /// Writes the starting data when the tables are empty.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="requirements">The common requirements module, for the requirements of the recipes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(
        ProductionDbContext dbContext,
        ResourceCatalog resources,
        IReadOnlyList<RaceDto> races,
        IPlanetsModule planets,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IRequirementsModule requirements,
        IReadOnlyDictionary<string, int>? technologyIds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(races);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(requirements);

        // The seed runs while the recipes are empty, so it fills the gaps of a database that
        // was emptied by hand and never overwrites a change of the admin panel (task 00023,
        // decision 7).
        if (await dbContext.Recipes.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ProductionSeedData data = Load();

        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id);
        Dictionary<string, int> raceIds = races.ToDictionary(race => race.Code, race => race.Id);

        // The factory of a recipe is named by its neutral code, and the code becomes the
        // identifier of the building type (documentation/tech/09, section 3.3).
        Dictionary<string, int> factoryIds = [];

        foreach (string factory in data.Recipes
            .Select(recipe => recipe.Factory)
            .Where(code => code is not null)
            .Distinct(StringComparer.Ordinal)
            .Select(code => code!)
            .OrderBy(code => code, StringComparer.Ordinal))
        {
            int buildingTypeId = await planets.FindBuildingTypeIdAsync(factory, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The factory '{factory}' of the starting data does not exist.");

            factoryIds[factory] = buildingTypeId;
        }

        // The codes of the file only link the rows of the file, so each must name one row.
        EnsureUnique(data.PartGroups.Select(group => group.Code), "group of the parts");
        EnsureUnique(data.Parts.Select(part => part.Code), "part");
        EnsureUnique(data.Recipes.Select(recipe => recipe.Code), "recipe");

        Dictionary<string, int> partGroupIds = [];
        Dictionary<string, int> partTypeIds = [];
        Dictionary<string, int> characteristicIds = [];

        // Every row takes the next code of the counter of the server, and the keys of its texts
        // are built from that code (documentation/tech/09, sections 3.2 and 4.1).

        // The characteristics come first: the groups of the parts point at them, and the parts
        // keep their values (task 00027).
        foreach (SeedCharacteristic characteristic in data.Characteristics ?? [])
        {
            string code = await codes.NextAsync(ProductionTextKeys.CharacteristicCodeGroup, cancellationToken).ConfigureAwait(false);

            CharacteristicEntity entity = new()
            {
                Code = code,
                NameKey = ProductionTextKeys.CharacteristicName(code),
                DescriptionKey = ProductionTextKeys.CharacteristicDescription(code),
                UnitKey = characteristic.Unit is null ? null : ProductionTextKeys.CharacteristicUnit(code),
                SortOrder = characteristic.SortOrder,
            };

            dbContext.Characteristics.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            characteristicIds[characteristic.Code] = entity.Id;

            await texts.AddMissingAsync(
                [
                    .. ProductionSeedTexts.Entries(entity.NameKey, characteristic.Name),
                    .. ProductionSeedTexts.Entries(entity.DescriptionKey, characteristic.Description),
                    .. ProductionSeedTexts.Entries(entity.UnitKey, characteristic.Unit),
                ],
                ProductionSeedActor,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (SeedPartGroup group in data.PartGroups)
        {
            string code = await codes.NextAsync(ProductionTextKeys.PartGroupCodeGroup, cancellationToken).ConfigureAwait(false);

            PartGroupEntity entity = new()
            {
                Code = code,
                NameKey = ProductionTextKeys.GroupName(code),
                DescriptionKey = ProductionTextKeys.GroupDescription(code),
                SortOrder = group.SortOrder,
            };

            dbContext.PartGroups.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            partGroupIds[group.Code] = entity.Id;

            await texts.AddMissingAsync(
                [
                    .. ProductionSeedTexts.Entries(entity.NameKey, group.Name),
                    .. ProductionSeedTexts.Entries(entity.DescriptionKey, group.Description),
                ],
                ProductionSeedActor,
                cancellationToken).ConfigureAwait(false);
        }

        // The characteristics of every group are bound after the groups exist, so the link
        // points at real rows of both sides (task 00027).
        foreach (SeedGroupCharacteristic link in data.GroupCharacteristics ?? [])
        {
            PartGroupCharacteristicEntity entity = new()
            {
                PartGroupId = partGroupIds[link.Group],
                CharacteristicId = characteristicIds[link.Characteristic],
                SortOrder = link.SortOrder,
                IsRequired = link.IsRequired,
            };

            dbContext.PartGroupCharacteristics.Add(entity);
        }

        if (data.GroupCharacteristics is { Count: > 0 })
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (SeedPart part in data.Parts)
        {
            string code = await codes.NextAsync(ProductionTextKeys.PartTypeCodeGroup, cancellationToken).ConfigureAwait(false);

            PartTypeEntity entity = new()
            {
                Code = code,
                NameKey = ProductionTextKeys.PartName(code),
                DescriptionKey = ProductionTextKeys.PartDescription(code),
                PartGroupId = partGroupIds[part.Group],
                MarkLevel = part.MarkLevel,
                SortOrder = part.SortOrder,
                Weight = part.Weight ?? 1m,
            };

            foreach (KeyValuePair<string, SeedAttributeValue> attribute in part.Attributes)
            {
                entity.AttributeIds.Add(new PartAttributeEntity
                {
                    AttributeKey = attribute.Key,
                    NumberValue = attribute.Value.Number,
                    CodeValue = attribute.Value.Code,
                    TextValue = attribute.Value.Text,
                });
            }

            dbContext.PartTypes.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            partTypeIds[part.Code] = entity.Id;

            // The values of the characteristics of the part come after the part itself, so
            // they point at a real row of it (task 00027).
            foreach (KeyValuePair<string, SeedCharacteristicValue> value in part.Characteristics ?? EmptyCharacteristics)
            {
                PartCharacteristicValueEntity characteristicValue = new()
                {
                    PartTypeId = entity.Id,
                    CharacteristicId = characteristicIds[value.Key],
                    NumberValue = value.Value.Number,
                };

                foreach (KeyValuePair<int, decimal> level in value.Value.Levels ?? EmptyLevels)
                {
                    characteristicValue.Levels.Add(new PartCharacteristicLevelValueEntity
                    {
                        Level = level.Key,
                        NumberValue = level.Value,
                    });
                }

                dbContext.PartCharacteristicValues.Add(characteristicValue);
            }

            if (part.Characteristics is { Count: > 0 })
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await texts.AddMissingAsync(
                [
                    .. ProductionSeedTexts.Entries(entity.NameKey, part.Name),
                    .. ProductionSeedTexts.Entries(entity.DescriptionKey, part.Description),
                ],
                ProductionSeedActor,
                cancellationToken).ConfigureAwait(false);
        }

        Dictionary<string, int> recipeIds = [];

        foreach (SeedRecipe recipe in data.Recipes)
        {
            string code = await codes.NextAsync(ProductionTextKeys.RecipeCodeGroup, cancellationToken).ConfigureAwait(false);

            RecipeEntity entity = new()
            {
                Code = code,
                NameKey = ProductionTextKeys.RecipeName(code),
                DescriptionKey = ProductionTextKeys.RecipeDescription(code),
                OutputQuantity = recipe.OutputQuantity,
                DurationSeconds = recipe.DurationSeconds,
                SortOrder = recipe.SortOrder,
                PartTypeId = recipe.Part is not null ? partTypeIds[recipe.Part] : null,
                ResourceTypeId = recipe.Resource is not null ? resourceIds[recipe.Resource] : null,
                BuildingTypeId = recipe.Factory is not null ? factoryIds[recipe.Factory] : null,
            };

            foreach (SeedIngredient ingredient in recipe.Ingredients)
            {
                entity.IngredientIds.Add(new RecipeIngredientEntity
                {
                    ResourceTypeId = ingredient.Resource is not null ? resourceIds[ingredient.Resource] : null,
                    PartTypeId = ingredient.Part is not null ? partTypeIds[ingredient.Part] : null,
                    Amount = ingredient.Amount,
                });
            }

            dbContext.Recipes.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            recipeIds[recipe.Code] = entity.Id;

            await texts.AddMissingAsync(
                [
                    .. ProductionSeedTexts.Entries(entity.NameKey, recipe.Name),
                    .. ProductionSeedTexts.Entries(entity.DescriptionKey, recipe.Description),
                ],
                ProductionSeedActor,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (SeedRaceBonus bonus in data.RaceBonuses)
        {
            dbContext.RacePartBonuses.Add(new RacePartBonusEntity
            {
                RaceId = raceIds[bonus.Race],
                PartGroupId = partGroupIds[bonus.Group],
                Multiplier = bonus.Multiplier,
                IsExclusive = bonus.IsExclusive,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The requirements of the recipes live in the common requirements module.
        foreach (SeedRecipe recipe in data.Recipes)
        {
            if (recipe.Requirements.Count == 0 && recipe.Race is null)
            {
                continue;
            }

            List<RequirementDefinition> definitions = [];

            foreach (SeedRequirement requirement in recipe.Requirements)
            {
                // A technology gate is written when the technologies are already in the database.
                // A run that fills the production before the research leaves the gate out; the
                // full start of the server writes it (task 00028).
                if (requirement.Type == RequirementTypes.TechnologyLevel && technologyIds is not { Count: > 0 })
                {
                    continue;
                }

                // The target is named by its neutral code and becomes the identifier of the row.
                int targetId = requirement.Type switch
                {
                    RequirementTypes.BuildingLevel => await planets.FindBuildingTypeIdAsync(requirement.Target, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"The building '{requirement.Target}' of the starting data does not exist."),
                    RequirementTypes.TechnologyLevel => technologyIds!.TryGetValue(requirement.Target, out int technologyId)
                        ? technologyId
                        : throw new InvalidOperationException($"The technology '{requirement.Target}' of the starting data does not exist."),
                    ProductionRequirementTypes.Race => raceIds[requirement.Target],
                    _ => throw new InvalidOperationException($"The starting data names the requirement type '{requirement.Type}', which it cannot resolve."),
                };

                definitions.Add(new RequirementDefinition(requirement.Type, targetId, requirement.TargetLevel));
            }

            if (recipe.Race is not null)
            {
                definitions.Add(new RequirementDefinition(
                    ProductionRequirementTypes.Race,
                    raceIds[recipe.Race],
                    0));
            }

            await requirements.ReplaceAsync(
                RequirementSubjectTypes.Recipe,
                recipeIds[recipe.Code],
                [
                    .. definitions.Select(definition => new SubjectRequirement(recipeIds[recipe.Code], 1, definition)),
                ],
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static void EnsureUnique(IEnumerable<string> codes, string kind)
    {
        if (codes.GroupBy(code => code, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new InvalidOperationException($"The starting data names the {kind} '{duplicate.Key}' twice.");
        }
    }

    /// <summary>
    /// The author of the texts of the starting data.
    /// </summary>
    public const string ProductionSeedActor = "production_seed";

}

/// <summary>The starting data of the production module.</summary>
/// <param name="PartGroups">The groups of the parts.</param>
/// <param name="Parts">The parts.</param>
/// <param name="Recipes">The recipes.</param>
/// <param name="RaceBonuses">The bonuses of the races in the groups of the parts.</param>
/// <param name="Characteristics">The characteristics of the parts (task 00027).</param>
/// <param name="GroupCharacteristics">The characteristics of the groups of the parts (task 00027).</param>
public sealed record ProductionSeedData(
    IReadOnlyList<SeedPartGroup> PartGroups,
    IReadOnlyList<SeedPart> Parts,
    IReadOnlyList<SeedRecipe> Recipes,
    IReadOnlyList<SeedRaceBonus> RaceBonuses,
    IReadOnlyList<SeedCharacteristic>? Characteristics = null,
    IReadOnlyList<SeedGroupCharacteristic>? GroupCharacteristics = null);

/// <summary>A characteristic of the starting data.</summary>
/// <param name="Code">The neutral code of the characteristic.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name of the characteristic.</param>
/// <param name="Description">The description of the characteristic.</param>
/// <param name="Unit">The unit of measure, if any.</param>
public sealed record SeedCharacteristic(
    string Code,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    IReadOnlyDictionary<string, string>? Unit = null);

/// <summary>A characteristic of a group of the parts of the starting data.</summary>
/// <param name="Group">The neutral code of the group of the parts.</param>
/// <param name="Characteristic">The neutral code of the characteristic.</param>
/// <param name="SortOrder">The order in the list of the group.</param>
/// <param name="IsRequired">Whether a part of the group must have the value of the characteristic.</param>
public sealed record SeedGroupCharacteristic(
    string Group,
    string Characteristic,
    int SortOrder,
    bool IsRequired = false);

/// <summary>A group of the parts of the starting data.</summary>
/// <param name="Code">The neutral code of the group.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name of the group.</param>
/// <param name="Description">The description of the group.</param>
public sealed record SeedPartGroup(
    string Code,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description);

/// <summary>A part of the starting data.</summary>
/// <param name="Code">The neutral code of the part.</param>
/// <param name="Group">The neutral code of the group of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name of the part.</param>
/// <param name="Description">The description of the part.</param>
/// <param name="Attributes">The attributes of the part.</param>
/// <param name="Weight">The weight of one part in tonnes (task 00027).</param>
/// <param name="Characteristics">The values of the characteristics of the part (task 00027).</param>
public sealed record SeedPart(
    string Code,
    string Group,
    int MarkLevel,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    IReadOnlyDictionary<string, SeedAttributeValue> Attributes,
    decimal? Weight = null,
    IReadOnlyDictionary<string, SeedCharacteristicValue>? Characteristics = null);

/// <summary>The value of a characteristic of a part of the starting data.</summary>
/// <param name="Number">The value of the characteristic.</param>
/// <param name="Levels">The values of the characteristic by the level of the part, when it has them.</param>
public sealed record SeedCharacteristicValue(decimal Number, IReadOnlyDictionary<int, decimal>? Levels = null);

/// <summary>An attribute of a part of the starting data.</summary>
/// <param name="Number">The value as a number, or <see langword="null"/>.</param>
/// <param name="Code">The value as a logical code, or <see langword="null"/>.</param>
/// <param name="Text">The value as a text, or <see langword="null"/>.</param>
public sealed record SeedAttributeValue(decimal? Number = null, string? Code = null, string? Text = null);

/// <summary>A recipe of the starting data.</summary>
/// <param name="Code">The neutral code of the recipe.</param>
/// <param name="Part">The neutral code of the part the recipe makes, or <see langword="null"/>.</param>
/// <param name="Resource">The neutral code of the resource the recipe makes, or <see langword="null"/>.</param>
/// <param name="Factory">The neutral code of the factory that makes the product.</param>
/// <param name="OutputQuantity">How many units one batch gives.</param>
/// <param name="DurationSeconds">The time of one unit, in game seconds.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="Name">The name of the recipe.</param>
/// <param name="Description">The description of the recipe.</param>
/// <param name="Ingredients">The ingredients of the recipe.</param>
/// <param name="Requirements">The requirements of the recipe.</param>
/// <param name="Race">The neutral code of the race that may make the recipe alone, or <see langword="null"/>.</param>
public sealed record SeedRecipe(
    string Code,
    string? Part,
    string? Resource,
    string? Factory,
    long OutputQuantity,
    long DurationSeconds,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    IReadOnlyList<SeedIngredient> Ingredients,
    IReadOnlyList<SeedRequirement> Requirements,
    string? Race);

/// <summary>An ingredient of a recipe of the starting data.</summary>
/// <param name="Resource">The neutral code of the resource, or <see langword="null"/>.</param>
/// <param name="Part">The neutral code of the part, or <see langword="null"/>.</param>
/// <param name="Amount">How many of the ingredient one unit takes.</param>
public sealed record SeedIngredient(string? Resource, string? Part, long Amount);

/// <summary>A requirement of a recipe of the starting data.</summary>
/// <param name="Type">The logical code of the kind of the requirement.</param>
/// <param name="Target">The neutral code of the target.</param>
/// <param name="TargetLevel">The required level of the target.</param>
public sealed record SeedRequirement(string Type, string Target, int TargetLevel);

/// <summary>A bonus of a race in a group of the parts of the starting data.</summary>
/// <param name="Race">The neutral code of the race.</param>
/// <param name="Group">The neutral code of the group of the parts.</param>
/// <param name="Multiplier">The multiplier of the bonus.</param>
/// <param name="IsExclusive">Whether the race may make the top modules of the group alone.</param>
public sealed record SeedRaceBonus(string Race, string Group, decimal Multiplier, bool IsExclusive);

/// <summary>
/// The texts of the starting data of the production module.
/// </summary>
/// <remarks>
/// The helper lives in this module, so the module keeps no reference to another module of
/// the game (documentation/tech/01, section 8).
/// </remarks>
public static class ProductionSeedTexts
{
    /// <summary>
    /// The author of the texts of the starting data.
    /// </summary>
    public const string Author = "production_seed";

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
