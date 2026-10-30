using System.Text.Json;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Data;

/// <summary>
/// Fills the tables of the processes with the starting data (task 00014).
/// </summary>
/// <remarks>
/// The starting data lives in the embedded file <c>Data/process-reference-seed.json</c>: the
/// kinds of the processes, their recipes with their ingredients and their parameters. Every
/// row has a neutral code and its texts in every language (task 00022). The seed runs only
/// while the tables are empty, so it never overwrites a change of the admin panel (task
/// 00023, decision 7).
/// </remarks>
public static class ProcessReferenceSeed
{
    /// <summary>
    /// The name of the embedded starting data.
    /// </summary>
    public const string ResourceName = "Aetheris.Processes.reference-seed.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Reads the starting data.
    /// </summary>
    /// <returns>The starting data.</returns>
    public static ProcessSeedData Load()
    {
        using Stream stream = typeof(ProcessReferenceSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is missing.");

        return JsonSerializer.Deserialize<ProcessSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"The embedded starting data {ResourceName} is empty.");
    }

    /// <summary>
    /// Writes the starting data when the tables are empty.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="races">The races of the world.</param>
    /// <param name="planets">The bodies module, for the buildings the processes run in.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="requirements">The common requirements module, for the requirements of the recipes.</param>
    /// <param name="partIds">The parts of the production module by their neutral code.</param>
    /// <param name="technologyIds">The technologies by their neutral code.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the data was written.</returns>
    public static async Task<bool> SeedAsync(
        ProcessesDbContext dbContext,
        ResourceCatalog resources,
        IReadOnlyList<RaceDto> races,
        IPlanetsModule planets,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IRequirementsModule requirements,
        IReadOnlyDictionary<string, int> partIds,
        IReadOnlyDictionary<string, int> technologyIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(races);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(partIds);
        ArgumentNullException.ThrowIfNull(technologyIds);

        // The seed runs while the recipes are empty, so it fills the gaps of a database that
        // was emptied by hand and never overwrites a change of the admin panel (task 00023,
        // decision 7).
        if (await dbContext.ProcessRecipes.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ProcessSeedData data = Load();

        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id);
        Dictionary<string, int> groupIds = resources.Groups.ToDictionary(group => group.Code, group => group.Id);

        // The building a process runs in is named by its class, and the class becomes the
        // identifier of the building type (documentation/tech/09, section 3.3).
        Dictionary<string, int> buildingIds = [];

        foreach (string buildingClass in data.Recipes
            .Select(recipe => recipe.BuildingClass)
            .Where(value => value is not null)
            .Distinct(StringComparer.Ordinal)
            .Select(value => value!)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            int buildingTypeId = await planets.FindBuildingTypeIdByClassAsync(buildingClass, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The building of the class '{buildingClass}' of the starting data does not exist.");

            buildingIds[buildingClass] = buildingTypeId;
        }

        Dictionary<string, int> typeIds = [];
        List<ProcessTypeEntity> types = [];

        foreach (SeedProcessType type in data.Types)
        {
            string code = await codes.NextAsync(ProcessTextKeys.ProcessTypeCodeGroup, cancellationToken).ConfigureAwait(false);

            ProcessTypeEntity entity = new()
            {
                Code = code,
                NameKey = ProcessTextKeys.TypeName(code),
                DescriptionKey = ProcessTextKeys.TypeDescription(code),
                HandlerCode = type.Handler,
                RefundsInput = type.RefundsInput,
                SortOrder = type.SortOrder,
            };

            types.Add(entity);

            await texts.AddMissingAsync(
                [
                    .. ProcessSeedTexts.Entries(entity.NameKey, type.Name),
                    .. ProcessSeedTexts.Entries(entity.DescriptionKey, type.Description),
                ],
                ProcessSeedTexts.Author,
                cancellationToken).ConfigureAwait(false);
        }

        dbContext.ProcessTypes.AddRange(types);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The rows get their numbers when they are saved, so the kinds are read after the
        // save and not before it.
        foreach (ProcessTypeEntity entity in types)
        {
            typeIds[TypeKeyOf(data, entity.HandlerCode)] = entity.Id;
        }

        Dictionary<string, int> recipeIds = [];
        List<ProcessRecipeEntity> recipes = [];

        foreach (SeedProcessRecipe recipe in data.Recipes)
        {
            string code = await codes.NextAsync(ProcessTextKeys.ProcessRecipeCodeGroup, cancellationToken).ConfigureAwait(false);

            ProcessRecipeEntity entity = new()
            {
                Code = code,
                NameKey = ProcessTextKeys.RecipeName(code),
                DescriptionKey = ProcessTextKeys.RecipeDescription(code),
                ProcessTypeId = typeIds[recipe.Type],
                BuildingTypeId = recipe.BuildingClass is null ? null : buildingIds[recipe.BuildingClass],
                SourceGroupId = recipe.SourceGroup is null ? null : groupIds[recipe.SourceGroup],
                TargetGroupId = recipe.TargetGroup is null ? null : groupIds[recipe.TargetGroup],
                DurationSeconds = recipe.DurationSeconds,
                ShipStub = recipe.ShipStub,
                SortOrder = recipe.SortOrder,
            };

            recipes.Add(entity);

            await texts.AddMissingAsync(
                [
                    .. ProcessSeedTexts.Entries(entity.NameKey, recipe.Name),
                    .. ProcessSeedTexts.Entries(entity.DescriptionKey, recipe.Description),
                ],
                ProcessSeedTexts.Author,
                cancellationToken).ConfigureAwait(false);
        }

        dbContext.ProcessRecipes.AddRange(recipes);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        for (int index = 0; index < data.Recipes.Count; index++)
        {
            recipeIds[data.Recipes[index].Key] = recipes[index].Id;
        }

        // The rows get their numbers when they are saved, so the recipes are read after the
        // save and not before it.
        for (int index = 0; index < data.Recipes.Count; index++)
        {
            SeedProcessRecipe recipe = data.Recipes[index];
            ProcessRecipeEntity entity = recipes[index];

            foreach (SeedIngredient ingredient in recipe.Ingredients)
            {
                entity.IngredientIds.Add(new ProcessRecipeIngredientEntity
                {
                    ResourceTypeId = ingredient.Resource is null ? null : resourceIds[ingredient.Resource],
                    PartTypeId = ingredient.Part is null ? null : IdOf(partIds, ingredient.Part, "part"),
                    Amount = ingredient.Amount,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (SeedProcessParam parameter in data.Params)
        {
            string code = await codes.NextAsync(ProcessTextKeys.ProcessParamCodeGroup, cancellationToken).ConfigureAwait(false);

            ProcessParamEntity entity = new()
            {
                Code = code,
                NameKey = ProcessTextKeys.ParamName(code),
                DescriptionKey = ProcessTextKeys.ParamDescription(code),
                ProcessTypeId = typeIds[parameter.Type],
                ParamKind = parameter.Kind,
                LevelSource = parameter.LevelSource,
                LevelTargetId = parameter.LevelTarget is null ? null : IdOf(technologyIds, parameter.LevelTarget, "technology"),
                Level = parameter.Level,
                Value = parameter.Value,
                ValueMax = parameter.ValueMax,
                SortOrder = parameter.SortOrder,
            };

            dbContext.ProcessParams.Add(entity);

            await texts.AddMissingAsync(
                [
                    .. ProcessSeedTexts.Entries(entity.NameKey, parameter.Name),
                    .. ProcessSeedTexts.Entries(entity.DescriptionKey, parameter.Description),
                ],
                ProcessSeedTexts.Author,
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The requirements of the recipes live in the common requirements module, so the seed
        // writes them through its contract and never into the tables of that module.
        foreach (SeedProcessRecipe recipe in data.Recipes)
        {
            if (recipe.Requirements.Count == 0)
            {
                continue;
            }

            List<RequirementDefinition> definitions = [.. recipe.Requirements.Select(requirement => new RequirementDefinition(
                requirement.Type,
                requirement.TargetId,
                requirement.TargetLevel))];

            await requirements.AddAsync(
                RequirementSubjectTypes.ProcessRecipe,
                recipeIds[recipe.Key],
                1,
                definitions,
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Finds a row of another module the starting data names by its neutral code.
    /// </summary>
    /// <param name="ids">The rows by their code.</param>
    /// <param name="code">The code the starting data names.</param>
    /// <param name="kind">The kind of the row, for the message.</param>
    /// <returns>The identifier of the row.</returns>
    /// <exception cref="InvalidOperationException">The row does not exist.</exception>
    private static int IdOf(IReadOnlyDictionary<string, int> ids, string code, string kind) =>
        ids.TryGetValue(code, out int id)
            ? id
            : throw new InvalidOperationException($"The starting data of the processes name the {kind} '{code}', which does not exist.");

    /// <summary>
    /// Finds the key of a kind of a process in the starting data by its handler.
    /// </summary>
    /// <param name="data">The starting data.</param>
    /// <param name="handlerCode">The logical code of the handler of the kind.</param>
    /// <returns>The key of the kind.</returns>
    private static string TypeKeyOf(ProcessSeedData data, string handlerCode) =>
        data.Types.First(type => string.Equals(type.Handler, handlerCode, StringComparison.Ordinal)).Key;
}

/// <summary>
/// The starting data of the processes.
/// </summary>
/// <param name="Types">The kinds of the processes.</param>
/// <param name="Recipes">The recipes of the processes.</param>
/// <param name="Params">The parameters of the processes.</param>
public sealed record ProcessSeedData(
    IReadOnlyList<SeedProcessType> Types,
    IReadOnlyList<SeedProcessRecipe> Recipes,
    IReadOnlyList<SeedProcessParam> Params);

/// <summary>
/// A kind of a process in the starting data.
/// </summary>
/// <param name="Key">The key of the kind inside the starting data.</param>
/// <param name="Handler">The logical code of the handler of the kind.</param>
/// <param name="RefundsInput">Whether cancelling an order of the kind returns its input.</param>
/// <param name="SortOrder">The place of the kind in the lists.</param>
/// <param name="Name">The name of the kind by language.</param>
/// <param name="Description">The description of the kind by language.</param>
public sealed record SeedProcessType(
    string Key,
    string Handler,
    bool RefundsInput,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description);

/// <summary>
/// A recipe of a process in the starting data.
/// </summary>
/// <param name="Key">The key of the recipe inside the starting data.</param>
/// <param name="Type">The key of the kind of the process.</param>
/// <param name="BuildingClass">The class of the building the process runs in.</param>
/// <param name="SourceGroup">The neutral code of the group of the resources it takes.</param>
/// <param name="TargetGroup">The neutral code of the group of the resources it gives.</param>
/// <param name="DurationSeconds">How long one unit takes, in game seconds.</param>
/// <param name="ShipStub">The stub of the ship on the orbit of the body.</param>
/// <param name="SortOrder">The place of the recipe in the lists.</param>
/// <param name="Name">The name of the recipe by language.</param>
/// <param name="Description">The description of the recipe by language.</param>
/// <param name="Ingredients">The ingredients of the recipe.</param>
/// <param name="Requirements">The requirements of the recipe.</param>
public sealed record SeedProcessRecipe(
    string Key,
    string Type,
    string? BuildingClass,
    string? SourceGroup,
    string? TargetGroup,
    long DurationSeconds,
    string? ShipStub,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description,
    IReadOnlyList<SeedIngredient> Ingredients,
    IReadOnlyList<SeedRequirement> Requirements);

/// <summary>
/// An ingredient of a recipe in the starting data.
/// </summary>
/// <param name="Resource">The neutral code of the resource the ingredient takes.</param>
/// <param name="Part">The neutral code of the part the ingredient takes.</param>
/// <param name="Amount">How many units one unit of the process takes.</param>
public sealed record SeedIngredient(string? Resource, string? Part, long Amount);

/// <summary>
/// A requirement of a recipe in the starting data.
/// </summary>
/// <param name="Type">The logical type of the requirement.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The level that is required.</param>
public sealed record SeedRequirement(string Type, int TargetId, int TargetLevel);

/// <summary>
/// A parameter of a process in the starting data.
/// </summary>
/// <param name="Type">The key of the kind of the process.</param>
/// <param name="Kind">The logical kind of the parameter.</param>
/// <param name="LevelSource">The source of the level the parameter follows.</param>
/// <param name="LevelTarget">The neutral code of the technology whose level the parameter follows.</param>
/// <param name="Level">The level the parameter starts at.</param>
/// <param name="Value">The value of the parameter.</param>
/// <param name="ValueMax">The second value of the parameter, for the ranges.</param>
/// <param name="SortOrder">The place of the parameter in the lists.</param>
/// <param name="Name">The name of the parameter by language.</param>
/// <param name="Description">The description of the parameter by language.</param>
public sealed record SeedProcessParam(
    string Type,
    string Kind,
    string LevelSource,
    string? LevelTarget,
    int Level,
    decimal Value,
    decimal? ValueMax,
    int SortOrder,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyDictionary<string, string> Description);

/// <summary>
/// The texts of the starting data of the processes.
/// </summary>
public static class ProcessSeedTexts
{
    /// <summary>
    /// The author of the texts of the starting data.
    /// </summary>
    public const string Author = "processes_seed";

    /// <summary>
    /// Builds the texts of one key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="texts">The text by language, or <see langword="null"/>.</param>
    /// <returns>The texts.</returns>
    public static IEnumerable<LocalizationTextEntry> Entries(string key, IReadOnlyDictionary<string, string>? texts) =>
        texts is null ? [] : texts.Select(text => new LocalizationTextEntry(key, text.Key, text.Value));
}
