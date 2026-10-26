using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.World;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// The deletion of an account: on request with a delay, and for an account whose address
/// was not confirmed in time (task 00006, decisions 3 and 10).
/// </summary>
/// <remarks>
/// A requested deletion waits <c>security.account.deletion_days</c>; a sign-in within that
/// time cancels it. The deletion itself releases what the account holds in the world, so its
/// start body becomes free again, and removes the account with its personal data.
/// </remarks>
public sealed class AccountLifecycle
{
    /// <summary>The scheduled event of a requested deletion.</summary>
    public const string DeletionEventType = "account.deletion_due";

    private readonly AetherisDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameClock _clock;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountLifecycle"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="userManager">The account manager.</param>
    /// <param name="world">The world module.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock of the real time.</param>
    public AccountLifecycle(
        AetherisDbContext dbContext,
        UserManager<IdentityUser> userManager,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameClock clock,
        SecuritySettings settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _userManager = userManager;
        _world = world;
        _scheduler = scheduler;
        _clock = clock;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Requests the deletion of an account.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The moment the account will be deleted.</returns>
    public async Task<DateTimeOffset> RequestDeletionAsync(string userId, CancellationToken cancellationToken)
    {
        PlayerProfileEntity profile = await GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);

        // The database keeps microseconds; the moment is cut to them, so the event payload
        // compares equal to the stored value.
        DateTimeOffset exact = _timeProvider.GetUtcNow();
        DateTimeOffset now = exact.AddTicks(-(exact.UtcTicks % TimeSpan.TicksPerMicrosecond));

        if (profile.DeletionDueAt is { } pending)
        {
            return pending;
        }

        profile.DeletionRequestedAt = now;
        profile.DeletionDueAt = now + _settings.DeletionDelay;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                RegistrationService.AccountEventSector,
                DeletionEventType,
                _clock.GameNow + _settings.DeletionDelay,
                JsonSerializer.Serialize(new DeletionEventPayload(userId, now)),
                $"{DeletionEventType}:{userId}:{now.UtcTicks}"),
            cancellationToken).ConfigureAwait(false);

        return profile.DeletionDueAt.Value;
    }

    /// <summary>
    /// Cancels a pending deletion.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when a deletion was pending and is now cancelled.</returns>
    public async Task<bool> CancelDeletionAsync(string userId, CancellationToken cancellationToken)
    {
        int changed = await _dbContext.PlayerProfiles
            .Where(entry => entry.UserId == userId && entry.DeletionDueAt != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.DeletionDueAt, (DateTimeOffset?)null)
                    .SetProperty(entry => entry.DeletionRequestedAt, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);

        return changed > 0;
    }

    /// <summary>
    /// Deletes an account now, with what it holds in the world.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the account existed.</returns>
    /// <remarks>It runs in the transaction of the caller, for example the one of a scheduled event.</remarks>
    public async Task<bool> PurgeAsync(string userId, CancellationToken cancellationToken)
    {
        IdentityUser? user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return false;
        }

        await _world.ReleasePlayerAsync(userId, cancellationToken).ConfigureAwait(false);

        await _dbContext.RefreshTokens.Where(entry => entry.UserId == userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.AccountAccessLog.Where(entry => entry.UserId == userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.RequestIdempotency.Where(entry => entry.UserId == userId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        IdentityResult deleted = await _userManager.DeleteAsync(user).ConfigureAwait(false);

        if (!deleted.Succeeded)
        {
            throw new InvalidOperationException($"The account {userId} could not be deleted.");
        }

        return true;
    }

    /// <summary>
    /// Deletes an account whose address is still not confirmed.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the account is handled.</returns>
    public async Task ExpireUnconfirmedAsync(string userId, CancellationToken cancellationToken)
    {
        bool unconfirmed = await _dbContext.Users
            .AnyAsync(user => user.Id == userId && !user.EmailConfirmed, cancellationToken)
            .ConfigureAwait(false);

        if (unconfirmed)
        {
            await PurgeAsync(userId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes an account whose deletion request is still pending.
    /// </summary>
    /// <param name="payload">The payload of the deletion event.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the account is handled.</returns>
    /// <remarks>A cancelled request, or a newer one, leaves the account alone.</remarks>
    public async Task CompleteDeletionAsync(DeletionEventPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        bool pending = await _dbContext.PlayerProfiles
            .AnyAsync(entry => entry.UserId == payload.UserId && entry.DeletionRequestedAt == payload.RequestedAt, cancellationToken)
            .ConfigureAwait(false);

        if (pending)
        {
            await PurgeAsync(payload.UserId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PlayerProfileEntity> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        PlayerProfileEntity? profile = await _dbContext.PlayerProfiles
            .SingleOrDefaultAsync(entry => entry.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            // An account created before task 00006 has no profile yet.
            profile = new PlayerProfileEntity { UserId = userId, RegisteredAt = _timeProvider.GetUtcNow() };
            _dbContext.PlayerProfiles.Add(profile);
        }

        return profile;
    }
}

/// <summary>
/// The payload of a deletion event.
/// </summary>
/// <param name="UserId">The account.</param>
/// <param name="RequestedAt">The moment of the request the event belongs to.</param>
public sealed record DeletionEventPayload(
    [property: System.Text.Json.Serialization.JsonPropertyName("user_id")] string UserId,
    [property: System.Text.Json.Serialization.JsonPropertyName("requested_at")] DateTimeOffset RequestedAt);

/// <summary>
/// Removes an account whose address was not confirmed in time.
/// </summary>
public sealed class UnconfirmedAccountExpiryHandler : IScheduledEventHandler
{
    private readonly AccountLifecycle _lifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnconfirmedAccountExpiryHandler"/> class.
    /// </summary>
    /// <param name="lifecycle">The account lifecycle.</param>
    public UnconfirmedAccountExpiryHandler(AccountLifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);

        _lifecycle = lifecycle;
    }

    /// <inheritdoc />
    public string EventType => RegistrationService.UnconfirmedExpiryEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        AccountEventPayload payload = JsonSerializer.Deserialize<AccountEventPayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException("The payload of the account event is empty.");

        return _lifecycle.ExpireUnconfirmedAsync(payload.UserId, cancellationToken);
    }
}

/// <summary>
/// Removes an account at the end of the delay of its deletion request.
/// </summary>
public sealed class AccountDeletionHandler : IScheduledEventHandler
{
    private readonly AccountLifecycle _lifecycle;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountDeletionHandler"/> class.
    /// </summary>
    /// <param name="lifecycle">The account lifecycle.</param>
    public AccountDeletionHandler(AccountLifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);

        _lifecycle = lifecycle;
    }

    /// <inheritdoc />
    public string EventType => AccountLifecycle.DeletionEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        DeletionEventPayload payload = JsonSerializer.Deserialize<DeletionEventPayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException("The payload of the deletion event is empty.");

        return _lifecycle.CompleteDeletionAsync(payload, cancellationToken);
    }
}
