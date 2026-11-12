namespace Aetheris.World.Api;

/// <summary>A completed scan, including data for the report.</summary>
/// <param name="Id">The snapshot identifier.</param>
/// <param name="SectorId">The sector of its centre.</param>
/// <param name="SectorName">The name of the sector of the centre.</param>
/// <param name="X">The local x coordinate.</param>
/// <param name="Y">The local y coordinate.</param>
/// <param name="Z">The local z coordinate.</param>
/// <param name="Radius">The scan radius.</param>
/// <param name="ScannedAt">The scan time.</param>
/// <param name="Systems">The systems in the scan.</param>
/// <param name="Objects">The recorded objects.</param>
public sealed record ScanSnapshotResult(long Id, int SectorId, string SectorName, double X, double Y, double Z, double Radius, DateTimeOffset ScannedAt, IReadOnlyList<ScanSystemResult> Systems, IReadOnlyList<ScanObjectResult> Objects);

/// <summary>A system in a completed scan.</summary>
/// <param name="Id">The system identifier.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="Name">The name.</param>
public sealed record ScanSystemResult(long Id, int SectorId, string Name);

/// <summary>An object in a completed scan.</summary>
/// <param name="Id">The object identifier.</param>
/// <param name="SystemId">The system, if any.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="ObjectClass">The object class.</param>
/// <param name="PlanetId">The planet identifier, if any.</param>
/// <param name="HasDeposits">Whether resources were recorded.</param>
public sealed record ScanObjectResult(long Id, long? SystemId, int SectorId, string Name, string ObjectClass, long? PlanetId, bool HasDeposits);
