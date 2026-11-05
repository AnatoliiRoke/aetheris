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
        Dictionary<string, int> raceIds = races.ToDictionary(race => race.Code, race => race.Id);

        // The characteristics of the starting data may be missing as well: the number of the
        // slots of a hull and the extra slot of the race joined the data later (task 00038).
        bool added = await AddMissingCharacteristicsAsync(dbContext, data, texts, codes, cancellationToken).ConfigureAwait(false);

        // The links of the parts to the hulls and to the races may be missing even when every
        // part and every recipe is already there (task 00042), so the step looks for them too.
        bool missingLinks = await HasMissingCompatibilityLinksAsync(dbContext, data, partIds, raceIds, cancellationToken).ConfigureAwait(false);

        Dictionary<string, int> groupIds = await IdsAsync(
            dbContext.PartGroups.Select(group => new CodeId { Code = group.Code, Id = group.Id }),
            DatabaseCodes(data.PartGroups.Select(group => group.Code), ProductionTextKeys.PartGroupCodeGroup),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, int> characteristicIds = await IdsAsync(
            dbContext.Characteristics.Select(characteristic => new CodeId { Code = characteristic.Code, Id = characteristic.Id }),
            DatabaseCodes((data.Characteristics ?? []).Select(characteristic => characteristic.Code), ProductionTextKeys.CharacteristicCodeGroup),
            cancellationToken).ConfigureAwait(false);

        // The attributes and the values of the characteristics of the parts may be missing as
        // well: the role of a hull and of a module and the kind of the component joined the
        // data later (task 00038).
        added |= await AddMissingPartDetailsAsync(dbContext, data, partIds, characteristicIds, cancellationToken).ConfigureAwait(false);

        if (missingParts.Count == 0 && missingRecipes.Count == 0 && !missingLinks)
        {
            return added;
        }

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

        // The links of the parts to the hulls and to the races join the starting data later
        // (task 00042), so the step adds the ones that are missing and leaves the others as the
        // admin panel has them.
        added |= await AddCompatibilityLinksAsync(dbContext, data, partIds, raceIds, cancellationToken).ConfigureAwait(false);

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
    /// Tells whether the starting data names a link of a part to a hull or to a race that the
    /// database does not have yet.
    /// </summary>
    /// <summary>
    /// Adds the attributes and the values of the characteristics of the parts of the starting
    /// data that are missing.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="data">The starting data.</param>
    /// <param name="partIds">The identifiers of the parts of the database, by the codes of the file.</param>
    /// <param name="characteristicIds">The identifiers of the characteristics of the database, by the codes of the file.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a row was added.</returns>
    /// <remarks>
    /// The step only adds: an attribute or a value that the admin panel has already changed
    /// stays as it is, so the rules of the game survive the update of the starting data.
    /// </remarks>
    private static async Task<bool> AddMissingPartDetailsAsync(
        ProductionDbContext dbContext,
        ProductionSeedData data,
        IReadOnlyDictionary<string, int> partIds,
        IReadOnlyDictionary<string, int> characteristicIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. partIds.Values];

        if (ids.Count == 0)
        {
            return false;
        }

        Dictionary<int, HashSet<string>> attributeKeys = await dbContext.PartAttributes.AsNoTracking()
            .Where(attribute => ids.Contains(attribute.PartTypeId))
            .GroupBy(attribute => attribute.PartTypeId)
            .ToDictionaryAsync(
                group => group.Key,
                group => (HashSet<string>)group.Select(attribute => attribute.AttributeKey).ToHashSet(StringComparer.Ordinal),
                cancellationToken)
            .ConfigureAwait(false);

        Dictionary<int, HashSet<int>> characteristicKeys = await dbContext.PartCharacteristicValues.AsNoTracking()
            .Where(value => ids.Contains(value.PartTypeId))
            .GroupBy(value => value.PartTypeId)
            .ToDictionaryAsync(
                group => group.Key,
                group => (HashSet<int>)group.Select(value => value.CharacteristicId).ToHashSet(),
                cancellationToken)
            .ConfigureAwait(false);

        bool added = false;

        foreach (SeedPart part in data.Parts)
        {
            if (!partIds.TryGetValue(part.Code, out int partId))
            {
                continue;
            }

            HashSet<string> keys = attributeKeys.GetValueOrDefault(partId) ?? [];

            foreach (KeyValuePair<string, SeedAttributeValue> attribute in part.Attributes)
            {
                if (!keys.Add(attribute.Key))
                {
                    continue;
                }

                dbContext.PartAttributes.Add(new PartAttributeEntity
                {
                    PartTypeId = partId,
                    AttributeKey = attribute.Key,
                    NumberValue = attribute.Value.Number,
                    CodeValue = attribute.Value.Code,
                    TextValue = attribute.Value.Text,
                });

                added = true;
            }

            HashSet<int> known = characteristicKeys.GetValueOrDefault(partId) ?? [];

            foreach (KeyValuePair<string, SeedCharacteristicValue> value in part.Characteristics ?? new Dictionary<string, SeedCharacteristicValue>())
            {
                if (!characteristicIds.TryGetValue(value.Key, out int characteristicId) || !known.Add(characteristicId))
                {
                    continue;
                }

                PartCharacteristicValueEntity characteristicValue = new()
                {
                    PartTypeId = partId,
                    CharacteristicId = characteristicId,
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
                added = true;
            }
        }

        if (added)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return added;
    }

    /// <summary>
    /// Adds the characteristics of the starting data whose rows are missing.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="data">The starting data.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a row was added.</returns>
    /// <remarks>
    /// A characteristic that joins the starting data later takes the next code of the counter
    /// of the server, like the parts and the recipes do. The step only adds: a characteristic
    /// whose row is already there keeps the kind and the texts the admin panel has given it.
    /// </remarks>
    private static async Task<bool> AddMissingCharacteristicsAsync(
        ProductionDbContext dbContext,
        ProductionSeedData data,
        ILocalizationTexts texts,
        ICodeSequence codes,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> databaseCodes = DatabaseCodes(
            (data.Characteristics ?? []).Select(characteristic => characteristic.Code),
            ProductionTextKeys.CharacteristicCodeGroup);

        HashSet<string> existing = await dbContext.Characteristics.AsNoTracking()
            .Select(characteristic => characteristic.Code)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        List<SeedCharacteristic> missing = [.. (data.Characteristics ?? [])
            .Where(characteristic => !existing.Contains(databaseCodes[characteristic.Code]))];

        if (missing.Count == 0)
        {
            return false;
        }

        List<CharacteristicEntity> entities = [];

        foreach (SeedCharacteristic characteristic in missing)
        {
            string code = await codes.NextAsync(ProductionTextKeys.CharacteristicCodeGroup, cancellationToken).ConfigureAwait(false);

            CharacteristicEntity entity = new()
            {
                Code = code,
                NameKey = ProductionTextKeys.CharacteristicName(code),
                DescriptionKey = ProductionTextKeys.CharacteristicDescription(code),
                UnitKey = characteristic.Unit is null ? null : ProductionTextKeys.CharacteristicUnit(code),
                Kind = characteristic.Kind,
                SortOrder = characteristic.SortOrder,
            };

            dbContext.Characteristics.Add(entity);
            entities.Add(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await texts.AddMissingAsync(
            [
                .. missing.Zip(entities).SelectMany(pair => (IEnumerable<LocalizationTextEntry>)
                [
                    .. ProductionSeedTexts.Entries(pair.Second.NameKey, pair.First.Name),
                    .. ProductionSeedTexts.Entries(pair.Second.DescriptionKey, pair.First.Description),
                    .. ProductionSeedTexts.Entries(pair.Second.UnitKey, pair.First.Unit),
                ]),
            ],
            ProductionReferenceSeed.ProductionSeedActor,
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static async Task<bool> HasMissingCompatibilityLinksAsync(
        ProductionDbContext dbContext,
        ProductionSeedData data,
        IReadOnlyDictionary<string, int> partIds,
        IReadOnlyDictionary<string, int> raceIds,
        CancellationToken cancellationToken)
    {
        HashSet<(int PartTypeId, int HullPartTypeId)> existingHulls =
        [
            .. (await dbContext.PartHullCompatibilities
                .AsNoTracking()
                .Select(link => new { link.PartTypeId, link.HullPartTypeId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .Select(link => (link.PartTypeId, link.HullPartTypeId)),
        ];

        HashSet<(int PartTypeId, int RaceId)> existingRaces =
        [
            .. (await dbContext.PartRaceCompatibilities
                .AsNoTracking()
                .Select(link => new { link.PartTypeId, link.RaceId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .Select(link => (link.PartTypeId, link.RaceId)),
        ];

        foreach (SeedPartHull link in data.PartHulls ?? [])
        {
            if (!partIds.TryGetValue(link.Part, out int partId))
            {
                continue;
            }

            foreach (string hull in link.Hulls)
            {
                if (partIds.TryGetValue(hull, out int hullId) && !existingHulls.Contains((partId, hullId)))
                {
                    return true;
                }
            }
        }

        foreach (SeedPartRace link in data.PartRaces ?? [])
        {
            if (!partIds.TryGetValue(link.Part, out int partId))
            {
                continue;
            }

            foreach (string race in link.Races)
            {
                if (raceIds.TryGetValue(race, out int raceId) && !existingRaces.Contains((partId, raceId)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Adds the links of the parts to the hulls and to the races that are missing.
    /// </summary>
    /// <remarks>
    /// The step only adds: a link that is already there is not touched, so a change of the
    /// admin panel stays. A link whose part, hull or race is not in the database waits for a
    /// later start, and it is written with both sides of the link as real rows.
    /// </remarks>
    private static async Task<bool> AddCompatibilityLinksAsync(
        ProductionDbContext dbContext,
        ProductionSeedData data,
        IReadOnlyDictionary<string, int> partIds,
        IReadOnlyDictionary<string, int> raceIds,
        CancellationToken cancellationToken)
    {
        List<PartHullCompatibilityEntity> hulls = [];
        List<PartRaceCompatibilityEntity> races = [];

        foreach (SeedPartHull link in data.PartHulls ?? [])
        {
            if (!partIds.TryGetValue(link.Part, out int partId))
            {
                continue;
            }

            foreach (string hull in link.Hulls)
            {
                if (partIds.TryGetValue(hull, out int hullId))
                {
                    hulls.Add(new PartHullCompatibilityEntity
                    {
                        PartTypeId = partId,
                        HullPartTypeId = hullId,
                    });
                }
            }
        }

        foreach (SeedPartRace link in data.PartRaces ?? [])
        {
            if (!partIds.TryGetValue(link.Part, out int partId))
            {
                continue;
            }

            foreach (string race in link.Races)
            {
                if (raceIds.TryGetValue(race, out int raceId))
                {
                    races.Add(new PartRaceCompatibilityEntity
                    {
                        PartTypeId = partId,
                        RaceId = raceId,
                    });
                }
            }
        }

        if (hulls.Count == 0 && races.Count == 0)
        {
            return false;
        }

        // The links that are already there are left as the admin panel has them, so only the
        // missing ones are written. The reading is in one query, so the table is not walked
        // row by row.
        List<PartHullCompatibilityEntity> existingHullRows = await dbContext.PartHullCompatibilities
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PartRaceCompatibilityEntity> existingRaceRows = await dbContext.PartRaceCompatibilities
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<(int PartTypeId, int HullPartTypeId)> existingHulls =
            [.. existingHullRows.Select(link => (link.PartTypeId, link.HullPartTypeId))];
        HashSet<(int PartTypeId, int RaceId)> existingRaces =
            [.. existingRaceRows.Select(link => (link.PartTypeId, link.RaceId))];

        dbContext.PartHullCompatibilities.AddRange([.. hulls.Where(link => !existingHulls.Contains((link.PartTypeId, link.HullPartTypeId)))]);
        dbContext.PartRaceCompatibilities.AddRange([.. races.Where(link => !existingRaces.Contains((link.PartTypeId, link.RaceId)))]);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
