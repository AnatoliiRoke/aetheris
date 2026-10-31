using Aetheris.Economy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Economy.Data;

/// <summary>
/// The tables of the economy module: the population of the bodies and the balances of the
/// civilizations (task 00015, section 3.1).
/// </summary>
/// <remarks>
/// The bodies, the buildings, the technologies and the resources belong to the other modules,
/// so this module keeps only their identifiers and reads them through the contracts of the
/// other modules, and its tables carry no foreign keys into the tables of the other modules.
/// Names are snake_case, and a reference to one row ends with <c>_id</c>
/// (documentation/tech/08, rule 3).
/// </remarks>
public sealed class EconomyDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public EconomyDbContext(DbContextOptions<EconomyDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the populations of the bodies.</summary>
    public DbSet<PlanetPopulationEntity> PlanetPopulations => Set<PlanetPopulationEntity>();

    /// <summary>Gets the balances of the civilizations.</summary>
    public DbSet<CivilizationBalanceEntity> CivilizationBalances => Set<CivilizationBalanceEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<PlanetPopulationEntity>(entity =>
        {
            entity.ToTable("planet_population");
            entity.HasKey(e => e.PlanetId);

            // The key is the identifier of the body, kept here; the table does not number it.
            entity.Property(e => e.PlanetId).HasColumnName("planet_id").ValueGeneratedNever();
            entity.HasIndex(e => e.OwnerPlayerId);
            entity.Property(e => e.SectorId).HasColumnName("sector_id");
            entity.Property(e => e.OwnerPlayerId).HasColumnName("owner_player_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.AmountFraction).HasColumnName("amount_fraction");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.ToTable(table => table.HasCheckConstraint("ck_planet_population_amount", "amount >= 0"));
        });

        modelBuilder.Entity<CivilizationBalanceEntity>(entity =>
        {
            entity.ToTable("civilization_balance");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.HasIndex(e => new { e.PlayerId, e.ResourceTypeId }).IsUnique();
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.ResourceTypeId).HasColumnName("resource_type_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.AmountFraction).HasColumnName("amount_fraction");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Version).HasColumnName("version");

            // The balance of a currency may go below zero only when the currency carries the flag
            // of the debt in the reference table of the world (task 00024, decision 8). The flag
            // lives in another table, so the database cannot check it here; the rule lives in the
            // economy service, which is the only writer of the balance, and a test holds it.
        });
    }
}
