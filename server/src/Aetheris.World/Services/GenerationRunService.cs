using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Aetheris.World.Services;

/// <summary>
/// The generation runs of the admin panel: the estimate before the confirmation, the start,
/// the work in the background and the history (task 00007, decision 3).
/// </summary>
/// <remarks>
/// <para>
/// The start only stores the run and schedules an event; the scheduler picks it up and
/// <see cref="ExecuteAsync"/> creates the sectors one by one through
/// <see cref="WorldGenerationService.AddSectorAsync"/>, the same command the admin panel had
/// since task 00004. Only one run is queued or running at a time: a unique index on
/// <c>generation_run.active_lock</c> refuses a second one, even from two servers.
/// </para>
/// <para>
/// The work runs in its own service scope, outside the transaction of the scheduler, so the
/// status is visible while the sectors are created and a failure keeps the sectors created
/// before it.
/// </para>
/// </remarks>
public sealed class GenerationRunService
{
    /// <summary>
    /// The scheduled event type of a generation run.
    /// </summary>
    public const string EventType = "world.generation_run";

    /// <summary>
    /// The sector of the scheduled event, which belongs to no sector of the map.
    /// </summary>
    public const int EventSector = 0;

    /// <summary>
    /// The number of the bounds whose average gives the expected number of systems of a sector.
    /// </summary>
    private const int BoundCount = 2;

