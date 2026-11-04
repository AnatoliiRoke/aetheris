using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Data;

/// <summary>
/// Adds to a filled database the parts and the recipes that joined the starting data later
/// (task 00043, module РПУ-X1B).
/// </summary>
/// <remarks>
/// <para>
/// The first run of the seed gives the rows of the file the codes of the server counters in the
/// order of the file: the n-th part becomes <c>part_n</c>, the n-th recipe <c>recipe_n</c>, the
/// n-th group <c>part_group_n</c> and the n-th characteristic <c>characteristic_n</c>. A row that
/// joins the file is appended at its end, so in a filled database it is the code that is missing.
/// </para>
/// <para>
/// The step only adds: a row whose code is already there is not touched, so a change of the admin
/// panel stays. An archived row is still there, so it does not come back. A part whose group is
/// not in the database waits for a later start, and a recipe whose part is missing waits with it.
/// </para>
/// </remarks>
public static class ProductionSeedAdditions
{
    /// <summary>
    /// Adds the parts and the recipes of the starting data whose codes are missing.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="resources">The resource catalog of the world.</param>
    /// <param name="races">The races of the world.</param>
    /// <param name="planets">The bodies module, for the factories and the building gates.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="requirements">The common requirements module, for the requirements of the recipes.</param>
    /// <param name="technologyIds">The identifiers of the technologies by their codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a row was added.</returns>
    public static async Task<bool> SeedMissingAsync(
        ProductionDbContext dbContext,
        ResourceCatalog resources,
        IReadOnlyList<RaceDto> races,
        IPlanetsModule planets,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IRequirementsModule requirements,
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
        ArgumentNullException.ThrowIfNull(technologyIds);

        // A database the seed has not filled yet gets everything from the seed itself.
        if (!await dbContext.Recipes.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        ProductionSeedData data = ProductionReferenceSeed.Load();

        Dictionary<string, string> partCodes = DatabaseCodes(data.Parts.Select(part => part.Code), ProductionTextKeys.PartTypeCodeGroup);
        Dictionary<string, string> recipeCodes = DatabaseCodes(data.Recipes.Select(recipe => recipe.Code), ProductionTextKeys.RecipeCodeGroup);

        Dictionary<string, int> partIds = await IdsAsync(dbContext.PartTypes.Select(part => new CodeId { Code = part.Code, Id = part.Id }), partCodes, cancellationToken).ConfigureAwait(false);
        Dictionary<string, int> recipeIds = await IdsAsync(dbContext.Recipes.Select(recipe => new CodeId { Code = recipe.Code, Id = recipe.Id }), recipeCodes, cancellationToken).ConfigureAwait(false);

        List<SeedPart> missingParts = [.. data.Parts.Where(part => !partIds.ContainsKey(part.Code))];
        List<SeedRecipe> missingRecipes = [.. data.Recipes.Where(recipe => !recipeIds.ContainsKey(recipe.Code))];

        if (missingParts.Count == 0 && missingRecipes.Count == 0)
        {
            return false;
        }

        Dictionary<string, int> groupIds = await IdsAsync(
            dbContext.PartGroups.Select(group => new CodeId { Code = group.Code, Id = group.Id }),
            DatabaseCodes(data.PartGroups.Select(group => group.Code), ProductionTextKeys.PartGroupCodeGroup),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, int> characteristicIds = await IdsAsync(
            dbContext.Characteristics.Select(characteristic => new CodeId { Code = characteristic.Code, Id = characteristic.Id }),
            DatabaseCodes((data.Characteristics ?? []).Select(characteristic => characteristic.Code), ProductionTextKeys.CharacteristicCodeGroup),
            cancellationToken).ConfigureAwait(false);

        bool added = false;
        List<SeedPart> addableParts = [.. missingParts.Where(part => groupIds.ContainsKey(part.Group))];
        List<PartTypeEntity> newParts = [];

        foreach (SeedPart part in addableParts)
        {
            PartTypeEntity entity = new()
            {
                Code = partCodes[part.Code],
                NameKey = ProductionTextKeys.PartName(partCodes[part.Code]),
                DescriptionKey = ProductionTextKeys.PartDescription(partCodes[part.Code]),
                PartGroupId = groupIds[part.Group],
                MarkLevel = part.MarkLevel,
                SortOrder = part.SortOrder,
                Weight = part.Weight ?? 1m,
                ImageKey = part.ImageKey,
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
            newParts.Add(entity);
        }

        if (newParts.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach ((SeedPart part, PartTypeEntity entity) in addableParts.Zip(newParts))
            {
                partIds[part.Code] = entity.Id;

                foreach (KeyValuePair<string, SeedCharacteristicValue> value in (part.Characteristics ?? new Dictionary<string, SeedCharacteristicValue>())
                    .Where(value => characteristicIds.ContainsKey(value.Key)))
                {
                    PartCharacteristicValueEntity characteristicValue = new()
                    {
                        PartTypeId = entity.Id,
                        CharacteristicId = characteristicIds[value.Key],
                        NumberValue = value.Value.Number,
                    };

                    foreach (KeyValuePair<int, decimal> level in value.Value.Levels ?? new Dictionary<int, decimal>())
                    {
                        characteristicValue.Levels.Add(new PartCharacteristicLevelValueEntity
                        {
                            Level = level.Key,
                            NumberValue = level.Value,
                        });
                    }

                    dbContext.PartCharacteristicValues.Add(characteristicValue);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await texts.AddMissingAsync(
                [
                    .. addableParts.Zip(newParts).SelectMany(pair => (IEnumerable<LocalizationTextEntry>)
                    [
                        .. ProductionSeedTexts.Entries(pair.Second.NameKey, pair.First.Name),
                        .. ProductionSeedTexts.Entries(pair.Second.DescriptionKey, pair.First.Description),
                    ]),
                ],
                ProductionReferenceSeed.ProductionSeedActor,
                cancellationToken).ConfigureAwait(false);

            await RaiseAsync(codes, ProductionTextKeys.PartTypeCodeGroup, newParts.Select(part => part.Code), cancellationToken).ConfigureAwait(false);
            added = true;
        }

        Dictionary<string, int> resourceIds = resources.Resources.ToDictionary(resource => resource.Code, resource => resource.Id);
        Dictionary<string, int> raceIds = races.ToDictionary(race => race.Code, race => race.Id);

        // A recipe is added when its product, its ingredients and the technologies of its gates
        // are all in the database; a recipe without its gate would be free, so it waits instead.
        List<SeedRecipe> readyRecipes =
        [
            .. missingRecipes.Where(recipe =>
                (recipe.Part is null || partIds.ContainsKey(recipe.Part))
                && recipe.Requirements.All(requirement =>
                    requirement.Type != RequirementTypes.TechnologyLevel || technologyIds.ContainsKey(requirement.Target))
                && (recipe.Resource is null || resourceIds.ContainsKey(recipe.Resource))
                && recipe.Ingredients.All(ingredient =>
                    (ingredient.Part is null || partIds.ContainsKey(ingredient.Part))
                    && (ingredient.Resource is null || resourceIds.ContainsKey(ingredient.Resource)))),
        ];

        if (readyRecipes.Count == 0)
        {
            return added;
        }

        Dictionary<string, int> factoryIds = [];

        foreach (string factory in readyRecipes
            .Select(recipe => recipe.Factory)
            .Where(code => code is not null)
            .Distinct(StringComparer.Ordinal)
            .Select(code => code!))
        {
            factoryIds[factory] = await planets.FindBuildingTypeIdAsync(factory, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The factory '{factory}' of the starting data does not exist.");
        }

        List<RecipeEntity> newRecipes = [];

        foreach (SeedRecipe recipe in readyRecipes)
        {
            RecipeEntity entity = new()
            {
                Code = recipeCodes[recipe.Code],
                NameKey = ProductionTextKeys.RecipeName(recipeCodes[recipe.Code]),
                DescriptionKey = ProductionTextKeys.RecipeDescription(recipeCodes[recipe.Code]),
                OutputQuantity = recipe.OutputQuantity,
                DurationSeconds = recipe.DurationSeconds,
                SortOrder = recipe.SortOrder,
                PartTypeId = recipe.Part is not null ? partIds[recipe.Part] : null,
                ResourceTypeId = recipe.Resource is not null ? resourceIds[recipe.Resource] : null,
                BuildingTypeId = recipe.Factory is not null ? factoryIds[recipe.Factory] : null,
            };

            foreach (SeedIngredient ingredient in recipe.Ingredients)
            {
                entity.IngredientIds.Add(new RecipeIngredientEntity
                {
                    ResourceTypeId = ingredient.Resource is not null ? resourceIds[ingredient.Resource] : null,
                    PartTypeId = ingredient.Part is not null ? partIds[ingredient.Part] : null,
                    Amount = ingredient.Amount,
                });
            }

            dbContext.Recipes.Add(entity);
            newRecipes.Add(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. readyRecipes.Zip(newRecipes).SelectMany(pair => (IEnumerable<LocalizationTextEntry>)
                [
                    .. ProductionSeedTexts.Entries(pair.Second.NameKey, pair.First.Name),
                    .. ProductionSeedTexts.Entries(pair.Second.DescriptionKey, pair.First.Description),
                ]),
            ],
            ProductionReferenceSeed.ProductionSeedActor,
            cancellationToken).ConfigureAwait(false);

        foreach ((SeedRecipe recipe, RecipeEntity entity) in readyRecipes.Zip(newRecipes))
        {
            List<RequirementDefinition> definitions = [];

            foreach (SeedRequirement requirement in recipe.Requirements)
            {
                int targetId = requirement.Type switch
                {
                    RequirementTypes.BuildingLevel => await planets.FindBuildingTypeIdAsync(requirement.Target, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"The building '{requirement.Target}' of the starting data does not exist."),
                    RequirementTypes.TechnologyLevel => technologyIds.TryGetValue(requirement.Target, out int technologyId)
                        ? technologyId
                        : throw new InvalidOperationException($"The technology '{requirement.Target}' of the starting data does not exist."),
                    ProductionRequirementTypes.Race => raceIds[requirement.Target],
                    _ => throw new InvalidOperationException($"The starting data names the requirement type '{requirement.Type}', which it cannot resolve."),
                };

                definitions.Add(new RequirementDefinition(requirement.Type, targetId, requirement.TargetLevel));
            }

            if (recipe.Race is not null)
            {
                definitions.Add(new RequirementDefinition(ProductionRequirementTypes.Race, raceIds[recipe.Race], 0));
            }

            if (definitions.Count == 0)
            {
                continue;
            }

            await requirements.ReplaceAsync(
                RequirementSubjectTypes.Recipe,
                entity.Id,
                [.. definitions.Select(definition => new SubjectRequirement(entity.Id, 1, definition))],
                cancellationToken).ConfigureAwait(false);
        }

        await RaiseAsync(codes, ProductionTextKeys.RecipeCodeGroup, newRecipes.Select(recipe => recipe.Code), cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gives every code of the file the code the first run of the seed gave its row.
    /// </summary>
    private static Dictionary<string, string> DatabaseCodes(IEnumerable<string> fileCodes, string group) =>
        fileCodes
            .Select((code, index) => (code, index))
            .ToDictionary(pair => pair.code, pair => $"{group}_{pair.index + 1}", StringComparer.Ordinal);

    /// <summary>
    /// Reads the identifiers of the rows of the database by the codes of the file, in one query.
    /// </summary>
    private static async Task<Dictionary<string, int>> IdsAsync(
        IQueryable<CodeId> rows,
        Dictionary<string, string> databaseCodes,
        CancellationToken cancellationToken)
    {
        List<string> wanted = [.. databaseCodes.Values];

        Dictionary<string, int> idsByDatabaseCode = await rows
            .Where(row => wanted.Contains(row.Code))
            .ToDictionaryAsync(row => row.Code, row => row.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        return databaseCodes
            .Where(pair => idsByDatabaseCode.ContainsKey(pair.Value))
            .ToDictionary(pair => pair.Key, pair => idsByDatabaseCode[pair.Value], StringComparer.Ordinal);
    }

    /// <summary>
    /// Lifts the counter of a group above the codes the step wrote, so the next row of the admin
    /// panel gets a free code.
    /// </summary>
    private static async Task RaiseAsync(ICodeSequence codes, string group, IEnumerable<string> written, CancellationToken cancellationToken)
    {
        int largest = written
            .Select(code => int.Parse(code.AsSpan(group.Length + 1), System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();

        if (largest > 0)
        {
            await codes.RaiseAsync(group, largest, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The code and the identifier of a row.</summary>
    private sealed class CodeId
    {
        /// <summary>The neutral code of the row.</summary>
        public required string Code { get; init; }

        /// <summary>The identifier of the row.</summary>
        public required int Id { get; init; }
    }
}
