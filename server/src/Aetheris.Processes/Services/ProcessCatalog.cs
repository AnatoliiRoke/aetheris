using Aetheris.Requirements.Services;

namespace Aetheris.Processes.Services;

/// <summary>
/// The catalog of the processes: the kinds of the processes, their recipes, their parameters
/// and the requirements of their recipes (task 00014).
/// </summary>
/// <remarks>
/// The catalog is read from the tables and kept in memory until they change, so the engine of
/// the processes asks the database once and not once for every recipe.
/// </remarks>
public sealed class ProcessCatalog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessCatalog"/> class.
    /// </summary>
    /// <param name="types">The kinds of the processes.</param>
    /// <param name="recipes">The recipes of the processes.</param>
    /// <param name="parameters">The parameters of the processes.</param>
    /// <param name="requirements">The requirements of the recipes.</param>
    public ProcessCatalog(
        IReadOnlyList<ProcessTypeInfo> types,
        IReadOnlyList<ProcessRecipeInfo> recipes,
        IReadOnlyList<ProcessParamInfo> parameters,
        IReadOnlyList<ProcessRequirementInfo> requirements)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(requirements);

        Types = types;
        Recipes = recipes;
        Parameters = parameters;
        Requirements = requirements;
    }

    /// <summary>Gets the kinds of the processes.</summary>
    public IReadOnlyList<ProcessTypeInfo> Types { get; }

    /// <summary>Gets the recipes of the processes.</summary>
    public IReadOnlyList<ProcessRecipeInfo> Recipes { get; }

    /// <summary>Gets the parameters of the processes.</summary>
    public IReadOnlyList<ProcessParamInfo> Parameters { get; }

    /// <summary>Gets the requirements of the recipes.</summary>
    public IReadOnlyList<ProcessRequirementInfo> Requirements { get; }

    /// <summary>
    /// Finds a kind of a process by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the kind.</param>
    /// <returns>The kind, or <see langword="null"/> when it does not exist.</returns>
    public ProcessTypeInfo? FindType(int id) => Types.FirstOrDefault(type => type.Id == id);

    /// <summary>
    /// Finds a recipe of a process by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the recipe.</param>
    /// <returns>The recipe, or <see langword="null"/> when it does not exist.</returns>
    public ProcessRecipeInfo? FindRecipe(int id) => Recipes.FirstOrDefault(recipe => recipe.Id == id);

    /// <summary>
    /// Gets the requirements of a recipe.
    /// </summary>
    /// <param name="recipeId">The recipe.</param>
    /// <returns>The requirements of the recipe.</returns>
    public IReadOnlyList<ProcessRequirementInfo> RequirementsOf(int recipeId) =>
        [.. Requirements.Where(requirement => requirement.SubjectId == recipeId)];

    /// <summary>
    /// Gets the parameters of a kind of a process.
    /// </summary>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <returns>The parameters of the kind.</returns>
    public IReadOnlyList<ProcessParamInfo> ParametersOf(int processTypeId) =>
        [.. Parameters.Where(parameter => parameter.ProcessTypeId == processTypeId)];

    /// <summary>
    /// Makes the catalog an order of the queue looks at: the same catalog, with the parameters
    /// of one kind of a process replaced by the ones the order was made with, so a parameter
    /// archived or changed after the order entered the queue does not change what it gives
    /// (task 00014, section 11).
    /// </summary>
    /// <param name="processTypeId">The kind of the process of the order.</param>
    /// <param name="orderParameters">The parameters the order was made with.</param>
    /// <returns>The catalog of the order.</returns>
    public ProcessCatalog WithOrderParameters(int processTypeId, IReadOnlyList<ProcessParamInfo> orderParameters)
    {
        ArgumentNullException.ThrowIfNull(orderParameters);

        return new ProcessCatalog(
            Types,
            Recipes,
            [.. Parameters.Where(parameter => parameter.ProcessTypeId != processTypeId), .. orderParameters],
            Requirements);
    }

    /// <summary>
    /// Finds the step of a parameter of a kind of a process that holds at a level: the row with
    /// the highest level that the source has reached. An archived row is out of effect.
    /// </summary>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <param name="paramKind">The logical kind of the parameter.</param>
    /// <param name="levelSource">The source of the level the parameter follows.</param>
    /// <param name="levelTargetId">The thing whose level the parameter follows, or <see langword="null"/> for the building or the part of the order.</param>
    /// <param name="level">The level the source has reached.</param>
    /// <returns>The step, or <see langword="null"/> when no row holds at the level.</returns>
    public ProcessParamInfo? ParamAt(int processTypeId, string paramKind, string levelSource, int? levelTargetId, int level) =>
        Parameters
            .Where(parameter => !parameter.IsArchived
                && parameter.ProcessTypeId == processTypeId
                && string.Equals(parameter.ParamKind, paramKind, StringComparison.Ordinal)
                && string.Equals(parameter.LevelSource, levelSource, StringComparison.Ordinal)
                && parameter.LevelTargetId == levelTargetId
                && parameter.Level <= level)
            .OrderByDescending(parameter => parameter.Level)
            .ThenBy(parameter => parameter.SortOrder)
            .ThenBy(parameter => parameter.Id)
            .FirstOrDefault();

    /// <summary>
    /// Finds the thing whose level a parameter of a kind of a process follows, for the
    /// parameters that name it: the technology of the conversion, for example.
    /// </summary>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <param name="paramKind">The logical kind of the parameter.</param>
    /// <param name="levelSource">The source of the level the parameter follows.</param>
    /// <returns>The thing, or <see langword="null"/> when no row names one.</returns>
    public int? TargetOf(int processTypeId, string paramKind, string levelSource) =>
        Parameters
            .Where(parameter => !parameter.IsArchived
                && parameter.ProcessTypeId == processTypeId
                && string.Equals(parameter.ParamKind, paramKind, StringComparison.Ordinal)
                && string.Equals(parameter.LevelSource, levelSource, StringComparison.Ordinal)
                && parameter.LevelTargetId is not null)
            .OrderBy(parameter => parameter.Level)
            .ThenBy(parameter => parameter.SortOrder)
            .ThenBy(parameter => parameter.Id)
            .Select(parameter => parameter.LevelTargetId)
            .FirstOrDefault();
}

