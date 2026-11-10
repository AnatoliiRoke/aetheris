namespace Aetheris.World.Data.Entities;

/// <summary>
/// The actions written to <c>world_generation_log</c>.
/// </summary>
public static class WorldGenerationActions
{
    /// <summary>The reference tables were filled with the starting data.</summary>
    public const string ReferenceSeeded = "reference_seeded";

    /// <summary>The galaxy was created.</summary>
    public const string GalaxyCreated = "galaxy_created";

    /// <summary>A sector was generated.</summary>
    public const string SectorGenerated = "sector_generated";

    /// <summary>An empty sector was generated again.</summary>
    public const string SectorRegenerated = "sector_regenerated";

    /// <summary>A field was depleted and disappeared.</summary>
    public const string FieldDepleted = "field_depleted";

    /// <summary>New fields appeared in a sector.</summary>
    public const string FieldsSpawned = "fields_spawned";
}
