using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Mail.Configuration;
using Aetheris.Mail.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Mail.Events;

/// <summary>
/// Removes the letters that lay in the bin longer than the time of the settings (task 00060).
/// </summary>
/// <remarks>
/// <para>
/// The work runs as an event of the scheduler, so nothing asks the table of the bin and the
/// letters go when the time comes, and not when a player opens the mail (task 00060, section 3.1,
/// point 4).
/// </para>
/// <para>
/// The handler puts the next event in its place, so the bin is cleaned again and again without a
/// timer that asks the table (documentation/tech/07).
/// </para>
/// </remarks>
public sealed class MailTrashCleanupHandler : IScheduledEventHandler
{
    /// <summary>
    /// The type of the event of the cleanup of the bin.
    /// </summary>
    public const string CleanupEventType = "mail.trash_cleanup";

    /// <summary>
    /// The sector that owns the event: the cleanup of the bin does not belong to a place of the
    /// world, so it takes the first one.
    /// </summary>
    private const int SectorId = 0;

    /// <summary>
    /// The default of the number of the days a letter lies in the bin, when the settings do not
    /// name it yet.
    /// </summary>
    private const long DefaultTrashDays = 7;

    private readonly MailService _mail;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailTrashCleanupHandler"/> class.
    /// </summary>
    /// <param name="mail">The mail of the players.</param>
    /// <param name="scheduler">The scheduler of the events.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The clock of the game.</param>
    public MailTrashCleanupHandler(
        MailService mail,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _mail = mail;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
    }

    /// <inheritdoc />
    public string EventType => CleanupEventType;

    /// <inheritdoc />
    public async Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        await _mail.CleanTrashAsync(cancellationToken).ConfigureAwait(false);

        await ScheduleNextAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts the next cleanup of the bin in place (task 00060).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event is in place.</returns>
    public async Task ScheduleNextAsync(CancellationToken cancellationToken = default)
    {
        int days = (int)_config.GetInt64(MailConfigKeys.TrashDays, DefaultTrashDays);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                SectorId,
                CleanupEventType,
                _clock.GameNow.AddDays(days),
                JsonSerializer.Serialize(new CleanupPayload(_clock.GameNow)),
                $"{CleanupEventType}:{_clock.GameNow.UtcTicks}"),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The body of the event of the cleanup of the bin.
    /// </summary>
    /// <param name="At">The moment of game time the event runs at.</param>
    public sealed record CleanupPayload(DateTimeOffset At);
}
