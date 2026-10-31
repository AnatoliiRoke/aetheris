using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research;
using Aetheris.Research.Api;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Research;

/// <summary>
/// Tests of the technologies and the branches of the tree in the admin panel (task 00023,
/// sections 4.5 and 4.6).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class TechnologyReferenceTests
{
    private const string Technologies = "/research/technologies";

    private const string Branches = "/research/branches";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TechnologyReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public TechnologyReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A technology added in the admin panel closes another one by its requirement, like a
    /// technology of the starting data: the second one opens once the first is learned (acceptance
    /// criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_technology_is_added_from_the_admin_panel_and_its_requirement_works()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int first, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids, "Оптика", "Optics")));
        (int second, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids, "Лінзи", "Lenses", requiredTechnologyId: first)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);
        IResearchModule research = services.GetRequiredService<IResearchModule>();

        ResearchTreeDto before = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        TechnologyDto closed = before.Technologies.Single(technology => technology.Id == second);
        Assert.Matches("^tech_[0-9]+$", closed.Code);
        Assert.Equal(ResearchTechnologyStatuses.Unavailable, closed.Status);
        Assert.Equal(ResearchErrorCodes.RequirementUnmet, closed.ReasonCode);
        Assert.Equal(ResearchTechnologyStatuses.Available, before.Technologies.Single(technology => technology.Id == first).Status);

        ResearchDbContext dbContext = services.GetRequiredService<ResearchDbContext>();
        dbContext.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = player.Account.UserId, TechnologyId = first, Level = 1 });
        await dbContext.SaveChangesAsync(Token);

        ResearchTreeDto after = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        Assert.Equal(ResearchTechnologyStatuses.Available, after.Technologies.Single(technology => technology.Id == second).Status);
    }

    /// <summary>
    /// A level that opens a building closes the building until the level is learned, as the
    /// starting data does, and the building catalog sees the new requirement at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_that_opens_a_building_closes_the_building()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, Aetheris.Planets.Configuration.PlanetsConfigKeys.CatalogCacheSeconds, "3600");
        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);

        (int id, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids, "Енергомережа", "Grid", opensBuildingId: plant.Id)));

        IReadOnlyList<RequirementDefinition> requirements = await services.GetRequiredService<IRequirementsModule>()
            .GetAsync(RequirementSubjectTypes.BuildingType, plant.Id, 1, Token);
        Assert.Contains(requirements, requirement => requirement.RequirementType == RequirementTypes.TechnologyLevel && requirement.TargetId == id);
    }

    /// <summary>
    /// A branch added in the admin panel shows in the tree without a change of the code; the
    /// technologies link to it by its identifier, and the code holds no list of the branches
    /// (acceptance criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_branch_of_the_technologies_is_added_from_the_admin_panel()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int branchId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Branches, Branch("Наука", "Science", 7)));
        (int technologyId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids with { BranchId = branchId }, "Телескоп", "Telescope")));

        using IServiceScope scope = _factory.Services.CreateScope();
        ResearchTreeDto tree = await scope.ServiceProvider.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        ResearchBranchDto branch = tree.Branches.Last();
        Assert.Equal(branchId, branch.Id);
        Assert.Matches("^branch_[0-9]+$", branch.Branch);
        Assert.Equal(ResearchBranchTextKeys.Name(branch.Branch), branch.NameKey);
        Assert.Equal(branchId, tree.Technologies.Single(technology => technology.Id == technologyId).BranchId);

        // The old logical codes of the branches live only in the migration that moved them.
        List<string> found = [];
        string sources = Path.Combine(ServerRoot(), "src", "Aetheris.Research");

        foreach (string file in Directory.EnumerateFiles(sources, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            found.AddRange(OldBranchCodes().Matches(await File.ReadAllTextAsync(file, Token)).Select(match => Path.GetFileName(file) + ": " + match.Value));
        }

        Assert.True(found.Count == 0, "Branches named in the code: " + string.Join(", ", found));
    }

    /// <summary>
    /// An archived technology works for the player who learned it and its queue finishes, but no
    /// new research of it starts and a player without it does not see it; the return from the
    /// archive offers it again (acceptance criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_archived_technology_works_for_its_owners_but_is_not_offered()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids, "Метеорологія", "Meteorology", levels: 3)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IResearchModule research = services.GetRequiredService<IResearchModule>();
        await PlanetsTestData.SetLevelAsync(services, owner.PlanetId, SeedCodes.ScienceCenter, 1);
        await PlanetsTestData.SetLevelAsync(services, stranger.PlanetId, SeedCodes.ScienceCenter, 1);
        await PlanetsTestData.SetBalanceAsync(services, owner.PlanetId, new Dictionary<int, long> { [ids.Iron] = 100_000 }, PlanetsTestData.GameNow(services));

        ResearchDbContext dbContext = services.GetRequiredService<ResearchDbContext>();
        dbContext.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = owner.Account.UserId, TechnologyId = id, Level = 1 });
        await dbContext.SaveChangesAsync(Token);
        ResearchTreeDto queued = await research.EnqueueAsync(owner.Account.UserId, owner.PlanetId, id, 2, Token);
        Assert.Contains(queued.Queue, item => item.TechnologyId == id && item.TargetLevel == 2);

        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Technologies, id, version));

        ResearchTreeDto mine = await research.GetTreeAsync(owner.Account.UserId, owner.PlanetId, Token);
        Assert.Contains(mine.Queue, item => item.TechnologyId == id);
        TechnologyDto kept = mine.Technologies.Single(technology => technology.Id == id);
        Assert.Equal(1, kept.LearnedLevel);

        GameException refused = await Assert.ThrowsAsync<GameException>(() => research.EnqueueAsync(owner.Account.UserId, owner.PlanetId, id, 3, Token));
        Assert.Equal(ResearchErrorCodes.TechnologyArchived, refused.Code);

        ResearchTreeDto theirs = await research.GetTreeAsync(stranger.Account.UserId, stranger.PlanetId, Token);
        Assert.DoesNotContain(theirs.Technologies, technology => technology.Id == id);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Technologies, id, archived, archive: false));
        ResearchTreeDto restored = await research.GetTreeAsync(stranger.Account.UserId, stranger.PlanetId, Token);
        Assert.Equal(ResearchTechnologyStatuses.Available, restored.Technologies.Single(technology => technology.Id == id).Status);
    }

    /// <summary>
    /// Twenty parallel creations of technologies and of branches get twenty different neutral
    /// codes each.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_creations_get_twenty_different_codes()
    {
        const int parallelCount = 20;

        await ResetAsync();
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        HttpResponseMessage[] technologies = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Technologies, Technology(ids, "Технологія " + index, "Technology " + index))));
        HttpResponseMessage[] branches = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Branches, Branch("Гілка " + index, "Branch " + index, index))));

        List<int> technologyIds = [];
        List<int> branchIds = [];

        foreach (HttpResponseMessage response in technologies)
        {
            technologyIds.Add((await ReferenceAdmin.ChangedAsync(response)).Id);
        }

        foreach (HttpResponseMessage response in branches)
        {
            branchIds.Add((await ReferenceAdmin.ChangedAsync(response)).Id);
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        ResearchDbContext dbContext = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();
        List<string> technologyCodes = await dbContext.Technologies.AsNoTracking().Where(technology => technologyIds.Contains(technology.Id)).Select(technology => technology.Code).ToListAsync(Token);
        List<string> branchCodes = await dbContext.TechnologyBranches.AsNoTracking().Where(branch => branchIds.Contains(branch.Id)).Select(branch => branch.Code).ToListAsync(Token);

        Assert.Equal(parallelCount, technologyCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(parallelCount, branchCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("branch_1", branchCodes);
    }

    /// <summary>
    /// The key test of the thesis of the author: a building, a technology and a branch written
    /// straight into the tables, without the seed and without the admin panel, reach the game
    /// (acceptance criterion 11).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_element_added_only_to_the_tables_reaches_the_game()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        Ids ids = await IdsAsync();

        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        BuildingTypeEntity building = new()
        {
            Code = "building_900",
            NameKey = "planets.building.building_900.name",
            DescriptionKey = "planets.building.building_900.description",
            SortOrder = 900,
            CategoryId = await planets.BuildingCategories.AsNoTracking().OrderBy(category => category.Id).Select(category => category.Id).FirstAsync(Token),
            LevelIds = [new BuildingLevelEntity { Level = 1, DurationSeconds = 60, CostIds = [new BuildingLevelCostEntity { ResourceTypeId = ids.Iron, Amount = 5 }] }],
        };
        planets.BuildingTypes.Add(building);
        await planets.SaveChangesAsync(Token);

        ResearchDbContext research = services.GetRequiredService<ResearchDbContext>();
        TechnologyBranchEntity branch = new()
        {
            Code = "branch_900",
            NameKey = "research.technology_branch.branch_900.name",
            DescriptionKey = "research.technology_branch.branch_900.description",
            SortOrder = 900,
        };
        research.TechnologyBranches.Add(branch);
        await research.SaveChangesAsync(Token);

        research.Technologies.Add(new TechnologyEntity
        {
            Code = "tech_900",
            NameKey = "research.technology.tech_900.name",
            DescriptionKey = "research.technology.tech_900.description",
            BranchId = branch.Id,
            SortOrder = 1,
            MaxLevel = 1,
            ResearchCenterLevel = 1,
            LevelIds = [new TechnologyLevelEntity { Level = 1, DurationSeconds = 60, CostIds = [new TechnologyLevelCostEntity { ResourceTypeId = ids.Iron, Amount = 5 }] }],
        });
        await research.SaveChangesAsync(Token);

        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, PlanetsTestData.GameNow(services));
        Assert.Contains(overview.Buildings, entry => entry.Code == "building_900");

        ResearchTreeDto tree = await services.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        Assert.Contains(tree.Branches, entry => entry.Branch == "branch_900");
        Assert.Contains(tree.Technologies, entry => entry.Code == "tech_900" && entry.BranchId == branch.Id);
    }

    /// <summary>
    /// A branch changed in the admin panel keeps its change after a repeated start of the module
    /// (acceptance criterion 10).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_does_not_overwrite_a_changed_branch()
    {
        await ResetAsync();
        using HttpClient admin = await OwnerAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        TechnologyBranchEntity first = await services.GetRequiredService<ResearchDbContext>().TechnologyBranches.AsNoTracking()
            .OrderBy(branch => branch.SortOrder).FirstAsync(Token);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Branches, first.Id, first.Version, Branch("Господарство", "Husbandry", 42)));
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Branches, first.Id);
        Assert.Equal("Husbandry", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(42, record.GetProperty("values").GetProperty("sort_order").GetInt32());
    }

    private static object Branch(string ukrainian, string english, int sortOrder) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        sort_order = sortOrder,
    };

    private static object Technology(Ids ids, string ukrainian, string english, int levels = 1, int? requiredTechnologyId = null, int? opensBuildingId = null) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        branch_id = ids.BranchId,
        sort_order = 99,
        research_center_level = 1,
        levels = Enumerable.Range(1, levels).Select(level => new
        {
            level,
            duration_seconds = 60,
            costs = new[] { new { resource_type_id = ids.Iron, amount = 10 * level } },
            effects = opensBuildingId is { } buildingId && level == 1
                ? new object[] { new { effect_type = TechnologyEffectTypes.Unlocks, unlock_kind = TechnologyUnlockKinds.BuildingType, unlock_target_id = buildingId, value = 1 } }
                : [new { effect_type = TechnologyEffectTypes.MetalProduction, value = 1.01m }],
            requirements = requiredTechnologyId is { } technologyId && level == 1
                ? new[] { new { requirement_type = RequirementTypes.TechnologyLevel, target_id = technologyId, target_level = 1 } }
                : [],
        }),
    };

    private static string ServerRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The root of the server was not found.");
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ResearchConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IResearchModule>().EnsureAsync(Token);
    }

    private async Task<Ids> IdsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        int branchId = await services.GetRequiredService<ResearchDbContext>().TechnologyBranches.AsNoTracking()
            .OrderBy(branch => branch.SortOrder).Select(branch => branch.Id).FirstAsync(Token);

        return new Ids(await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron), branchId);
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

    [GeneratedRegex("\"(economy|fleet|engines_and_logistics|defence|colonisation|trade)\"")]
    private static partial Regex OldBranchCodes();

    /// <summary>The rows the values of the tests name.</summary>
    /// <param name="Iron">The first metal.</param>
    /// <param name="BranchId">The branch of the new technologies.</param>
    private sealed record Ids(int Iron, int BranchId);
}
