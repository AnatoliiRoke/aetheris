using System.Text;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aetheris.World.Data;

/// <summary>
/// The tables of the world module (task 00004, section 2.3).
/// </summary>
/// <remarks>
/// The module owns these tables; other modules reach them only through
/// <see cref="IWorldModule"/>. Every world table carries <c>sector_id</c>, the key of the
/// logical sharding by sectors. Names are snake_case, and a reference to one row ends with
/// <c>_id</c> (documentation/tech/08, rule 3).
/// </remarks>
public sealed class WorldDbContext : DbContext
{
    /// <summary>
    /// The length of a code column.
    /// </summary>
    private const int CodeLength = 64;

    /// <summary>
    /// The length of a name or a localisation key column.
    /// </summary>
    private const int NameLength = 128;

    /// <summary>
    /// The length of a player identifier, the same as the account identifier.
    /// </summary>
    private const int PlayerIdLength = 450;

    /// <summary>
    /// The precision of a coefficient column.
    /// </summary>
    private const int CoefficientPrecision = 12;

    /// <summary>
    /// The scale of a coefficient column.
    /// </summary>
    private const int CoefficientScale = 6;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public WorldDbContext(DbContextOptions<WorldDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the resource groups.</summary>
    public DbSet<ResourceGroupEntity> ResourceGroups => Set<ResourceGroupEntity>();

    /// <summary>Gets the resource types.</summary>
    public DbSet<ResourceTypeEntity> ResourceTypes => Set<ResourceTypeEntity>();

    /// <summary>Gets the celestial object types.</summary>
    public DbSet<CelestialObjectTypeEntity> ObjectTypes => Set<CelestialObjectTypeEntity>();

    /// <summary>Gets the size classes of bodies.</summary>
    public DbSet<PlanetSizeClassEntity> SizeClasses => Set<PlanetSizeClassEntity>();

    /// <summary>Gets the generation rule groups.</summary>
    public DbSet<GenerationRuleGroupEntity> RuleGroups => Set<GenerationRuleGroupEntity>();

    /// <summary>Gets the generation rules.</summary>
    public DbSet<GenerationRuleEntity> Rules => Set<GenerationRuleEntity>();

    /// <summary>Gets the races.</summary>
    public DbSet<RaceEntity> Races => Set<RaceEntity>();

    /// <summary>Gets the start resources of the races.</summary>
    public DbSet<RaceStartResourceEntity> RaceStartResources => Set<RaceStartResourceEntity>();

    /// <summary>Gets the galaxies.</summary>
    public DbSet<GalaxyEntity> Galaxies => Set<GalaxyEntity>();

    /// <summary>Gets the sectors.</summary>
    public DbSet<SectorEntity> Sectors => Set<SectorEntity>();

    /// <summary>Gets the star systems.</summary>
    public DbSet<StarSystemEntity> Systems => Set<StarSystemEntity>();

    /// <summary>Gets the celestial objects.</summary>
    public DbSet<CelestialObjectEntity> Objects => Set<CelestialObjectEntity>();

    /// <summary>Gets the bodies.</summary>
    public DbSet<PlanetEntity> Planets => Set<PlanetEntity>();

    /// <summary>Gets the deposits.</summary>
    public DbSet<DepositEntity> Deposits => Set<DepositEntity>();

    /// <summary>Gets the asteroid fields.</summary>
    public DbSet<AsteroidFieldEntity> Fields => Set<AsteroidFieldEntity>();

    /// <summary>Gets the field compositions.</summary>
    public DbSet<FieldCompositionEntity> FieldCompositions => Set<FieldCompositionEntity>();

    /// <summary>Gets the nebulae.</summary>
    public DbSet<NebulaEntity> Nebulae => Set<NebulaEntity>();

    /// <summary>Gets the nebula compositions.</summary>
    public DbSet<NebulaCompositionEntity> NebulaCompositions => Set<NebulaCompositionEntity>();

    /// <summary>Gets the start allocations.</summary>
    public DbSet<StartPlanetAllocationEntity> StartAllocations => Set<StartPlanetAllocationEntity>();

    /// <summary>Gets the civilizations of the players.</summary>
    public DbSet<PlayerCivilizationEntity> Civilizations => Set<PlayerCivilizationEntity>();

    /// <summary>Gets the storage of the bodies.</summary>
    public DbSet<PlanetStorageEntity> Storage => Set<PlanetStorageEntity>();

    /// <summary>Gets the start buildings waiting for the buildings module.</summary>
    public DbSet<StartBuildingGrantEntity> StartBuildingGrants => Set<StartBuildingGrantEntity>();

    /// <summary>Gets the generation log.</summary>
    public DbSet<WorldGenerationLogEntity> GenerationLog => Set<WorldGenerationLogEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureReferenceTables(modelBuilder);
        ConfigureWorldTables(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
    }

    private static void ConfigureReferenceTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResourceGroupEntity>(entity =>
        {
            entity.ToTable("resource_group");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<ResourceTypeEntity>(entity =>
        {
            entity.ToTable("resource_type");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasOne<ResourceGroupEntity>().WithMany().HasForeignKey(e => e.ResourceGroupId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CelestialObjectTypeEntity>(entity =>
        {
            entity.ToTable("celestial_object_type");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.ObjectClass).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<PlanetSizeClassEntity>(entity =>
        {
            entity.ToTable("planet_size_class");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<GenerationRuleGroupEntity>(entity =>
        {
            entity.ToTable("generation_rule_group");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.HasIndex(e => new { e.ObjectTypeId, e.Code }).IsUnique();
            entity.HasOne<CelestialObjectTypeEntity>().WithMany().HasForeignKey(e => e.ObjectTypeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.RuleIds).WithOne().HasForeignKey(rule => rule.RuleGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GenerationRuleEntity>(entity =>
        {
            entity.ToTable("generation_rule");
            entity.Property(e => e.CoefficientMin).HasPrecision(CoefficientPrecision, CoefficientScale);
            entity.Property(e => e.CoefficientMax).HasPrecision(CoefficientPrecision, CoefficientScale);
            entity.HasIndex(e => new { e.RuleGroupId, e.ResourceId }).IsUnique();
            entity.HasOne<ResourceTypeEntity>().WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RaceEntity>(entity =>
        {
            entity.ToTable("race");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasOne<CelestialObjectTypeEntity>().WithMany().HasForeignKey(e => e.PreferredObjectTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.StartResourceIds).WithOne().HasForeignKey(resource => resource.RaceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RaceStartResourceEntity>(entity =>
        {
            entity.ToTable("race_start_resource");
            entity.HasIndex(e => new { e.RaceId, e.ResourceTypeId }).IsUnique();
            entity.HasOne<ResourceTypeEntity>().WithMany().HasForeignKey(e => e.ResourceTypeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorldTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GalaxyEntity>(entity =>
        {
            entity.ToTable("galaxy");
            entity.Property(e => e.Name).HasMaxLength(NameLength);
        });

        modelBuilder.Entity<SectorEntity>(entity =>
        {
            entity.ToTable("sector");
            entity.Property(e => e.Name).HasMaxLength(NameLength);
            entity.HasIndex(e => new { e.GalaxyId, e.LayoutIndex }).IsUnique();
            entity.HasOne<GalaxyEntity>().WithMany().HasForeignKey(e => e.GalaxyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StarSystemEntity>(entity =>
        {
            entity.ToTable("star_system");
            entity.Property(e => e.Name).HasMaxLength(NameLength);

            // A system is found by its coordinates within the sector.
            entity.HasIndex(e => new { e.SectorId, e.X, e.Y });
            entity.HasIndex(e => new { e.SectorId, e.Number }).IsUnique();
            entity.HasOne<SectorEntity>().WithMany().HasForeignKey(e => e.SectorId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ObjectIds).WithOne(o => o.System).HasForeignKey(o => o.SystemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CelestialObjectEntity>(entity =>
        {
            entity.ToTable("celestial_object");
            entity.Property(e => e.Name).HasMaxLength(NameLength);

            // The objects of one system, and the objects of one type in a sector.
            entity.HasIndex(e => new { e.SystemId, e.Orbit });
            entity.HasIndex(e => new { e.SectorId, e.ObjectTypeId });
            entity.HasOne<SectorEntity>().WithMany().HasForeignKey(e => e.SectorId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CelestialObjectTypeEntity>().WithMany().HasForeignKey(e => e.ObjectTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlanetEntity>(entity =>
        {
            entity.ToTable("planet");
            entity.Property(e => e.OwnerPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.Name).HasMaxLength(NameLength);

            // The free bodies of a sector, and the bodies of a player.
            entity.HasIndex(e => new { e.SectorId, e.OwnerPlayerId });
            entity.HasIndex(e => e.OwnerPlayerId);
            entity.HasIndex(e => e.SystemId);
            entity.HasIndex(e => e.CelestialObjectId).IsUnique();
            entity.HasOne(e => e.CelestialObject).WithOne().HasForeignKey<PlanetEntity>(e => e.CelestialObjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StarSystemEntity>().WithMany().HasForeignKey(e => e.SystemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<PlanetSizeClassEntity>().WithMany().HasForeignKey(e => e.SizeClassId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.DepositIds).WithOne().HasForeignKey(deposit => deposit.PlanetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DepositEntity>(entity =>
        {
            entity.ToTable("deposit");
            entity.Property(e => e.Coefficient).HasPrecision(CoefficientPrecision, CoefficientScale);
            entity.HasIndex(e => new { e.PlanetId, e.ResourceId }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<ResourceTypeEntity>().WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AsteroidFieldEntity>(entity =>
        {
            entity.ToTable("asteroid_field");

            // The fields that still exist in a sector.
            entity.HasIndex(e => new { e.SectorId, e.DepletedAt });
            entity.HasIndex(e => e.SystemId);
            entity.HasIndex(e => e.CelestialObjectId).IsUnique();
            entity.HasOne(e => e.CelestialObject).WithOne().HasForeignKey<AsteroidFieldEntity>(e => e.CelestialObjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StarSystemEntity>().WithMany().HasForeignKey(e => e.SystemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.CompositionIds).WithOne().HasForeignKey(share => share.AsteroidFieldId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldCompositionEntity>(entity =>
        {
            entity.ToTable("field_composition");
            entity.Property(e => e.Coefficient).HasPrecision(CoefficientPrecision, CoefficientScale);
            entity.HasIndex(e => new { e.AsteroidFieldId, e.ResourceId }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<ResourceTypeEntity>().WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NebulaEntity>(entity =>
        {
            entity.ToTable("nebula");
            entity.HasIndex(e => e.SectorId);
            entity.HasIndex(e => e.CelestialObjectId).IsUnique();
            entity.HasOne(e => e.CelestialObject).WithOne().HasForeignKey<NebulaEntity>(e => e.CelestialObjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.CompositionIds).WithOne().HasForeignKey(share => share.NebulaId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NebulaCompositionEntity>(entity =>
        {
            entity.ToTable("nebula_composition");
            entity.Property(e => e.Coefficient).HasPrecision(CoefficientPrecision, CoefficientScale);
            entity.HasIndex(e => new { e.NebulaId, e.ResourceId }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<ResourceTypeEntity>().WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StartPlanetAllocationEntity>(entity =>
        {
            entity.ToTable("start_planet_allocation");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => e.PlanetId).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerCivilizationEntity>(entity =>
        {
            entity.ToTable("player_civilization");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => e.PlanetId).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<RaceEntity>().WithMany().HasForeignKey(e => e.RaceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PlanetEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlanetStorageEntity>(entity =>
        {
            entity.ToTable("planet_storage");
            entity.HasIndex(e => new { e.PlanetId, e.ResourceTypeId }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ResourceTypeEntity>().WithMany().HasForeignKey(e => e.ResourceTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_planet_storage_amount", "amount >= 0"));
        });

        modelBuilder.Entity<StartBuildingGrantEntity>(entity =>
        {
            entity.ToTable("start_building_grant");
            entity.HasKey(e => e.PlanetId);
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorldGenerationLogEntity>(entity =>
        {
            entity.ToTable("world_generation_log");
            entity.Property(e => e.Action).HasMaxLength(CodeLength);
            entity.Property(e => e.ActorId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => new { e.SectorId, e.At });
        });
    }

    /// <summary>
    /// Converts every column, key and index name to snake_case.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (IMutableKey key in entityType.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (IMutableForeignKey foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (IMutableIndex index in entityType.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }

    /// <summary>
    /// Converts a PascalCase name to snake_case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The converted name.</returns>
    public static string ToSnakeCase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        StringBuilder builder = new(name.Length + name.Length);

        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];

            if (char.IsUpper(current) && index > 0 && name[index - 1] != '_'
                && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1])
                    || (index + 1 < name.Length && char.IsLower(name[index + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
