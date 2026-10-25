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
}
