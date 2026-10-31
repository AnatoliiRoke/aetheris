using Aetheris.Requirements.Services;

namespace Aetheris.Production.Services;

/// <summary>
/// The catalog of the production: the groups of the parts, the parts, the recipes and the
/// requirements of the recipes, as they are in memory (task 00013).
/// </summary>
/// <param name="Groups">The groups of the parts in the order of the lists.</param>
/// <param name="Parts">The parts in the order of the lists.</param>
/// <param name="Recipes">The recipes in the order of the lists.</param>
/// <param name="Requirements">The requirements of every recipe.</param>
/// <param name="Characteristics">The characteristics of the parts, in the order of the lists.</param>
public sealed record ProductionCatalog(
    IReadOnlyList<PartGroupInfo> Groups,
    IReadOnlyList<PartTypeInfo> Parts,
    IReadOnlyList<RecipeInfo> Recipes,
    IReadOnlyList<ProductionRequirementInfo> Requirements,
    IReadOnlyList<CharacteristicInfo> Characteristics)
{
    private readonly Dictionary<int, PartGroupInfo> _groupsById = Groups.ToDictionary(group => group.Id);
    private readonly Dictionary<int, PartTypeInfo> _partsById = Parts.ToDictionary(part => part.Id);
    private readonly Dictionary<int, RecipeInfo> _recipesById = Recipes.ToDictionary(recipe => recipe.Id);
    private readonly Dictionary<int, CharacteristicInfo> _characteristicsById = Characteristics.ToDictionary(characteristic => characteristic.Id);

    /// <summary>
    /// Finds a group of the parts.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <returns>The group, or <see langword="null"/>.</returns>
    public PartGroupInfo? FindGroup(int groupId) => _groupsById.GetValueOrDefault(groupId);

    /// <summary>
    /// Finds a part.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The part, or <see langword="null"/>.</returns>
    public PartTypeInfo? FindPart(int partId) => _partsById.GetValueOrDefault(partId);

    /// <summary>
    /// Finds a characteristic.
    /// </summary>
    /// <param name="characteristicId">The characteristic.</param>
    /// <returns>The characteristic, or <see langword="null"/>.</returns>
    public CharacteristicInfo? FindCharacteristic(int characteristicId) => _characteristicsById.GetValueOrDefault(characteristicId);

    /// <summary>
    /// Finds a recipe.
    /// </summary>
    /// <param name="recipeId">The recipe.</param>
    /// <returns>The recipe, or <see langword="null"/>.</returns>
    public RecipeInfo? FindRecipe(int recipeId) => _recipesById.GetValueOrDefault(recipeId);

    /// <summary>
    /// Finds the recipes a factory makes.
    /// </summary>
    /// <param name="buildingTypeId">The factory.</param>
    /// <returns>The recipes.</returns>
    public IEnumerable<RecipeInfo> RecipesOfFactory(int buildingTypeId) =>
        Recipes.Where(recipe => recipe.BuildingTypeId == buildingTypeId);

    /// <summary>
    /// Gets the requirements of a recipe that apply to it.
    /// </summary>
    /// <param name="recipeId">The recipe.</param>
    /// <returns>The requirements.</returns>
    public IEnumerable<ProductionRequirementInfo> RequirementsOf(int recipeId) =>
        Requirements.Where(requirement => requirement.SubjectId == recipeId
            && string.Equals(requirement.SubjectType, RequirementSubjectTypes.Recipe, StringComparison.Ordinal));
}

/// <summary>A group of the parts with its order in the lists.</summary>
/// <param name="Id">The group.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="IsArchived">Whether the group is archived.</param>
/// <param name="Characteristics">The characteristics the parts of the group have, in the order of the form.</param>
public sealed record PartGroupInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    int SortOrder,
    bool IsArchived,
    IReadOnlyList<PartGroupCharacteristicInfo> Characteristics);

/// <summary>A characteristic of a group of the parts.</summary>
/// <param name="CharacteristicId">The characteristic.</param>
/// <param name="Code">The stable code of the characteristic.</param>
/// <param name="NameKey">The localisation key of the name of the characteristic.</param>
/// <param name="UnitKey">The localisation key of the unit of measure, or <see langword="null"/>.</param>
/// <param name="SortOrder">The order in the list of the group.</param>
/// <param name="IsRequired">Whether a part of the group must have the value of the characteristic.</param>
public sealed record PartGroupCharacteristicInfo(
    int CharacteristicId,
    string Code,
    string NameKey,
    string? UnitKey,
    int SortOrder,
    bool IsRequired);