/// <summary>
/// A kind of a process with the handler that runs it.
/// </summary>
/// <param name="Id">The identifier of the kind.</param>
/// <param name="Code">The neutral code of the kind.</param>
/// <param name="NameKey">The key of the name in the texts of the game data.</param>
/// <param name="DescriptionKey">The key of the description in the texts of the game data.</param>
/// <param name="ImageKey">The key of the image.</param>
/// <param name="HandlerCode">The logical code of the handler of the kind.</param>
/// <param name="RefundsInput">Whether cancelling an order of the kind returns its input.</param>
/// <param name="IsArchived">Whether the kind is archived.</param>
/// <param name="SortOrder">The place of the kind in the lists.</param>
public sealed record ProcessTypeInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    string HandlerCode,
    bool RefundsInput,
    bool IsArchived,
    int SortOrder);

/// <summary>
/// A recipe of a process with its ingredients.
/// </summary>
/// <param name="Id">The identifier of the recipe.</param>
/// <param name="Code">The neutral code of the recipe.</param>
/// <param name="NameKey">The key of the name in the texts of the game data.</param>
/// <param name="DescriptionKey">The key of the description in the texts of the game data.</param>
/// <param name="ImageKey">The key of the image.</param>
/// <param name="ProcessTypeId">The kind of the process.</param>
/// <param name="BuildingTypeId">The building the process runs in.</param>
/// <param name="SourceGroupId">The group of the resources the process takes.</param>
/// <param name="TargetGroupId">The group of the resources the process gives.</param>
/// <param name="DurationSeconds">How long one unit takes, in game seconds.</param>
/// <param name="ShipStub">The stub of the ship on the orbit of the body.</param>
/// <param name="IsArchived">Whether the recipe is archived.</param>
/// <param name="SortOrder">The place of the recipe in the lists.</param>
/// <param name="IngredientIds">The ingredients of the recipe.</param>
public sealed record ProcessRecipeInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    int ProcessTypeId,
    int? BuildingTypeId,
    int? SourceGroupId,
    int? TargetGroupId,
    long DurationSeconds,
    string? ShipStub,
    bool IsArchived,
    int SortOrder,
    IReadOnlyList<ProcessIngredientInfo> IngredientIds);

