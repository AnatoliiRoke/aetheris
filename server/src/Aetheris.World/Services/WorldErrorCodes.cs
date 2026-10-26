namespace Aetheris.World.Services;

/// <summary>
/// The error codes of the world module, sent to the client as localisation keys.
/// </summary>
public static class WorldErrorCodes
{
    /// <summary>The world configuration is not consistent.</summary>
    public const string ConfigInvalid = "world_config_invalid";

    /// <summary>A sector with owners cannot be generated again.</summary>
    public const string SectorNotEmpty = "world_sector_not_empty";

    /// <summary>No free start body is left.</summary>
    public const string NoStartPlanet = "world_no_start_planet";

    /// <summary>The configured start body type does not exist.</summary>
    public const string StartTypeUnknown = "world_start_type_unknown";

    /// <summary>The player already has a civilization; the race does not change.</summary>
    public const string CivilizationExists = "world_civilization_exists";

    /// <summary>The chosen race does not exist.</summary>
    public const string RaceUnknown = "world_race_unknown";

    /// <summary>The name of the home body is too short.</summary>
    public const string HomeNameTooShort = "planet_name_too_short";

    /// <summary>The name of the home body is too long.</summary>
    public const string HomeNameTooLong = "planet_name_too_long";

    /// <summary>The name of the home body has characters that are not allowed.</summary>
    public const string HomeNameInvalidCharacters = "planet_name_invalid_characters";

    /// <summary>The name of the home body contains a restricted word.</summary>
    public const string HomeNameRestricted = "planet_name_restricted";

    /// <summary>Another generation run is queued or running; only one runs at a time.</summary>
    public const string GenerationRunning = "world_generation_running";

    /// <summary>The run asks for more sectors or star systems than one run may create.</summary>
    public const string GenerationTooLarge = "world_generation_too_large";

    /// <summary>The rule set of the run does not exist.</summary>
    public const string RuleSetUnknown = "world_rule_set_unknown";

    /// <summary>The run stopped because the server restarted during it.</summary>
    public const string GenerationInterrupted = "world_generation_interrupted";

    /// <summary>The record changed since the administrator opened it; nothing was written.</summary>
    public const string VersionConflict = "world_version_conflict";
}
