using Aetheris.Core.Configuration;
using Aetheris.Host.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.World;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Builds the input of the world for the tests.
/// </summary>
public static class WorldTestData
{
    /// <summary>
    /// A small sector, so the database tests stay fast; the performance test sets its own size.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> SmallSectors = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WorldConfigKeys.SystemsMin] = "12",
        [WorldConfigKeys.SystemsMax] = "20",
        [WorldConfigKeys.FreeFieldsMin] = "2",
        [WorldConfigKeys.FreeFieldsMax] = "3",
        [WorldConfigKeys.MinFieldsPerSector] = "6",
    };

    /// <summary>
    /// Builds the reference snapshot from the embedded starting data, with identifiers in the
    /// order of the file, as a fresh database would give them.
    /// </summary>
    /// <returns>The reference data.</returns>
    public static WorldReferenceData ReferenceFromSeed()
    {
        ReferenceSeedData seed = WorldReferenceSeed.Load();

        Dictionary<string, int> groupIds = seed.ResourceGroups.Select((group, index) => (group.Code, Id: index + 1))
            .ToDictionary(entry => entry.Code, entry => entry.Id, StringComparer.Ordinal);
        Dictionary<string, int> resourceIds = seed.Resources.Select((resource, index) => (resource.Code, Id: index + 1))
            .ToDictionary(entry => entry.Code, entry => entry.Id, StringComparer.Ordinal);
        Dictionary<string, int> typeIds = seed.ObjectTypes.Select((type, index) => (type.Code, Id: index + 1))
            .ToDictionary(entry => entry.Code, entry => entry.Id, StringComparer.Ordinal);

        int ruleId = 0;

        return new WorldReferenceData(
            seed.ResourceGroups.Select(group => new ReferenceResourceGroup(groupIds[group.Code], group.Code, group.AllowedOnPlanet)),
            seed.Resources.Select(resource => new ReferenceResource(resourceIds[resource.Code], resource.Code, groupIds[resource.Group])),
            seed.ObjectTypes.Select(type => new ReferenceObjectType(
                typeIds[type.Code],
                type.Code,
                type.ObjectClass,
                type.IsColonizable,
                type.GenerationWeight,
                type.MassMin,
                type.MassMax)),
            seed.SizeClasses.Select((size, index) => new ReferenceSizeClass(index + 1, size.Code, size.GenerationWeight, size.SizeMin, size.SizeMax)),
            seed.RuleGroups.Select((group, index) => new ReferenceRuleGroup(
                index + 1,
                typeIds[group.ObjectType],
                group.Code,
                group.MinPicks,
                group.MaxPicks,
                group.ExtraPickProbability,
                [
                    .. group.Rules.Select(rule => new ReferenceRule(
                        ++ruleId,
                        resourceIds[rule.Resource],
                        rule.Weight,
                        rule.CoefficientMin,
                        rule.CoefficientMax)),
                ])));
    }

    /// <summary>
    /// Builds the settings from the starting values, with some values replaced.
    /// </summary>
    /// <param name="overrides">The replaced values.</param>
    /// <returns>The settings.</returns>
    public static WorldSettings Settings(IReadOnlyDictionary<string, string>? overrides = null)
    {
        GameConfig config = new();
        Dictionary<string, ConfigParameter> values = WorldConfigKeys.Definitions
            .ToDictionary(definition => definition.Key, definition => new ConfigParameter(definition.Key, definition.DefaultValue, 1), StringComparer.Ordinal);

        foreach ((string key, string value) in overrides ?? new Dictionary<string, string>())
        {
            values[key] = new ConfigParameter(key, value, 1);
        }

        config.Replace(values);

        return WorldSettings.From(config);
    }

    /// <summary>
    /// Builds the whole generator input.
    /// </summary>
    /// <param name="overrides">The replaced parameter values.</param>
    /// <returns>The input.</returns>
    public static WorldGenerationConfig Config(IReadOnlyDictionary<string, string>? overrides = null) =>
        new(Settings(overrides), ReferenceFromSeed());

    /// <summary>
    /// Gets the identifiers of the resources of a group.
    /// </summary>
    /// <param name="reference">The reference data.</param>
    /// <param name="groupCode">The group code.</param>
    /// <returns>The resources.</returns>
    public static HashSet<int> ResourcesOfGroup(WorldReferenceData reference, string groupCode)
    {
        int groupId = reference.Groups.Single(group => group.Code == groupCode).Id;

        return [.. reference.Resources.Where(resource => resource.ResourceGroupId == groupId).Select(resource => resource.Id)];
    }

    /// <summary>
    /// Clears the database, writes world parameters and creates the world.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="overrides">The world parameters for the test; small sectors by default.</param>
    /// <returns>A task that completes when the world exists.</returns>
    public static async Task ResetWorldAsync(AetherisTestFactory factory, IReadOnlyDictionary<string, string>? overrides = null)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        await AetherisTestFactory.ResetDatabaseAsync(scope.ServiceProvider);

        foreach ((string key, string value) in overrides ?? SmallSectors)
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, value, reload: false);
        }

        await scope.ServiceProvider.GetRequiredService<ConfigReloader>().ReloadAsync();
        await scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The class names of the bodies.
    /// </summary>
    public static readonly string[] BodyClasses = [WorldObjectClasses.Planet, WorldObjectClasses.GasGiant];
}