    /// <summary>
    /// The PostgreSQL error code of a unique violation.
    /// </summary>
    private const string UniqueViolation = PostgresErrorCodes.UniqueViolation;

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly IEventScheduler _scheduler;
    private readonly IGameClock _clock;
    private readonly IGameConfig _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GenerationRunService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationRunService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="scopeFactory">Creates the scope of the work in the background.</param>
    /// <param name="timeProvider">The clock of the server.</param>
    /// <param name="logger">The logger.</param>
    public GenerationRunService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        IEventScheduler scheduler,
        IGameClock clock,
        IGameConfig config,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<GenerationRunService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _scheduler = scheduler;
        _clock = clock;
        _config = config;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Tells what a run of the given size would create, for the confirmation screen.
    /// </summary>
    /// <param name="sectorCount">The number of sectors.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The estimate.</returns>
    public async Task<GenerationEstimateDto> EstimateAsync(int sectorCount, CancellationToken cancellationToken)
    {
        WorldSettings settings = WorldSettings.From(_config);
        int estimated = EstimateSystems(settings, sectorCount);

        long? activeRunId = await _dbContext.GenerationRuns.AsNoTracking()
            .Where(run => run.ActiveLock == true)
            .Select(run => (long?)run.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        int groupCount = await _dbContext.RuleGroups.CountAsync(cancellationToken).ConfigureAwait(false);
        int ruleCount = await _dbContext.Rules.CountAsync(cancellationToken).ConfigureAwait(false);

        return new GenerationEstimateDto(
            sectorCount,
            estimated,
            settings.SystemsMin,
            settings.SystemsMax,
            settings.MaxSectorsPerRun,
            settings.MaxSystemsPerRun,
            FitsLimits(settings, sectorCount, estimated),
            activeRunId,
            settings.StatusPollSeconds,
            [new GenerationRuleSetDto(GenerationRuleSets.Current, groupCount, ruleCount)]);
    }

    /// <summary>
    /// Stores a run and schedules its work.
    /// </summary>
    /// <param name="request">The run.</param>
    /// <param name="initiatorId">The administrator.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The queued run.</returns>
    /// <exception cref="GameException">The run is too large, the rule set is unknown, or another run is active.</exception>
    public async Task<GenerationRunDto> StartAsync(StartGenerationRequest request, string initiatorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(initiatorId);

        string ruleSet = string.IsNullOrWhiteSpace(request.RuleSet) ? GenerationRuleSets.Current : request.RuleSet.Trim();

        if (!string.Equals(ruleSet, GenerationRuleSets.Current, StringComparison.Ordinal))
        {
            throw new GameException(WorldErrorCodes.RuleSetUnknown, $"The rule set '{ruleSet}' does not exist.");
        }

        WorldSettings settings = WorldSettings.From(_config);
        int estimated = EstimateSystems(settings, request.SectorCount);

        if (!FitsLimits(settings, request.SectorCount, estimated))
        {
            throw new GameException(
                WorldErrorCodes.GenerationTooLarge,
                "The run asks for more than one run may create.",
                new Dictionary<string, object?>
                {
                    ["max_sectors_per_run"] = settings.MaxSectorsPerRun,
                    ["max_systems_per_run"] = settings.MaxSystemsPerRun,
                    ["estimated_system_count"] = estimated,
                });
        }

        GenerationRunEntity run = new()
        {
            InitiatorId = initiatorId,
            SectorCount = request.SectorCount,
            Seed = request.Seed,
            RuleSet = ruleSet,
            EstimatedSystemCount = estimated,
            Status = GenerationRunStatuses.Queued,
            ActiveLock = true,
            CreatedAt = _timeProvider.GetUtcNow(),
            Summary = JsonSerializer.Serialize(EmptySummary(), WorldJson.Options),
        };

        try
        {
            await _unitOfWork.RunAsync(
                async () =>
                {
                    _dbContext.GenerationRuns.Add(run);
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await _scheduler.ScheduleAsync(
                        new ScheduledEventRequest(
                            EventSector,
                            EventType,
                            _clock.GameNow,
                            JsonSerializer.Serialize(new GenerationRunPayload(run.Id), WorldJson.Options),
                            $"{EventType}:{run.Id.ToString(CultureInfo.InvariantCulture)}"),
                        cancellationToken).ConfigureAwait(false);

                    return run.Id;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            _dbContext.ChangeTracker.Clear();

            throw new GameException(WorldErrorCodes.GenerationRunning, "Another generation run is queued or running.");
        }

        return ToDto(run);
    }

    /// <summary>
    /// Does the work of a run: creates its sectors one by one and stores the result. Called by
    /// the scheduler; never throws, a failure is stored in the run.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the run has ended.</returns>
    public async Task ExecuteAsync(long runId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        WorldDbContext dbContext = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        WorldGenerationService generation = scope.ServiceProvider.GetRequiredService<WorldGenerationService>();

        GenerationRunEntity? run = await dbContext.GenerationRuns
            .SingleOrDefaultAsync(entry => entry.Id == runId, cancellationToken).ConfigureAwait(false);

        if (run is null || run.Status != GenerationRunStatuses.Queued)
        {
            return;
        }

        run.Status = GenerationRunStatuses.Running;
        run.StartedAt = _timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        List<int> sectorIds = [];
        List<GeneratedSectorDto> sectors = [];

        try
        {
            for (int index = 0; index < run.SectorCount; index++)
            {
                // The first sector takes the given seed itself, the next ones derive theirs from
                // it, so the same seed always gives the same sectors.
                long? seed = run.Seed is null ? null
                    : index == 0 ? run.Seed
                    : WorldRandom.DeriveSeed(run.Seed.Value, index);

                SectorSummary sector = await generation.AddSectorAsync(seed, false, run.InitiatorId, cancellationToken).ConfigureAwait(false);

                sectorIds.Add(sector.Id);
                sectors.Add(new GeneratedSectorDto(sector.Id, sector.Seed));
            }

            await FinishAsync(dbContext, runId, GenerationRunStatuses.Succeeded, sectorIds, sectors, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The generation run {RunId} failed after {Sectors} sectors.", runId, sectorIds.Count);

            string code = exception is GameException gameException ? gameException.Code : GameErrorCodes.InternalError;

            await FinishAsync(dbContext, runId, GenerationRunStatuses.Failed, sectorIds, sectors, code, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Marks the runs that were running when the server stopped as failed, so the lock of the
    /// single run is released. A queued run keeps its scheduled event and runs after the start.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of runs marked.</returns>
    public Task<int> FailInterruptedAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        return _dbContext.GenerationRuns
            .Where(run => run.Status == GenerationRunStatuses.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.Status, GenerationRunStatuses.Failed)
                    .SetProperty(run => run.ActiveLock, (bool?)null)
                    .SetProperty(run => run.FinishedAt, now)
                    .SetProperty(run => run.Error, WorldErrorCodes.GenerationInterrupted),
                cancellationToken);
    }

    /// <summary>
    /// Gets one run.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The run, or <see langword="null"/>.</returns>
    public async Task<GenerationRunDto?> GetAsync(long runId, CancellationToken cancellationToken)
    {
        GenerationRunEntity? run = await _dbContext.GenerationRuns.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == runId, cancellationToken).ConfigureAwait(false);

        return run is null ? null : ToDto(run);
    }

    /// <summary>
    /// Lists the runs, newest first.
    /// </summary>
    /// <param name="page">The page; the search text finds a status or a run number.</param>
    /// <param name="status">Only this status, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<GenerationRunDto>> ListAsync(PageRequest page, string? status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<GenerationRunEntity> query = _dbContext.GenerationRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(run => run.Status == status);
        }

        if (page.Search is { } search)
        {
            long? number = AdminPaging.AsNumber(search);
            string pattern = AdminPaging.ContainsPattern(search);

            query = query.Where(run => run.Id == number
                || run.Seed == number
                || EF.Functions.ILike(run.Status, pattern, AdminPaging.LikeEscape)
                || EF.Functions.ILike(run.InitiatorId, pattern, AdminPaging.LikeEscape));
        }

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<GenerationRunEntity> runs = await (page.Descending || page.Sort is null ? query.OrderByDescending(run => run.Id) : query.OrderBy(run => run.Id))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PageResult<GenerationRunDto>([.. runs.Select(ToDto)], total, page.Page, page.PageSize);
    }

    private static int EstimateSystems(WorldSettings settings, int sectorCount) =>
        (int)Math.Min(int.MaxValue, (long)sectorCount * (settings.SystemsMin + settings.SystemsMax) / BoundCount);

    private static bool FitsLimits(WorldSettings settings, int sectorCount, int estimated) =>
        sectorCount >= 1 && sectorCount <= settings.MaxSectorsPerRun && estimated <= settings.MaxSystemsPerRun;

    private async Task FinishAsync(
        WorldDbContext dbContext,
        long runId,
        string status,
        List<int> sectorIds,
        List<GeneratedSectorDto> sectors,
        string? error,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        int systems = await dbContext.Systems
            .CountAsync(system => sectorIds.Contains(system.SectorId), cancellationToken).ConfigureAwait(false);

        Dictionary<string, int> byClass = await dbContext.Objects.AsNoTracking()
            .Where(entry => sectorIds.Contains(entry.SectorId))
            .Join(dbContext.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => type.ObjectClass)
            .GroupBy(objectClass => objectClass)
            .Select(group => new { ObjectClass = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.ObjectClass, entry => entry.Count, cancellationToken).ConfigureAwait(false);

        GenerationSummaryDto summary = new(sectorIds, sectors, systems, byClass);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        string summaryJson = JsonSerializer.Serialize(summary, WorldJson.Options);

        await dbContext.GenerationRuns
            .Where(run => run.Id == runId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.Status, status)
                    .SetProperty(run => run.ActiveLock, (bool?)null)
                    .SetProperty(run => run.FinishedAt, now)
                    .SetProperty(run => run.Summary, summaryJson)
                    .SetProperty(run => run.Error, error),
                cancellationToken).ConfigureAwait(false);
    }

    private static GenerationRunDto ToDto(GenerationRunEntity run) =>
        new(
            run.Id,
            run.InitiatorId,
            run.SectorCount,
            run.Seed,
            run.RuleSet,
            run.EstimatedSystemCount,
            run.Status,
            run.CreatedAt,
            run.StartedAt,
            run.FinishedAt,
            ReadSummary(run.Summary),
            run.Error);

    private static GenerationSummaryDto EmptySummary() => new([], [], 0, new Dictionary<string, int>());

    private static GenerationSummaryDto ReadSummary(string json)
    {
        GenerationSummaryDto? summary = JsonSerializer.Deserialize<GenerationSummaryDto>(json, WorldJson.Options);

        return summary is null
            ? EmptySummary()
            : new GenerationSummaryDto(
                summary.SectorIds ?? [],
                summary.Sectors ?? [],
                summary.SystemCount,
                summary.ObjectCountsByClass ?? new Dictionary<string, int>());
    }
}

/// <summary>
/// The payload of the scheduled event of a generation run.
/// </summary>
/// <param name="RunId">The run.</param>
public sealed record GenerationRunPayload(long RunId);