/// <summary>A characteristic of the parts.</summary>
/// <param name="Id">The characteristic.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="UnitKey">The localisation key of the unit of measure, or <see langword="null"/>.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="IsArchived">Whether the characteristic is archived.</param>
public sealed record CharacteristicInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? UnitKey,
    string? ImageKey,
    int SortOrder,
    bool IsArchived);

/// <summary>A part with its attributes.</summary>
/// <param name="Id">The part.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="PartGroupId">The group of the part.</param>
/// <param name="MarkLevel">The mark level of the part, from one to three.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="IsArchived">Whether the part is archived.</param>
/// <param name="Attributes">The attributes of the part by their key.</param>
/// <param name="Weight">The weight of one part in tonnes (task 00027).</param>
/// <param name="Characteristics">The values of the characteristics of the part by the characteristic.</param>
public sealed record PartTypeInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    int PartGroupId,
    int MarkLevel,
    int SortOrder,
    bool IsArchived,
    IReadOnlyDictionary<string, PartAttributeInfo> Attributes,
    decimal Weight,
    IReadOnlyDictionary<int, PartCharacteristicInfo> Characteristics);

/// <summary>The value of a characteristic of a part.</summary>
/// <param name="CharacteristicId">The characteristic.</param>
/// <param name="NumberValue">The value of the characteristic.</param>
/// <param name="Levels">The values of the characteristic by the level of the part, when it has them.</param>
public sealed record PartCharacteristicInfo(
    int CharacteristicId,
    decimal NumberValue,
    IReadOnlyDictionary<int, decimal> Levels);

/// <summary>An attribute of a part.</summary>
/// <param name="AttributeKey">The key of the parameter.</param>
/// <param name="NumberValue">The value as a number, or <see langword="null"/>.</param>
/// <param name="CodeValue">The value as a logical code, or <see langword="null"/>.</param>
/// <param name="TextValue">The value as a text, or <see langword="null"/>.</param>
public sealed record PartAttributeInfo(string AttributeKey, decimal? NumberValue, string? CodeValue, string? TextValue);

/// <summary>A recipe with its ingredients.</summary>
/// <param name="Id">The recipe.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="BuildingTypeId">The factory that makes the product, or <see langword="null"/>.</param>
/// <param name="PartTypeId">The part the recipe makes, or <see langword="null"/>.</param>
/// <param name="ResourceTypeId">The resource the recipe makes, or <see langword="null"/>.</param>
/// <param name="OutputQuantity">How many units one batch gives.</param>
/// <param name="DurationSeconds">The time of one unit, in game seconds.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="IsArchived">Whether the recipe is archived.</param>
/// <param name="Ingredients">The ingredients of one unit.</param>
public sealed record RecipeInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    int? BuildingTypeId,
    int? PartTypeId,
    int? ResourceTypeId,
    long OutputQuantity,
    long DurationSeconds,
    int SortOrder,
    bool IsArchived,
    IReadOnlyList<IngredientInfo> IngredientIds);

/// <summary>An ingredient of a recipe.</summary>
/// <param name="ResourceTypeId">The resource the ingredient is, or <see langword="null"/>.</param>
/// <param name="PartTypeId">The part the ingredient is, or <see langword="null"/>.</param>
/// <param name="Amount">How many of the ingredient one unit takes.</param>
public sealed record IngredientInfo(int? ResourceTypeId, int? PartTypeId, long Amount);

/// <summary>
/// A requirement of a recipe of the production module (task 00013, decision 2.7).
/// </summary>
/// <param name="Id">The requirement.</param>
/// <param name="SubjectId">The recipe the requirement closes.</param>
/// <param name="SubjectType">The subject type of the requirement.</param>
/// <param name="RequirementType">The requirement type.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The required level of the target.</param>
public sealed record ProductionRequirementInfo(
    int Id,
    int SubjectId,
    string SubjectType,
    string RequirementType,
    int TargetId,
    int TargetLevel);
