using System.Text.Json;
using Aetheris.World.Geometry;

namespace Aetheris.World.Api;

/// <summary>
/// The JSON settings of the world API: snake_case names, so a list of references reads
/// <c>deposit_ids</c> (documentation/tech/08, rule 3).
/// </summary>
public static class WorldJson
{
    /// <summary>
    /// Gets the serializer settings.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

/// <summary>What the first start created.</summary>
/// <param name="ReferenceSeeded">Whether the reference tables were filled.</param>
/// <param name="GalaxyCreated">Whether the galaxy was created.</param>
/// <param name="CreatedSectorIds">The sectors that were created.</param>
public sealed record WorldBootstrapResult(bool ReferenceSeeded, bool GalaxyCreated, IReadOnlyList<int> CreatedSectorIds);

/// <summary>A generated sector.</summary>
/// <param name="Id">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="Seed">The seed.</param>
/// <param name="LayoutIndex">The index on the layout spiral.</param>
/// <param name="IsNewbie">Whether newcomers start there.</param>
/// <param name="Origin">The origin in the galaxy.</param>
/// <param name="SystemCount">The number of star systems.</param>
public sealed record SectorSummary(int Id, string Name, long Seed, int LayoutIndex, bool IsNewbie, Point3 Origin, int SystemCount);

/// <summary>The statistics of a sector for the admin panel.</summary>
/// <param name="SectorId">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="Seed">The seed.</param>
/// <param name="IsNewbie">Whether newcomers start there.</param>
/// <param name="SystemCount">The star systems.</param>
/// <param name="ObjectCountsByType">The celestial objects by type code.</param>
/// <param name="BodyCount">The bodies on orbits.</param>
/// <param name="FreeColonizableCount">The bodies that a player may still take.</param>
/// <param name="OwnedCount">The bodies with an owner.</param>
/// <param name="FieldCount">The fields that exist.</param>
/// <param name="FieldMass">The total mass of those fields.</param>
/// <param name="NebulaCount">The nebulae.</param>
public sealed record SectorStatistics(
    int SectorId,
    string Name,
    long Seed,
    bool IsNewbie,
    int SystemCount,
    IReadOnlyDictionary<string, int> ObjectCountsByType,
    int BodyCount,
    int FreeColonizableCount,
    int OwnedCount,
    int FieldCount,
    long FieldMass,
    int NebulaCount);

/// <summary>The start body of a player.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="CelestialObjectId">Its celestial object.</param>
/// <param name="SystemId">The star system.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="AlreadyAllocated">Whether the player had it before this call.</param>
public sealed record StartPlanetResult(long PlanetId, long CelestialObjectId, long SystemId, int SectorId, bool AlreadyAllocated);

/// <summary>A sector as the client sees it.</summary>
/// <param name="Id">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x coordinate of the origin.</param>
/// <param name="Y">The y coordinate of the origin.</param>
/// <param name="Z">The z coordinate of the origin.</param>
/// <param name="IsNewbie">Whether newcomers start there.</param>
public sealed record SectorDto(int Id, string Name, double X, double Y, double Z, bool IsNewbie);

/// <summary>A star system as the client sees it.</summary>
/// <param name="Id">The system.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="Number">The number within the sector.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x coordinate within the sector.</param>
/// <param name="Y">The y coordinate within the sector.</param>
/// <param name="Z">The z coordinate within the sector.</param>
/// <param name="InNebula">Whether the system lies inside a nebula.</param>
public sealed record SystemDto(long Id, int SectorId, int Number, string Name, double X, double Y, double Z, bool InNebula);

/// <summary>A celestial object as the client sees it.</summary>
/// <param name="Id">The celestial object.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SystemId">The star system, if any.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="ObjectClass">The class of the type.</param>
/// <param name="Orbit">The orbit, if any.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x coordinate within the sector.</param>
/// <param name="Y">The y coordinate within the sector.</param>
/// <param name="Z">The z coordinate within the sector.</param>
/// <param name="PlanetId">The body row, for a planet or a gas giant.</param>
/// <param name="SizeClassId">The size class of a body.</param>
/// <param name="Size">The size of a body.</param>
/// <param name="OwnerPlayerId">The owner of a body.</param>
/// <param name="StationLimit">The largest number of stations, for a gas giant.</param>
/// <param name="DepositIds">The deposits of a body.</param>
/// <param name="AsteroidFieldId">The field row, for a field.</param>
/// <param name="Mass">The mass that is left in a field.</param>
/// <param name="NebulaId">The nebula row, for a nebula.</param>
/// <param name="Radius">The radius of a nebula.</param>
/// <param name="CompositionIds">The composition of a field or a nebula.</param>
public sealed record ObjectDto(
    long Id,
    int SectorId,
    long? SystemId,
    int ObjectTypeId,
    string ObjectClass,
    int? Orbit,
    string Name,
    double X,
    double Y,
    double Z,
    long? PlanetId,
    int? SizeClassId,
    int? Size,
    string? OwnerPlayerId,
    int? StationLimit,
    IReadOnlyList<long> DepositIds,
    long? AsteroidFieldId,
    long? Mass,
    long? NebulaId,
    double? Radius,
    IReadOnlyList<long> CompositionIds);

/// <summary>A deposit of a body.</summary>
/// <param name="Id">The deposit.</param>
/// <param name="PlanetId">The body.</param>
/// <param name="ResourceId">The resource.</param>
/// <param name="Coefficient">The coefficient.</param>
public sealed record DepositDto(long Id, long PlanetId, int ResourceId, decimal Coefficient);

/// <summary>A share of the composition of a field or a nebula.</summary>
/// <param name="Id">The share.</param>
/// <param name="AsteroidFieldId">The field, if the share belongs to one.</param>
/// <param name="NebulaId">The nebula, if the share belongs to one.</param>
/// <param name="ResourceId">The resource.</param>
/// <param name="Coefficient">The share, from 0 to 1.</param>
public sealed record CompositionDto(long Id, long? AsteroidFieldId, long? NebulaId, int ResourceId, decimal Coefficient);

/// <summary>The content of a star system.</summary>
/// <param name="System">The system.</param>
/// <param name="Objects">The visible objects.</param>
/// <param name="Deposits">The deposits the objects name.</param>
/// <param name="Compositions">The composition shares the objects name.</param>
public sealed record SystemContentDto(
    SystemDto System,
    IReadOnlyList<ObjectDto> Objects,
    IReadOnlyList<DepositDto> Deposits,
    IReadOnlyList<CompositionDto> Compositions);

/// <summary>A free body that a player may take.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="CelestialObjectId">Its celestial object.</param>
/// <param name="SystemId">The star system.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="Orbit">The orbit.</param>
/// <param name="Name">The name.</param>
/// <param name="SizeClassId">The size class.</param>
/// <param name="Size">The size.</param>
/// <param name="IsTerraformed">Whether the body is terraformed already; it stays free for everyone (task 00014, section 9).</param>
public sealed record FreePlanetDto(
    long PlanetId,
    long CelestialObjectId,
    long SystemId,
    int SectorId,
    int ObjectTypeId,
    int? Orbit,
    string Name,
    int SizeClassId,
    int Size,
    bool IsTerraformed = false);

/// <summary>A row of a reference table, for the client to name the identifiers.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The neutral code, for the journals and debugging only.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="GroupCode">The class code of an object type, if the table has one.</param>
/// <param name="GroupId">The resource group of a resource type, if the table has one.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/> for the neutral placeholder.</param>
public sealed record ReferenceDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? GroupCode,
    int? GroupId,
    string? ImageKey);

