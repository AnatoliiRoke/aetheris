using System.Net;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the tree of the groups of the parts (task 00043): the sections of the production
/// screen and the weapons with the energy and the kinetic ones inside them come from the data.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PartGroupTreeTests
{
    private const string Groups = "/production/part-groups";

    /// <summary>The groups of the starting data: eighteen with parts and four that hold groups.</summary>
    private const int SeedGroupCount = 22;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartGroupTreeTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public PartGroupTreeTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The seed puts every group with parts under a section, the energy weapons, the kinetic weapons
    /// and the ammunition under the weapons, and the weapons under the ship modules; a section holds
    /// no parts of its own.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_puts_the_groups_into_sections_and_the_weapons_into_a_group_of_their_own()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        List<PartGroupEntity> groups = await dbContext.PartGroups.AsNoTracking().ToListAsync(Token);
        HashSet<int> groupsWithParts = [.. await dbContext.PartTypes.AsNoTracking().Select(part => part.PartGroupId).Distinct().ToListAsync(Token)];
        Dictionary<string, PartGroupEntity> byCode = groups.ToDictionary(group => group.Code);

        Assert.Equal(SeedGroupCount, groups.Count);

        // The three sections of the screen are the groups of the top level, without parts.
        List<PartGroupEntity> sections = [.. groups.Where(group => group.ParentGroupId is null)];
        Assert.Equal(["part_group_19", "part_group_20", "part_group_21"], sections.Select(group => group.Code).Order(StringComparer.Ordinal));
        Assert.All(sections, section => Assert.DoesNotContain(section.Id, groupsWithParts));

        // The weapons lie in the ship modules and hold the energy weapons, the kinetic weapons and
        // the ammunition.
        PartGroupEntity weapons = byCode["part_group_22"];
        Assert.Equal(byCode["part_group_19"].Id, weapons.ParentGroupId);
        Assert.Equal(
            ["part_group_3", "part_group_4", "part_group_7"],
            groups.Where(group => group.ParentGroupId == weapons.Id).Select(group => group.Code).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(weapons.Id, groupsWithParts);

        // Every group with parts reaches a section through its parents.
        Dictionary<int, PartGroupEntity> byId = groups.ToDictionary(group => group.Id);

        foreach (int groupId in groupsWithParts)
        {
            PartGroupEntity group = byId[groupId];
            Assert.NotNull(group.ParentGroupId);

            while (group.ParentGroupId is { } parentId)
            {
                group = byId[parentId];
            }

            Assert.Contains(group, sections);
        }
    }

    /// <summary>
    /// The list of the recipes of the production screen brings the tree of the groups with it, so
    /// the client builds its tabs without another request.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_list_of_the_recipes_brings_the_tree_of_the_groups()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ProductionRecipeListDto list = await services.GetRequiredService<ProductionQueueService>().ReadRecipesAsync(player.Account.UserId, player.PlanetId, Token);
        List<PartGroupEntity> groups = await services.GetRequiredService<ProductionDbContext>().PartGroups.AsNoTracking().ToListAsync(Token);

        Assert.Equal(groups.Count, list.Groups.Count);

        foreach (PartGroupEntity group in groups)
        {
            ProductionPartGroupDto dto = Assert.Single(list.Groups, entry => entry.PartGroupId == group.Id);
            Assert.Equal(group.Code, dto.Code);
            Assert.Equal(group.NameKey, dto.NameKey);
            Assert.Equal(group.ParentGroupId, dto.ParentGroupId);
            Assert.Equal(group.SortOrder, dto.SortOrder);
        }

        // Every recipe of a part names a group of the tree.
        Assert.All(
            list.Recipes.Where(recipe => recipe.PartGroupCode is not null),
            recipe => Assert.Contains(list.Groups, group => group.Code == recipe.PartGroupCode));
    }

    /// <summary>
    /// The parent of a group is chosen in the admin panel; an unknown parent, the group itself or
    /// a group inside it is refused, so the groups never make a loop.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_parent_of_a_group_is_chosen_in_the_admin_panel_without_a_loop()
    {
        await ResetAsync();
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

        (int sectionId, int sectionVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Groups, Group("Розділ", "Section", 90, null)));
        (int childId, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Groups, Group("Підгрупа", "Subgroup", 1, sectionId)));

        JsonElement child = await ReferenceAdmin.ReadAsync(admin, Groups, childId);
        Assert.Equal(sectionId, child.GetProperty("values").GetProperty("parent_group_id").GetInt32());
        Assert.Equal(JsonValueKind.Null, (await ReferenceAdmin.ReadAsync(admin, Groups, sectionId)).GetProperty("values").GetProperty("parent_group_id").ValueKind);

        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Groups, Group("Сирота", "Orphan", 1, int.MaxValue)),
            HttpStatusCode.BadRequest,
            "parent_group_id",
            AdminReferenceErrors.Unknown);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Groups, sectionId, sectionVersion, Group("Розділ", "Section", 90, sectionId)),
            HttpStatusCode.BadRequest,
            "parent_group_id",
            AdminReferenceErrors.NotApplicable);
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.UpdateAsync(admin, Groups, sectionId, sectionVersion, Group("Розділ", "Section", 90, childId)),
            HttpStatusCode.BadRequest,
            "parent_group_id",
            AdminReferenceErrors.NotApplicable);

        using IServiceScope scope = _factory.Services.CreateScope();
        PartGroupEntity stored = await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartGroups.AsNoTracking()
            .SingleAsync(group => group.Id == sectionId, Token);
        Assert.Null(stored.ParentGroupId);
        Assert.Equal(sectionVersion, stored.Version);
    }

    private static object Group(string ukrainian, string english, int sortOrder, int? parentGroupId) => new
    {
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
        sort_order = sortOrder,
        parent_group_id = parentGroupId,
    };

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope reset = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(reset.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await reset.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}
