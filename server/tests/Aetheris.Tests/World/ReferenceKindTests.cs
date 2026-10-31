using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Security;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the shared shell of the reference kinds and of the resources of the world in the admin
/// panel (task 00023, sections 4.1–4.3).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReferenceKindTests
{
    private const string Resources = "/world/resources";

    private const string Groups = "/world/resource-groups";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceKindTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ReferenceKindTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A resource is created, changed, archived and returned from the archive in the admin panel:
    /// the list hides an archived row unless the filter asks for it, and the texts change with the
    /// row (acceptance criteria 1 and 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_resource_is_changed_and_archived_from_the_admin_panel()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);
        int minerals = await GroupIdAsync(SeedCodes.MineralGroup);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Олово", "Tin")));
        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, id);
        string code = record.GetProperty("code").GetString()!;
        Assert.Equal(version, record.GetProperty("version").GetInt32());
        Assert.Equal("Олово", record.GetProperty("values").GetProperty("name").GetProperty("uk").GetString());

        (_, int changed) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource(minerals, "Олово біле", "White tin")));
        Assert.Equal(version + 1, changed);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ResourceTypeEntity resource = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking().SingleAsync(entry => entry.Id == id, Token);
            LocalizationPack english = await scope.ServiceProvider.GetRequiredService<ILocalizationTexts>().GetPackAsync("en", Token);

            Assert.Equal(minerals, resource.ResourceGroupId);
            Assert.Equal("White tin", english.Texts[resource.NameKey]);
        }

        (_, int archived) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Resources, id, changed));
        Assert.DoesNotContain(code, await ReferenceAdmin.ListCodesAsync(admin, Resources));
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Resources, "archived=true"));
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Resources, "all=true"));

        // An archived row is archived once; a second archiving is refused by the rule of the archive.
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.ArchiveAsync(admin, Resources, id, archived),
            HttpStatusCode.UnprocessableEntity,
            "is_archived",
            AdminReferenceErrors.StateUnchanged);

        // An archived group is not offered for a new resource.
        (int groupId, int groupVersion) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(
            admin,
            Groups,
            new { name = ReferenceAdmin.Texts("Сплави", "Alloys"), description = ReferenceAdmin.Texts("Сплави металів", "Metal alloys"), allowed_on_planet = false }));
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Groups, groupId, groupVersion));
        await ReferenceAdmin.AssertFieldAsync(
            await ReferenceAdmin.CreateAsync(admin, Resources, Resource(groupId, "Бронза", "Bronze")),
            HttpStatusCode.UnprocessableEntity,
            "resource_group_id",
            AdminReferenceErrors.Archived);

        (_, int restored) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Resources, id, archived, archive: false));
        Assert.Equal(archived + 1, restored);
        Assert.Contains(code, await ReferenceAdmin.ListCodesAsync(admin, Resources, "search=" + Uri.EscapeDataString("біле")));
    }

    /// <summary>
    /// A change needs a reason, and the journal keeps the old and the new values with the reason
    /// (acceptance criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_reference_change_needs_a_reason_and_is_written_to_the_journal()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);
        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Цинк", "Zinc")));

        HttpResponseMessage withoutReason = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource(metals, "Цинк сірий", "Grey zinc"), reason: null);
        await ReferenceAdmin.AssertFieldAsync(withoutReason, HttpStatusCode.BadRequest, "reason", AdminReason.Required);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource(metals, "Цинк сірий", "Grey zinc")));

        using IServiceScope scope = _factory.Services.CreateScope();
        AdminAuditLogEntity audit = await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == owner.AdminId && record.Action == WorldReferenceAdminService.ResourceDescriptor.Permissions.Updated)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);

        Assert.Equal(AdminReferenceEndpoints.Target(WorldReferenceAdminService.ResourceKind, id), audit.Target);
        Assert.Contains("Zinc", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("Grey zinc", audit.NewValue, StringComparison.Ordinal);
        Assert.Equal(ReferenceAdmin.Reason, audit.Reason);
    }

    /// <summary>
    /// A change made from an old version is refused and writes nothing (acceptance criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_reference_change_that_meets_an_old_version_is_refused()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);
        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Нікель", "Nickel")));

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource(metals, "Нікель 2", "Nickel 2")));
        HttpResponseMessage stale = await ReferenceAdmin.UpdateAsync(admin, Resources, id, version, Resource(metals, "Нікель 3", "Nickel 3"));
        HttpResponseMessage staleArchive = await ReferenceAdmin.ArchiveAsync(admin, Resources, id, version);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AdminReferenceErrors.VersionConflict, await TestAccounts.ReadErrorAsync(stale));
        Assert.Equal(HttpStatusCode.Conflict, staleArchive.StatusCode);

        JsonElement record = await ReferenceAdmin.ReadAsync(admin, Resources, id);
        Assert.Equal("Nickel 2", record.GetProperty("values").GetProperty("name").GetProperty("en").GetString());
        Assert.False(record.GetProperty("is_archived").GetBoolean());
    }

    /// <summary>
    /// No reference kind has a delete: the routes of every kind answer only reads, creations,
    /// changes and the archive, and an archived row stays in its table (acceptance criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_reference_row_is_never_deleted()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        List<string> routes =
        [
            .. scope.ServiceProvider.GetServices<IAdminReferenceService>()
                .SelectMany(service => service.Descriptors)
                .Select(descriptor => AdminEndpoints.Prefix + descriptor.Route),
        ];

        List<string> deletes =
        [
            .. _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => routes.Exists(route => endpoint.RoutePattern.RawText!.StartsWith(route, StringComparison.Ordinal)))
                .Where(endpoint => endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true)
                .Select(endpoint => endpoint.RoutePattern.RawText!),
        ];

        Assert.True(routes.Count >= 5, "The reference kinds were not found.");
        Assert.Empty(deletes);

        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient admin = await OwnerAsync();
        int metals = await GroupIdAsync(SeedCodes.MetalGroup);
        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Свинець", "Lead")));
        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, Resources, id, version));

        using IServiceScope after = _factory.Services.CreateScope();
        ResourceTypeEntity resource = await after.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking().SingleAsync(entry => entry.Id == id, Token);
        Assert.True(resource.IsArchived);
    }

    /// <summary>
    /// A PNG or a WebP picture is stored and served by its key; another type and a too large file
    /// are refused by the limits of the configuration; an unknown key is not found, and a row only
    /// takes a key of a stored picture (acceptance criterion 8).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_image_is_uploaded_and_served()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        string folder = Path.Combine(Path.GetTempPath(), "aetheris-reference-images-" + Guid.NewGuid().ToString("N"));

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ReferenceImageKeys.Directory, folder, reload: false);
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ReferenceImageKeys.MaxBytes, "2048");
        }

        try
        {
            using HttpClient admin = await OwnerAsync();
            byte[] png = [.. Convert.FromHexString("89504E470D0A1A0A"), .. new byte[64]];
            byte[] webp = [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray(), .. new byte[32]];
            byte[] gif = [.. "GIF89a"u8.ToArray(), .. new byte[32]];
            byte[] large = [.. Convert.FromHexString("89504E470D0A1A0A"), .. new byte[4096]];

            string pngKey = await UploadedKeyAsync(await UploadAsync(admin, png));
            string webpKey = await UploadedKeyAsync(await UploadAsync(admin, webp));
            await ReferenceAdmin.AssertFieldAsync(await UploadAsync(admin, gif), HttpStatusCode.UnprocessableEntity, ReferenceImageEndpoints.FileField, ReferenceImageEndpoints.TypeRefused);
            await ReferenceAdmin.AssertFieldAsync(await UploadAsync(admin, large), HttpStatusCode.UnprocessableEntity, ReferenceImageEndpoints.FileField, ReferenceImageEndpoints.TooLarge);

            using HttpClient visitor = _factory.CreateBrowserClient();
            HttpResponseMessage served = await visitor.GetAsync("/api/v1/images/" + pngKey, Token);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal(ReferenceImageFormats.Png, served.Content.Headers.ContentType?.MediaType);
            Assert.Equal(png, await served.Content.ReadAsByteArrayAsync(Token));
            Assert.EndsWith(".webp", webpKey, StringComparison.Ordinal);

            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/v1/images/" + new string('a', 32) + ".png", Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/v1/images/..%2Fappsettings.json", Token)).StatusCode);

            int metals = await GroupIdAsync(SeedCodes.MetalGroup);
            await ReferenceAdmin.AssertFieldAsync(
                await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Вольфрам", "Tungsten", new string('b', 32) + ".png")),
                HttpStatusCode.BadRequest,
                AdminReferenceRules.ImageField,
                AdminReferenceErrors.Unknown);

            (int id, _) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, Resources, Resource(metals, "Вольфрам", "Tungsten", pngKey)));
            Assert.Equal(pngKey, (await ReferenceAdmin.ReadAsync(admin, Resources, id)).GetProperty("image_key").GetString());
        }
        finally
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            await AetherisTestFactory.ResetDatabaseAsync(scope.ServiceProvider);

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// Every endpoint of every reference kind has an action code that is a permission of the seed,
    /// the main role has them all, the menu lists the kinds the administrator may read, and a role
    /// without the permission is refused (acceptance criterion 14; the roles themselves wait for
    /// the task of the rights of the admin panel).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_admin_endpoints_of_the_modules_have_their_permissions()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        List<AdminReferenceDescriptor> descriptors = [.. scope.ServiceProvider.GetServices<IAdminReferenceService>().SelectMany(service => service.Descriptors)];
        Dictionary<string, string> kinds = await main.AdminPermissions.AsNoTracking().ToDictionaryAsync(permission => permission.Code, permission => permission.Kind, Token);
        int ownerId = await main.AdminRoles.Where(role => role.Code == AdminRoles.Owner).Select(role => role.Id).SingleAsync(Token);
        HashSet<int> ownerGrants = [.. await main.AdminRolePermissions.Where(grant => grant.AdminRoleId == ownerId).Select(grant => grant.AdminPermissionId).ToListAsync(Token)];
        Dictionary<string, int> permissionIds = await main.AdminPermissions.AsNoTracking().ToDictionaryAsync(permission => permission.Code, permission => permission.Id, Token);

        Assert.Equal(
            ["resource", "resource_group", "building", "building_category", "technology", "technology_branch", "part_group", "part", "recipe",
             "process_type", "process_recipe", "process_param"],
            descriptors.OrderBy(descriptor => descriptor.SortOrder).Select(descriptor => descriptor.Kind));
        foreach (AdminReferenceDescriptor descriptor in descriptors)
        {
            Assert.All(descriptor.Permissions.Screens, code => Assert.Equal(AdminPermissionKinds.Screen, kinds[code]));
            Assert.All(descriptor.Permissions.Actions, code => Assert.Equal(AdminPermissionKinds.Action, kinds[code]));
            Assert.All(descriptor.Permissions.Screens.Concat(descriptor.Permissions.Actions), code => Assert.Contains(permissionIds[code], ownerGrants));
        }

        using HttpClient owner = await OwnerAsync();
        using JsonDocument menu = JsonDocument.Parse(await owner.GetStringAsync(AdminEndpoints.Prefix + "/reference/kinds", Token));
        Assert.Equal(descriptors.Count, menu.RootElement.GetProperty("kinds").GetArrayLength());
        Assert.Contains("uk", menu.RootElement.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));
        Assert.Contains(ReferenceImageFormats.Webp, menu.RootElement.GetProperty("image_types").EnumerateArray().Select(type => type.GetString()));

        TestAdmin moderator = await TestAdmins.CreateAsync(_factory, AdminRoles.Moderation);
        using HttpClient moderation = await TestAdmins.SignInAsync(_factory, moderator);
        Assert.Equal(HttpStatusCode.Forbidden, (await moderation.GetAsync(AdminEndpoints.Prefix + "/reference/kinds", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await moderation.GetAsync(AdminEndpoints.Prefix + "/planets/buildings", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReferenceAdmin.CreateAsync(moderation, Resources, new { })).StatusCode);
    }

    private static object Resource(int groupId, string ukrainian, string english, string? imageKey = null) => new
    {
        image_key = imageKey,
        resource_group_id = groupId,
        scope = ResourceScopes.Planet,
        name = ReferenceAdmin.Texts(ukrainian, english),
        description = ReferenceAdmin.Texts(ukrainian + " опис", english + " description"),
    };

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content)
    {
        using ByteArrayContent body = new(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        return await client.PostAsync(AdminEndpoints.Prefix + ReferenceImageEndpoints.UploadRoute, body, Token);
    }

    private static async Task<string> UploadedKeyAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using JsonDocument document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("image_key").GetString()!;
    }

    private async Task<HttpClient> OwnerAsync() =>
        await TestAdmins.SignInAsync(_factory, await TestAdmins.CreateAsync(_factory, AdminRoles.Owner));

    private async Task<int> GroupIdAsync(string code)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceGroups
            .Where(group => group.Code == code)
            .Select(group => group.Id)
            .SingleAsync(Token);
    }
}
