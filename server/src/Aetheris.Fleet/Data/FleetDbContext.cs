using Aetheris.Fleet.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Data;

/// <summary>
/// The tables of the fleet module: the projects of the ships and their slots (task 00038,
/// section 3.2).
/// </summary>
/// <remarks>
/// The parts live in the tables of the production and the bodies in the tables of the world,
/// so this module keeps only their identifiers and reads them through the contracts of the
/// other modules, and its tables carry no foreign keys into the tables of the other modules.
/// Names are snake_case, and a reference to one row ends with <c>_id</c>
/// (documentation/tech/08, rule 3).
/// </remarks>
public sealed class FleetDbContext : DbContext
{
    /// <summary>
    /// The length of the name of a project.
    /// </summary>
    private const int NameLength = 128;

    /// <summary>
    /// The length of the identifier of a player.
    /// </summary>
    private const int PlayerIdLength = 450;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public FleetDbContext(DbContextOptions<FleetDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the projects of the ships.</summary>
    public DbSet<ShipDesignEntity> ShipDesigns => Set<ShipDesignEntity>();

    /// <summary>Gets the slots of the projects.</summary>
    public DbSet<ShipDesignSlotEntity> ShipDesignSlots => Set<ShipDesignSlotEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureGameTables(modelBuilder);
        SnakeCaseNames.Apply(modelBuilder);
    }

    private static void ConfigureGameTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipDesignEntity>(entity =>
        {
            entity.ToTable("ship_design");
            entity.Property(e => e.OwnerPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.Name).HasMaxLength(NameLength);
            entity.HasIndex(e => e.OwnerPlayerId);
            entity.HasIndex(e => new { e.OwnerPlayerId, e.IsArchived });
            entity.HasIndex(e => e.PlanetId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<ShipDesignSlotEntity>(entity =>
        {
            entity.ToTable("ship_design_slot");
            entity.HasIndex(e => e.DesignId);
            entity.HasIndex(e => new { e.DesignId, e.SlotIndex }).IsUnique();
            entity.HasOne<ShipDesignEntity>().WithMany().HasForeignKey(e => e.DesignId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