/// <summary>The reference tables of the world.</summary>
/// <param name="ResourceGroups">The resource groups.</param>
/// <param name="Resources">The resource types.</param>
/// <param name="ObjectTypes">The object types.</param>
/// <param name="SizeClasses">The size classes.</param>
public sealed record WorldReferenceDto(
    IReadOnlyList<ReferenceDto> ResourceGroups,
    IReadOnlyList<ReferenceDto> Resources,
    IReadOnlyList<ReferenceDto> ObjectTypes,
    IReadOnlyList<ReferenceDto> SizeClasses);

/// <summary>A race the client offers at the first step of the game and on the landing page.</summary>
/// <param name="Id">The race.</param>
/// <param name="Code">The neutral code, for the journals and debugging only.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="MoodKey">The localisation key of the mood.</param>
/// <param name="TraitKeys">The localisation keys of the strengths.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/> for the neutral placeholder.</param>
/// <param name="ColorKey">The key of the colour, or <see langword="null"/> for the neutral one.</param>
/// <param name="PreferredObjectTypeId">The preferred start body type, if any.</param>
public sealed record RaceDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string MoodKey,
    IReadOnlyList<string> TraitKeys,
    string? ImageKey,
    string? ColorKey,
    int? PreferredObjectTypeId);

/// <summary>The civilization of a player.</summary>
/// <param name="RaceId">The race.</param>
/// <param name="RaceCode">The neutral code of the race, for debugging only.</param>
/// <param name="RaceNameKey">The localisation key of the name of the race.</param>
/// <param name="PlanetId">The home body.</param>
/// <param name="PlanetName">The name the player gave the home body.</param>
/// <param name="SystemId">The star system of the home body.</param>
/// <param name="SectorId">The sector of the home body.</param>
/// <param name="FoundedAt">The moment of creation.</param>
public sealed record CivilizationSummary(
    [property: System.Text.Json.Serialization.JsonPropertyName("race_id")] int RaceId,
    [property: System.Text.Json.Serialization.JsonPropertyName("race_code")] string RaceCode,
    [property: System.Text.Json.Serialization.JsonPropertyName("race_name_key")] string RaceNameKey,
    [property: System.Text.Json.Serialization.JsonPropertyName("planet_id")] long PlanetId,
    [property: System.Text.Json.Serialization.JsonPropertyName("planet_name")] string? PlanetName,
    [property: System.Text.Json.Serialization.JsonPropertyName("system_id")] long SystemId,
    [property: System.Text.Json.Serialization.JsonPropertyName("sector_id")] int SectorId,
    [property: System.Text.Json.Serialization.JsonPropertyName("founded_at")] DateTimeOffset FoundedAt);

