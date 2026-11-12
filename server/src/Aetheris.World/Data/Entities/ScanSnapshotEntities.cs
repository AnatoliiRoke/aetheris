namespace Aetheris.World.Data.Entities;

/// <summary>A player's record of the world at a moment of scanning.</summary>
public sealed class ScanSnapshotEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }
    /// <summary>Gets or sets the owner.</summary>
    public string PlayerId { get; set; } = string.Empty;
    /// <summary>Gets or sets the sector of the centre.</summary>
    public int SectorId { get; set; }
    /// <summary>Gets or sets the local x coordinate.</summary>
    public double X { get; set; }
    /// <summary>Gets or sets the local y coordinate.</summary>
    public double Y { get; set; }
    /// <summary>Gets or sets the local z coordinate.</summary>
    public double Z { get; set; }
    /// <summary>Gets or sets the radius.</summary>
    public double Radius { get; set; }
    /// <summary>Gets or sets the time of the scan.</summary>
    public DateTimeOffset ScannedAt { get; set; }
    /// <summary>Gets or sets the expiration time.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Gets or sets the recorded systems.</summary>
    public List<ScanSnapshotSystemEntity> SystemIds { get; set; } = [];
    /// <summary>Gets or sets the recorded objects.</summary>
    public List<ScanSnapshotObjectEntity> ObjectIds { get; set; } = [];
}

/// <summary>A system as it was at the time of a scan.</summary>
public sealed class ScanSnapshotSystemEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }
    /// <summary>Gets or sets the snapshot.</summary>
    public long ScanSnapshotId { get; set; }
    /// <summary>Gets or sets the original system identifier.</summary>
    public long SystemId { get; set; }
    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the system number.</summary>
    public int Number { get; set; }
    /// <summary>Gets or sets whether the system was in a nebula.</summary>
    public bool InNebula { get; set; }
    /// <summary>Gets or sets the local x coordinate.</summary>
    public double X { get; set; }
    /// <summary>Gets or sets the local y coordinate.</summary>
    public double Y { get; set; }
    /// <summary>Gets or sets the local z coordinate.</summary>
    public double Z { get; set; }
}

/// <summary>An object as it was at the time of a scan.</summary>
public sealed class ScanSnapshotObjectEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }
    /// <summary>Gets or sets the snapshot.</summary>
    public long ScanSnapshotId { get; set; }
    /// <summary>Gets or sets the original object identifier.</summary>
    public long CelestialObjectId { get; set; }
    /// <summary>Gets or sets the original system identifier, if any.</summary>
    public long? SystemId { get; set; }
    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }
    /// <summary>Gets or sets the type.</summary>
    public int ObjectTypeId { get; set; }
    /// <summary>Gets or sets the orbit.</summary>
    public int? Orbit { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the local x coordinate.</summary>
    public double X { get; set; }
    /// <summary>Gets or sets the local y coordinate.</summary>
    public double Y { get; set; }
    /// <summary>Gets or sets the local z coordinate.</summary>
    public double Z { get; set; }
    /// <summary>Gets or sets the owner at the time of the scan.</summary>
    public string? OwnerPlayerId { get; set; }
    /// <summary>Gets or sets the original planet identifier.</summary>
    public long? PlanetId { get; set; }
    /// <summary>Gets or sets the size class of a planet.</summary>
    public int? SizeClassId { get; set; }
    /// <summary>Gets or sets the size of a planet.</summary>
    public int? Size { get; set; }
    /// <summary>Gets or sets whether a planet was terraformed.</summary>
    public bool IsTerraformed { get; set; }
    /// <summary>Gets or sets the original field identifier.</summary>
    public long? AsteroidFieldId { get; set; }
    /// <summary>Gets or sets the original nebula identifier.</summary>
    public long? NebulaId { get; set; }
    /// <summary>Gets or sets the nebula radius.</summary>
    public double? NebulaRadius { get; set; }
    /// <summary>Gets or sets whether the nebula blocked jumps.</summary>
    public bool NebulaBlocksJumpDrive { get; set; }
    /// <summary>Gets or sets the field mass, if applicable.</summary>
    public long? FieldMass { get; set; }
    /// <summary>Gets or sets the resource shares.</summary>
    public List<ScanSnapshotDepositEntity> DepositIds { get; set; } = [];
}

/// <summary>A resource coefficient recorded in a scan.</summary>
public sealed class ScanSnapshotDepositEntity
{
    /// <summary>Gets or sets the recorded object.</summary>
    public long ScanSnapshotObjectId { get; set; }
    /// <summary>Gets or sets the resource.</summary>
    public int ResourceId { get; set; }
    /// <summary>Gets or sets the recorded coefficient.</summary>
    public decimal Coefficient { get; set; }
}
