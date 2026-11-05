namespace Aetheris.Production.Api;

/// <summary>
/// The catalog of the ships: the hulls a player may build and the modules that go into them
/// (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// The catalog is the data of the production: the hulls with their number of slots, their hold
/// and their role, and the modules with the hulls they fit, their role and what they take and
/// give. The constructor of the ships reads it and never the tables of the module.
/// </remarks>
/// <param name="Hulls">The hulls of the catalog.</param>
/// <param name="Modules">The modules of the catalog.</param>
public sealed record ShipCatalogDto(
    IReadOnlyList<ShipHullDto> Hulls,
    IReadOnlyList<ShipModuleDto> Modules,
    IReadOnlyList<ShipModuleGroupDto> ModuleGroups);

/// <summary>
/// A hull of the catalog of the ships.
/// </summary>
/// <param name="Id">The hull.</param>
/// <param name="Code">The neutral code of the hull.</param>
/// <param name="NameKey">The localisation key of the name of the hull.</param>
/// <param name="DescriptionKey">The localisation key of the description of the hull.</param>
/// <param name="ImageKey">The key of the picture of the hull, or <see langword="null"/>.</param>
/// <param name="PartGroupId">The group of the hull.</param>
/// <param name="PartGroupCode">The neutral code of the group of the hull.</param>
/// <param name="PartGroupNameKey">The localisation key of the name of the group of the hull: the class of the hull.</param>
/// <param name="Role">The role of the hull: what it carries.</param>
/// <param name="Slots">How many slots the hull has.</param>
/// <param name="RaceBonusSlots">How many extra slots the race of the player gives the hull.</param>
/// <param name="HoldCapacity">How much the hold of the hull takes.</param>
/// <param name="Integrity">How strong the hull is.</param>
/// <param name="Weight">The weight of the hull in tonnes.</param>
/// <param name="IsArchived">Whether the hull is archived: it is not offered for new designs (task 00023).</param>
public sealed record ShipHullDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    int PartGroupId,
    string PartGroupCode,
    string PartGroupNameKey,
    string Role,
    int Slots,
    int RaceBonusSlots,
    decimal HoldCapacity,
    decimal Integrity,
    decimal Weight,
    bool IsArchived);

/// <summary>
/// A module of the catalog of the ships.
/// </summary>
/// <param name="Id">The module.</param>
/// <param name="Code">The neutral code of the module.</param>
/// <param name="NameKey">The localisation key of the name of the module.</param>
/// <param name="DescriptionKey">The localisation key of the description of the module.</param>
/// <param name="ImageKey">The key of the picture of the module, or <see langword="null"/>.</param>
/// <param name="PartGroupId">The group of the module: its type.</param>
/// <param name="PartGroupCode">The neutral code of the group of the module.</param>
/// <param name="PartGroupNameKey">The localisation key of the name of the group of the module: its type.</param>
/// <param name="Role">The role of the module: what it may be put on.</param>
/// <param name="Component">The kind of the component the module is: the drive, the reactor or the life support, or <see langword="null"/> when it is an ordinary module.</param>
/// <param name="CompatibleHullIds">The hulls the module fits.</param>
/// <param name="RaceIds">The races the module belongs to.</param>
/// <param name="EnergyUse">How much energy the module takes, or gives for a generator.</param>
/// <param name="Power">How much power the module gives, for a reactor.</param>
/// <param name="Crew">How many people the module needs.</param>
/// <param name="DrivePush">How much push the module gives, for a drive.</param>
/// <param name="Damage">How much damage the module makes, for a weapon.</param>
/// <param name="DamageKind">The kind of the damage the module makes: the energy or the kinetic one.</param>
/// <param name="ShieldCapacity">How much the shield of the module takes, for a shield.</param>
/// <param name="Weight">The weight of one module in tonnes.</param>
/// <param name="MarkLevels">The marks of the module that the catalog holds.</param>
/// <param name="IsArchived">Whether the module is archived: it is not offered for new designs (task 00023).</param>
/// <param name="IsAmmunition">Whether the module is ammunition of a weapon: it is loaded, not fitted.</param>
/// <param name="IsCapsule">Whether the module is a capsule of inhabitants: it is used on a body, not fitted.</param>
public sealed record ShipModuleDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    int PartGroupId,
    string PartGroupCode,
    string PartGroupNameKey,
    string Role,
    string? Component,
    IReadOnlyList<int> CompatibleHullIds,
    IReadOnlyList<int> RaceIds,
    decimal EnergyUse,
    decimal Power,
    decimal Crew,
    decimal DrivePush,
    decimal Damage,
    string? DamageKind,
    decimal ShieldCapacity,
    decimal Weight,
    IReadOnlyList<int> MarkLevels,
    bool IsArchived,
    bool IsAmmunition = false,
    bool IsCapsule = false);

/// <summary>
/// A group of the modules of the catalog: the type of a module, with the name of the group and
/// how many modules of it the catalog holds (task 00038, the filters of the module picker).
/// </summary>
/// <param name="Code">The neutral code of the group.</param>
/// <param name="NameKey">The localisation key of the name of the group.</param>
/// <param name="Count">How many modules of the group the catalog holds.</param>
public sealed record ShipModuleGroupDto(
    string Code,
    string NameKey,
    int Count);
