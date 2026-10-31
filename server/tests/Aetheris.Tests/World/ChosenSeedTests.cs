using Aetheris.Core.Configuration;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Data;
using Aetheris.Economy.Data.Entities;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Planets;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Data;
using Aetheris.Production;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements.Data;
using Aetheris.Requirements.Data.Entities;
using Aetheris.Requirements.Services;
using Aetheris.Research;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// The first seed of the chosen elements (task 00028): counts, texts, references, the
/// growth rule and a progress simulation that follows the analysis model.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ChosenSeedTests
{
    private const int ResourceCount = 31;

    private const int ResourceGroupCount = 6;

    private const int RaceCount = 4;

    private const int BuildingCount = 56;

    private const int TechnologyCount = 20;

    private const int PartCount = 106;

    private const int ModuleCount = 79;

    private const int HullCount = 27;

    private const int RecipeCount = 114;

    private const int PartGroupCount = 18;

    private const int BranchCount = 2;

    private const int StartResourceCount = 6;

    private const int LastModuleSortOrder = 79;

    private const int SecondLevel = 2;

    private const int FourthLevel = 4;

    private const int SimulationPassLimit = 5000;

    private const string ReservedTechnologyCode = "tech_18";

    private const string CostBaseKey = "world.seed.cost.base";

    private const string CostGrowthKey = "world.seed.cost.growth";

    private const string DurationBaseKey = "world.seed.duration.base_seconds";

    private const string DurationGrowthKey = "world.seed.duration.growth";

    private const string Ukrainian = "uk";

    private const string English = "en";

    private const string EditedText = "admin edit";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChosenSeedTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ChosenSeedTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A fresh database holds the chosen catalog, both languages, no dangling reference and
    /// no negative amount, and every building, technology and part can be reached when the
    /// science-centre gate is ignored the way the analysis simulation ignores it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_chosen_catalog_is_complete_and_reachable()
    {
        await EnsureChosenSeedAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IGameConfig config = services.GetRequiredService<IGameConfig>();

        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        ResearchDbContext research = services.GetRequiredService<ResearchDbContext>();
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();
        ProcessesDbContext processes = services.GetRequiredService<ProcessesDbContext>();
        RequirementsDbContext requirements = services.GetRequiredService<RequirementsDbContext>();
        AetherisDbContext host = services.GetRequiredService<AetherisDbContext>();

        List<ResourceTypeEntity> resources = await world.ResourceTypes.AsNoTracking().ToListAsync(Token);
        List<RaceEntity> races = await world.Races.AsNoTracking().Include(race => race.StartResourceIds).ToListAsync(Token);
        List<BuildingTypeEntity> buildingTypes = await planets.BuildingTypes.AsNoTracking()
            .Include(type => type.LevelIds).ThenInclude(level => level.CostIds)
            .Include(type => type.LevelIds).ThenInclude(level => level.EffectIds)
            .ToListAsync(Token);
        List<TechnologyEntity> technologies = await research.Technologies.AsNoTracking()
            .Include(technology => technology.LevelIds).ThenInclude(level => level.CostIds)
            .ToListAsync(Token);
        List<PartTypeEntity> parts = await production.PartTypes.AsNoTracking().ToListAsync(Token);
        List<RecipeEntity> recipes = await production.Recipes.AsNoTracking().Include(recipe => recipe.IngredientIds).ToListAsync(Token);
        List<RequirementEntity> requirementRows = await requirements.Requirements.AsNoTracking().ToListAsync(Token);

        Assert.Equal(ResourceCount, resources.Count);
        Assert.Equal(ResourceGroupCount, await world.ResourceGroups.CountAsync(Token));
        Assert.Equal(RaceCount, races.Count);
        Assert.Equal(BuildingCount, buildingTypes.Count);
        Assert.Equal(TechnologyCount, technologies.Count);
        Assert.Equal(BranchCount, await research.TechnologyBranches.CountAsync(Token));
        Assert.Contains(technologies, technology => technology.Code == ReservedTechnologyCode);
        Assert.Equal(PartCount, parts.Count);
        Assert.Equal(ModuleCount, parts.Count(part => part.SortOrder <= LastModuleSortOrder));
        Assert.Equal(HullCount, parts.Count(part => part.SortOrder > LastModuleSortOrder));
        Assert.Equal(RecipeCount, recipes.Count);
        Assert.Equal(PartGroupCount, await production.PartGroups.CountAsync(Token));
        Assert.Equal(parts.Select(part => part.Id).ToHashSet(), recipes.Where(recipe => recipe.PartTypeId is int).Select(recipe => recipe.PartTypeId!.Value).ToHashSet());

        Assert.NotNull(config.GetRaw(CostBaseKey));
        Assert.NotNull(config.GetRaw(CostGrowthKey));
        Assert.NotNull(config.GetRaw(DurationBaseKey));
        Assert.NotNull(config.GetRaw(DurationGrowthKey));

        long costBase = config.GetInt64(CostBaseKey);
        double costGrowth = config.GetDouble(CostGrowthKey);
        long durationBase = config.GetInt64(DurationBaseKey);
        double durationGrowth = config.GetDouble(DurationGrowthKey);

        BuildingTypeEntity startBuilding = Assert.Single(buildingTypes, type => type.IsStartBuilding);
        Assert.Contains(startBuilding.LevelIds, level => level.EffectIds.Any(effect => effect.EffectType == BuildingEffectTypes.LevelCap));
        AssertGrowth(startBuilding, SecondLevel, costBase, costGrowth, durationBase, durationGrowth);
        AssertGrowth(startBuilding, FourthLevel, costBase, costGrowth, durationBase, durationGrowth);

        TechnologyEntity firstTechnology = Assert.Single(technologies, technology => technology.SortOrder == technologies.Min(entry => entry.SortOrder));
        AssertGrowth(firstTechnology, SecondLevel, costBase, costGrowth, durationBase, durationGrowth);

        Assert.All(buildingTypes.SelectMany(type => type.LevelIds).SelectMany(level => level.CostIds), cost => Assert.True(cost.Amount >= 0));
        Assert.All(buildingTypes.SelectMany(type => type.LevelIds), level => Assert.True(level.DurationSeconds >= 0));
        Assert.All(technologies.SelectMany(technology => technology.LevelIds).SelectMany(level => level.CostIds), cost => Assert.True(cost.Amount >= 0));
        Assert.All(technologies.SelectMany(technology => technology.LevelIds), level => Assert.True(level.DurationSeconds >= 0));
        Assert.All(recipes.SelectMany(recipe => recipe.IngredientIds), ingredient => Assert.True(ingredient.Amount >= 0));
        Assert.All(await processes.ProcessRecipeIngredients.AsNoTracking().Select(ingredient => ingredient.Amount).ToListAsync(Token), amount => Assert.True(amount >= 0));
        Assert.All(parts, part => Assert.True(part.Weight > 0));
        Assert.All(resources, resource => Assert.True(resource.Weight > 0));

        await AssertNamesAsync(
            host,
            [
                .. resources.Select(resource => resource.NameKey),
                .. races.Select(race => race.NameKey),
                .. buildingTypes.Select(type => type.NameKey),
                .. technologies.Select(technology => technology.NameKey),
                .. parts.Select(part => part.NameKey),
            ]);

        HashSet<int> buildingIds = [.. buildingTypes.Select(type => type.Id)];
        HashSet<int> technologyIds = [.. technologies.Select(technology => technology.Id)];
        HashSet<int> resourceIds = [.. resources.Select(resource => resource.Id)];
        HashSet<int> raceIds = [.. races.Select(race => race.Id)];
        HashSet<int> recipeIds = [.. recipes.Select(recipe => recipe.Id)];
        HashSet<int> partIds = [.. parts.Select(part => part.Id)];
        HashSet<int> processRecipeIds = [.. await processes.ProcessRecipes.AsNoTracking().Select(recipe => recipe.Id).ToListAsync(Token)];

        foreach (RequirementEntity requirement in requirementRows)
        {
            Assert.True(SubjectExists(requirement, buildingIds, technologyIds, recipeIds, partIds, processRecipeIds), requirement.SubjectType);
            Assert.True(TargetExists(requirement, buildingIds, technologyIds, resourceIds, raceIds), requirement.RequirementType);
        }

        Assert.DoesNotContain(
            requirementRows,
            requirement => requirement.SubjectType == RequirementSubjectTypes.Technology
                && requirement.RequirementType == RequirementTypes.TechnologyLevel
                && requirement.TargetId == requirement.SubjectId
                && requirement.TargetLevel >= requirement.SubjectLevel);
        Assert.DoesNotContain(
            requirementRows,
            requirement => requirement.SubjectType == RequirementSubjectTypes.BuildingType
                && requirement.RequirementType == RequirementTypes.BuildingLevel
                && requirement.TargetId == requirement.SubjectId
                && requirement.TargetLevel >= requirement.SubjectLevel);

        List<int> parameterTargets = await processes.ProcessParams.AsNoTracking()
            .Where(parameter => parameter.LevelTargetId != null)
            .Select(parameter => parameter.LevelTargetId!.Value)
            .ToListAsync(Token);
        Assert.All(parameterTargets, target => Assert.Contains(target, technologyIds));

        Dictionary<int, int> buildingLevels = buildingTypes.ToDictionary(type => type.Id, _ => 0);
        Dictionary<int, int> technologyLevels = technologies.ToDictionary(technology => technology.Id, _ => 0);
        int startLevel = (int)config.GetInt64(PlanetsConfigKeys.StartBuildingLevel);
        buildingLevels[startBuilding.Id] = startLevel;

        BuildingTypeEntity scienceCenter = Assert.Single(buildingTypes, type => type.BuildingClass == BuildingClasses.ResearchCenter);
        Dictionary<(string SubjectType, int SubjectId), List<RequirementEntity>> gates = Group(requirementRows);

        List<string> unfinished = Simulate(
            buildingTypes,
            technologies,
            buildingLevels,
            technologyLevels,
            gates,
            scienceCenter.Id);

        Assert.Empty(unfinished);

        foreach (RequirementEntity requirement in requirementRows.Where(requirement => requirement.SubjectType == RequirementSubjectTypes.Recipe))
        {
            Assert.True(
                GateMet(requirement, buildingLevels, technologyLevels, ignoreBuildingLevel: false),
                requirement.RequirementType);
        }

        Assert.All(races, race =>
        {
            Assert.Equal(StartResourceCount, race.StartResourceIds.Count);
            Assert.All(race.StartResourceIds, entry => Assert.Equal(costBase, entry.Amount));
        });

        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        int foundedRaceId = await world.Civilizations.AsNoTracking()
            .Where(civilization => civilization.PlayerId == player.Account.UserId)
            .Select(civilization => civilization.RaceId)
            .SingleAsync(Token);
        RaceEntity foundedRace = races.Single(race => race.Id == foundedRaceId);
        HashSet<int> currencyIds = [.. resources.Where(resource => resource.Scope == ResourceScopes.Civilization).Select(resource => resource.Id)];

        Assert.Contains(foundedRace.StartResourceIds, entry => currencyIds.Contains(entry.ResourceTypeId));
        Assert.Contains(foundedRace.StartResourceIds, entry => !currencyIds.Contains(entry.ResourceTypeId));
        Assert.Equal(
            foundedRace.StartResourceIds.Where(entry => !currencyIds.Contains(entry.ResourceTypeId)).ToDictionary(entry => entry.ResourceTypeId, entry => entry.Amount),
            await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId));

        foreach (RaceStartResourceEntity currency in foundedRace.StartResourceIds.Where(entry => currencyIds.Contains(entry.ResourceTypeId)))
        {
            Assert.Equal(costBase, await EconomyTestData.BalanceAsync(services, player.Account.UserId, currency.ResourceTypeId));
        }

        PlanetPopulationEntity population = await services.GetRequiredService<EconomyDbContext>().PlanetPopulations.AsNoTracking()
            .SingleAsync(entry => entry.PlanetId == player.PlanetId, Token);
        Assert.Equal(config.GetInt64(EconomyConfigKeys.PopulationInitial), population.Amount);
    }

    /// <summary>
    /// A second run adds nothing over an edited text or an edited cost.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_second_run_keeps_an_admin_edit()
    {
        await EnsureChosenSeedAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        int requirementCount = await services.GetRequiredService<RequirementsDbContext>().Requirements.CountAsync(Token);
        int buildingCount = await services.GetRequiredService<PlanetsDbContext>().BuildingTypes.CountAsync(Token);

        ResourceTypeEntity iron = await services.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .SingleAsync(resource => resource.Code == SeedCodes.Iron, Token);
        LocalizationTextEntity text = await services.GetRequiredService<AetherisDbContext>().LocalizationTexts
            .SingleAsync(entry => entry.Key == iron.NameKey && entry.Language == Ukrainian, Token);
        Assert.NotEqual(EditedText, text.Text);
        text.Text = EditedText;

        BuildingLevelCostEntity cost = await services.GetRequiredService<PlanetsDbContext>().BuildingLevelCosts
            .OrderBy(entry => entry.Id)
            .FirstAsync(Token);
        long editedAmount = cost.Amount + 1;
        cost.Amount = editedAmount;
        await services.GetRequiredService<AetherisDbContext>().SaveChangesAsync(Token);
        await services.GetRequiredService<PlanetsDbContext>().SaveChangesAsync(Token);

        await services.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);
        await services.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IProcessesModule>().EnsureAsync(Token);

        using IServiceScope again = _factory.Services.CreateScope();
        Assert.Equal(EditedText, await again.ServiceProvider.GetRequiredService<AetherisDbContext>().LocalizationTexts.AsNoTracking()
            .Where(entry => entry.Key == iron.NameKey && entry.Language == Ukrainian)
            .Select(entry => entry.Text)
            .SingleAsync(Token));
        Assert.Equal(editedAmount, await again.ServiceProvider.GetRequiredService<PlanetsDbContext>().BuildingLevelCosts.AsNoTracking()
            .Where(entry => entry.Id == cost.Id)
            .Select(entry => entry.Amount)
            .SingleAsync(Token));
        Assert.Equal(requirementCount, await again.ServiceProvider.GetRequiredService<RequirementsDbContext>().Requirements.CountAsync(Token));
        Assert.Equal(buildingCount, await again.ServiceProvider.GetRequiredService<PlanetsDbContext>().BuildingTypes.CountAsync(Token));
    }

    private async Task EnsureChosenSeedAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IResearchModule>().EnsureAsync(Token);
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await scope.ServiceProvider.GetRequiredService<IProcessesModule>().EnsureAsync(Token);
    }

    private static void AssertGrowth(
        BuildingTypeEntity buildingType,
        int level,
        long costBase,
        double costGrowth,
        long durationBase,
        double durationGrowth)
    {
        BuildingLevelEntity levelRow = Assert.Single(buildingType.LevelIds, entry => entry.Level == level);
        Assert.NotEmpty(levelRow.CostIds);
        Assert.Equal(Grown(durationBase, durationGrowth, level), levelRow.DurationSeconds);
        Assert.All(levelRow.CostIds, cost => Assert.Equal(Grown(costBase, costGrowth, level), cost.Amount));
    }

    private static void AssertGrowth(
        TechnologyEntity technology,
        int level,
        long costBase,
        double costGrowth,
        long durationBase,
        double durationGrowth)
    {
        TechnologyLevelEntity levelRow = Assert.Single(technology.LevelIds, entry => entry.Level == level);
        Assert.NotEmpty(levelRow.CostIds);
        Assert.Equal(Grown(durationBase, durationGrowth, level), levelRow.DurationSeconds);
        Assert.All(levelRow.CostIds, cost => Assert.Equal(Grown(costBase, costGrowth, level), cost.Amount));
    }

    /// <summary>
    /// The same rounding the seed writer uses: base × (1 + coefficient)^(level − 1), half away from zero.
    /// </summary>
    private static long Grown(long basis, double coefficient, int level)
    {
        int steps = level - 1;

        return (long)Math.Round(basis * Math.Pow(1d + coefficient, steps), MidpointRounding.AwayFromZero);
    }

    private static async Task AssertNamesAsync(AetherisDbContext host, IReadOnlyCollection<string> nameKeys)
    {
        List<LocalizationTextEntity> texts = await host.LocalizationTexts.AsNoTracking()
            .Where(text => nameKeys.Contains(text.Key))
            .ToListAsync(Token);
        Dictionary<(string Key, string Language), string> byKey = texts.ToDictionary(
            text => (text.Key, text.Language),
            text => text.Text);

        foreach (string nameKey in nameKeys)
        {
            Assert.True(byKey.TryGetValue((nameKey, Ukrainian), out string? ukrainian) && !string.IsNullOrWhiteSpace(ukrainian), nameKey);
            Assert.True(byKey.TryGetValue((nameKey, English), out string? english) && !string.IsNullOrWhiteSpace(english), nameKey);
        }
    }

    private static bool SubjectExists(
        RequirementEntity requirement,
        HashSet<int> buildingIds,
        HashSet<int> technologyIds,
        HashSet<int> recipeIds,
        HashSet<int> partIds,
        HashSet<int> processRecipeIds) =>
        requirement.SubjectType switch
        {
            RequirementSubjectTypes.BuildingType => buildingIds.Contains(requirement.SubjectId),
            RequirementSubjectTypes.Technology => technologyIds.Contains(requirement.SubjectId),
            RequirementSubjectTypes.Recipe => recipeIds.Contains(requirement.SubjectId),
            RequirementSubjectTypes.Module => partIds.Contains(requirement.SubjectId),
            RequirementSubjectTypes.ProcessRecipe => processRecipeIds.Contains(requirement.SubjectId),
            _ => false,
        };

    private static bool TargetExists(
        RequirementEntity requirement,
        HashSet<int> buildingIds,
        HashSet<int> technologyIds,
        HashSet<int> resourceIds,
        HashSet<int> raceIds) =>
        requirement.RequirementType switch
        {
            RequirementTypes.BuildingLevel => buildingIds.Contains(requirement.TargetId),
            RequirementTypes.TechnologyLevel => technologyIds.Contains(requirement.TargetId),
            RequirementTypes.Deposit => resourceIds.Contains(requirement.TargetId),
            ProductionRequirementTypes.Race => raceIds.Contains(requirement.TargetId),
            _ => false,
        };

    private static Dictionary<(string SubjectType, int SubjectId), List<RequirementEntity>> Group(List<RequirementEntity> requirementRows)
    {
        Dictionary<(string SubjectType, int SubjectId), List<RequirementEntity>> grouped = [];

        foreach (RequirementEntity requirement in requirementRows)
        {
            (string, int) key = (requirement.SubjectType, requirement.SubjectId);

            if (!grouped.TryGetValue(key, out List<RequirementEntity>? rows))
            {
                rows = [];
                grouped[key] = rows;
            }

            rows.Add(requirement);
        }

        return grouped;
    }

    /// <summary>
    /// Raises every building and technology while deposits are available and a technology
    /// does not wait for the science centre. Returns the codes that stay below their maximum.
    /// </summary>
    private static List<string> Simulate(
        List<BuildingTypeEntity> buildingTypes,
        List<TechnologyEntity> technologies,
        Dictionary<int, int> buildingLevels,
        Dictionary<int, int> technologyLevels,
        Dictionary<(string SubjectType, int SubjectId), List<RequirementEntity>> gates,
        int scienceCenterId)
    {
        for (int pass = 0; pass < SimulationPassLimit; pass++)
        {
            bool progressed = false;

            foreach (BuildingTypeEntity buildingType in buildingTypes)
            {
                int next = buildingLevels[buildingType.Id] + 1;
                int maxLevel = buildingType.LevelIds.Max(level => level.Level);

                if (next > maxLevel)
                {
                    continue;
                }

                bool capsLevels = buildingType.LevelIds.Any(level => level.EffectIds.Any(effect => effect.EffectType == BuildingEffectTypes.LevelCap));

                if (!capsLevels && next > LevelCap(buildingTypes, buildingLevels, buildingType.Id))
                {
                    continue;
                }

                if (!RowsMet(gates, RequirementSubjectTypes.BuildingType, buildingType.Id, next, buildingLevels, technologyLevels, ignoreBuildingLevel: false))
                {
                    continue;
                }

                buildingLevels[buildingType.Id] = next;
                progressed = true;
            }

            foreach (TechnologyEntity technology in technologies)
            {
                int next = technologyLevels[technology.Id] + 1;

                if (next > technology.MaxLevel || buildingLevels[scienceCenterId] < technology.ResearchCenterLevel)
                {
                    continue;
                }

                // building_level gates of a technology are the science-centre curve. The analysis
                // model leaves them out; the file keeps them and the combined rules jam.
                if (!RowsMet(gates, RequirementSubjectTypes.Technology, technology.Id, next, buildingLevels, technologyLevels, ignoreBuildingLevel: true))
                {
                    continue;
                }

                technologyLevels[technology.Id] = next;
                progressed = true;
            }

            if (!progressed)
            {
                break;
            }
        }

        return
        [
            .. buildingTypes
                .Where(type => buildingLevels[type.Id] != type.LevelIds.Max(level => level.Level))
                .Select(type => type.Code + " " + buildingLevels[type.Id] + "/" + type.LevelIds.Max(level => level.Level)),
            .. technologies
                .Where(technology => technologyLevels[technology.Id] != technology.MaxLevel)
                .Select(technology => technology.Code + " " + technologyLevels[technology.Id] + "/" + technology.MaxLevel),
        ];
    }

    private static long LevelCap(List<BuildingTypeEntity> buildingTypes, Dictionary<int, int> buildingLevels, int buildingTypeId)
    {
        long cap = 0;

        foreach (BuildingTypeEntity buildingType in buildingTypes)
        {
            if (buildingType.Id == buildingTypeId)
            {
                continue;
            }

            int level = buildingLevels[buildingType.Id];
            BuildingLevelEffectEntity? effect = buildingType.LevelIds
                .SingleOrDefault(entry => entry.Level == level)?.EffectIds
                .FirstOrDefault(entry => entry.EffectType == BuildingEffectTypes.LevelCap);

            if (effect is not null)
            {
                cap = Math.Max(cap, (long)decimal.Floor(effect.Value));
            }
        }

        return cap;
    }

    private static bool RowsMet(
        Dictionary<(string SubjectType, int SubjectId), List<RequirementEntity>> gates,
        string subjectType,
        int subjectId,
        int nextLevel,
        Dictionary<int, int> buildingLevels,
        Dictionary<int, int> technologyLevels,
        bool ignoreBuildingLevel)
    {
        if (!gates.TryGetValue((subjectType, subjectId), out List<RequirementEntity>? rows))
        {
            return true;
        }

        foreach (RequirementEntity requirement in rows)
        {
            if (requirement.SubjectLevel > nextLevel)
            {
                continue;
            }

            if (!GateMet(requirement, buildingLevels, technologyLevels, ignoreBuildingLevel))
            {
                return false;
            }
        }

        return true;
    }

    private static bool GateMet(
        RequirementEntity requirement,
        Dictionary<int, int> buildingLevels,
        Dictionary<int, int> technologyLevels,
        bool ignoreBuildingLevel) =>
        requirement.RequirementType switch
        {
            RequirementTypes.Deposit => true,
            ProductionRequirementTypes.Race => true,
            RequirementTypes.BuildingLevel => ignoreBuildingLevel || buildingLevels.GetValueOrDefault(requirement.TargetId) >= requirement.TargetLevel,
            RequirementTypes.TechnologyLevel => technologyLevels.GetValueOrDefault(requirement.TargetId) >= requirement.TargetLevel,
            _ => false,
        };
}
