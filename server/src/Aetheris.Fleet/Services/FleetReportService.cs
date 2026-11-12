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
    /// The kind of the letter of the report of a fleet, so the client may draw it with a mark of its
    /// own.
    /// </summary>
    public const string ReportKindCode = "fleet_report";

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
}
