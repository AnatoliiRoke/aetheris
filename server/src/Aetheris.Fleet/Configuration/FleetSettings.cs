using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Fleet.Configuration;

/// <summary>
/// The parameters of the fleet, read at once from the configuration.
/// </summary>
/// <remarks>
/// A missing or unreadable value falls back to the starting value of its definition in
/// <see cref="FleetConfigKeys.Definitions"/>, so the code never carries its own number.
/// </remarks>
public sealed record FleetSettings
{
    /// <summary>
    /// Gets the least set of ammunition the hold of a hull must take.
    /// </summary>
    public decimal MinimumAmmunition { get; init; }

    /// <summary>
    /// Gets the largest number of the characters of the name of a project.
    /// </summary>
    public int DesignNameMaxLength { get; init; }

    /// <summary>Gets the places of a shipyard queue when the building has no effect yet.</summary>
    public int QueueBaseSlots { get; init; }

    /// <summary>Gets the most places a shipyard queue may have.</summary>
    public int QueueMaxSlots { get; init; }

    /// <summary>Gets the shortest assembly, in game seconds.</summary>
    public long AssemblyMinSeconds { get; init; }

    /// <summary>Gets the longest assembly, in game seconds.</summary>
    public long AssemblyMaxSeconds { get; init; }

    /// <summary>Gets the size that gets the shortest assembly.</summary>
    public decimal SizeMin { get; init; }

    /// <summary>Gets the size that gets the longest assembly.</summary>
    public decimal SizeMax { get; init; }

    /// <summary>Gets how many tonnes count as one slot of size.</summary>
    public decimal MassPerSlot { get; init; }

    /// <summary>Gets the credits of the assembly for one slot.</summary>
    public decimal CreditsPerSlot { get; init; }

    /// <summary>Gets the credits of the assembly for one tonne.</summary>
    public decimal CreditsPerTonne { get; init; }

    /// <summary>Gets the share of the credits spent on the modules that the assembly takes.</summary>
    public decimal CreditShare { get; init; }

    /// <summary>Gets the share of the resources spent on the modules that the assembly takes.</summary>
    public decimal MaterialShare { get; init; }

    /// <summary>Gets the share of the credit price of the fitted modules that a finished ship takes per hour.</summary>
    public decimal UpkeepShare { get; init; }

    /// <summary>Gets the largest number of the characters of the name of a fleet.</summary>
    public int FleetNameMaxLength { get; init; }

    /// <summary>Gets the most orders a flight plan of a fleet may hold.</summary>
    public int FlightPlanMaxOrders { get; init; }

    /// <summary>Gets the length of a cycle of extraction, in game seconds.</summary>
    public int ExtractionCycleSeconds { get; init; }

    /// <summary>Gets how far a fleet may be from the point of a free asteroid field and still be at it, in the units of the map.</summary>
    public double ExtractionPlaceTolerance { get; init; }

    /// <summary>Gets the lifetime of a scan snapshot in game seconds.</summary>
    public long ScanSnapshotLifetimeSeconds { get; init; }

    /// <summary>Gets the push of the reference point of the speed, in kilonewtons.</summary>
    public decimal SpeedReferencePush { get; init; }

    /// <summary>Gets the mass of the reference point of the speed, in tonnes.</summary>
    public decimal SpeedReferenceMass { get; init; }

    /// <summary>Gets the speed of the reference point, in light-years per hour.</summary>
    public decimal SpeedReferenceSpeed { get; init; }

    /// <summary>
    /// Reads the parameters.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <returns>The parameters.</returns>
    public static FleetSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new FleetSettings
        {
            MinimumAmmunition = ReadDecimal(config, FleetConfigKeys.MinimumAmmunition),
            DesignNameMaxLength = ReadInt32(config, FleetConfigKeys.DesignNameMaxLength),
            QueueBaseSlots = ReadInt32(config, FleetConfigKeys.QueueBaseSlots),
            QueueMaxSlots = ReadInt32(config, FleetConfigKeys.QueueMaxSlots),
            AssemblyMinSeconds = ReadInt64(config, FleetConfigKeys.AssemblyMinSeconds),
            AssemblyMaxSeconds = ReadInt64(config, FleetConfigKeys.AssemblyMaxSeconds),
            SizeMin = ReadDecimal(config, FleetConfigKeys.SizeMin),
            SizeMax = ReadDecimal(config, FleetConfigKeys.SizeMax),
            MassPerSlot = ReadDecimal(config, FleetConfigKeys.MassPerSlot),
            CreditsPerSlot = ReadDecimal(config, FleetConfigKeys.CreditsPerSlot),
            CreditsPerTonne = ReadDecimal(config, FleetConfigKeys.CreditsPerTonne),
            CreditShare = ReadDecimal(config, FleetConfigKeys.CreditShare),
            MaterialShare = ReadDecimal(config, FleetConfigKeys.MaterialShare),
            UpkeepShare = ReadDecimal(config, FleetConfigKeys.UpkeepShare),
            FleetNameMaxLength = ReadInt32(config, FleetConfigKeys.FleetNameMaxLength),
            FlightPlanMaxOrders = ReadInt32(config, FleetConfigKeys.FlightPlanMaxOrders),
            ExtractionCycleSeconds = ReadInt32(config, FleetConfigKeys.ExtractionCycleSeconds),
            ExtractionPlaceTolerance = (double)ReadDecimal(config, FleetConfigKeys.ExtractionPlaceTolerance),
            ScanSnapshotLifetimeSeconds = ReadInt64(config, FleetConfigKeys.ScanSnapshotLifetimeSeconds),
            SpeedReferencePush = ReadDecimal(config, FleetConfigKeys.SpeedReferencePush),
            SpeedReferenceMass = ReadDecimal(config, FleetConfigKeys.SpeedReferenceMass),
            SpeedReferenceSpeed = ReadDecimal(config, FleetConfigKeys.SpeedReferenceSpeed),
        };
    }

    private static long ReadInt64(IGameConfig config, string key) =>
        long.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole)
            ? whole
            : long.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static decimal ReadDecimal(IGameConfig config, string key) =>
        decimal.TryParse(config.GetRaw(key), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : decimal.Parse(Default(key), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) =>
        int.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : int.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static string Default(string key) =>
        FleetConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}
