namespace Aetheris.Fleet.Api;

/// <summary>
/// A slot of a project as the client sends it (task 00038, section 3.2).
/// </summary>
/// <param name="SlotIndex">The number of the slot, from zero.</param>
/// <param name="PartTypeId">The part in the slot.</param>
/// <param name="MarkLevel">The mark of the part in the slot.</param>
/// <param name="HasRaceBonus">Whether the part in the slot carries the bonus of the race that made it.</param>
public sealed record ShipDesignSlotInput(
    int SlotIndex,
    int PartTypeId,
    int MarkLevel,
    bool HasRaceBonus);

/// <summary>
/// A project of a ship as the builder shows it (task 00038, section 3.3).
/// </summary>
/// <remarks>
/// The project carries the hull, the slots with their modules and the summary of what the
/// project gives: the weight, the energy, the crew and the hold. The names of the parts are
/// not here: they are in the catalog of the ships, so the client reads them once and never
/// holds a table of its own.
/// </remarks>
/// <param name="Id">The project.</param>
/// <param name="Name">The name of the project.</param>
/// <param name="PlanetId">The body the project was made on.</param>
/// <param name="HullPartTypeId">The hull of the project.</param>
/// <param name="HasRaceBonusSlot">Whether the project has the extra slot of the race.</param>
/// <param name="IsArchived">Whether the project is archived.</param>
/// <param name="CreatedAt">The moment of game time the project was created at.</param>
/// <param name="UpdatedAt">The moment of game time the project was changed at.</param>
/// <param name="Version">The version of the project; a change with an older version is refused.</param>
/// <param name="Slots">The slots of the project, in the order of their numbers.</param>
/// <param name="Summary">What the project gives.</param>
/// <param name="HasBuiltShips">Whether a finished ship of this project exists. Such a project is not changed or archived until that ship is taken apart.</param>
public sealed record ShipDesignDto(
    long Id,
    string Name,
    long PlanetId,
    int HullPartTypeId,
    bool HasRaceBonusSlot,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    IReadOnlyList<ShipDesignSlotDto> Slots,
    ShipDesignSummaryDto Summary,
    bool HasBuiltShips);

/// <summary>
/// A slot of a project of a ship (task 00038, section 3.2).
/// </summary>
/// <param name="SlotIndex">The number of the slot, from zero.</param>
/// <param name="PartTypeId">The part in the slot.</param>
/// <param name="MarkLevel">The mark of the part in the slot.</param>
/// <param name="HasRaceBonus">Whether the part in the slot carries the bonus of the race that made it.</param>
public sealed record ShipDesignSlotDto(
    int SlotIndex,
    int PartTypeId,
    int MarkLevel,
    bool HasRaceBonus);

/// <summary>
/// What a project of a ship gives (task 00038, section 3.3).
/// </summary>
/// <param name="TotalSlots">How many slots the hull has.</param>
/// <param name="UsedSlots">How many slots the project takes.</param>
/// <param name="FreeSlots">How many slots are left.</param>
/// <param name="Weight">The weight of the hull and of every module, in tonnes.</param>
/// <param name="EnergyUse">How much energy the modules take.</param>
/// <param name="EnergyPower">How much power the reactors give.</param>
/// <param name="Crew">How many people the modules need.</param>
/// <param name="HoldCapacity">How much the hold of the hull takes.</param>
/// <param name="Integrity">How strong the hull is.</param>
/// <param name="DrivePush">How much push the drives of the ship give.</param>
/// <param name="EnergyDamage">How much energy damage the weapons of the ship make in one round.</param>
/// <param name="KineticDamage">The greatest kinetic damage the weapons make in one round, from the ammunition they accept.</param>
/// <param name="ShieldCapacity">How much the shields of the ship take.</param>
/// <param name="HasDrive">Whether the project has a drive.</param>
/// <param name="HasReactor">Whether the project has a reactor.</param>
/// <param name="Armor">How much integrity the fitted armor adds.</param>
/// <param name="ShieldRegen">How fast the shields of the ship recover, with the shield restorers.</param>
/// <param name="KineticDamageMin">The least kinetic damage the weapons make in one round, from the ammunition they accept.</param>
/// <param name="KineticDamageMax">The greatest kinetic damage the weapons make in one round, from the ammunition they accept.</param>
/// <param name="Facts">The characteristics of the fitted modules that the plaque adds up. A zero amount is left out.</param>
/// <param name="DamageMin">The least damage all the weapons make in one round: the damage of the energy weapons and, for every weapon that fires ammunition, its least round (task 00040).</param>
/// <param name="DamageMax">The greatest damage all the weapons make in one round, counted the same way with the greatest round of every weapon (task 00040).</param>
public sealed record ShipDesignSummaryDto(
    int TotalSlots,
    int UsedSlots,
    int FreeSlots,
    decimal Weight,
    decimal EnergyUse,
    decimal EnergyPower,
    decimal Crew,
    decimal HoldCapacity,
    decimal Integrity,
    decimal DrivePush,
    decimal EnergyDamage,
    decimal KineticDamage,
    decimal ShieldCapacity,
    bool HasDrive,
    bool HasReactor,
    decimal Armor,
    decimal ShieldRegen,
    decimal KineticDamageMin,
    decimal KineticDamageMax,
    IReadOnlyList<ShipDesignFactDto> Facts,
    decimal DamageMin = 0m,
    decimal DamageMax = 0m);

