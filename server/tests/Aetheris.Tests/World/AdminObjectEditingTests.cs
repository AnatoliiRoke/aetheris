using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the cards of the admin panel that correct a field, a body or a star system, and of
/// the screens of the generation rules (task 00009).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminObjectEditingTests
{
    private const string Reason = "Manual correction in a test";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminObjectEditingTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AdminObjectEditingTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The tables of the fields, the bodies and the systems show x, y and z; a free field shows
    /// its own place from the database (acceptance criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_tables_show_the_coordinates()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient client = await OwnerAsync();
        (long fieldId, CelestialObjectEntity entry) = await FreeFieldAsync();

        using JsonDocument fields = await GetAsync(client, $"/world/fields?search={fieldId}");
        JsonElement row = Assert.Single(fields.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(entry.X, row.GetProperty("x").GetDouble());
        Assert.Equal(entry.Y, row.GetProperty("y").GetDouble());
        Assert.Equal(entry.Z, row.GetProperty("z").GetDouble());
        Assert.StartsWith("world.object_type.", row.GetProperty("object_type_name_key").GetString(), StringComparison.Ordinal);

        using JsonDocument systems = await GetAsync(client, "/world/systems");
        JsonElement system = systems.RootElement.GetProperty("items")[0];
        Assert.True(system.TryGetProperty("z", out _));
        Assert.StartsWith("world.object_type.", system.GetProperty("star_type_name_key").GetString(), StringComparison.Ordinal);

        using JsonDocument bodies = await GetAsync(client, "/world/objects");
        JsonElement body = bodies.RootElement.GetProperty("items")[0];
        Assert.True(body.TryGetProperty("x", out _));
        Assert.True(body.TryGetProperty("z", out _));
        Assert.StartsWith("world.size.", body.GetProperty("size_class_name_key").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The card of a field shows the whole field; a change of every allowed value is stored, the
    /// journal keeps the old and the new value with the reason, and a change without a reason
    /// stores nothing (acceptance criteria 2 and 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_field_is_changed_with_a_reason_and_journaled()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);
        (long fieldId, _) = await FreeFieldAsync();
        Dictionary<string, int> resources = await ResourcesAsync();
        int richType = await TypeIdAsync("field_rich");

        using JsonDocument card = await GetAsync(client, $"/world/fields/{fieldId}");
        JsonElement before = card.RootElement;
        Assert.NotEmpty(before.GetProperty("composition_ids").EnumerateArray());
        Assert.Equal(0.95m, before.GetProperty("bounds").GetProperty("max_composition_share").GetDecimal());

        object body = new
        {
            version = before.GetProperty("version").GetInt32(),
            reason = Reason,
            name = "Edited field",
            object_type_id = richType,
            x = 10.5,
            y = 20.25,
            z = 0d,
            mass = 1234L,
            initial_mass = 5000L,
            depleted = false,
            composition_ids = new[]
            {
                new { resource_id = resources["iron"], coefficient = 0.5m },
                new { resource_id = resources["quartzite"], coefficient = 0.1m },
            },
        };

        HttpResponseMessage withoutReason = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/fields/{fieldId}",
            new { version = before.GetProperty("version").GetInt32(), reason = " ", name = "Lost", object_type_id = richType, mass = 1L, initial_mass = 1L },
            Token);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);

        HttpResponseMessage saved = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/fields/{fieldId}", body, Token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using JsonDocument after = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Token));
        JsonElement field = after.RootElement;
        Assert.Equal("Edited field", field.GetProperty("name").GetString());
        Assert.Equal(richType, field.GetProperty("object_type_id").GetInt32());
        Assert.Equal(10.5, field.GetProperty("x").GetDouble());
        Assert.Equal(1234, field.GetProperty("mass").GetInt64());
        Assert.Equal(5000, field.GetProperty("initial_mass").GetInt64());
        Assert.Equal(before.GetProperty("version").GetInt32() + 1, field.GetProperty("version").GetInt32());
        Assert.Equal(
            [resources["iron"], resources["quartzite"]],
            field.GetProperty("composition_ids").EnumerateArray().Select(share => share.GetProperty("resource_id").GetInt32()).Order());

        AdminAuditLogEntity audit = await LastAuditAsync(owner, "admin.world_field_updated");
        Assert.Equal("field:" + fieldId, audit.Target);
        Assert.Equal(Reason, audit.Reason);
        Assert.Contains("\"mass\":", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("\"mass\":1234", audit.NewValue, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"Edited field\"", audit.NewValue, StringComparison.Ordinal);

        // The field is marked as depleted and comes back again.
        HttpResponseMessage depleted = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/fields/{fieldId}",
            WithVersion(body, field.GetProperty("version").GetInt32(), depleted: true),
            Token);
        using JsonDocument depletedCard = JsonDocument.Parse(await depleted.Content.ReadAsStringAsync(Token));
        Assert.NotEqual(JsonValueKind.Null, depletedCard.RootElement.GetProperty("depleted_at").ValueKind);

        HttpResponseMessage restored = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/fields/{fieldId}",
            WithVersion(body, depletedCard.RootElement.GetProperty("version").GetInt32(), depleted: false),
            Token);
        using JsonDocument restoredCard = JsonDocument.Parse(await restored.Content.ReadAsStringAsync(Token));
        Assert.Equal(JsonValueKind.Null, restoredCard.RootElement.GetProperty("depleted_at").ValueKind);
    }

    /// <summary>
    /// A change that breaks a rule is refused with the reason and stores nothing: shares above
    /// the limit, a negative mass, a place outside the sector, an unknown resource, a type of
    /// another class (acceptance criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_field_change_that_breaks_a_rule_is_refused()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient client = await OwnerAsync();
        (long fieldId, _) = await FreeFieldAsync();
        Dictionary<string, int> resources = await ResourcesAsync();
        int fieldType = await TypeIdAsync("field_ordinary");
        int planetType = await TypeIdAsync("terrestrial");

        using JsonDocument card = await GetAsync(client, $"/world/fields/{fieldId}");
        int version = card.RootElement.GetProperty("version").GetInt32();
        long mass = card.RootElement.GetProperty("mass").GetInt64();

        object Field(int typeId, double x, long newMass, params (int ResourceId, decimal Coefficient)[] shares) => new
        {
            version,
            reason = Reason,
            name = "Field",
            object_type_id = typeId,
            x,
            y = 1d,
            z = 0d,
            mass = newMass,
            initial_mass = 10L,
            depleted = false,
            composition_ids = shares.Select(share => new { resource_id = share.ResourceId, coefficient = share.Coefficient }).ToArray(),
        };

        await AssertRefusedAsync(client, fieldId, Field(fieldType, 1, 10, (resources["iron"], 0.6m), (resources["copper"], 0.4m)), "composition_ids", AdminEditErrors.ShareTooLarge);
        await AssertRefusedAsync(client, fieldId, Field(fieldType, 1, -1), "mass", AdminEditErrors.Negative);
        await AssertRefusedAsync(client, fieldId, Field(fieldType, 1001, 10), "x", AdminEditErrors.OutOfBounds);
        await AssertRefusedAsync(client, fieldId, Field(fieldType, 1, 10, (999_999, 0.1m)), "composition_ids", AdminEditErrors.Unknown);
        await AssertRefusedAsync(client, fieldId, Field(planetType, 1, 10), "object_type_id", AdminEditErrors.ClassMismatch);
        await AssertRefusedAsync(client, fieldId, Field(999_999, 1, 10), "object_type_id", AdminEditErrors.Unknown);

        using JsonDocument unchanged = await GetAsync(client, $"/world/fields/{fieldId}");
        Assert.Equal(version, unchanged.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(mass, unchanged.RootElement.GetProperty("mass").GetInt64());
    }

    /// <summary>
    /// A save with an older version gets 409 and writes nothing; a cycle of mining between the
    /// opening of the card and its save is kept (acceptance criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stale_card_is_refused_and_the_mining_is_kept()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient client = await OwnerAsync();
        (long fieldId, _) = await FreeFieldAsync();

        using JsonDocument card = await GetAsync(client, $"/world/fields/{fieldId}");
        JsonElement opened = card.RootElement;
        long massBefore = opened.GetProperty("mass").GetInt64();

        // A player mines the field while the administrator looks at the card.
        long remaining;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FieldService fields = scope.ServiceProvider.GetRequiredService<FieldService>();
            remaining = (await fields.ExtractAsync(fieldId, 100, long.MaxValue, Token)).RemainingMass;
        }

        Assert.Equal(massBefore - 100, remaining);

        HttpResponseMessage stale = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/fields/{fieldId}",
            new
            {
                version = opened.GetProperty("version").GetInt32(),
                reason = Reason,
                name = opened.GetProperty("name").GetString(),
                object_type_id = opened.GetProperty("object_type_id").GetInt32(),
                x = opened.GetProperty("x").GetDouble(),
                y = opened.GetProperty("y").GetDouble(),
                z = opened.GetProperty("z").GetDouble(),
                mass = massBefore,
                initial_mass = opened.GetProperty("initial_mass").GetInt64(),
                depleted = false,
                composition_ids = Array.Empty<object>(),
            },
            Token);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(WorldErrorCodes.VersionConflict, await TestAccounts.ReadErrorAsync(stale));

        using JsonDocument now = await GetAsync(client, $"/world/fields/{fieldId}");
        Assert.Equal(remaining, now.RootElement.GetProperty("mass").GetInt64());
        Assert.NotEmpty(now.RootElement.GetProperty("composition_ids").EnumerateArray());
    }

    /// <summary>
    /// A body changes its name, type, size, orbit and deposits; an owner sent in the request is
    /// ignored, the request has no such field, and a taken orbit or a resource that may not lie
    /// on a body is refused (acceptance criteria 3, 4 and 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_body_is_changed_without_touching_the_owner()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient client = await OwnerAsync();
        Dictionary<string, int> resources = await ResourcesAsync();

        long planetId;
        long neighbourId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            int planetClassTypes = await world.ObjectTypes.CountAsync(type => type.ObjectClass == WorldObjectClasses.Planet, Token);
            Assert.True(planetClassTypes > 1);

            var pair = await world.Planets.AsNoTracking()
                .Join(world.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet.Id, planet.SystemId, entry.ObjectTypeId })
                .Join(world.ObjectTypes, body => body.ObjectTypeId, type => type.Id, (body, type) => new { body.Id, body.SystemId, type.ObjectClass })
                .Where(body => body.ObjectClass == WorldObjectClasses.Planet)
                .GroupBy(body => body.SystemId)
                .Where(group => group.Count() > 1)
                .Select(group => new { First = group.Min(body => body.Id), Second = group.Max(body => body.Id) })
                .FirstAsync(Token);

            planetId = pair.First;
            neighbourId = pair.Second;

            PlanetEntity owned = await world.Planets.SingleAsync(planet => planet.Id == planetId, Token);
            owned.OwnerPlayerId = "player-of-the-test";
            await world.SaveChangesAsync(Token);
        }

        using JsonDocument card = await GetAsync(client, $"/world/objects/{planetId}");
        JsonElement opened = card.RootElement;
        JsonElement bounds = opened.GetProperty("bounds");
        List<int> taken = [.. bounds.GetProperty("taken_orbit_ids").EnumerateArray().Select(orbit => orbit.GetProperty("orbit").GetInt32())];
        int freeOrbit = Enumerable.Range(bounds.GetProperty("orbit_min").GetInt32(), bounds.GetProperty("orbit_max").GetInt32())
            .First(orbit => !taken.Contains(orbit) && orbit != opened.GetProperty("orbit").GetInt32());
        int neighbourOrbit;
        using (JsonDocument neighbour = await GetAsync(client, $"/world/objects/{neighbourId}"))
        {
            neighbourOrbit = neighbour.RootElement.GetProperty("orbit").GetInt32();
        }

        JsonElement sizeClass = bounds.GetProperty("size_class_ids")[0];
        int desert = await TypeIdAsync("desert");

        object Body(int orbit, int resourceId, decimal coefficient) => new
        {
            version = opened.GetProperty("version").GetInt32(),
            reason = Reason,
            name = "Edited planet",
            object_type_id = desert,
            size_class_id = sizeClass.GetProperty("id").GetInt32(),
            size = sizeClass.GetProperty("size_min").GetInt32(),
            orbit,
            owner_player_id = "someone-else",
            deposit_ids = new[] { new { resource_id = resourceId, coefficient } },
        };

        await AssertBodyRefusedAsync(client, planetId, Body(neighbourOrbit, resources["iron"], 1m), "orbit", AdminEditErrors.OrbitTaken);
        await AssertBodyRefusedAsync(client, planetId, Body(freeOrbit, resources["ether"], 1m), "deposit_ids", AdminEditErrors.NotAllowedOnBody);
        await AssertBodyRefusedAsync(client, planetId, Body(freeOrbit, resources["iron"], 2.5m), "deposit_ids", AdminEditErrors.OutOfBounds);

        HttpResponseMessage saved = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/objects/{planetId}", Body(freeOrbit, resources["iron"], 1.25m), Token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using JsonDocument after = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Token));
        JsonElement body = after.RootElement;
        Assert.Equal("Edited planet", body.GetProperty("name").GetString());
        Assert.Equal(desert, body.GetProperty("object_type_id").GetInt32());
        Assert.Equal(freeOrbit, body.GetProperty("orbit").GetInt32());
        Assert.Equal("player-of-the-test", body.GetProperty("owner_player_id").GetString());
        JsonElement deposit = Assert.Single(body.GetProperty("deposit_ids").EnumerateArray());
        Assert.Equal(1.25m, deposit.GetProperty("coefficient").GetDecimal());
        Assert.Equal("world.resource.iron", deposit.GetProperty("resource_name_key").GetString());

        // The request type has nothing that reaches the owner or the name the owner gave.
        Assert.DoesNotContain(
            typeof(AdminBodyUpdate).GetProperties(),
            property => property.Name.Contains("Owner", StringComparison.Ordinal));
    }

    /// <summary>
    /// A star system moves with its objects, gets another star type and refuses a place outside
    /// the sector and a type that is not a star.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_system_moves_with_its_objects()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient client = await OwnerAsync();

        long systemId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            systemId = await world.Systems.Where(system => world.Planets.Any(planet => planet.SystemId == system.Id)).Select(system => system.Id).FirstAsync(Token);
        }

        using JsonDocument card = await GetAsync(client, $"/world/systems/{systemId}");
        int version = card.RootElement.GetProperty("version").GetInt32();
        int redDwarf = await TypeIdAsync("red_dwarf");
        int terrestrial = await TypeIdAsync("terrestrial");

        object System(double x, int starTypeId) => new { version, reason = Reason, name = "Edited system", x, y = 33.5, z = 0d, star_type_id = starTypeId };

        HttpResponseMessage outside = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/systems/{systemId}", System(-5, redDwarf), Token);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Contains(AdminEditErrors.OutOfBounds, await outside.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        HttpResponseMessage notAStar = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/systems/{systemId}", System(5, terrestrial), Token);
        Assert.Contains(AdminEditErrors.ClassMismatch, await notAStar.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        HttpResponseMessage saved = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/systems/{systemId}", System(44.25, redDwarf), Token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using JsonDocument after = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Token));
        Assert.Equal(redDwarf, after.RootElement.GetProperty("star_type_id").GetInt32());
        Assert.Equal("Edited system", after.RootElement.GetProperty("name").GetString());

        using IServiceScope check = _factory.Services.CreateScope();
        WorldDbContext db = check.ServiceProvider.GetRequiredService<WorldDbContext>();
        List<CelestialObjectEntity> objects = await db.Objects.AsNoTracking().Where(entry => entry.SystemId == systemId).ToListAsync(Token);
        Assert.NotEmpty(objects);
        Assert.All(objects, entry => Assert.Equal((44.25, 33.5), (entry.X, entry.Y)));
    }

    /// <summary>
    /// The object types and the composition rules are listed like the database holds them,
    /// changed within their bounds with a reason and a version, journaled, and used by the next
    /// generation without a restart; the objects that exist stay as they are (acceptance
    /// criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_generation_rules_are_edited_and_used_by_the_next_generation()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);
        int ordinary = await TypeIdAsync("field_ordinary");
        int rich = await TypeIdAsync("field_rich");

        using JsonDocument types = await GetAsync(client, "/world/object-types?object_class=asteroid_field");
        List<JsonElement> fieldTypes = [.. types.RootElement.GetProperty("items").EnumerateArray()];
        Assert.Equal(2, fieldTypes.Count);
        JsonElement ordinaryRow = fieldTypes.Single(row => row.GetProperty("id").GetInt32() == ordinary);
        Assert.Equal(500_000, ordinaryRow.GetProperty("mass_min").GetInt64());
        int ordinaryVersion = ordinaryRow.GetProperty("version").GetInt32();
        int richVersion = fieldTypes.Single(row => row.GetProperty("id").GetInt32() == rich).GetProperty("version").GetInt32();

        HttpResponseMessage inverted = await UpdateTypeAsync(client, ordinary, ordinaryVersion, 80, 900, 800);
        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);
        Assert.Contains(AdminEditErrors.RangeInverted, await inverted.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        HttpResponseMessage negative = await UpdateTypeAsync(client, ordinary, ordinaryVersion, -1, 777, 777);
        Assert.Contains(AdminEditErrors.Negative, await negative.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        // Switching off every field type would stop the generator: refused.
        Assert.Equal(HttpStatusCode.OK, (await UpdateTypeAsync(client, rich, richVersion, 0, 300_000, 1_000_000)).StatusCode);
        HttpResponseMessage noFields = await UpdateTypeAsync(client, ordinary, ordinaryVersion, 0, 777, 777);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noFields.StatusCode);
        Assert.Equal(WorldErrorCodes.ConfigInvalid, await TestAccounts.ReadErrorAsync(noFields));

        Assert.Equal(HttpStatusCode.OK, (await UpdateTypeAsync(client, ordinary, ordinaryVersion, 80, 777, 777)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UpdateTypeAsync(client, ordinary, ordinaryVersion, 80, 778, 778)).StatusCode);

        AdminAuditLogEntity audit = await LastAuditAsync(owner, "admin.world_object_type_updated");
        Assert.Equal("object_type:" + ordinary, audit.Target);
        Assert.Contains("\"mass_min\":500000", audit.OldValue, StringComparison.Ordinal);
        Assert.Contains("\"mass_min\":777", audit.NewValue, StringComparison.Ordinal);
        Assert.Equal(Reason, audit.Reason);

        // A rule is switched off, and a new one is added to its group.
        using JsonDocument rules = await GetAsync(client, $"/world/generation-rules?object_type_id={ordinary}&search=crystals");
        JsonElement quartzite = rules.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("resource_code").GetString() == "quartzite");
        Assert.Equal("world.resource.quartzite", quartzite.GetProperty("resource_name_key").GetString());

        HttpResponseMessage switchedOff = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/generation-rules/{quartzite.GetProperty("id").GetInt32()}",
            RuleBody(quartzite, isActive: false),
            Token);
        Assert.Equal(HttpStatusCode.OK, switchedOff.StatusCode);
        using JsonDocument switchedOffVersion = JsonDocument.Parse(await switchedOff.Content.ReadAsStringAsync(Token));
        Assert.Equal(quartzite.GetProperty("version").GetInt32() + 1, switchedOffVersion.RootElement.GetProperty("version").GetInt32());

        // The old version of the rule is refused now.
        HttpResponseMessage staleRule = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/generation-rules/{quartzite.GetProperty("id").GetInt32()}",
            RuleBody(quartzite, isActive: true),
            Token);
        Assert.Equal(HttpStatusCode.Conflict, staleRule.StatusCode);

        HttpResponseMessage duplicate = await client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/generation-rules",
            RuleBody(quartzite, isActive: true),
            Token);
        Assert.Contains(AdminEditErrors.Duplicate, await duplicate.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        HttpResponseMessage inverse = await client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/generation-rules",
            RuleBody(quartzite, isActive: true, minimum: 0.5m, maximum: 0.1m),
            Token);
        Assert.Contains(AdminEditErrors.RangeInverted, await inverse.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        List<long> existingBefore = await FieldMassesAsync(sectorId: null);

        int sectorId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();
            sectorId = (await world.AddSectorAsync(4242, false, owner.AdminId, Token)).Id;
        }

        List<long> newMasses = await FieldMassesAsync(sectorId);
        Assert.NotEmpty(newMasses);
        Assert.All(newMasses, mass => Assert.Equal(777, mass));

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            int quartziteId = quartzite.GetProperty("resource_id").GetInt32();
            Assert.False(await world.FieldCompositions.AnyAsync(share => share.SectorId == sectorId && share.ResourceId == quartziteId, Token));
            Assert.Equal(existingBefore, (await FieldMassesAsync(sectorId: null)).Take(existingBefore.Count));
        }
    }

    /// <summary>
    /// A rule of a planet type keeps its multipliers within the range of the configuration.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_planet_rule_stays_within_the_deposit_range()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        using HttpClient client = await OwnerAsync();
        int terrestrial = await TypeIdAsync("terrestrial");

        using JsonDocument rules = await GetAsync(client, $"/world/generation-rules?object_type_id={terrestrial}");
        JsonElement rule = rules.RootElement.GetProperty("items")[0];
        Assert.Equal(1.4m, rule.GetProperty("rich_coefficient_min").GetDecimal());
        Assert.Equal(1.9m, rule.GetProperty("rich_coefficient_max").GetDecimal());

        HttpResponseMessage tooRich = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/generation-rules/{rule.GetProperty("id").GetInt32()}",
            RuleBody(rule, isActive: true, minimum: 0.8m, maximum: 2.5m),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, tooRich.StatusCode);
        Assert.Contains(AdminEditErrors.OutOfBounds, await tooRich.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    private static object RuleBody(JsonElement rule, bool isActive, decimal? minimum = null, decimal? maximum = null) => new
    {
        version = rule.GetProperty("version").GetInt32(),
        reason = Reason,
        rule_group_id = rule.GetProperty("rule_group_id").GetInt32(),
        resource_id = rule.GetProperty("resource_id").GetInt32(),
        weight = rule.GetProperty("weight").GetInt32(),
        coefficient_min = minimum ?? rule.GetProperty("coefficient_min").GetDecimal(),
        coefficient_max = maximum ?? rule.GetProperty("coefficient_max").GetDecimal(),
        rich_coefficient_min = rule.GetProperty("rich_coefficient_min").ValueKind == JsonValueKind.Null ? (decimal?)null : rule.GetProperty("rich_coefficient_min").GetDecimal(),
        rich_coefficient_max = rule.GetProperty("rich_coefficient_max").ValueKind == JsonValueKind.Null ? (decimal?)null : rule.GetProperty("rich_coefficient_max").GetDecimal(),
        is_active = isActive,
    };

    private static Task<HttpResponseMessage> UpdateTypeAsync(HttpClient client, int typeId, int version, int weight, long massMin, long massMax) =>
        client.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/world/object-types/{typeId}",
            new { version, reason = Reason, generation_weight = weight, mass_min = massMin, mass_max = massMax, is_colonizable = false },
            Token);

    private static object WithVersion(object body, int version, bool depleted)
    {
        Dictionary<string, object?> values = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(body))!;
        values["version"] = version;
        values["depleted"] = depleted;

        return values;
    }

    private static async Task AssertRefusedAsync(HttpClient client, long fieldId, object body, string field, string code)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/fields/{fieldId}", body, Token);
        await AssertFieldErrorAsync(response, field, code);
    }

    private static async Task AssertBodyRefusedAsync(HttpClient client, long planetId, object body, string field, string code)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(AdminEndpoints.Prefix + $"/world/objects/{planetId}", body, Token);
        await AssertFieldErrorAsync(response, field, code);
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal("validation_failed", error.RootElement.GetProperty("error").GetString());
        Assert.Contains(code, error.RootElement.GetProperty("details").GetProperty(field).EnumerateArray().Select(entry => entry.GetString()));
    }

    private async Task<HttpClient> OwnerAsync()
    {
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);

        return await TestAdmins.SignInAsync(_factory, owner);
    }

    private static async Task<JsonDocument> GetAsync(HttpClient client, string path) =>
        JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + path, Token));

    private async Task<(long FieldId, CelestialObjectEntity Object)> FreeFieldAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        var field = await world.Fields.AsNoTracking()
            .Where(entry => entry.SystemId == null && entry.DepletedAt == null)
            .Join(world.Objects, entry => entry.CelestialObjectId, entry => entry.Id, (field, entry) => new { field.Id, Object = entry })
            .OrderBy(entry => entry.Id)
            .FirstAsync(Token);

        return (field.Id, field.Object);
    }

    private async Task<Dictionary<string, int>> ResourcesAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        return await world.ResourceTypes.AsNoTracking().ToDictionaryAsync(resource => resource.Code, resource => resource.Id, StringComparer.Ordinal, Token);
    }

    private async Task<int> TypeIdAsync(string code)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        return await world.ObjectTypes.Where(type => type.Code == code).Select(type => type.Id).SingleAsync(Token);
    }

    private async Task<List<long>> FieldMassesAsync(int? sectorId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        return await world.Fields.AsNoTracking()
            .Where(field => sectorId == null || field.SectorId == sectorId)
            .OrderBy(field => field.Id)
            .Select(field => field.InitialMass)
            .ToListAsync(Token);
    }

    private async Task<AdminAuditLogEntity> LastAuditAsync(TestAdmin admin, string action)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        return await main.AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == admin.AdminId && record.Action == action)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);
    }
}
