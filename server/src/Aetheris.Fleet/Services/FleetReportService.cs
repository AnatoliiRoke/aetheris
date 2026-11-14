using System.Text.Json;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Mail;
using Aetheris.Mail.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The report of a fleet (task 00060): what the fleet took from a place, written to the player as a
/// letter of the game.
/// </summary>
/// <remarks>
/// <para>
/// The report is the first source of the letters of the game: the modules of the game tell what
/// happened to the fleets of the player through the contract of the mail, and the mail writes the
/// letter (documentation/tech/01, section 8).
/// </para>
/// <para>
/// The letter carries the key of its text and the parameters of it, so the text is read from the
/// texts of the game and may be translated (task 00060, section 3.1, point 5).
/// </para>
/// </remarks>
public sealed class FleetReportService
{
    /// <summary>
    /// The key of the text of the report of a fleet in the texts of the game.
    /// </summary>
    public const string ReportTextKey = "mail.fleet.extraction_report";

    /// <summary>
    /// The key of the text of the report of a colonization (task 00073).
    /// </summary>
    public const string ColonizationReportTextKey = "mail.fleet.colonized";

    /// <summary>
    /// The kind of the letter of the report of a colonization (task 00073), so the client may draw
    /// it with a mark of its own.
    /// </summary>
    public const string ColonizationReportKindCode = "fleet_colonized";

    /// <summary>
    /// The kind of the letter of the report of a fleet, so the client may draw it with a mark of its
    /// own.
    /// </summary>
    public const string ReportKindCode = "fleet_report";

    /// <summary>
    /// The key of the text of the report of a load that was short of what the order named (author's
    /// rule, 14.11.2026).
    /// </summary>
    public const string LoadShortReportTextKey = "mail.fleet.load_short";

    /// <summary>
    /// The kind of the letter of the report of a short load, so the client may draw the list of what
    /// was not loaded.
    /// </summary>
    public const string LoadShortReportKindCode = "fleet_load_short";

    private readonly FleetDbContext _dbContext;
    private readonly IMailModule _mail;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetReportService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the fleet module.</param>
    /// <param name="mail">The mail of the players.</param>
    /// <param name="world">The world, for the names of the resources.</param>
    public FleetReportService(FleetDbContext dbContext, IMailModule mail, IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _mail = mail;
        _world = world;
    }

    /// <summary>Writes the completed scan report to its owner.</summary>
    /// <param name="playerId">The owner of the scan.</param>
    /// <param name="result">The recorded data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the work.</returns>
    public async Task WriteScanReportAsync(string playerId, ScanSnapshotResult result, CancellationToken cancellationToken)
    {
        ILookup<long?, ScanObjectResult> bySystem = result.Objects.ToLookup(obj => obj.SystemId);
        string parameters = JsonSerializer.Serialize(new
        {
            snapshot_id = result.Id,
            sector_id = result.SectorId,
            sector_name = result.SectorName,
            x = result.X,
            y = result.Y,
            z = result.Z,
            radius = result.Radius,
            scanned_at = result.ScannedAt,
            systems = result.Systems.Select(system => new
            {
                id = system.Id,
                sector_id = system.SectorId,
                name = system.Name,
                has_deposits = bySystem[system.Id].Any(obj => obj.HasDeposits),
                has_gas_giants = bySystem[system.Id].Any(obj => obj.ObjectClass == WorldObjectClasses.GasGiant),
                objects = bySystem[system.Id].Where(obj => obj.PlanetId is not null || obj.ObjectClass == WorldObjectClasses.AsteroidField),
            }),
            free_fields = bySystem[null].Where(obj => obj.ObjectClass == WorldObjectClasses.AsteroidField),
        }, WorldJson.Options);
        long? id = await _mail.SendSystemAsync(playerId, MailCategoryCodes.Report, "mail.scan_report", parameters, "scan_report", cancellationToken).ConfigureAwait(false);
        if (id is null) throw new InvalidOperationException("The scan report category is unavailable.");
    }

