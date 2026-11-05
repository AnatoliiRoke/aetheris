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
    ShipDesignSummaryDto Summary);

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
/// <param name="HasDrive">Whether the project has a drive.</param>
/// <param name="HasReactor">Whether the project has a reactor.</param>
/// <param name="HasLifeSupport">Whether the project has life support.</param>
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
    bool HasDrive,
    bool HasReactor,
    bool HasLifeSupport);

/// <summary>
/// The catalog of the ships as the builder reads it (task 00038, section 3.2).
/// </summary>
/// <param name="Hulls">The hulls a player may build of.</param>
/// <param name="Modules">The modules that go into the slots of a hull.</param>
/// <param name="DesignLimit">How many projects the player may keep.</param>
/// <param name="DesignCount">How many projects the player has.</param>
public sealed record ShipCatalogViewDto(
    IReadOnlyList<ShipHullViewDto> Hulls,
    IReadOnlyList<ShipModuleViewDto> Modules,
    int DesignLimit,
    int DesignCount);

/// <summary>
/// A hull of the catalog of the ships for the builder (task 00038, section 3.3).
/// </summary>
/// <param name="Id">The hull.</param>
/// <param name="Code">The neutral code of the hull.</param>
/// <param name="NameKey">The localisation key of the name of the hull.</param>
/// <param name="DescriptionKey">The localisation key of the description of the hull.</param>
/// <param name="ImageKey">The key of the picture of the hull, or <see langword="null"/>.</param>
/// <param name="PartGroupCode">The neutral code of the group of the hull.</param>
/// <param name="Role">The role of the hull: what it carries.</param>
/// <param name="Slots">How many slots the hull has.</param>
/// <param name="HoldCapacity">How much the hold of the hull takes.</param>
/// <param name="Integrity">How strong the hull is.</param>
/// <param name="Weight">The weight of the hull in tonnes.</param>
/// <param name="AllowsRaceBonusSlot">Whether the race may give the hull one more slot.</param>
/// <param name="Stock">How many of the hull the body has on its stock.</param>
public sealed record ShipHullViewDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    string PartGroupCode,
    string Role,
    int Slots,
    decimal HoldCapacity,
    decimal Integrity,
    decimal Weight,
    bool AllowsRaceBonusSlot,
    long Stock);

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
/// <param name="Weight">The weight of one module in tonnes.</param>
/// <param name="MarkLevels">The marks of the module that the catalog holds.</param>
/// <param name="Stock">How many of the module the body has on its stock.</param>
public sealed record ShipModuleViewDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    string PartGroupCode,
    string Role,
    string? Component,
    IReadOnlyList<int> CompatibleHullIds,
    IReadOnlyList<int> RaceIds,
    decimal EnergyUse,
    decimal Power,
    decimal Crew,
    decimal Weight,
    IReadOnlyList<int> MarkLevels,
    long Stock);
