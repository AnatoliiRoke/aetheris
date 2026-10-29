using Aetheris.Production.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Data;

/// <summary>
/// The tables of the production module: the groups of the parts, the parts, the recipes and
/// the queue of the factories (task 00013, section 3.1).
/// </summary>
/// <remarks>
/// The bodies, the resources and the buildings belong to the other modules, so this module
/// keeps only their identifiers and reads them through the contracts of the other modules,
/// and its tables carry no foreign keys into the tables of the other modules. Names are
/// snake_case, and a reference to one row ends with <c>_id</c> (documentation/tech/08, rule 3).
/// </remarks>
public sealed class ProductionDbContext : DbContext
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
    /// Initializes a new instance of the <see cref="ProductionDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public ProductionDbContext(DbContextOptions<ProductionDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the groups of the parts.</summary>
    public DbSet<PartGroupEntity> PartGroups => Set<PartGroupEntity>();

    /// <summary>Gets the types of the parts.</summary>
    public DbSet<PartTypeEntity> PartTypes => Set<PartTypeEntity>();

    /// <summary>Gets the attributes of the parts.</summary>
    public DbSet<PartAttributeEntity> PartAttributes => Set<PartAttributeEntity>();

    /// <summary>Gets the recipes.</summary>
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();

    /// <summary>Gets the ingredients of the recipes.</summary>
    public DbSet<RecipeIngredientEntity> RecipeIngredients => Set<RecipeIngredientEntity>();

    /// <summary>Gets the records of the queues of the factories.</summary>
    public DbSet<ProductionOrderEntity> ProductionOrders => Set<ProductionOrderEntity>();

    /// <summary>Gets the ingredients the records of the queues paid for.</summary>
    public DbSet<ProductionOrderCostEntity> ProductionOrderCosts => Set<ProductionOrderCostEntity>();

    /// <summary>Gets the stacks of the parts on the bodies.</summary>
    public DbSet<PartStockEntity> PartStocks => Set<PartStockEntity>();

    /// <summary>Gets the states of the storages of the parts.</summary>
    public DbSet<PartStorageEntity> PartStorages => Set<PartStorageEntity>();

    /// <summary>Gets the extra places of the queues of the factories.</summary>
    public DbSet<ProductionQueueStateEntity> ProductionQueueStates => Set<ProductionQueueStateEntity>();

    /// <summary>Gets the bonuses of the races in the groups of the parts.</summary>
    public DbSet<RacePartBonusEntity> RacePartBonuses => Set<RacePartBonusEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureReferenceTables(modelBuilder);
        ConfigureGameTables(modelBuilder);
        SnakeCaseNames.Apply(modelBuilder);
    }

    private static void ConfigureReferenceTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PartGroupEntity>(entity =>
        {
            entity.ToTable("part_group");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<PartTypeEntity>(entity =>
        {
            entity.ToTable("part_type");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => new { e.PartGroupId, e.SortOrder });
            entity.HasOne<PartGroupEntity>().WithMany().HasForeignKey(e => e.PartGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_part_type_mark_level", "mark_level >= 1 AND mark_level <= 3"));
        });

        modelBuilder.Entity<PartAttributeEntity>(entity =>
        {
            entity.ToTable("part_attribute");
            entity.HasIndex(e => new { e.PartTypeId, e.AttributeKey }).IsUnique();
            entity.HasOne<PartTypeEntity>().WithMany(e => e.AttributeIds).HasForeignKey(e => e.PartTypeId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.AttributeKey).HasMaxLength(CodeLength);
        });

        modelBuilder.Entity<RecipeEntity>(entity =>
        {
            entity.ToTable("recipe");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.BuildingTypeId);
            entity.HasIndex(e => e.PartTypeId);
            entity.HasIndex(e => e.ResourceTypeId);
            entity.ToTable(table => table.HasCheckConstraint("ck_recipe_output_quantity", "output_quantity >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_recipe_duration_seconds", "duration_seconds >= 0"));
        });

        modelBuilder.Entity<RecipeIngredientEntity>(entity =>
        {
            entity.ToTable("recipe_ingredient");
            entity.HasIndex(e => new { e.RecipeId, e.ResourceTypeId });
            entity.HasIndex(e => new { e.RecipeId, e.PartTypeId });
            entity.HasOne<RecipeEntity>().WithMany(e => e.IngredientIds).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_recipe_ingredient_amount", "amount >= 0"));
        });
    }

    private static void ConfigureGameTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductionOrderEntity>(entity =>
        {
            entity.ToTable("production_order");
            entity.HasIndex(e => new { e.PlanetId, e.PlanetBuildingId, e.Position });
            entity.HasIndex(e => e.SectorId);
            entity.HasIndex(e => e.RecipeId);
            entity.ToTable(table => table.HasCheckConstraint("ck_production_order_quantity", "quantity > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_production_order_produced", "produced_quantity >= 0 AND produced_quantity <= quantity"));
        });

        modelBuilder.Entity<ProductionOrderCostEntity>(entity =>
        {
            entity.ToTable("production_order_cost");
            entity.HasIndex(e => new { e.ProductionOrderId, e.ResourceTypeId });
            entity.HasIndex(e => new { e.ProductionOrderId, e.PartTypeId });
            entity.HasOne<ProductionOrderEntity>().WithMany(e => e.CostIds).HasForeignKey(e => e.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_production_order_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<PartStockEntity>(entity =>
        {
            entity.ToTable("part_stock");
            entity.HasIndex(e => new { e.PlanetId, e.PartTypeId, e.MarkLevel, e.HasRaceBonus, e.ConditionPercent }).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.ToTable(table => table.HasCheckConstraint("ck_part_stock_condition", "condition_percent >= 1 AND condition_percent <= 100"));
            entity.ToTable(table => table.HasCheckConstraint("ck_part_stock_quantity", "quantity >= 0"));
        });

        modelBuilder.Entity<PartStorageEntity>(entity =>
        {
            entity.ToTable("part_storage");
            entity.HasIndex(e => e.PlanetId).IsUnique();
            entity.HasIndex(e => e.SectorId);
            entity.ToTable(table => table.HasCheckConstraint("ck_part_storage_capacity", "capacity >= 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_part_storage_quantity", "quantity >= 0"));
        });

        modelBuilder.Entity<ProductionQueueStateEntity>(entity =>
        {
            entity.ToTable("production_queue_state");
            entity.HasIndex(e => new { e.PlanetId, e.PlanetBuildingId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_production_queue_state_extra_slots", "extra_queue_slots >= 0"));
        });

        modelBuilder.Entity<RacePartBonusEntity>(entity =>
        {
            entity.ToTable("race_part_bonus");
            entity.HasIndex(e => new { e.RaceId, e.PartGroupId }).IsUnique();
            entity.HasOne<PartGroupEntity>().WithMany().HasForeignKey(e => e.PartGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_race_part_bonus_multiplier", "multiplier > 0"));
        });
    }
}
