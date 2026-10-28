using Aetheris.Research.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Data;

/// <summary>
/// The tables of the research module: technologies, their levels and effects, the knowledge
/// of the players and the queue of the researches (task 00012, section 3.1).
/// </summary>
/// <remarks>
/// The bodies and the resources belong to the world, so this module keeps only their
/// identifiers and reads them through the contracts of the other modules, and its tables
/// carry no foreign keys into the tables of the other modules. Names are snake_case, and a
/// reference to one row ends with <c>_id</c> (documentation/tech/08, rule 3).
/// </remarks>
public sealed class ResearchDbContext : DbContext
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
    /// Initializes a new instance of the <see cref="ResearchDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public ResearchDbContext(DbContextOptions<ResearchDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the branches of the tree.</summary>
    public DbSet<TechnologyBranchEntity> TechnologyBranches => Set<TechnologyBranchEntity>();

    /// <summary>Gets the technologies.</summary>
    public DbSet<TechnologyEntity> Technologies => Set<TechnologyEntity>();

    /// <summary>Gets the levels of the technologies.</summary>
    public DbSet<TechnologyLevelEntity> TechnologyLevels => Set<TechnologyLevelEntity>();

    /// <summary>Gets the costs of the levels.</summary>
    public DbSet<TechnologyLevelCostEntity> TechnologyLevelCosts => Set<TechnologyLevelCostEntity>();

    /// <summary>Gets the effects of the levels.</summary>
    public DbSet<TechnologyLevelEffectEntity> TechnologyLevelEffects => Set<TechnologyLevelEffectEntity>();

    /// <summary>Gets what the players have learned.</summary>
    public DbSet<PlayerTechnologyEntity> PlayerTechnologies => Set<PlayerTechnologyEntity>();

    /// <summary>Gets the states of the queues of the players.</summary>
    public DbSet<ResearchQueueStateEntity> ResearchQueueStates => Set<ResearchQueueStateEntity>();

    /// <summary>Gets the records of the queue of the researches.</summary>
    public DbSet<ResearchQueueItemEntity> ResearchQueueItems => Set<ResearchQueueItemEntity>();

    /// <summary>Gets the resources the records of the queue paid for.</summary>
    public DbSet<ResearchQueueCostEntity> ResearchQueueCosts => Set<ResearchQueueCostEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureReferenceTables(modelBuilder);
        ConfigurePlayerTables(modelBuilder);
        SnakeCaseNames.Apply(modelBuilder);
    }

    private static void ConfigureReferenceTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TechnologyBranchEntity>(entity =>
        {
            entity.ToTable("technology_branch");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<TechnologyEntity>(entity =>
        {
            entity.ToTable("technology");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => new { e.BranchId, e.SortOrder });
            entity.HasOne<TechnologyBranchEntity>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_technology_max_level", "max_level >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_technology_research_center_level", "research_center_level >= 1"));
        });

        modelBuilder.Entity<TechnologyLevelEntity>(entity =>
        {
            entity.ToTable("technology_level");
            entity.HasIndex(e => new { e.TechnologyId, e.Level }).IsUnique();
            entity.HasOne<TechnologyEntity>().WithMany(e => e.LevelIds).HasForeignKey(e => e.TechnologyId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_technology_level_level", "level >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_technology_level_duration", "duration_seconds >= 0"));
        });

        modelBuilder.Entity<TechnologyLevelCostEntity>(entity =>
        {
            entity.ToTable("technology_level_cost");
            entity.HasIndex(e => new { e.TechnologyLevelId, e.ResourceTypeId }).IsUnique();
            entity.HasOne<TechnologyLevelEntity>().WithMany(e => e.CostIds).HasForeignKey(e => e.TechnologyLevelId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_technology_level_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<TechnologyLevelEffectEntity>(entity =>
        {
            entity.ToTable("technology_level_effect");
            entity.Property(e => e.EffectType).HasMaxLength(CodeLength);
            entity.Property(e => e.UnlockKind).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.TechnologyLevelId);
            entity.HasOne<TechnologyLevelEntity>().WithMany(e => e.EffectIds).HasForeignKey(e => e.TechnologyLevelId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePlayerTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerTechnologyEntity>(entity =>
        {
            entity.ToTable("player_technology");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => new { e.PlayerId, e.TechnologyId }).IsUnique();
            entity.HasOne<TechnologyEntity>().WithMany().HasForeignKey(e => e.TechnologyId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_player_technology_level", "level >= 0"));
        });

        modelBuilder.Entity<ResearchQueueStateEntity>(entity =>
        {
            entity.ToTable("research_queue_state");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.ToTable(table => table.HasCheckConstraint("ck_research_queue_state_extra_slots", "extra_queue_slots >= 0"));
        });

        modelBuilder.Entity<ResearchQueueItemEntity>(entity =>
        {
            entity.ToTable("research_queue_item");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => new { e.PlayerId, e.Position }).IsUnique();
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.PlanetId);
            entity.HasOne<TechnologyEntity>().WithMany().HasForeignKey(e => e.TechnologyId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_research_queue_item_target_level", "target_level >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_research_queue_item_position", "position >= 1"));
        });

        modelBuilder.Entity<ResearchQueueCostEntity>(entity =>
        {
            entity.ToTable("research_queue_cost");
            entity.HasIndex(e => new { e.ResearchQueueItemId, e.ResourceTypeId }).IsUnique();
            entity.HasOne<ResearchQueueItemEntity>().WithMany(e => e.CostIds).HasForeignKey(e => e.ResearchQueueItemId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_research_queue_cost_amount", "amount >= 0"));
        });
    }
}
