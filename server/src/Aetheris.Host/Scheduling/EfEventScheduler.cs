using Aetheris.Core.Scheduling;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Scheduling;

/// <summary>
/// Stores scheduled events in the <c>scheduled_event</c> table.
/// </summary>
public sealed class EfEventScheduler : IEventScheduler
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfEventScheduler"/> class.
    /// </summary>
    /// <param name="dbContext">The main database context of the request.</param>
    public EfEventScheduler(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> ScheduleAsync(ScheduledEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Type);

        if (request.DedupeKey is not null
            && await _dbContext.ScheduledEvents.AnyAsync(
                scheduled => scheduled.DedupeKey == request.DedupeKey && scheduled.Status == ScheduledEventStatus.Pending,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        _dbContext.ScheduledEvents.Add(new ScheduledEventEntity
        {
            SectorId = request.SectorId,
            Type = request.Type,
            RunAt = request.RunAt,
            Payload = request.Payload,
            DedupeKey = request.DedupeKey,
            Status = ScheduledEventStatus.Pending,
            Version = 1,
        });

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
