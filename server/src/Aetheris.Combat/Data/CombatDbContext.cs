using Aetheris.Combat.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aetheris.Combat.Data;

/// <summary>
/// The tables of the combat module: the target rules and the behaviors (task 00062).
/// </summary>
public sealed class CombatDbContext : DbContext
{
    private const int CodeLength = 64;

    private const int NameLength = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public CombatDbContext(DbContextOptions<CombatDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the target rules.</summary>
    public DbSet<TargetRuleEntity> TargetRules => Set<TargetRuleEntity>();

    /// <summary>Gets the behaviors.</summary>
    public DbSet<BehaviorEntity> Behaviors => Set<BehaviorEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        Configure(modelBuilder.Entity<TargetRuleEntity>(), "target_rule");
        Configure(modelBuilder.Entity<BehaviorEntity>(), "behavior");
        SnakeCaseNames.Apply(modelBuilder);
    }

    private static void Configure<TRow>(EntityTypeBuilder<TRow> entity, string table)
        where TRow : class, ICombatRuleRow
    {
        entity.ToTable(table);
        entity.Property(row => row.Code).HasMaxLength(CodeLength);
        entity.Property(row => row.LogicCode).HasMaxLength(CodeLength);
        entity.Property(row => row.NameKey).HasMaxLength(NameLength);
        entity.Property(row => row.DescriptionKey).HasMaxLength(NameLength);
        entity.Property(row => row.ImageKey).HasMaxLength(CodeLength);
        entity.HasIndex(row => row.Code).IsUnique();
        entity.HasIndex(row => row.LogicCode).IsUnique();
        entity.Property(row => row.IsArchived).HasDefaultValue(false);
    }
}