/// <summary>
/// One summed characteristic of the fitted modules.
/// </summary>
/// <param name="Kind">The kind of the characteristic.</param>
/// <param name="NameKey">The localisation key of the name of the characteristic.</param>
/// <param name="UnitKey">The localisation key of the unit, or <see langword="null"/>.</param>
/// <param name="Amount">The amount the fitted modules add together.</param>
public sealed record ShipDesignFactDto(
    string Kind,
    string NameKey,
    string? UnitKey,
    decimal Amount);

/// <summary>
/// The catalog of the ships as the builder reads it (task 00038, section 3.2).
/// </summary>
/// <param name="Hulls">The hulls a player may build of.</param>
/// <param name="Modules">The modules that go into the slots of a hull.</param>
/// <param name="ModuleGroups">The types of the modules, for the filters of the picker.</param>
/// <param name="DesignLimit">How many projects the player may keep.</param>
/// <param name="DesignCount">How many projects the player has.</param>
/// <param name="Ammunition">The ammunition a kinetic weapon may fire. It is not offered as a module.</param>
public sealed record ShipCatalogViewDto(
    IReadOnlyList<ShipHullViewDto> Hulls,
    IReadOnlyList<ShipModuleViewDto> Modules,
    IReadOnlyList<ShipModuleGroupViewDto> ModuleGroups,
    int DesignLimit,
    int DesignCount,
    IReadOnlyList<ShipAmmunitionViewDto> Ammunition);

/// <summary>
/// A hull of the catalog of the ships for the builder (task 00038, section 3.3).
/// </summary>
/// <param name="Id">The hull.</param>
/// <param name="Code">The neutral code of the hull.</param>
/// <param name="NameKey">The localisation key of the name of the hull.</param>
/// <param name="DescriptionKey">The localisation key of the description of the hull.</param>
/// <param name="ImageKey">The key of the picture of the hull, or <see langword="null"/>.</param>
/// <param name="PartGroupCode">The neutral code of the group of the hull.</param>
/// <param name="PartGroupNameKey">The localisation key of the name of the group of the hull: the class of the hull.</param>
/// <param name="Role">The role of the hull: what it carries.</param>
/// <param name="Slots">How many slots the hull has.</param>
/// <param name="HoldCapacity">How much the hold of the hull takes.</param>
/// <param name="Integrity">How strong the hull is.</param>
/// <param name="Weight">The weight of the hull in tonnes.</param>
/// <param name="RaceBonusSlots">How many extra slots the race gives the hull.</param>
/// <param name="Crew">How many people the hull needs. The starting data sets this to the number of slots.</param>
public sealed record ShipHullViewDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    string PartGroupCode,
    string PartGroupNameKey,
    string Role,
    int Slots,
    decimal HoldCapacity,
    decimal Integrity,
    decimal Weight,
    int RaceBonusSlots,
    decimal Crew);

