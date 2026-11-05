using Aetheris.Core.Configuration;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// Builds the catalog of the ships: the hulls with their number of slots, their hold and their
/// role, and the modules with the hulls they fit and what they take and give (task 00038).
/// </summary>
/// <remarks>
/// The catalog is read from the tables of the production through the cache of the module, so it
/// never walks them row by row. The number of slots of a hull is the value of its characteristic
/// and, for a hull that has none yet, the value of the configuration.
/// </remarks>
public sealed class ShipCatalogService
{
    /// <summary>The kind of the characteristic that holds the number of slots of a hull.</summary>
    public const string SlotsCharacteristic = CharacteristicKinds.HullSlots;

    /// <summary>The kind of the characteristic that holds the capacity of the hold of a hull.</summary>
    public const string HoldCharacteristic = CharacteristicKinds.HullCapacity;

    /// <summary>The kind of the characteristic that holds the strength of a hull.</summary>
    public const string IntegrityCharacteristic = CharacteristicKinds.Integrity;

    /// <summary>The kind of the characteristic that holds the energy a module takes.</summary>
    public const string EnergyUseCharacteristic = CharacteristicKinds.EnergyUse;

    /// <summary>The kind of the characteristic that holds the power a reactor gives.</summary>
    public const string PowerCharacteristic = CharacteristicKinds.Power;

    /// <summary>The kind of the characteristic that holds the crew a module needs.</summary>
    public const string CrewCharacteristic = CharacteristicKinds.Crew;

    /// <summary>The kind of the characteristic that holds the extra slots the race gives a hull.</summary>
    public const string RaceBonusSlotsCharacteristic = CharacteristicKinds.HullRaceSlot;

    private readonly ProductionCatalogCache _catalog;
    private readonly ProductionDbContext _dbContext;
    private readonly IRequirementsModule _requirements;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipCatalogService"/> class.
    /// </summary>
    /// <param name="catalog">The cache of the catalog of the production.</param>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="config">The game configuration.</param>
    public ShipCatalogService(
        ProductionCatalogCache catalog,
        ProductionDbContext dbContext,
        IRequirementsModule requirements,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(config);

        _catalog = catalog;
        _dbContext = dbContext;
        _requirements = requirements;
        _config = config;
    }

    /// <summary>
    /// Reads the catalog of the ships.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog of the ships.</returns>
    public async Task<ShipCatalogDto> GetAsync(CancellationToken cancellationToken = default)
    {
        ProductionCatalog catalog = await _catalog.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);
        ProductionSettings settings = ProductionSettings.From(_config);

        // The characteristics are found by their kind, because the neutral code of a
        // characteristic is given by the server and differs from one database to another.
        Dictionary<string, int> characteristicIds = catalog.Characteristics
            .Where(characteristic => characteristic.Kind is not null)
            .GroupBy(characteristic => characteristic.Kind!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

        HashSet<int> hullGroupIds = [.. catalog.Groups
            .Where(group => group.IsHullGroup)
            .Select(group => group.Id)];

        // The role of a hull is the role of its group, so every hull of a group carries the
        // same role. The role is read from the parts of the group, which the catalog holds.
        Dictionary<int, string> groupRoles = [];

        foreach (PartTypeInfo part in catalog.Parts)
        {
            if (!hullGroupIds.Contains(part.PartGroupId) || groupRoles.ContainsKey(part.PartGroupId))
            {
                continue;
            }

            string? role = RoleOf(part, PartAttributeKeys.HullRole);

            if (role is not null)
            {
                groupRoles[part.PartGroupId] = role;
            }
        }

        List<ShipHullDto> hulls =
        [
            .. catalog.Parts
                .Where(part => hullGroupIds.Contains(part.PartGroupId))
                .Select(part => new ShipHullDto(
                    part.Id,
                    part.Code,
                    part.NameKey,
                    part.DescriptionKey,
                    part.ImageKey,
                    part.PartGroupId,
                    catalog.FindGroup(part.PartGroupId)?.Code ?? string.Empty,
                    groupRoles.GetValueOrDefault(part.PartGroupId, HullRoles.Civil),
                    SlotsOf(part, characteristicIds, settings.DefaultHullSlots),
                    RaceBonusSlotsOf(part, characteristicIds),
                    ValueOf(part, characteristicIds, HoldCharacteristic),
                    ValueOf(part, characteristicIds, IntegrityCharacteristic),
                    part.Weight,
                    part.IsArchived)),
        ];

        List<ShipModuleDto> modules =
        [
            .. catalog.Parts
                .Where(part => !hullGroupIds.Contains(part.PartGroupId))
                .Select(part => new ShipModuleDto(
                    part.Id,
                    part.Code,
                    part.NameKey,
                    part.DescriptionKey,
                    part.ImageKey,
                    part.PartGroupId,
                    catalog.FindGroup(part.PartGroupId)?.Code ?? string.Empty,
                    RoleOf(part, PartAttributeKeys.Role) ?? PartRoles.Universal,
                    RoleOf(part, PartAttributeKeys.Component),
                    part.CompatibleHullIds,
                    part.RaceIds,
                    ValueOf(part, characteristicIds, EnergyUseCharacteristic),
                    ValueOf(part, characteristicIds, PowerCharacteristic),
                    ValueOf(part, characteristicIds, CrewCharacteristic),
                    part.Weight,
                    [part.MarkLevel],
                    part.IsArchived)),
        ];

        return new ShipCatalogDto(hulls, modules);
    }

    /// <summary>
    /// Reads the number of slots of a hull: the value of its characteristic, or the value of the
    /// configuration for the hulls that have none yet.
    /// </summary>
    private static int SlotsOf(PartTypeInfo part, IReadOnlyDictionary<string, int> characteristicIds, int fallback)
    {
        if (!characteristicIds.TryGetValue(SlotsCharacteristic, out int slotsId))
        {
            return fallback;
        }

        return part.Characteristics.TryGetValue(slotsId, out PartCharacteristicInfo? value)
            ? Convert.ToInt32(value.NumberValue, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;
    }

    /// <summary>
    /// Reads the number of the extra slots the race gives a hull: the value of its
    /// characteristic, and zero for the hulls whose race gives nothing.
    /// </summary>
    private static int RaceBonusSlotsOf(PartTypeInfo part, IReadOnlyDictionary<string, int> characteristicIds)
    {
        if (!characteristicIds.TryGetValue(RaceBonusSlotsCharacteristic, out int slotsId))
        {
            return 0;
        }

        return part.Characteristics.TryGetValue(slotsId, out PartCharacteristicInfo? value)
            ? Convert.ToInt32(value.NumberValue, System.Globalization.CultureInfo.InvariantCulture)
            : 0;
    }

    /// <summary>
    /// Reads the value of a characteristic of a part.
    /// </summary>
    private static decimal ValueOf(PartTypeInfo part, IReadOnlyDictionary<string, int> characteristicIds, string characteristic)
    {
        if (!characteristicIds.TryGetValue(characteristic, out int characteristicId))
        {
            return 0m;
        }

        return part.Characteristics.TryGetValue(characteristicId, out PartCharacteristicInfo? value)
            ? value.NumberValue
            : 0m;
    }

    /// <summary>
    /// Reads the code of an attribute of a part.
    /// </summary>
    private static string? RoleOf(PartTypeInfo part, string key) =>
        part.Attributes.TryGetValue(key, out PartAttributeInfo? attribute)
            ? attribute.CodeValue
            : null;
}