/// <summary>What another module needs to know about a body (task 00011).</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="OwnerPlayerId">The owner, or <see langword="null"/> for a free body.</param>
/// <param name="Name">The name the owner gave the body.</param>
/// <param name="Deposits">The deposits with their coefficients.</param>
/// <param name="IsTerraformed">Whether the body is terraformed already (task 00014, section 9).</param>
/// <param name="IsColonizable">Whether the type of the body may be colonized; a gas giant may not (task 00014, section 11).</param>
public sealed record PlanetFacts(
    long PlanetId,
    int SectorId,
    string? OwnerPlayerId,
    string? Name,
    IReadOnlyList<PlanetDepositFact> Deposits,
    bool IsTerraformed = false,
    bool IsColonizable = true);

/// <summary>A deposit of a body: the resource and the richness coefficient.</summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Coefficient">The richness coefficient of the deposit.</param>
public sealed record PlanetDepositFact(int ResourceTypeId, decimal Coefficient);

/// <summary>The resource types and their groups, for the modules that keep amounts of them.</summary>
/// <param name="Groups">The resource groups.</param>
/// <param name="Resources">The resource types.</param>
public sealed record ResourceCatalog(IReadOnlyList<ResourceGroupFact> Groups, IReadOnlyList<ResourceTypeFact> Resources);

/// <summary>A resource group.</summary>
/// <param name="Id">The group.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="IsArchived">Whether the group is archived: it is not offered for new rows (task 00023).</param>
public sealed record ResourceGroupFact(int Id, string Code, string NameKey, bool IsArchived = false);

