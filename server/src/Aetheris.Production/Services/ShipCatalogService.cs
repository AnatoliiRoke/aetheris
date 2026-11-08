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

    /// <summary>The kind of the characteristic that holds the push a drive gives.</summary>
    public const string DrivePushCharacteristic = CharacteristicKinds.DrivePush;

    /// <summary>The kind of the characteristic that holds the damage a weapon makes.</summary>
    public const string DamageCharacteristic = CharacteristicKinds.Damage;

    /// <summary>The kind of the characteristic that holds the capacity of a shield.</summary>
    public const string ShieldCapacityCharacteristic = CharacteristicKinds.ShieldCapacity;

    /// <summary>The kind of the characteristic that holds the cargo a module adds to the hold.</summary>
    public const string CargoCharacteristic = CharacteristicKinds.Cargo;

    /// <summary>The kind of the characteristic that holds how fast a shield recovers.</summary>
    public const string ShieldRegenCharacteristic = CharacteristicKinds.ShieldRegen;

    /// <summary>The kind of the characteristic that holds how many shield units a module restores.</summary>
    public const string ShieldRestoreCharacteristic = CharacteristicKinds.ShieldRestore;

    /// <summary>The kind of the characteristic that holds the shots a weapon makes in one round.</summary>
    public const string RateCharacteristic = CharacteristicKinds.Rate;

    /// <summary>The kind of the characteristic that holds the size of a launcher.</summary>
    public const string LauncherSizeCharacteristic = CharacteristicKinds.LauncherSize;

    /// <summary>The kind of the characteristic that holds the extra slots the race gives a hull.</summary>
    public const string RaceBonusSlotsCharacteristic = CharacteristicKinds.HullRaceSlot;

    /// <summary>The kind of the characteristic that holds the upkeep of a hull or a module, in credits per hour.</summary>
    public const string UpkeepCharacteristic = CharacteristicKinds.Upkeep;

    /// <summary>The kind of the characteristic that holds the assembly time of a hull, in game seconds.</summary>
    public const string AssemblyTimeCharacteristic = CharacteristicKinds.AssemblyTime;

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

        // The hulls a ship may be built of: the combat and the transport ones. The drones are
        // carried in the hold and the structures are not ships (game-design/05, section 3.1б).
        HashSet<int> shipHullIds = [.. catalog.Parts
            .Where(part => hullGroupIds.Contains(part.PartGroupId))
            .Where(part => IsShipHullRole(groupRoles.GetValueOrDefault(part.PartGroupId)))
            .Select(part => part.Id)];

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
                    catalog.FindGroup(part.PartGroupId)?.NameKey ?? string.Empty,
                    groupRoles.GetValueOrDefault(part.PartGroupId, HullRoles.Civil),
                    SlotsOf(part, characteristicIds, settings.DefaultHullSlots),
                    RaceBonusSlotsOf(part, characteristicIds),
                    ValueOf(part, characteristicIds, HoldCharacteristic),
                    ValueOf(part, characteristicIds, IntegrityCharacteristic),
                    part.Weight,
                    part.IsArchived,
                    ValueOf(part, characteristicIds, UpkeepCharacteristic),
                    ValueOf(part, characteristicIds, CrewCharacteristic),
                    AssemblySecondsOf(part, characteristicIds))),
        ];

        List<ShipModuleDto> modules =
        [
            .. catalog.Parts
                .Where(part => !hullGroupIds.Contains(part.PartGroupId))
                .Where(part => !IsNotAShipModule(part))
                .Where(part => part.CompatibleHullIds.Any(id => shipHullIds.Contains(id)))
                .Select(part => new ShipModuleDto(
                    part.Id,
                    part.Code,
                    part.NameKey,
                    part.DescriptionKey,
                    part.ImageKey,
                    part.PartGroupId,
                    catalog.FindGroup(part.PartGroupId)?.Code ?? string.Empty,
                    catalog.FindGroup(part.PartGroupId)?.NameKey ?? string.Empty,
                    RoleOf(part, PartAttributeKeys.Role) ?? PartRoles.Universal,
                    RoleOf(part, PartAttributeKeys.Component),
                    part.CompatibleHullIds,
                    part.RaceIds,
                    ValueOf(part, characteristicIds, EnergyUseCharacteristic),
                    ValueOf(part, characteristicIds, PowerCharacteristic),
                    ValueOf(part, characteristicIds, CrewCharacteristic),
                    ValueOf(part, characteristicIds, DrivePushCharacteristic),
                    ValueOf(part, characteristicIds, DamageCharacteristic),
                    RoleOf(part, PartAttributeKeys.DamageKind),
                    ValueOf(part, characteristicIds, ShieldCapacityCharacteristic),
                    part.Weight,
                    [part.MarkLevel],
                    part.IsArchived,
                    IsAmmunition(part),
                    IsCapsule(part),
                    ValueOf(part, characteristicIds, CargoCharacteristic),
                    ValueOf(part, characteristicIds, IntegrityCharacteristic),
                    ValueOf(part, characteristicIds, ShieldRegenCharacteristic),
                    ValueOf(part, characteristicIds, ShieldRestoreCharacteristic),
                    ValueOf(part, characteristicIds, RateCharacteristic),
                    ValueOf(part, characteristicIds, LauncherSizeCharacteristic),
                    FactsOf(part, catalog),
                    ValueOf(part, characteristicIds, UpkeepCharacteristic))),
        ];

        // The ammunition is not a module of a ship: the constructor does not offer it, and the
        // kinetic damage of a project is the damage of the rounds the fitted weapon accepts.
        List<ShipAmmunitionDto> ammunition =
        [
            .. catalog.Parts
                .Where(part => IsAmmunition(part) && !part.IsArchived)
                .Select(part => new ShipAmmunitionDto(
                    part.Id,
                    ValueOf(part, characteristicIds, DamageCharacteristic),
                    RoleOf(part, PartAttributeKeys.DamageKind),
                    ValueOf(part, characteristicIds, LauncherSizeCharacteristic))),
        ];

        // The types of the modules: the groups of the parts are the types, so the picker of the
        // modules filters by them (task 00038, the filters of the module picker). The counts are
        // of the modules of the catalog, so the reader of the catalog counts the modules of
        // every type again for the ones it offers.
        List<ShipModuleGroupDto> moduleGroups =
        [
            .. modules
                .Where(module => !string.IsNullOrEmpty(module.PartGroupCode))
                .GroupBy(module => module.PartGroupCode, StringComparer.Ordinal)
                .Select(group => new ShipModuleGroupDto(
                    group.Key,
                    group.First().PartGroupNameKey,
                    group.Count()))
                .OrderBy(group => group.Code, StringComparer.Ordinal),
        ];

        return new ShipCatalogDto(hulls, modules, moduleGroups, ammunition);
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
    /// Reads the assembly time of a hull. A hull without the characteristic has no time yet.
    /// </summary>
    private static long AssemblySecondsOf(PartTypeInfo part, IReadOnlyDictionary<string, int> characteristicIds)
    {
        decimal seconds = ValueOf(part, characteristicIds, AssemblyTimeCharacteristic);

        return seconds < 1m ? 0 : (long)decimal.Ceiling(seconds);
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
    /// Reads the characteristics the plaque of a project adds up.
    /// </summary>
    /// <param name="part">The module.</param>
    /// <param name="catalog">The catalog of the production.</param>
    /// <returns>The characteristics, in the order of the lists. A zero amount is left out.</returns>
    private static List<ShipModuleFactDto> FactsOf(PartTypeInfo part, ProductionCatalog catalog)
    {
        List<ShipModuleFactDto> facts = [];

        foreach (PartCharacteristicInfo entry in part.Characteristics.Values)
        {
            CharacteristicInfo? characteristic = catalog.FindCharacteristic(entry.CharacteristicId);

            if (characteristic?.Kind is not string kind
                || !CharacteristicKinds.IsPlaqueFact(kind)
                || entry.NumberValue == 0m)
            {
                continue;
            }

            facts.Add(new ShipModuleFactDto(
                kind,
                characteristic.NameKey,
                characteristic.UnitKey,
                entry.NumberValue,
                characteristic.SortOrder));
        }

        facts.Sort(static (left, right) => left.SortOrder.CompareTo(right.SortOrder));

        return facts;
    }

    /// <summary>
    /// Reads the code of an attribute of a part.
    /// </summary>
    private static string? RoleOf(PartTypeInfo part, string key) =>
        part.Attributes.TryGetValue(key, out PartAttributeInfo? attribute)
            ? attribute.CodeValue
            : null;

    /// <summary>
    /// Tells whether the part is marked as not a module of a ship: the ammunition and the
    /// capsules of inhabitants are not fitted to a hull, so they never show in the constructor
    /// (the decision of the author, task 00038).
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns><see langword="true"/> when the part is not a module of a ship.</returns>
    private static bool IsNotAShipModule(PartTypeInfo part) =>
        part.Attributes.ContainsKey(PartAttributeKeys.NotAShipModule);

    /// <summary>
    /// Tells whether a hull of the role may carry a ship: a combat hull and a transport one.
    /// A drone is carried in the hold and a structure is not a ship (game-design/05, section
    /// 3.1б, and the decision of the author in task 00042).
    /// </summary>
    /// <param name="role">The role of the hull, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> when a ship may be built of the hull.</returns>
    private static bool IsShipHullRole(string? role) =>
        string.Equals(role, HullRoles.Combat, StringComparison.Ordinal)
        || string.Equals(role, HullRoles.Transport, StringComparison.Ordinal);

    /// <summary>
    /// Tells whether the part is ammunition of a weapon: it is loaded into the weapon, not
    /// fitted to a hull.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns><see langword="true"/> when the part is ammunition.</returns>
    private static bool IsAmmunition(PartTypeInfo part) =>
        part.Attributes.ContainsKey(PartAttributeKeys.NotAShipModule)
        && !part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule);

    /// <summary>
    /// Tells whether the part is a capsule of inhabitants: it is used on a body, not fitted to
    /// a hull.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns><see langword="true"/> when the part is a capsule.</returns>
    private static bool IsCapsule(PartTypeInfo part) =>
        part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule);
}
