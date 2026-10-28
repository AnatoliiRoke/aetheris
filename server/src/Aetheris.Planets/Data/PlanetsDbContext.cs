using Aetheris.Planets.Data.Entities;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aetheris.Planets.Data;

/// <summary>
/// The tables of the bodies module: buildings, requirements, construction queues and the
/// balance of the bodies (task 00011, section 3.1).
/// </summary>
/// <remarks>
/// The bodies, the resources and the resource groups belong to the world; this module keeps
/// only their identifiers and reads them through <see cref="World.IWorldModule"/>, so its
/// tables carry no foreign keys into the world tables. Names are snake_case, and a reference
/// to one row ends with <c>_id</c> (documentation/tech/08, rule 3).
/// </remarks>
public sealed class PlanetsDbContext : DbContext
{
    /// <summary>
    /// The length of a code column.
    /// </summary>
    private const int CodeLength = 64;

    /// <summary>
    /// The length of a localisation key column.
    /// </summary>
    private const int NameLength = 128;

    /// <summary>
    /// The length of a player identifier, the same as the account identifier.
    /// </summary>
    private const int PlayerIdLength = 450;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetsDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public PlanetsDbContext(DbContextOptions<PlanetsDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the building types.</summary>
    public DbSet<BuildingTypeEntity> BuildingTypes => Set<BuildingTypeEntity>();

    /// <summary>Gets the levels of the building types.</summary>
    public DbSet<BuildingLevelEntity> BuildingLevels => Set<BuildingLevelEntity>();

    /// <summary>Gets the costs of the levels.</summary>
    public DbSet<BuildingLevelCostEntity> BuildingLevelCosts => Set<BuildingLevelCostEntity>();

    /// <summary>Gets the effects of the levels.</summary>
    public DbSet<BuildingLevelEffectEntity> BuildingLevelEffects => Set<BuildingLevelEffectEntity>();

    /// <summary>Gets the states of the bodies.</summary>
    public DbSet<PlanetStateEntity> PlanetStates => Set<PlanetStateEntity>();

    /// <summary>Gets the buildings of the bodies.</summary>
    public DbSet<PlanetBuildingEntity> PlanetBuildings => Set<PlanetBuildingEntity>();

    /// <summary>Gets the construction queues.</summary>
    public DbSet<ConstructionQueueItemEntity> QueueItems => Set<ConstructionQueueItemEntity>();

    /// <summary>Gets the costs paid for the queue records.</summary>
    public DbSet<ConstructionQueueCostEntity> QueueCosts => Set<ConstructionQueueCostEntity>();

    /// <summary>Gets the balance of the bodies.</summary>
    public DbSet<PlanetResourceEntity> PlanetResources => Set<PlanetResourceEntity>();

    /// <summary>Gets the storage states of the resource groups of the bodies.</summary>
    public DbSet<PlanetStorageGroupEntity> StorageGroups => Set<PlanetStorageGroupEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureReferenceTables(modelBuilder);
        ConfigurePlanetTables(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
    }

    private static void ConfigureReferenceTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BuildingTypeEntity>(entity =>
        {
            entity.ToTable("building_type");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.BuildingClass).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.BuildingClass);
            entity.HasMany(e => e.LevelIds).WithOne().HasForeignKey(level => level.BuildingTypeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuildingLevelEntity>(entity =>
        {
            entity.ToTable("building_level");
            entity.HasIndex(e => new { e.BuildingTypeId, e.Level }).IsUnique();
            entity.HasMany(e => e.CostIds).WithOne().HasForeignKey(cost => cost.BuildingLevelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.EffectIds).WithOne().HasForeignKey(effect => effect.BuildingLevelId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_building_level_duration", "duration_seconds >= 0"));
        });

        modelBuilder.Entity<BuildingLevelCostEntity>(entity =>
        {
            entity.ToTable("building_level_cost");
            entity.HasIndex(e => new { e.BuildingLevelId, e.ResourceTypeId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_building_level_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<BuildingLevelEffectEntity>(entity =>
        {
            entity.ToTable("building_level_effect");
            entity.Property(e => e.EffectType).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.BuildingLevelId);
        });
    }

    private static void ConfigurePlanetTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlanetStateEntity>(entity =>
        {
            entity.ToTable("planet_state");
            entity.HasKey(e => e.PlanetId);
            entity.Property(e => e.PlanetId).ValueGeneratedNever();
            entity.Property(e => e.OwnerPlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => e.SectorId);
            entity.HasIndex(e => e.OwnerPlayerId);
            entity.ToTable(table => table.HasCheckConstraint("ck_planet_state_extra_queue_slots", "extra_queue_slots >= 0"));
        });

        modelBuilder.Entity<PlanetBuildingEntity>(entity =>
        {
            entity.ToTable("planet_building");
            entity.HasIndex(e => new { e.PlanetId, e.BuildingTypeId }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetStateEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BuildingTypeEntity>().WithMany().HasForeignKey(e => e.BuildingTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_planet_building_level", "level >= 0"));
        });

        modelBuilder.Entity<ConstructionQueueItemEntity>(entity =>
        {
            entity.ToTable("construction_queue_item");
            // The queue of a body in its order. The places are numbered again after a removal in
            // one save, so they are not unique while the rows are written.
            entity.HasIndex(e => new { e.PlanetId, e.Position });
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetStateEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BuildingTypeEntity>().WithMany().HasForeignKey(e => e.BuildingTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.CostIds).WithOne().HasForeignKey(cost => cost.ConstructionQueueItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConstructionQueueCostEntity>(entity =>
        {
            entity.ToTable("construction_queue_cost");
            entity.HasIndex(e => new { e.ConstructionQueueItemId, e.ResourceTypeId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_construction_queue_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<PlanetResourceEntity>(entity =>
        {
            entity.ToTable("planet_resource");
            entity.HasIndex(e => new { e.PlanetId, e.ResourceTypeId }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetStateEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_planet_resource_amount", "amount >= 0"));
        });

        modelBuilder.Entity<PlanetStorageGroupEntity>(entity =>
        {
            entity.ToTable("planet_storage_group");
            entity.HasKey(e => new { e.PlanetId, e.ResourceGroupId });
            entity.HasIndex(e => e.SectorId);
            entity.HasOne<PlanetStateEntity>().WithMany().HasForeignKey(e => e.PlanetId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Converts every column, key and index name to snake_case, like the world context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                property.SetColumnName(WorldDbContext.ToSnakeCase(property.Name));
            }

            foreach (IMutableKey key in entityType.GetKeys())
            {
                key.SetName(WorldDbContext.ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (IMutableForeignKey foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(WorldDbContext.ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (IMutableIndex index in entityType.GetIndexes())
            {
                index.SetDatabaseName(WorldDbContext.ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }
}