/// <summary>
/// A module of the catalog of the ships for the builder (task 00038, section 3.3).
/// </summary>
/// <param name="Id">The module.</param>
/// <param name="Code">The neutral code of the module.</param>
/// <param name="NameKey">The localisation key of the name of the module.</param>
/// <param name="DescriptionKey">The localisation key of the description of the module.</param>
/// <param name="ImageKey">The key of the picture of the module, or <see langword="null"/>.</param>
/// <param name="PartGroupCode">The neutral code of the group of the module.</param>
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
/// <param name="IsAmmunition">Whether the module is ammunition of a weapon: it is loaded, not fitted.</param>
/// <param name="IsCapsule">Whether the module is a capsule of inhabitants: it is used on a body, not fitted.</param>
/// <param name="CargoCapacity">How much cargo the module adds to the hold, in tonnes.</param>
/// <param name="Armor">How much integrity the armor of the module adds.</param>
/// <param name="ShieldRegen">How fast the shield of the module recovers.</param>
/// <param name="ShieldRestore">How many shield units the module restores over time.</param>
/// <param name="Rate">How many shots the weapon makes in one round.</param>
/// <param name="LauncherSize">The size of the launcher, or zero when the weapon has none.</param>
/// <param name="Facts">The characteristics the plaque adds up.</param>
/// <param name="AmmunitionIds">The ammunition the weapon fires; a weapon that fires ammunition has no damage of its own (task 00040).</param>
/// <param name="ExtractionPlaceKinds">The kinds of the places the module takes from (task 00055): the classes of the objects of the map. A module that takes from none is not a module of extraction.</param>
/// <param name="FlightOrder">The flight plan order opened by the module, if any.</param>
/// <param name="ScanTarget">The kind of target detected by the module, if any.</param>
public sealed record ShipModuleViewDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
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
    bool IsAmmunition,
    bool IsCapsule,
    decimal CargoCapacity,
    decimal Armor,
    decimal ShieldRegen,
    decimal ShieldRestore,
    decimal Rate,
    decimal LauncherSize,
    IReadOnlyList<ShipModuleFactViewDto> Facts,
    IReadOnlyList<int> AmmunitionIds,
    IReadOnlyList<string> ExtractionPlaceKinds,
    string? FlightOrder = null,
    string? ScanTarget = null);

/// <summary>
/// One characteristic of a module that the plaque of a project adds up.
/// </summary>
/// <param name="Kind">The kind of the characteristic.</param>
/// <param name="NameKey">The localisation key of the name of the characteristic.</param>
/// <param name="UnitKey">The localisation key of the unit, or <see langword="null"/>.</param>
/// <param name="Amount">The amount the module adds.</param>
/// <param name="SortOrder">The order of the characteristic in the lists.</param>
public sealed record ShipModuleFactViewDto(
    string Kind,
    string NameKey,
    string? UnitKey,
    decimal Amount,
    int SortOrder);

/// <summary>
/// One round of ammunition the builder uses to count the kinetic damage of a project.
/// </summary>
/// <param name="Id">The ammunition.</param>
/// <param name="Damage">The damage of one round.</param>
/// <param name="DamageKind">The kind of the damage: the energy or the kinetic one.</param>
/// <param name="LauncherSize">The size of the launcher the round fits, or zero when it has none.</param>
public sealed record ShipAmmunitionViewDto(
    int Id,
    decimal Damage,
    string? DamageKind,
    decimal LauncherSize);

/// <summary>
/// A type of the modules of the catalog: the group of a module, with the name of the group and
/// how many modules of it the catalog holds (task 00038, the filters of the module picker).
/// </summary>
/// <param name="Code">The neutral code of the group.</param>
/// <param name="NameKey">The localisation key of the name of the group.</param>
/// <param name="Count">How many modules of the group the catalog holds.</param>
public sealed record ShipModuleGroupViewDto(
    string Code,
    string NameKey,
    int Count);