/// <summary>A resource type.</summary>
/// <param name="Id">The resource.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="ResourceGroupId">The group, if the resource has one: a currency of a civilization has none (task 00015).</param>
/// <param name="IsArchived">Whether the resource is archived: the stocks stay, it is not offered for new rows (task 00023).</param>
/// <param name="Scope">Where the resource lies: on a body or in the whole civilization (task 00015).</param>
/// <param name="EarnsPopulationIncome">Whether the population of a civilization brings this currency (task 00015).</param>
public sealed record ResourceTypeFact(int Id, string Code, string NameKey, int? ResourceGroupId, bool IsArchived = false, string Scope = Aetheris.World.Data.Entities.ResourceScopes.Planet, bool EarnsPopulationIncome = false)
{
    /// <summary>
    /// Gets whether the resource belongs to the whole civilization (a currency) and does not lie on a
    /// body, so the other modules need not know the code of the scope.
    /// </summary>
    public bool IsCivilizationScope => string.Equals(Scope, Aetheris.World.Data.Entities.ResourceScopes.Civilization, StringComparison.Ordinal);
}

/// <summary>A body that belongs to a player.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="SectorId">The sector of the body.</param>
public sealed record OwnedPlanet(long PlanetId, int SectorId);

/// <summary>
/// A body of a player with everything the screen of the civilization shows about it
/// (task 00010, section 7.4): the name, the type, the place in the galaxy and the deposits.
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Name">The name the owner gave the body.</param>
/// <param name="ObjectTypeId">The type of the body; the client reads the name and the picture of the type from the reference table.</param>
/// <param name="SectorId">The sector of the body.</param>
/// <param name="SystemId">The system of the body.</param>
/// <param name="Orbit">The orbit of the body in the system, if it has one.</param>
/// <param name="SizeClassId">The size class of the body.</param>
/// <param name="Size">The size of the body.</param>
/// <param name="IsTerraformed">Whether the body is terraformed already (task 00014, section 9).</param>
/// <param name="Deposits">The deposits of the body with their coefficients.</param>
public sealed record OwnedPlanetDto(
    long PlanetId,
    string? Name,
    int ObjectTypeId,
    int SectorId,
    long SystemId,
    int? Orbit,
    int SizeClassId,
    int Size,
    bool IsTerraformed,
    IReadOnlyList<PlanetDepositFact> Deposits);

/// <summary>The home body of a civilization with the start building of its race.</summary>
/// <param name="PlanetId">The home body.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="PlayerId">The player.</param>
/// <param name="StartBuildingTypeId">The start building of the race, if one is set.</param>
/// <param name="StartBuildingLevel">The level of the start building.</param>
public sealed record CivilizationHome(long PlanetId, int SectorId, string PlayerId, int? StartBuildingTypeId, int StartBuildingLevel);

/// <summary>
/// The logical codes of the outcome of the terraforming of a body (task 00014, section 9).
/// </summary>
public static class TerraformOutcomes
{
    /// <summary>The deposits of the body became rich and the body is marked as terraformed.</summary>
    public const string Done = "done";

    /// <summary>The body does not exist.</summary>
    public const string Unknown = "unknown";

    /// <summary>The body is a colony: only a free body is terraformed.</summary>
    public const string Colonized = "colonized";

    /// <summary>The type of the body may not be colonized, a gas giant for example: only a body that may be settled is terraformed (task 00014, section 11).</summary>
    public const string NotColonizable = "not_colonizable";

    /// <summary>The body is terraformed already: a body is terraformed once.</summary>
    public const string AlreadyTerraformed = "already_terraformed";
}

/// <summary>A deposit that became richer by the terraforming of a body (task 00014, section 11).</summary>
/// <param name="ResourceTypeId">The resource of the deposit.</param>
/// <param name="OldCoefficient">The coefficient before.</param>
/// <param name="NewCoefficient">The coefficient the roll gave the deposit.</param>
public sealed record TerraformedDeposit(int ResourceTypeId, decimal OldCoefficient, decimal NewCoefficient);

/// <summary>The outcome of the terraforming of a body (task 00014, sections 9 and 11).</summary>
/// <param name="Outcome">The outcome, one of <see cref="TerraformOutcomes"/>.</param>
/// <param name="Deposits">The deposits that became richer, each with the coefficient of its own roll.</param>
public sealed record TerraformResult(string Outcome, IReadOnlyList<TerraformedDeposit> Deposits)
{
    /// <summary>Gets the number of the deposits that became richer.</summary>
    public int ChangedDeposits => Deposits.Count;
}
