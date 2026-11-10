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
    /// The length of a state or a condition code.
    /// </summary>
    private const int CodeLength = 32;

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

    /// <summary>Gets the lock rows of the shipyard queues.</summary>
    public DbSet<ShipyardQueueStateEntity> ShipyardQueueStates => Set<ShipyardQueueStateEntity>();

    /// <summary>Gets the records of the shipyard queues.</summary>
    public DbSet<ShipyardOrderEntity> ShipyardOrders => Set<ShipyardOrderEntity>();

    /// <summary>Gets the costs of the shipyard records.</summary>
    public DbSet<ShipyardOrderCostEntity> ShipyardOrderCosts => Set<ShipyardOrderCostEntity>();

    /// <summary>Gets the parts of the shipyard records.</summary>
    public DbSet<ShipyardOrderPartEntity> ShipyardOrderParts => Set<ShipyardOrderPartEntity>();

    /// <summary>Gets the finished ships.</summary>
    public DbSet<ShipEntity> Ships => Set<ShipEntity>();

    /// <summary>Gets the parts of the finished ships.</summary>
    public DbSet<ShipModuleEntity> ShipModules => Set<ShipModuleEntity>();

    /// <summary>Gets the costs copied onto the finished ships.</summary>
    public DbSet<ShipCostEntity> ShipCosts => Set<ShipCostEntity>();

    /// <summary>Gets the fleets (task 00040).</summary>
    public DbSet<FleetEntity> Fleets => Set<FleetEntity>();

    /// <summary>Gets the ships of the fleets (task 00040).</summary>
    public DbSet<FleetShipEntity> FleetShips => Set<FleetShipEntity>();

    /// <summary>Gets the lock rows of the fleets of the players (task 00040).</summary>
    public DbSet<FleetPlayerStateEntity> FleetPlayerStates => Set<FleetPlayerStateEntity>();

    /// <summary>Gets the flight plans of the fleets (task 00050).</summary>
    public DbSet<FlightPlanEntity> FlightPlans => Set<FlightPlanEntity>();

    /// <summary>Gets the orders of the flight plans (task 00050).</summary>
    public DbSet<FlightPlanOrderEntity> FlightPlanOrders => Set<FlightPlanOrderEntity>();

    /// <summary>Gets the cargo of the ships (task 00050).</summary>
    public DbSet<ShipCargoEntity> ShipCargos => Set<ShipCargoEntity>();

    /// <summary>Gets the cargo of the fleets (task 00050).</summary>
    public DbSet<FleetCargoEntity> FleetCargos => Set<FleetCargoEntity>();

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

        modelBuilder.Entity<ShipyardQueueStateEntity>(entity =>
        {
            entity.ToTable("shipyard_queue_state");
            entity.HasKey(e => e.PlanetId);
            entity.Property(e => e.PlanetId).ValueGeneratedNever();
        });

        modelBuilder.Entity<ShipyardOrderEntity>(entity =>
        {
            entity.ToTable("shipyard_order");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.HasIndex(e => new { e.PlanetId, e.Position }).IsUnique();
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.ShipDesignId);
            entity.HasOne<ShipDesignEntity>().WithMany().HasForeignKey(e => e.ShipDesignId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_position", "position >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_duration", "base_duration_seconds >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_crew", "crew >= 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_quantity", "quantity >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_produced", "produced_quantity >= 0"));
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_produced_within", "produced_quantity <= quantity"));
        });

        modelBuilder.Entity<ShipyardOrderCostEntity>(entity =>
        {
            entity.ToTable("shipyard_order_cost");
            entity.HasIndex(e => new { e.OrderId, e.ResourceTypeId }).IsUnique();
            entity.HasOne<ShipyardOrderEntity>().WithMany(e => e.CostIds).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<ShipyardOrderPartEntity>(entity =>
        {
            entity.ToTable("shipyard_order_part");
            entity.HasIndex(e => e.OrderId);
            entity.HasOne<ShipyardOrderEntity>().WithMany(e => e.PartIds).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_shipyard_order_part_quantity", "quantity >= 0"));
        });

        modelBuilder.Entity<ShipEntity>(entity =>
        {
            entity.ToTable("ship");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.StateCode).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => new { e.PlayerId, e.PlanetId });
            entity.HasIndex(e => e.ShipDesignId);
            entity.HasOne<ShipDesignEntity>().WithMany().HasForeignKey(e => e.ShipDesignId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_ship_crew", "crew >= 0"));
        });

        modelBuilder.Entity<ShipModuleEntity>(entity =>
        {
            entity.ToTable("ship_module");
            entity.Property(e => e.ConditionCode).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.ShipId);
            entity.HasOne<ShipEntity>().WithMany(e => e.ModuleIds).HasForeignKey(e => e.ShipId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_ship_module_quantity", "quantity >= 0"));
        });

        modelBuilder.Entity<ShipCostEntity>(entity =>
        {
            entity.ToTable("ship_cost");
            entity.HasIndex(e => new { e.ShipId, e.ResourceTypeId }).IsUnique();
            entity.HasOne<ShipEntity>().WithMany(e => e.CostIds).HasForeignKey(e => e.ShipId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_ship_cost_amount", "amount >= 0"));
        });

        modelBuilder.Entity<FleetPlayerStateEntity>(entity =>
        {
            entity.ToTable("fleet_player_state");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength).ValueGeneratedNever();
        });

        modelBuilder.Entity<FleetEntity>(entity =>
        {
            entity.ToTable("fleet");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.Name).HasMaxLength(NameLength);
            entity.Property(e => e.StateCode).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => new { e.PlayerId, e.PlanetId });
            entity.HasIndex(e => e.HomePlanetId);
        });

        modelBuilder.Entity<FleetShipEntity>(entity =>
        {
            entity.ToTable("fleet_ship");
            entity.HasIndex(e => e.ShipId).IsUnique();
            entity.HasIndex(e => new { e.FleetId, e.Position }).IsUnique();
            entity.HasOne<FleetEntity>().WithMany(e => e.ShipIds).HasForeignKey(e => e.FleetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ShipEntity>().WithMany().HasForeignKey(e => e.ShipId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("ck_fleet_ship_position", "position >= 1"));
        });

        modelBuilder.Entity<FlightPlanEntity>(entity =>
        {
            entity.ToTable("flight_plan");
            entity.Property(e => e.PlayerId).HasMaxLength(PlayerIdLength);
            entity.Property(e => e.StateCode).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.PlayerId);
            entity.HasIndex(e => e.FleetId).IsUnique();
            entity.HasOne<FleetEntity>().WithMany().HasForeignKey(e => e.FleetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlightPlanOrderEntity>(entity =>
        {
            entity.ToTable("flight_plan_order");
            entity.Property(e => e.TypeCode).HasMaxLength(CodeLength);
            entity.Property(e => e.TargetKind).HasMaxLength(CodeLength);
            entity.HasIndex(e => e.FlightPlanId);
            entity.HasIndex(e => new { e.FlightPlanId, e.Position }).IsUnique();
            entity.HasOne<FlightPlanEntity>().WithMany(e => e.OrderIds).HasForeignKey(e => e.FlightPlanId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_flight_plan_order_position", "position >= 1"));
            entity.ToTable(table => table.HasCheckConstraint("ck_flight_plan_order_duration", "duration_seconds >= 0"));
        });

        modelBuilder.Entity<ShipCargoEntity>(entity =>
        {
            entity.ToTable("ship_cargo");
            entity.HasIndex(e => e.ShipId);
            entity.HasIndex(e => new { e.ShipId, e.ResourceId }).IsUnique();
            entity.HasOne<ShipEntity>().WithMany().HasForeignKey(e => e.ShipId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_ship_cargo_amount", "amount >= 0"));
        });

        modelBuilder.Entity<FleetCargoEntity>(entity =>
        {
            entity.ToTable("fleet_cargo");
            entity.HasIndex(e => e.FleetId);
            entity.HasIndex(e => new { e.FleetId, e.ResourceId }).IsUnique();
            entity.HasOne<FleetEntity>().WithMany().HasForeignKey(e => e.FleetId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("ck_fleet_cargo_amount", "amount >= 0"));
        });
    }
}