/// <summary>
/// An ingredient of a recipe of a process.
/// </summary>
/// <param name="ResourceTypeId">The resource the ingredient takes.</param>
/// <param name="PartTypeId">The part the ingredient takes.</param>
/// <param name="Amount">How many units one unit of the process takes.</param>
public sealed record ProcessIngredientInfo(int? ResourceTypeId, int? PartTypeId, long Amount);

/// <summary>
/// A parameter of a process.
/// </summary>
/// <param name="Id">The identifier of the parameter.</param>
/// <param name="Code">The neutral code of the parameter.</param>
/// <param name="NameKey">The key of the name in the texts of the game data.</param>
/// <param name="DescriptionKey">The key of the description in the texts of the game data.</param>
/// <param name="ProcessTypeId">The kind of the process.</param>
/// <param name="ParamKind">The logical kind of the parameter.</param>
/// <param name="LevelSource">The source of the level the parameter follows.</param>
/// <param name="LevelTargetId">The thing whose level the parameter follows.</param>
/// <param name="Level">The level the parameter starts at.</param>
/// <param name="Value">The value of the parameter.</param>
/// <param name="ValueMax">The second value of the parameter, for the ranges.</param>
/// <param name="IsArchived">Whether the parameter is archived.</param>
/// <param name="SortOrder">The place of the parameter in the lists.</param>
public sealed record ProcessParamInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    int ProcessTypeId,
    string ParamKind,
    string LevelSource,
    int? LevelTargetId,
    int Level,
    decimal Value,
    decimal? ValueMax,
    bool IsArchived,
    int SortOrder);

/// <summary>
/// A requirement of a recipe of a process.
/// </summary>
/// <param name="Id">The identifier of the requirement.</param>
/// <param name="SubjectId">The recipe the requirement belongs to.</param>
/// <param name="RequirementType">The logical type of the requirement.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The level that is required.</param>
public sealed record ProcessRequirementInfo(
    int Id,
    int SubjectId,
    string RequirementType,
    int TargetId,
    int TargetLevel);

/// <summary>
/// What the requirements of a recipe are checked against: the buildings of the body and the
/// technologies of the player (task 00014).
/// </summary>
/// <param name="BuildingLevels">The levels of the buildings of the body by their type.</param>
/// <param name="TechnologyLevels">The levels of the technologies of the player.</param>
/// <param name="BuildingNameKeys">The keys of the names of the buildings.</param>
/// <param name="TechnologyNameKeys">The keys of the names of the technologies.</param>
/// <param name="RaceId">The race of the player.</param>
/// <param name="RaceNameKeys">The keys of the names of the races.</param>
public sealed record ProcessRequirementContext(
    IReadOnlyDictionary<int, int> BuildingLevels,
    IReadOnlyDictionary<int, int> TechnologyLevels,
    IReadOnlyDictionary<int, string> BuildingNameKeys,
    IReadOnlyDictionary<int, string> TechnologyNameKeys,
    int RaceId,
    IReadOnlyDictionary<int, string> RaceNameKeys);

/// <summary>
/// A requirement that is not met, with the reason for the client.
/// </summary>
/// <param name="RequirementId">The requirement.</param>
/// <param name="RequirementType">The logical type of the requirement.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The level that is required.</param>
/// <param name="ReasonCode">The code of the reason.</param>
/// <param name="TargetNameKey">The key of the name of what is required.</param>
public sealed record ProcessUnmetRequirement(
    int RequirementId,
    string RequirementType,
    int TargetId,
    int TargetLevel,
    string ReasonCode,
    string? TargetNameKey);
