using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Requirements.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the building types in the admin panel (task 00023, section 4.4): a building is added,
/// changed and archived through the form, and a player sees it without a release or a restart.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BuildingReferenceTests
{
    private const string Buildings = "/planets/buildings";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildingReferenceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public BuildingReferenceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A building created in the admin panel gets a neutral code and its texts, a player sees it
    /// on the body with its next level, and its construction goes through the queue (acceptance
    /// criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_building_is_added_from_the_admin_panel_and_a_player_sees_it()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int id, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Обсерваторія", "Observatory", levels: 2)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        BuildingTypeEntity type = await services.GetRequiredService<PlanetsDbContext>().BuildingTypes.AsNoTracking()
            .Include(entry => entry.LevelIds).SingleAsync(entry => entry.Id == id, Token);

        Assert.Matches("^building_[0-9]+$", type.Code);
        Assert.Equal(2, type.LevelIds.Count);

        // The player reads the body through the API, as the client does.
        using JsonDocument overview = JsonDocument.Parse(await player.Client.GetStringAsync(PlanetsEndpoints.Prefix + "/" + player.PlanetId, Token));
        JsonElement building = overview.RootElement.GetProperty("buildings").EnumerateArray().Single(entry => entry.GetProperty("code").GetString() == type.Code);
        Assert.Equal(PlanetsTextKeys.Name(type.Code), building.GetProperty("name_key").GetString());
        Assert.Equal(1, building.GetProperty("next").GetProperty("level").GetInt32());

        DateTimeOffset now = PlanetsTestData.GameNow(services);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [ids.Iron] = 100_000 }, now);
        PlanetOverviewDto queued = await services.GetRequiredService<ConstructionService>().EnqueueAsync(player.PlanetId, id, now, Token);
        Assert.Contains(queued.Queue.Items, item => item.BuildingTypeId == id && item.TargetLevel == 1);

        PlanetOverviewDto built = await PlanetsTestData.ReadAsync(services, player.PlanetId, now.AddDays(1));
        Assert.Equal(1, built.Buildings.Single(entry => entry.BuildingTypeId == id).Level);
    }

    /// <summary>
    /// A change of a level in the admin panel takes effect at once, even when the catalog is kept
    /// in memory for long: the change drops the copy (acceptance criteria 2 and 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_changed_reference_takes_effect_without_a_restart()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, PlanetsConfigKeys.CatalogCacheSeconds, "3600");

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Ангар", "Hangar", levels: 1, cost: 50)));
        DateTimeOffset now = PlanetsTestData.GameNow(services);
        PlanetOverviewDto before = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);
        Assert.Equal(50, before.Buildings.Single(entry => entry.BuildingTypeId == id).Next.Costs.Single().Amount);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Buildings, id, version, Building(ids, "Ангар", "Hangar", levels: 1, cost: 75, duration: 999)));

        PlanetOverviewDto after = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);
        BuildingDto changed = after.Buildings.Single(entry => entry.BuildingTypeId == id);
        Assert.Equal(75, changed.Next.Costs.Single().Amount);
        Assert.Equal(999, changed.Next.DurationSeconds);
    }

    /// <summary>
    /// A building without levels, with a gap in the levels, with a level without a cost or with a
    /// level without requirements is refused by the rules of the levels; only the first level of
    /// the start building may have no requirement (acceptance criterion 2, decision 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_building_without_levels_or_costs_is_refused()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Порожня", "Empty", levels: 0)),
            HttpStatusCode.UnprocessableEntity,
            "levels",
            AdminReferenceErrors.LevelsMissing);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Пропуск", "Gap", levels: 2, firstLevel: 2)),
            HttpStatusCode.UnprocessableEntity,
            "levels",
            AdminReferenceErrors.LevelsGap);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Безкоштовна", "Free", levels: 1, cost: null)),
            HttpStatusCode.UnprocessableEntity,
            "levels.0.costs",
            AdminReferenceErrors.CostMissing);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Вільна", "Open", levels: 1, requirement: false)),
            HttpStatusCode.UnprocessableEntity,
            "levels.0.requirements",
            AdminReferenceErrors.RequirementsMissing);

        // The start building may open without a requirement, its second level may not.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlanetsDbContext>().BuildingTypes
                .ExecuteUpdateAsync(setters => setters.SetProperty(type => type.IsStartBuilding, false), Token);
        }

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Ратуша", "Town hall", levels: 2, requirement: false, start: true)),
            HttpStatusCode.UnprocessableEntity,
            "levels.1.requirements",
            AdminReferenceErrors.RequirementsMissing);
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Ратуша", "Town hall", levels: 1, requirement: false, start: true)));

        // A wrong value that breaks no rule of the levels is refused with 400, like every other
        // change of the admin panel.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Невідома", "Unknown", levels: 1, buildingClass: "no_such_class")),
            HttpStatusCode.BadRequest,
            "building_class",
            AdminReferenceErrors.Unknown);
    }

    /// <summary>
    /// An archived building works for the player who has it: its effects stay, its queue finishes,
    /// but its next level is not offered; a body without it does not list it, and the return from
    /// the archive offers it again (acceptance criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_archived_building_works_for_its_owners_but_is_not_offered()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Реактор", "Reactor", levels: 3, energy: 40)));

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ConstructionService construction = services.GetRequiredService<ConstructionService>();
        DateTimeOffset now = PlanetsTestData.GameNow(services);
        await PlanetsTestData.SetLevelAsync(services, owner.PlanetId, SeedCodes.AdminCenter, 5);
        await PlanetsTestData.SetLevelAsync(services, stranger.PlanetId, SeedCodes.AdminCenter, 5);
        await PlanetsTestData.SetBalanceAsync(services, owner.PlanetId, new Dictionary<int, long> { [ids.Iron] = 100_000 }, now);
        await construction.EnqueueAsync(owner.PlanetId, id, now, Token);
        PlanetOverviewDto built = await PlanetsTestData.ReadAsync(services, owner.PlanetId, now.AddDays(1));
        long energy = built.Energy.Output;
        await construction.EnqueueAsync(owner.PlanetId, id, now.AddDays(1), Token);

        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Buildings, id, version));

        // The queued level finishes, the effects stay, and no further level is offered.
        PlanetOverviewDto later = await PlanetsTestData.ReadAsync(services, owner.PlanetId, now.AddDays(2));
        BuildingDto building = later.Buildings.Single(entry => entry.BuildingTypeId == id);
        Assert.Equal(2, building.Level);
        Assert.True(later.Energy.Output > energy);
        Assert.False(building.Next.CanEnqueue);
        Assert.Contains(PlanetsErrorCodes.BuildingArchived, building.Next.ReasonCodes);

        GameException refused = await Assert.ThrowsAsync<GameException>(() => construction.EnqueueAsync(owner.PlanetId, id, now.AddDays(2), Token));
        Assert.Equal(PlanetsErrorCodes.BuildingArchived, refused.Code);

        PlanetOverviewDto elsewhere = await PlanetsTestData.ReadAsync(services, stranger.PlanetId, now.AddDays(2));
        Assert.DoesNotContain(elsewhere.Buildings, entry => entry.BuildingTypeId == id);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Buildings, id, archived, archive: false));
        PlanetOverviewDto restored = await PlanetsTestData.ReadAsync(services, stranger.PlanetId, now.AddDays(2));
        Assert.DoesNotContain(PlanetsErrorCodes.BuildingArchived, restored.Buildings.Single(entry => entry.BuildingTypeId == id).Next.ReasonCodes);
    }

    /// <summary>
    /// A record of the form is the whole building: the levels with their costs, effects and
    /// requirements come back as they were saved, and a level a player has cannot be removed
    /// (acceptance criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_building_is_edited_whole_and_keeps_the_levels_in_use()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Кузня", "Forge", levels: 3, energy: 5)));
        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Buildings, id);
        JsonElement levels = record.GetProperty("values").GetProperty("levels");

        Assert.Equal(3, levels.GetArrayLength());
        Assert.Equal(RequirementTypes.BuildingLevel, levels[0].GetProperty("requirements")[0].GetProperty("requirement_type").GetString());
        Assert.Equal(BuildingEffectTypes.EnergyOutput, levels[2].GetProperty("effects")[0].GetProperty("effect_type").GetString());

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, record.GetProperty("code").GetString()!, 2);
        }

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Buildings, id, version, Building(ids, "Кузня", "Forge", levels: 1)),
            HttpStatusCode.UnprocessableEntity,
            "levels",
            AdminReferenceErrors.LevelsInUse);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Buildings, id, version, Building(ids, "Кузня", "Forge", levels: 2, energy: 9)));
        JsonElement changed = (await ReferenceAdmin.ReadAsync(admin, Buildings, id)).GetProperty("values").GetProperty("levels");
        Assert.Equal(2, changed.GetArrayLength());
        Assert.Equal(18, changed[1].GetProperty("effects")[0].GetProperty("value").GetInt64());
    }

    /// <summary>
    /// Twenty parallel creations get twenty different neutral codes (acceptance criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_creations_get_twenty_different_codes()
    {
        const int parallelCount = 20;

        await PlanetsTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => ReferenceAdmin.CreateAsync(admin, Buildings, Building(ids, "Склад " + index, "Depot " + index, levels: 1))));
        List<int> created = [];

        foreach (HttpResponseMessage response in responses)
        {
            created.Add((await ReferenceAdmin.ChangedAsync(response)).Id);
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        List<string> codes = await scope.ServiceProvider.GetRequiredService<PlanetsDbContext>().BuildingTypes.AsNoTracking()
            .Where(type => created.Contains(type.Id))
            .Select(type => type.Code)
            .ToListAsync(Token);
        HashSet<string> seeded = [.. PlanetsReferenceSeed.Load().BuildingTypes.Select(type => type.Code)];

        Assert.Equal(parallelCount, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.DoesNotContain(code, seeded));
    }

    /// <summary>
    /// The seed never overwrites a row changed in the admin panel: a repeated start keeps the
    /// change (acceptance criterion 10).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_does_not_overwrite_changed_rows()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        Ids ids = await IdsAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Buildings, plant.Id, plant.Version, Building(ids, "Нова станція", "New plant", levels: 1, cost: 7)));

        await services.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);
        await services.GetRequiredService<Aetheris.Host.Configuration.ConfigSeedService>().SeedAsync(Token);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Buildings, plant.Id);
        Assert.Equal("New plant", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(1, record.GetProperty("values").GetProperty("levels").GetArrayLength());
        Assert.Equal(7, record.GetProperty("values").GetProperty("levels")[0].GetProperty("costs")[0].GetProperty("amount").GetInt64());
    }

    /// <summary>
    /// Builds the values of a building: every level costs iron, opens with the main building of
    /// level one and, when asked, produces energy.
    /// </summary>
    internal static object Building(
        Ids ids,
        string ukrainian,
        string english,
        int levels,
        long? cost = 10,
        bool requirement = true,
        bool start = false,
        int firstLevel = 1,
        long duration = 60,
        long? energy = null,
        string buildingClass = BuildingClasses.None) => new
        {
            name = ReferenceAdmin.Texts(ukrainian, english),
            description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
            building_class = buildingClass,
            sort_order = 50,
            is_start_building = start,
            levels = Enumerable.Range(firstLevel, levels).Select(level => new
            {
                level,
                duration_seconds = duration,
                costs = cost is null ? [] : new[] { new { resource_type_id = ids.Iron, amount = cost.Value } },
                effects = energy is null ? [] : new[] { new { effect_type = BuildingEffectTypes.EnergyOutput, value = energy.Value * level } },
                requirements = requirement && level == firstLevel
                    ? new[] { new { requirement_type = RequirementTypes.BuildingLevel, target_id = ids.AdminCenter, target_level = 1 } }
                    : [],
            }),
        };

    /// <summary>
    /// Reads the rows the building values of the tests name.
    /// </summary>
    internal async Task<Ids> IdsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        return new Ids(
            await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron),
            (await PlanetsTestData.TypeAsync(services, SeedCodes.AdminCenter)).Id);
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

    /// <summary>The rows the building values of the tests name.</summary>
    /// <param name="Iron">The first metal.</param>
    /// <param name="AdminCenter">The main building.</param>
    internal sealed record Ids(int Iron, int AdminCenter);
}
