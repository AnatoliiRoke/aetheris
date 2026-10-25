namespace Aetheris.World.Data.Entities;

/// <summary>
/// The classes of celestial objects.
/// </summary>
/// <remarks>
/// A class is the structure the code knows: a body has an owner and deposits, a field has a
/// mass that shrinks, a nebula has a radius. Everything a designer tunes (types, names,
/// weights, resources) is a row of the reference tables.
/// </remarks>
public static class WorldObjectClasses
{
    /// <summary>The star of a system.</summary>
    public const string Star = "star";

    /// <summary>A body that a player may own.</summary>
    public const string Planet = "planet";

    /// <summary>A body that holds stations and is never owned.</summary>
    public const string GasGiant = "gas_giant";

    /// <summary>An asteroid field.</summary>
    public const string AsteroidField = "asteroid_field";

    /// <summary>A nebula.</summary>
    public const string Nebula = "nebula";
}

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
