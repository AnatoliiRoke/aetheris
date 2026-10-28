using Aetheris.Requirements.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Requirements.Data;

/// <summary>
/// The tables of the common requirements module: the requirements of every closed subject
/// (task 00012, decision 3).
/// </summary>
/// <remarks>
/// The table belongs to this module, so the bodies module and the research module add their
/// requirements through the public contract of this module and neither reaches into the
/// tables of the other (documentation/tech/01, section 8). Names are snake_case, and a
/// reference to one row ends with <c>_id</c> (documentation/tech/08, rule 3).
/// </remarks>
public sealed class RequirementsDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementsDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public RequirementsDbContext(DbContextOptions<RequirementsDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the requirements of every closed subject.</summary>
    public DbSet<RequirementEntity> Requirements => Set<RequirementEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<RequirementEntity>(entity =>
        {
            entity.ToTable("requirement");
            entity.HasIndex(e => new { e.SubjectType, e.SubjectId, e.SubjectLevel });
            entity.ToTable(table => table.HasCheckConstraint("ck_requirement_subject_level", "subject_level >= 1"));
        });

        SnakeCaseNames.Apply(modelBuilder);
    }
}
