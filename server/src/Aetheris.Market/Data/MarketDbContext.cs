using Aetheris.Market.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Data;

/// <summary>
/// The tables of the market module: the levels of the market, the reference prices, the lots, the
/// deals, the deliveries, the exchanges and the history of the rate (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// The bodies, the resources, the buildings and the parts belong to the other modules, so this
/// module keeps only their identifiers and reads them through the contracts of the other
/// modules, and its tables carry no foreign keys into the tables of the other modules. Names are
/// snake_case, and a reference to one row ends with <c>_id</c> (documentation/tech/08, rule 3).
/// </remarks>
public sealed class MarketDbContext : DbContext
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
    /// The length of the identifier of a player.
    /// </summary>
    private const int PlayerIdLength = 450;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDbContext"/> class.
    /// </summary>
    /// <param name="options">The database options.</param>
    public MarketDbContext(DbContextOptions<MarketDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the levels of the market.</summary>
    public DbSet<MarketLevelEntity> MarketLevels => Set<MarketLevelEntity>();

    /// <summary>Gets the reference prices of the items.</summary>
    public DbSet<ReferencePriceEntity> ReferencePrices => Set<ReferencePriceEntity>();

    /// <summary>Gets the lots of the market.</summary>
    public DbSet<MarketLotEntity> MarketLots => Set<MarketLotEntity>();

    /// <summary>Gets the deals of the market.</summary>
    public DbSet<MarketDealEntity> MarketDeals => Set<MarketDealEntity>();

    /// <summary>Gets the deliveries of the market.</summary>
    public DbSet<MarketDeliveryEntity> MarketDeliveries => Set<MarketDeliveryEntity>();

    /// <summary>Gets the exchanges of the market.</summary>
    public DbSet<MarketExchangeEntity> MarketExchanges => Set<MarketExchangeEntity>();

    /// <summary>Gets the history of the rate of the exchange.</summary>
    public DbSet<MarketRateEntity> MarketRates => Set<MarketRateEntity>();

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
        modelBuilder.Entity<MarketLevelEntity>(entity =>
        {
            entity.ToTable("market_level");
            entity.Property(e => e.Code).HasMaxLength(CodeLength);
            entity.Property(e => e.Visibility).HasMaxLength(CodeLength);
            entity.Property(e => e.NameKey).HasMaxLength(NameLength);
            entity.Property(e => e.DescriptionKey).HasMaxLength(NameLength);
            entity.Property(e => e.ImageKey).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_market_level_commission", "commission_percent >= 0"));
        });

        modelBuilder.Entity<ReferencePriceEntity>(entity =>
        {
            entity.ToTable("market_reference_price");
            entity.HasIndex(e => e.ResourceTypeId);
            entity.HasIndex(e => e.PartTypeId);
            entity.HasIndex(e => new { e.ResourceTypeId, e.PartTypeId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_market_reference_price_price", "price >= 0"));
        });
    }

    private static void ConfigureGameTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketLotEntity>(entity =>
        {
            entity.ToTable("market_lot");
            entity.Property(e => e.SellerPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.State).HasMaxLength(CodeLength);
            entity.HasIndex(e => new { e.State, e.MarketLevelId });
            entity.HasIndex(e => e.SellerPlayerId);
            entity.HasIndex(e => e.ResourceTypeId);
            entity.HasIndex(e => e.PartTypeId);
            entity.ToTable(table => table.HasCheckConstraint("ck_market_lot_quantity", "quantity > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_market_lot_price", "price_per_unit >= 0"));
        });

        modelBuilder.Entity<MarketDealEntity>(entity =>
        {
            entity.ToTable("market_deal");
            entity.Property(e => e.SellerPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.BuyerPlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => e.LotId);
            entity.HasIndex(e => e.SellerPlayerId);
            entity.HasIndex(e => e.BuyerPlayerId);
            entity.HasIndex(e => e.CreatedAt);
            entity.ToTable(table => table.HasCheckConstraint("ck_market_deal_quantity", "quantity > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_market_deal_sum", "sum >= 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_market_deal_commission", "commission >= 0"));
        });

        modelBuilder.Entity<MarketDeliveryEntity>(entity =>
        {
            entity.ToTable("market_delivery");
            entity.Property(e => e.BuyerPlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.State).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.DealId);
            entity.HasIndex(e => new { e.BuyerPlayerId, e.State });
            entity.HasIndex(e => e.ArrivesAt);
            entity.ToTable(table => table.HasCheckConstraint("ck_market_delivery_quantity", "quantity > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_market_delivery_energy_rods", "energy_rod_amount >= 0"));
        });

        modelBuilder.Entity<MarketExchangeEntity>(entity =>
        {
            entity.ToTable("market_exchange");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.CreatedAt);
            entity.ToTable(table => table.HasCheckConstraint("ck_market_exchange_from_amount", "from_amount > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_market_exchange_to_amount", "to_amount > 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_market_exchange_rate", "rate > 0"));
        });

        modelBuilder.Entity<MarketRateEntity>(entity =>
        {
            entity.ToTable("market_rate");
            entity.HasIndex(e => e.RecordedAt);
            entity.ToTable(table => table.HasCheckConstraint("ck_market_rate_rate", "rate > 0"));
        });
    }
}