    /// <summary>
    /// Writes the report of a fleet to the player that owns it (task 00060).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="yields">The resources the fleet took.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the report was written.</returns>
    public async Task<bool> WriteExtractionReportAsync(
        long fleetId,
        IReadOnlyList<FleetYieldDto> yields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(yields);

        if (yields.Count == 0)
        {
            return false;
        }

        FleetEntity? fleet = await _dbContext.Fleets.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == fleetId, cancellationToken)
            .ConfigureAwait(false);

        if (fleet is null)
        {
            return false;
        }

        // The names of the resources come from the world, so the fleet does not know them.
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, string> names = catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.NameKey);

        List<FleetYieldDto> sorted = [.. yields.OrderBy(yield => yield.ResourceId)];

        string parameters = JsonSerializer.Serialize(new
        {
            fleet_name = fleet.Name,
            yields = sorted.Select(yield => new
            {
                resource_id = yield.ResourceId,
                name_key = names.GetValueOrDefault(yield.ResourceId, string.Empty),
                amount = yield.Amount,
            }),
        });

        long? messageId = await _mail.SendSystemAsync(
            fleet.PlayerId,
            MailCategoryCodes.Report,
            ReportTextKey,
            parameters,
            ReportKindCode,
            cancellationToken).ConfigureAwait(false);

        return messageId is not null;
    }

    /// <summary>
    /// Writes the report of a load that was short of what the order named (author's rule, 14.11.2026):
    /// for every item what was asked, what was loaded and why the rest was not. The load itself went
    /// on, so the report only tells the player.
    /// </summary>
    /// <param name="fleet">The fleet that loaded.</param>
    /// <param name="shortfalls">The items loaded short.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the report was written.</returns>
    public async Task<bool> WriteLoadShortReportAsync(
        FleetEntity fleet,
        IReadOnlyList<CargoShortfall> shortfalls,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(shortfalls);

        if (shortfalls.Count == 0)
        {
            return false;
        }

        // The names of the resources come from the world; the names of the parts come with the shortfalls.
        Dictionary<int, string> resourceNames = [];

        if (shortfalls.Any(entry => entry.Kind == CargoItemKinds.Resource))
        {
            ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
            resourceNames = catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.NameKey);
        }

        PlanetFacts? planet = await _world.GetPlanetFactsAsync(fleet.PlanetId, cancellationToken).ConfigureAwait(false);

        string parameters = JsonSerializer.Serialize(new
        {
            fleet_id = fleet.Id,
            fleet_name = fleet.Name,
            planet_id = fleet.PlanetId,
            planet_name = planet?.Name,
            items = shortfalls.Select(entry => new
            {
                kind = entry.Kind,
                id = entry.Id,
                mark_level = entry.MarkLevel,
                has_race_bonus = entry.HasRaceBonus,
                name_key = entry.Kind == CargoItemKinds.Resource ? resourceNames.GetValueOrDefault(entry.Id, string.Empty) : entry.NameKey ?? string.Empty,
                requested = entry.Requested,
                loaded = entry.Loaded,
                reason_code = entry.ReasonCode,
            }),
        });

        long? messageId = await _mail.SendSystemAsync(
            fleet.PlayerId,
            MailCategoryCodes.Report,
            LoadShortReportTextKey,
            parameters,
            LoadShortReportKindCode,
            cancellationToken).ConfigureAwait(false);

        return messageId is not null;
    }

    /// <summary>
    /// Writes the report of a colonization to the player that owns the fleet (task 00073).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="planetName">The name of the new colony.</param>
    /// <param name="inhabitants">How many people the colony has.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the report was written.</returns>
    public async Task<bool> WriteColonizationReportAsync(
        long fleetId,
        string planetName,
        long inhabitants,
        CancellationToken cancellationToken = default)
    {
        FleetEntity? fleet = await _dbContext.Fleets.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == fleetId, cancellationToken)
            .ConfigureAwait(false);

        if (fleet is null)
        {
            return false;
        }

        string parameters = JsonSerializer.Serialize(new
        {
            fleet_name = fleet.Name,
            planet_name = planetName,
            inhabitants,
        });

        long? messageId = await _mail.SendSystemAsync(
            fleet.PlayerId,
            MailCategoryCodes.Report,
            ColonizationReportTextKey,
            parameters,
            ColonizationReportKindCode,
            cancellationToken).ConfigureAwait(false);

        return messageId is not null;
    }
}
