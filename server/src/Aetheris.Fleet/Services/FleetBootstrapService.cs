using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Planets;
using Aetheris.Production;
using Aetheris.Requirements;

namespace Aetheris.Fleet.Services;

/// <summary>
/// Writes the hull levels a shipyard opens, once, from the ladder of the hulls (task 00039).
/// </summary>
/// <remarks>
/// A row that already names the shipyard is left as it is. A deleted row is written again on
/// the next start. A hull the catalog does not have is skipped: the ladder is not invented.
/// </remarks>
public sealed class FleetBootstrapService
{
    private const string LadderResource = "Aetheris.Fleet.hull-shipyard-levels.json";

    private readonly IProductionModule _production;
    private readonly IPlanetsModule _planets;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetBootstrapService"/> class.
    /// </summary>
    /// <param name="production">The catalog of the parts.</param>
    /// <param name="planets">The buildings, for the shipyard.</param>
    /// <param name="requirements">The requirements of the hulls.</param>
    public FleetBootstrapService(IProductionModule production, IPlanetsModule planets, IRequirementsModule requirements)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(requirements);

        _production = production;
        _planets = planets;
        _requirements = requirements;
    }

    /// <summary>
    /// Adds the missing rows. A repeated call changes nothing that is already there.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many rows were added, and the codes of the ladder that have no part.</returns>
    public async Task<FleetBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default)
    {
        int? shipyardTypeId = await _planets.FindBuildingTypeIdByClassAsync(BuildingClasses.Shipyard, cancellationToken).ConfigureAwait(false);

        if (shipyardTypeId is not int buildingTypeId)
        {
            return new FleetBootstrapResult(0, []);
        }

        HullLadder ladder = Load();
        IReadOnlyDictionary<string, int> partIds = await _production.FindPartIdsByFileCodeAsync(
            [.. ladder.Hulls.Select(hull => hull.Code).Where(code => !string.IsNullOrEmpty(code))],
            cancellationToken).ConfigureAwait(false);
        int added = 0;
        List<string> missing = [];

        foreach (HullLevel hull in ladder.Hulls)
        {
            if (string.IsNullOrEmpty(hull.Code) || !partIds.TryGetValue(hull.Code, out int partTypeId))
            {
                if (!string.IsNullOrEmpty(hull.Code))
                {
                    missing.Add(hull.Code);
                }

                continue;
            }

            if (await _requirements.EnsureModuleBuildingLevelAsync(partTypeId, buildingTypeId, hull.ShipyardLevel, cancellationToken).ConfigureAwait(false))
            {
                added++;
            }
        }

        return new FleetBootstrapResult(added, missing);
    }

    private static HullLadder Load()
    {
        using Stream stream = typeof(FleetBootstrapService).Assembly.GetManifestResourceStream(LadderResource)
            ?? throw new InvalidOperationException($"The assembly has no resource '{LadderResource}'.");

        return JsonSerializer.Deserialize<HullLadder>(stream)
            ?? throw new InvalidOperationException($"The resource '{LadderResource}' is empty.");
    }

    private sealed class HullLadder
    {
        [JsonPropertyName("hulls")]
        public List<HullLevel> Hulls { get; set; } = [];
    }

    private sealed class HullLevel
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("shipyard_level")]
        public int ShipyardLevel { get; set; }
    }
}

/// <summary>
/// What the preparation of the fleet wrote (task 00039).
/// </summary>
/// <param name="HullRequirementsAdded">How many hull rows were added.</param>
/// <param name="MissingHullCodes">The codes of the ladder that have no part in the catalog.</param>
public sealed record FleetBootstrapResult(int HullRequirementsAdded, IReadOnlyList<string> MissingHullCodes);
