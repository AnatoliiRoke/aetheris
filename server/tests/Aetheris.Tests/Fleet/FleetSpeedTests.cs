using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Services;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the speed and of the time of a flight (task 00053, the formula of task 00050, section 8.2):
/// <c>v = K · √(F / m)</c> with the constant taken from the reference point of the configuration.
/// </summary>
public sealed class FleetSpeedTests
{
    private static FleetSettings Settings => new()
    {
        SpeedReferencePush = 100m,
        SpeedReferenceMass = 1000m,
        SpeedReferenceSpeed = 45m,
    };

    /// <summary>
    /// The reference point of the author: 100 kN on 1000 t give 45 light-years per hour.
    /// </summary>
    [Fact]
    public void The_reference_point_gives_the_reference_speed()
    {
        Assert.Equal(45m, FleetSpeedService.ShipSpeed(100m, 1000m, Settings));
    }

    /// <summary>
    /// The reference point is data: when it changes, every ship changes with it, and the code keeps
    /// no number of its own.
    /// </summary>
    [Fact]
    public void The_reference_point_comes_from_the_settings()
    {
        FleetSettings other = Settings with { SpeedReferenceSpeed = 90m };

        Assert.Equal(90m, FleetSpeedService.ShipSpeed(100m, 1000m, other));

        // A quarter of the push gives half of the speed: the root of the push per tonne.
        Assert.Equal(45m, FleetSpeedService.ShipSpeed(25m, 1000m, other));
    }

    /// <summary>
    /// The «Kopalka» of the author with an empty hold and with a full one: 61.1 and 196.7 minutes for
    /// 53.76 light-years.
    /// </summary>
    [Fact]
    public void The_digger_flies_the_distance_of_the_author_in_the_times_of_the_document()
    {
        decimal empty = FleetSpeedService.ShipSpeed(200m, 1454.3m, Settings);
        decimal full = FleetSpeedService.ShipSpeed(200m, 1454.3m + 13600m, Settings);

        Assert.Equal(52.8m, decimal.Round(empty, 1));
        Assert.Equal(16.4m, decimal.Round(full, 1));

        double emptyMinutes = FleetSpeedService.FlightSeconds(53.76d, empty) / 60d;
        double fullMinutes = FleetSpeedService.FlightSeconds(53.76d, full) / 60d;

        Assert.Equal(61.1d, emptyMinutes, 1);
        Assert.Equal(196.7d, fullMinutes, 1);
    }

    /// <summary>
    /// A fleet flies at the speed of its slowest ship, and a ship that cannot fly grounds the fleet.
    /// </summary>
    [Fact]
    public void A_fleet_flies_at_the_speed_of_its_slowest_ship()
    {
        Assert.Equal(20m, FleetSpeedService.FleetSpeed([45m, 20m, 60m]));
        Assert.Equal(0m, FleetSpeedService.FleetSpeed([45m, 0m]));
        Assert.Equal(0m, FleetSpeedService.FleetSpeed([]));
    }

    /// <summary>
    /// The time is the distance over the speed, rounded up to a second and never below one; a fleet
    /// that cannot fly or has nowhere to go takes none.
    /// </summary>
    [Fact]
    public void The_time_of_a_flight_is_the_distance_over_the_speed()
    {
        Assert.Equal(3600L, FleetSpeedService.FlightSeconds(45d, 45m));
        Assert.Equal(1800L, FleetSpeedService.FlightSeconds(22.5d, 45m));
        Assert.Equal(1L, FleetSpeedService.FlightSeconds(0.0000001d, 45m));
        Assert.Equal(0L, FleetSpeedService.FlightSeconds(0d, 45m));
        Assert.Equal(0L, FleetSpeedService.FlightSeconds(10d, 0m));
    }

    /// <summary>
    /// A ship without a push or without a mass has no speed.
    /// </summary>
    [Fact]
    public void A_ship_without_push_or_mass_has_no_speed()
    {
        Assert.Equal(0m, FleetSpeedService.ShipSpeed(0m, 1000m, Settings));
        Assert.Equal(0m, FleetSpeedService.ShipSpeed(100m, 0m, Settings));
    }

    /// <summary>
    /// The cargo makes a ship slower, but not in proportion to its mass.
    /// </summary>
    [Fact]
    public void The_cargo_makes_a_ship_slower()
    {
        decimal light = FleetSpeedService.ShipSpeed(200m, 1000m, Settings);
        decimal heavy = FleetSpeedService.ShipSpeed(200m, 4000m, Settings);

        Assert.True(heavy < light);
        Assert.Equal(light / 2m, heavy, 4);
    }

    /// <summary>
    /// The configuration holds the reference point and the length of a cycle with their starting
    /// values and a description for the admin panel.
    /// </summary>
    [Fact]
    public void The_configuration_holds_the_reference_point_and_the_cycle()
    {
        Assert.Contains(FleetConfigKeys.Definitions, definition => definition.Key == FleetConfigKeys.ExtractionCycleSeconds && definition.DefaultValue == "900");
        Assert.Contains(FleetConfigKeys.Definitions, definition => definition.Key == FleetConfigKeys.SpeedReferencePush && definition.DefaultValue == "100");
        Assert.Contains(FleetConfigKeys.Definitions, definition => definition.Key == FleetConfigKeys.SpeedReferenceMass && definition.DefaultValue == "1000");
        Assert.Contains(FleetConfigKeys.Definitions, definition => definition.Key == FleetConfigKeys.SpeedReferenceSpeed && definition.DefaultValue == "45");
    }
}
