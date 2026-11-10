using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the module РПУ-X1B, the upgraded light launcher (task 00043).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class UpgradedLauncherTests
{
    private const string BaseName = "РПУ-X1";

    private const string UpgradeName = "РПУ-X1B";

    private const string UpgradeEnglishName = "Launcher X1B";

    private const string UkrainianLanguage = "uk";

    private const string EnglishLanguage = "en";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpgradedLauncherTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public UpgradedLauncherTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The starting data holds РПУ-X1B as РПУ-X1 with the next mark: the same group, attributes,
    /// recipe and requirements, and it comes last, so no code of the file shifts.
    /// </summary>
    [Fact]
    public void The_upgraded_launcher_is_the_light_launcher_with_the_next_mark()
    {
        ProductionSeedData data = ProductionReferenceSeed.Load();

        SeedPart basePart = data.Parts.Single(part => part.Name[UkrainianLanguage] == BaseName);
        SeedPart upgrade = data.Parts.Single(part => part.Name[UkrainianLanguage] == UpgradeName);
        SeedRecipe baseRecipe = data.Recipes.Single(recipe => recipe.Part == basePart.Code);
        SeedRecipe upgradeRecipe = data.Recipes.Single(recipe => recipe.Part == upgrade.Code);

        Assert.Equal(UpgradeEnglishName, upgrade.Name[EnglishLanguage]);
        Assert.Same(upgrade, data.Parts[^1]);
        Assert.Same(upgradeRecipe, data.Recipes[^1]);
        Assert.Equal($"part_{data.Parts.Count}.webp", upgrade.ImageKey);

        Assert.Equal(basePart.Group, upgrade.Group);
        Assert.Equal(basePart.MarkLevel + 1, upgrade.MarkLevel);
        Assert.Equal(basePart.SortOrder, upgrade.SortOrder);
        Assert.Equal(basePart.Attributes, upgrade.Attributes);
        Assert.Equal(basePart.Characteristics!.Keys.Order(StringComparer.Ordinal), upgrade.Characteristics!.Keys.Order(StringComparer.Ordinal));
        Assert.All(upgrade.Characteristics, value => Assert.True(value.Value.Number > basePart.Characteristics[value.Key].Number));
        Assert.True(upgrade.Weight > basePart.Weight);

        Assert.Equal(baseRecipe.Factory, upgradeRecipe.Factory);
        Assert.Equal(baseRecipe.OutputQuantity, upgradeRecipe.OutputQuantity);
        Assert.Equal(baseRecipe.DurationSeconds, upgradeRecipe.DurationSeconds);
        Assert.Equal(baseRecipe.SortOrder, upgradeRecipe.SortOrder);
        Assert.Equal(baseRecipe.Race, upgradeRecipe.Race);
        Assert.Equal(baseRecipe.Ingredients, upgradeRecipe.Ingredients);

        // The ladder of the hulls and the modules gives every part a level of its own factory
        // (task 00045), so the upgrade asks for the same factory and no science.
        Assert.All(upgradeRecipe.Requirements, requirement =>
        {
            Assert.Equal(RequirementTypes.BuildingLevel, requirement.Type);
            Assert.Equal(upgradeRecipe.Factory, requirement.Target);
        });
    }

    /// <summary>
    /// A filled database that lacks РПУ-X1B gets it at the start of the server with the code the
    /// fresh seed gives it, while the rows that are already there keep what was changed in them.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_filled_database_gets_the_upgraded_launcher_and_keeps_its_rows()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        IRequirementsModule requirements = services.GetRequiredService<IRequirementsModule>();
        await services.GetRequiredService<Aetheris.Research.IResearchModule>().EnsureAsync(Token);
        ProductionSeedData data = ProductionReferenceSeed.Load();
        string partCode = data.Parts[^1].Code;
        string recipeCode = data.Recipes[^1].Code;

        PartTypeEntity upgrade = await dbContext.PartTypes.AsNoTracking().SingleAsync(part => part.Code == partCode, Token);
        RecipeEntity upgradeRecipe = await dbContext.Recipes.AsNoTracking().SingleAsync(recipe => recipe.Code == recipeCode, Token);
        Assert.Equal(upgrade.Id, upgradeRecipe.PartTypeId);

        // The database of an older start: no РПУ-X1B, and an administrator changed the mark of РПУ-X1.
        RecipeEntity baseRecipe = await dbContext.Recipes.AsNoTracking()
            .SingleAsync(recipe => recipe.PartTypeId != null && recipe.SortOrder == upgradeRecipe.SortOrder && recipe.Id != upgradeRecipe.Id, Token);
        int changedMark = 3;
        await dbContext.PartTypes.Where(part => part.Id == baseRecipe.PartTypeId).ExecuteUpdateAsync(setters => setters.SetProperty(part => part.MarkLevel, changedMark), Token);
        await requirements.ReplaceAsync(RequirementSubjectTypes.Recipe, upgradeRecipe.Id, [], Token);
        await dbContext.Recipes.Where(recipe => recipe.Id == upgradeRecipe.Id).ExecuteDeleteAsync(Token);
        await dbContext.PartTypes.Where(part => part.Id == upgrade.Id).ExecuteDeleteAsync(Token);
        int partCount = await dbContext.PartTypes.CountAsync(Token);
        int recipeCount = await dbContext.Recipes.CountAsync(Token);

        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);

        Assert.Equal(partCount + 1, await dbContext.PartTypes.CountAsync(Token));
        Assert.Equal(recipeCount + 1, await dbContext.Recipes.CountAsync(Token));
        Assert.Equal(changedMark, await dbContext.PartTypes.Where(part => part.Id == baseRecipe.PartTypeId).Select(part => part.MarkLevel).SingleAsync(Token));

        PartTypeEntity restored = await dbContext.PartTypes.AsNoTracking().Include(part => part.AttributeIds).SingleAsync(part => part.Code == partCode, Token);
        RecipeEntity restoredRecipe = await dbContext.Recipes.AsNoTracking().Include(recipe => recipe.IngredientIds).SingleAsync(recipe => recipe.Code == recipeCode, Token);
        RecipeEntity baseRecipeRow = await dbContext.Recipes.AsNoTracking().Include(recipe => recipe.IngredientIds).SingleAsync(recipe => recipe.Id == baseRecipe.Id, Token);

        Assert.Equal(upgrade.MarkLevel, restored.MarkLevel);
        Assert.Equal(upgrade.PartGroupId, restored.PartGroupId);
        Assert.Equal(upgrade.ImageKey, restored.ImageKey);
        Assert.Equal(restored.Id, restoredRecipe.PartTypeId);
        Assert.Equal(baseRecipeRow.BuildingTypeId, restoredRecipe.BuildingTypeId);
        Assert.Equal(baseRecipeRow.DurationSeconds, restoredRecipe.DurationSeconds);
        Assert.Equal(
            baseRecipeRow.IngredientIds.Select(ingredient => (ingredient.ResourceTypeId, ingredient.PartTypeId, ingredient.Amount)).Order(),
            restoredRecipe.IngredientIds.Select(ingredient => (ingredient.ResourceTypeId, ingredient.PartTypeId, ingredient.Amount)).Order());
        Assert.NotEmpty(await dbContext.PartCharacteristicValues.Where(value => value.PartTypeId == restored.Id).ToListAsync(Token));

        // The gates are the ones of the starting data, the same as the gates of РПУ-X1 there.
        Dictionary<string, int> technologyIds = await services.GetRequiredService<Aetheris.Research.Data.ResearchDbContext>().Technologies.AsNoTracking()
            .ToDictionaryAsync(technology => technology.Code, technology => technology.Id, Token);
        SeedRecipe seedRecipe = data.Recipes[^1];
        IReadOnlyList<RequirementDefinition> restoredGates = await requirements.GetAsync(RequirementSubjectTypes.Recipe, restoredRecipe.Id, 1, Token);
        Assert.NotEmpty(restoredGates);

        List<(string Type, int TargetId, int TargetLevel)> expectedGates = [];

        foreach (Aetheris.Production.Data.SeedRequirement gate in seedRecipe.Requirements)
        {
            int targetId = gate.Type == RequirementTypes.BuildingLevel
                ? (await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>().FindBuildingTypeIdAsync(gate.Target, Token))!.Value
                : technologyIds[gate.Target];
            expectedGates.Add((gate.Type, targetId, gate.TargetLevel));
        }

        Assert.Equal(
            expectedGates.Order(),
            restoredGates.Select(gate => (gate.RequirementType, gate.TargetId, gate.TargetLevel)).Order());

        // A second start finds nothing missing.
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        Assert.Equal(partCount + 1, await dbContext.PartTypes.CountAsync(Token));
        Assert.Equal(recipeCount + 1, await dbContext.Recipes.CountAsync(Token));
    }

    /// <summary>
    /// The factory of РПУ-X1 offers РПУ-X1B with the same time, ingredients and gates.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_factory_makes_the_upgraded_launcher_like_the_light_launcher()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        ProductionSeedData data = ProductionReferenceSeed.Load();
        string recipeCode = data.Recipes[^1].Code;

        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(dbContext, services.GetRequiredService<IRequirementsModule>(), Token);
        RecipeInfo upgradeRecipe = catalog.Recipes.Single(recipe => recipe.Code == recipeCode);
        RecipeInfo baseRecipe = catalog.Recipes.Single(recipe => recipe.PartTypeId is not null && recipe.SortOrder == upgradeRecipe.SortOrder && recipe.Id != upgradeRecipe.Id);

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, services, baseRecipe);
        ProductionFactoryDto factory = await services.GetRequiredService<IProductionModule>()
            .GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token);

        ProductionRecipeDto baseOffer = factory.Recipes.Single(recipe => recipe.RecipeId == baseRecipe.Id);
        ProductionRecipeDto upgradeOffer = factory.Recipes.Single(recipe => recipe.RecipeId == upgradeRecipe.Id);

        Assert.Equal(baseOffer.UnitSeconds, upgradeOffer.UnitSeconds);
        Assert.Equal(baseOffer.IsAvailable, upgradeOffer.IsAvailable);
        Assert.Equal(baseOffer.PartGroupId, upgradeOffer.PartGroupId);
        Assert.Equal(
            baseOffer.Ingredients.Select(ingredient => (ingredient.ResourceTypeId, ingredient.PartTypeId, ingredient.Amount)).Order(),
            upgradeOffer.Ingredients.Select(ingredient => (ingredient.ResourceTypeId, ingredient.PartTypeId, ingredient.Amount)).Order());
        Assert.Equal(baseOffer.Reasons.Count, upgradeOffer.Reasons.Count);

        // In the list of the factory РПУ-X1B comes right after РПУ-X1.
        int baseIndex = factory.Recipes.ToList().FindIndex(recipe => recipe.RecipeId == baseRecipe.Id);
        Assert.Equal(upgradeRecipe.Id, factory.Recipes[baseIndex + 1].RecipeId);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}
