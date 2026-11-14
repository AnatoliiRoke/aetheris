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
/// <param name="ModuleGroups">The types of the modules.</param>
/// <param name="Ammunition">The ammunition a kinetic weapon may fire. It is not fitted to a hull.</param>
public sealed record ShipCatalogDto(
    IReadOnlyList<ShipHullDto> Hulls,
    IReadOnlyList<ShipModuleDto> Modules,
    IReadOnlyList<ShipModuleGroupDto> ModuleGroups,
    IReadOnlyList<ShipAmmunitionDto> Ammunition);

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
/// <param name="Upkeep">How many credits the hull costs per hour. Zero until the directory fills the characteristic.</param>
/// <param name="Crew">How many people the hull needs. Zero until the directory fills the characteristic.</param>
/// <param name="AssemblySeconds">The assembly time of the hull without a technology, in game seconds. It is its own characteristic, not the time to manufacture the hull. Zero until the directory fills the value.</param>
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
    bool IsArchived,
    decimal Upkeep = 0m,
    decimal Crew = 0m,
    long AssemblySeconds = 0,
    decimal Signature = 0m,
    decimal LeashRadius = 0m,
    decimal DroneSpeed = 0m);

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
/// <param name="CargoCapacity">How much cargo the module adds to the hold, in tonnes.</param>
/// <param name="Armor">How much integrity the armor of the module adds.</param>
/// <param name="ShieldRegen">How fast the shield of the module recovers.</param>
/// <param name="ShieldRestore">How many shield units the module restores over time.</param>
/// <param name="Rate">How many shots the weapon makes in one round.</param>
/// <param name="LauncherSize">The size of the launcher, or zero when the weapon has none.</param>
/// <param name="Facts">The characteristics the plaque adds up. A characteristic the plaque already shows on its own row is not here.</param>
/// <param name="AmmunitionIds">The ammunition the weapon fires, by their identifier (task 00040). A weapon that fires ammunition has no damage of its own.</param>
/// <param name="Extraction">What the module takes and where, when it is a module of extraction (task 00055). A module without rows takes nothing.</param>
/// <param name="FlightOrder">The order of a flight plan the module opens for a fleet, or <see langword="null"/> (task 00058).</param>
/// <param name="ScanTarget">The kind of target detected by the module, or <see langword="null"/>.</param>
/// <param name="EngineKind">The class of the drive, or <see langword="null"/> when the module is not a drive (task 00072).</param>
/// <param name="Fuel">The fuel of the drive and the share of every resource in it (task 00072). A drive without rows burns nothing.</param>
/// <param name="CargoOnly">Whether the module is only a cargo: it is never fitted to a hull and is carried in the hold of a ship (task 00073).</param>
/// <param name="CargoOrder">The order of a flight plan the cargo opens, one of <see cref="PartCargoOrders"/>, or <see langword="null"/> (task 00073).</param>
/// <param name="Accuracy">The accuracy of a weapon against each group of hulls, by the identifier of the group (task 00062). A part that is not a weapon has none.</param>
/// <param name="Integrity">How much integrity the module has. The armor bonus stays in <see cref="ShipModuleDto.Armor"/>.</param>
/// <param name="SignatureCut">The share of the hull signature the module removes, from zero to one.</param>
/// <param name="DefaultPowerPriority">The shutdown order a new slot of the module starts with. A smaller number turns off later.</param>
/// <param name="Range">How far a shot of the module reaches. Zero when the module is not a weapon.</param>
/// <param name="FlightSpeed">How fast a shot of the module travels. Zero means the shot arrives in the same tick.</param>
/// <param name="EnergyBlock">The energy block of a shield, as stored. A number above one is a percent.</param>
/// <param name="KineticBlock">The kinetic block of a shield, as stored. A number above one is a percent.</param>
/// <param name="Magazine">How many rounds the weapon holds. Zero when it has no magazine.</param>
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
    bool IsCapsule = false,
    decimal CargoCapacity = 0m,
    decimal Armor = 0m,
    decimal ShieldRegen = 0m,
    decimal ShieldRestore = 0m,
    decimal Rate = 0m,
    decimal LauncherSize = 0m,
    IReadOnlyList<ShipModuleFactDto>? Facts = null,
    decimal Upkeep = 0m,
    IReadOnlyList<int>? AmmunitionIds = null,
    IReadOnlyList<ShipModuleExtractionDto>? Extraction = null,
    string? FlightOrder = null,
    string? ScanTarget = null,
    string? EngineKind = null,
    IReadOnlyList<ShipModuleFuelDto>? Fuel = null,
    bool CargoOnly = false,
    string? CargoOrder = null,
    IReadOnlyDictionary<int, double>? Accuracy = null,
    decimal Integrity = 0m,
    decimal SignatureCut = 0m,
    short? DefaultPowerPriority = null,
    decimal Range = 0m,
    decimal FlightSpeed = 0m,
    decimal EnergyBlock = 0m,
    decimal KineticBlock = 0m,
    decimal Magazine = 0m);

/// <summary>
/// One resource of the fuel of a drive and its share in it (task 00072).
/// </summary>
/// <param name="ResourceTypeId">The resource the drive burns.</param>
/// <param name="Share">The share of the resource in the fuel of the drive.</param>
public sealed record ShipModuleFuelDto(int ResourceTypeId, double Share);

/// <summary>
/// One way a module of extraction takes from one kind of place (task 00055).
/// </summary>
/// <param name="PlaceKind">The kind of the place, one of <see cref="ExtractionPlaceKinds"/>.</param>
/// <param name="Mode">The way the module takes, one of <see cref="ExtractionModes"/>.</param>
/// <param name="ResourceId">The resource the module takes, for the way «listed», or <see langword="null"/>.</param>
/// <param name="ResourceGroupId">The group of the resources the module takes, for the way «group evenly», or <see langword="null"/>.</param>
/// <param name="ReducesMass">Whether the module takes the mass it processes from the place.</param>
public sealed record ShipModuleExtractionDto(
    string PlaceKind,
    string Mode,
    int? ResourceId,
    int? ResourceGroupId,
    bool ReducesMass);

/// <summary>
/// One characteristic of a module that the plaque of a project adds up.
/// </summary>
/// <param name="Kind">The kind of the characteristic.</param>
/// <param name="NameKey">The localisation key of the name of the characteristic.</param>
/// <param name="UnitKey">The localisation key of the unit, or <see langword="null"/>.</param>
/// <param name="Amount">The amount the module adds.</param>
/// <param name="SortOrder">The order of the characteristic in the lists.</param>
public sealed record ShipModuleFactDto(
    string Kind,
    string NameKey,
    string? UnitKey,
    decimal Amount,
    int SortOrder);

/// <summary>
/// One round of ammunition a kinetic weapon may fire.
/// </summary>
/// <param name="Id">The ammunition.</param>
/// <param name="Damage">The damage of one round.</param>
/// <param name="DamageKind">The kind of the damage: the energy or the kinetic one.</param>
/// <param name="LauncherSize">The size of the launcher the round fits, or zero when it has none.</param>
/// <param name="FlightSpeed">How fast the round travels. Zero means it arrives in the same tick.</param>
/// <param name="Range">How far the round reaches. Zero when the range stays on the weapon.</param>
public sealed record ShipAmmunitionDto(
    int Id,
    decimal Damage,
    string? DamageKind,
    decimal LauncherSize,
    decimal FlightSpeed = 0m,
    decimal Range = 0m);

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
