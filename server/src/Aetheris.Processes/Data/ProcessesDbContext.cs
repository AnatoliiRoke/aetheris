using Aetheris.Processes.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Data;

/// <summary>
/// The tables of the processes module: the kinds of the processes, their recipes, their
/// parameters and the queues of the buildings (task 00014, section 3.1).
/// </summary>
/// <remarks>
/// The bodies, the resources, the buildings, the parts and the technologies belong to the
/// other modules, so this module keeps only their identifiers and reads them through the
/// contracts of the other modules, and its tables carry no foreign keys into the tables of
/// the other modules. Names are snake_case, and a reference to one row ends with <c>_id</c>
/// (documentation/tech/08, rule 3).
/// </remarks>
public sealed class ProcessesDbContext : DbContext
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
    /// Initializes a new instance of the <see cref="ProcessesDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public ProcessesDbContext(DbContextOptions<ProcessesDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the kinds of the processes.</summary>
    public DbSet<ProcessTypeEntity> ProcessTypes => Set<ProcessTypeEntity>();

    /// <summary>Gets the recipes of the processes.</summary>
    public DbSet<ProcessRecipeEntity> ProcessRecipes => Set<ProcessRecipeEntity>();

    /// <summary>Gets the ingredients of the recipes of the processes.</summary>
    public DbSet<ProcessRecipeIngredientEntity> ProcessRecipeIngredients => Set<ProcessRecipeIngredientEntity>();

    /// <summary>Gets the parameters of the processes.</summary>
    public DbSet<ProcessParamEntity> ProcessParams => Set<ProcessParamEntity>();

    /// <summary>Gets the records of the queues of the processes.</summary>
    public DbSet<ProcessOrderEntity> ProcessOrders => Set<ProcessOrderEntity>();

    /// <summary>Gets the ingredients the records of the queues paid for.</summary>
    public DbSet<ProcessOrderCostEntity> ProcessOrderCosts => Set<ProcessOrderCostEntity>();

    /// <summary>Gets the extra places of the queues of the processes.</summary>
    public DbSet<ProcessQueueStateEntity> ProcessQueueStates => Set<ProcessQueueStateEntity>();

    /// <summary>Gets the levels of the technologies the orders in the queues were made at.</summary>
    public DbSet<ProcessOrderTechnologyEntity> ProcessOrderTechnologies => Set<ProcessOrderTechnologyEntity>();

    /// <summary>Gets the snapshots of the parameters the orders in the queues were made with.</summary>
    public DbSet<ProcessOrderParamEntity> ProcessOrderParams => Set<ProcessOrderParamEntity>();

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
        modelBuilder.Entity<ProcessTypeEntity>(entity =>
        {
            entity.ToTable("process_type");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.Property(e => e.HandlerCode).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.HandlerCode);
        });

        modelBuilder.Entity<ProcessRecipeEntity>(entity =>
        {
            entity.ToTable("process_recipe");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.Property(e => e.ShipStub).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.ProcessTypeId);
            entity.HasIndex(e => e.BuildingTypeId);
            entity.HasIndex(e => new { e.SourceGroupId, e.TargetGroupId });
            entity.HasOne<ProcessTypeEntity>().WithMany().HasForeignKey(e => e.ProcessTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_process_recipe_duration_seconds", "duration_seconds >= 0"));
        });

        modelBuilder.Entity<ProcessRecipeIngredientEntity>(entity =>
        {
            entity.ToTable("process_recipe_ingredient");
            entity.HasIndex(e => new { e.ProcessRecipeId, e.ResourceTypeId });
            entity.HasIndex(e => new { e.ProcessRecipeId, e.PartTypeId });
            entity.HasOne<ProcessRecipeEntity>().WithMany(e => e.IngredientIds).HasForeignKey(e => e.ProcessRecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_process_recipe_ingredient_amount", "amount >= 0"));
        });

        modelBuilder.Entity<ProcessParamEntity>(entity =>
        {
            entity.ToTable("process_param");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ParamKind).HasMaxLength(CodeLength);
            entity.Property(e => e.LevelSource).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.ProcessTypeId);
            entity.HasIndex(e => new { e.ProcessTypeId, e.ParamKind, e.LevelSource, e.LevelTargetId, e.Level });
            entity.HasOne<ProcessTypeEntity>().WithMany().HasForeignKey(e => e.ProcessTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_process_param_level", "level >= 1"));
        });
    }

    private static void ConfigureGameTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessOrderEntity>(entity =>
        {
            entity.ToTable("process_order");
            entity.HasIndex(e => new { e.PlanetId, e.PlanetBuildingId, e.Position });
            entity.HasIndex(e => e.SectorId);
            entity.HasIndex(e => e.ProcessRecipeId);
            entity.ToTable(table => table.HasCheckConstraint("ck_process_order_quantity", "quantity > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_process_order_produced", "produced_quantity >= 0 AND produced_quantity <= quantity"));
            entity.ToTable(table => table.HasCheckConstraint("ck_process_order_unit_seconds", "unit_seconds >= 0"));
        });

        modelBuilder.Entity<ProcessOrderCostEntity>(entity =>
        {
            entity.ToTable("process_order_cost");
            entity.HasIndex(e => new { e.ProcessOrderId, e.ResourceTypeId });
            entity.HasIndex(e => new { e.ProcessOrderId, e.PartTypeId });
            entity.HasOne<ProcessOrderEntity>().WithMany(e => e.CostIds).HasForeignKey(e => e.ProcessOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_process_order_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<ProcessOrderTechnologyEntity>(entity =>
        {
            entity.ToTable("process_order_technology");
            entity.HasIndex(e => e.ProcessOrderId);
            entity.HasOne<ProcessOrderEntity>().WithMany(e => e.TechnologyIds).HasForeignKey(e => e.ProcessOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_process_order_technology_level", "level >= 0"));
        });

        modelBuilder.Entity<ProcessOrderParamEntity>(entity =>
        {
            entity.ToTable("process_order_param");
            entity.Property(e => e.ParamKind).HasMaxLength(CodeLength);
            entity.Property(e => e.LevelSource).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.ProcessOrderId);
            entity.HasOne<ProcessOrderEntity>().WithMany(e => e.ParamIds).HasForeignKey(e => e.ProcessOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessQueueStateEntity>(entity =>
        {
            entity.ToTable("process_queue_state");
            entity.HasIndex(e => new { e.PlanetId, e.PlanetBuildingId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_process_queue_state_extra_slots", "extra_slots >= 0"));
        });
    }
}
