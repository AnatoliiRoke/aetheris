using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// A localisation key a row of a table renames together with its code.
/// </summary>
/// <param name="RowId">The identifier of the row.</param>
/// <param name="Column">The column of the row that holds the key.</param>
/// <param name="OldKey">The key built from the old code.</param>
/// <param name="NewKey">The key built from the code of the starting data.</param>
public sealed record SeedCodeKeyChange(int RowId, string Column, string OldKey, string NewKey);

/// <summary>
/// What the command finds in one table of a module.
/// </summary>
/// <param name="Table">The table.</param>
/// <param name="CodeGroup">The group of the counter of the table.</param>
/// <param name="Rows">How many rows the table holds.</param>
/// <param name="Moves">The rows whose code changes.</param>
/// <param name="Keys">The localisation keys that change with the codes.</param>
/// <param name="CounterBefore">The value of the counter of the group now.</param>
/// <param name="CounterRequired">The value the counter must reach: the largest number of the codes of the starting data.</param>
/// <param name="Problem">Why the rows cannot be matched with the starting data, or <see langword="null"/> when they can.</param>
public sealed record SeedCodeTableResult(
    string Table,
    string CodeGroup,
    int Rows,
    IReadOnlyList<SeedCodeMove> Moves,
    IReadOnlyList<SeedCodeKeyChange> Keys,
    int CounterBefore,
    int CounterRequired,
    string? Problem)
{
    /// <summary>
    /// Gets a value indicating whether the counter of the group stands below the codes of the starting data.
    /// </summary>
    public bool CounterBehind => CounterBefore < CounterRequired;

    /// <summary>
    /// Gets a value indicating whether the command has anything to change in the table.
    /// </summary>
    public bool HasChanges => Moves.Count > 0 || CounterBehind;
}

/// <summary>
/// What the command found in every table, and what it wrote.
/// </summary>
/// <param name="Tables">The result of every table.</param>
/// <param name="TextsRenamed">How many texts got a new key; zero when nothing was written.</param>
/// <param name="TextsDropped">How many texts were dropped because the text of the new key already exists.</param>
public sealed record SeedCodeReport(IReadOnlyList<SeedCodeTableResult> Tables, int TextsRenamed = 0, int TextsDropped = 0)
{
    /// <summary>
    /// Gets a value indicating whether a table cannot be matched with the starting data.
    /// </summary>
    public bool HasProblems => Tables.Any(table => table.Problem is not null);

    /// <summary>
    /// Gets a value indicating whether any table has something to change.
    /// </summary>
    public bool HasChanges => Tables.Any(table => table.HasChanges);
}

/// <summary>
/// Brings a database to the codes of the starting data (task 00052).
/// </summary>
/// <remarks>
/// <para>
/// Every module tells which of its tables the starting data fills (<see cref="ISeedCodeSource"/>).
/// The service reads the rows of each table in one query, matches them with the codes of the file
/// (<see cref="SeedCodeMatcher"/>), and renames the rows that differ together with the keys of their
/// texts and the texts themselves in one transaction. The counters of the codes are raised above the
/// codes of the file. A table that already matches is not touched, so a second run changes nothing.
/// </para>
/// <para>
/// A module reads and writes its own tables through <see cref="ISeedCodeTable"/> and its unit of
/// work, which joins the transaction of this service, so the service holds no knowledge of any
/// module and reads no table of it. Only the texts and the counters, which the host owns, are
/// written here.
/// </para>
/// </remarks>
public sealed class SeedCodeReconciliation
{
    /// <summary>
    /// The author written on the texts the command renames.
    /// </summary>
    public const string ChangedBy = "reconcile_seed_codes";

    private readonly IReadOnlyList<ISeedCodeTable> _tables;
    private readonly IDatabaseSession _session;
    private readonly AetherisDbContext _dbContext;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedCodeReconciliation"/> class.
    /// </summary>
    /// <param name="sources">The tables of every module the starting data fills.</param>
    /// <param name="session">The connection and the transaction of the request.</param>
    /// <param name="dbContext">The main database context, which owns the texts and the counters.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="timeProvider">The clock.</param>
    public SeedCodeReconciliation(
        IEnumerable<ISeedCodeSource> sources,
        IDatabaseSession session,
        AetherisDbContext dbContext,
        ICodeSequence codes,
        ILocalizationTexts texts,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tables = [.. sources.SelectMany(source => source.Tables)];
        _session = session;
        _dbContext = dbContext;
        _codes = codes;
        _texts = texts;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Finds what differs from the starting data and writes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was found.</returns>
    public Task<SeedCodeReport> CheckAsync(CancellationToken cancellationToken) => PlanAsync(cancellationToken);

    /// <summary>
    /// Brings every table to the codes of the starting data in one transaction. Nothing is written
    /// when a table cannot be matched.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was found and written.</returns>
    public async Task<SeedCodeReport> ReconcileAsync(CancellationToken cancellationToken)
    {
        await using IDatabaseTransaction transaction = await _session.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        SeedCodeReport plan = await PlanAsync(cancellationToken).ConfigureAwait(false);

        if (plan.HasProblems || !plan.HasChanges)
        {
            return plan;
        }

        foreach (SeedCodeTableResult table in plan.Tables.Where(table => table.Moves.Count > 0))
        {
            await WriteTableAsync(table, cancellationToken).ConfigureAwait(false);
        }

        (int renamed, int dropped) = await RenameTextsAsync(plan, cancellationToken).ConfigureAwait(false);

        foreach (SeedCodeTableResult table in plan.Tables.Where(table => table.CounterBehind))
        {
            await _codes.RaiseAsync(table.CodeGroup, table.CounterRequired, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // The texts changed outside the usual path, so the copy in memory is dropped.
        _texts.Invalidate();

        return plan with { TextsRenamed = renamed, TextsDropped = dropped };
    }

    private async Task<SeedCodeReport> PlanAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, int> counters = await _dbContext.CodeSequences.AsNoTracking()
            .ToDictionaryAsync(counter => counter.CodeGroup, counter => counter.LastNumber, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        List<SeedCodeTableResult> results = [];

        foreach (ISeedCodeTable table in _tables)
        {
            results.Add(await PlanTableAsync(table, counters, cancellationToken).ConfigureAwait(false));
        }

        return new SeedCodeReport(results);
    }

    private static async Task<SeedCodeTableResult> PlanTableAsync(
        ISeedCodeTable table,
        IReadOnlyDictionary<string, int> counters,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SeedCodeRowKeys> rows = await table.ReadAsync(cancellationToken).ConfigureAwait(false);
        int counterBefore = counters.GetValueOrDefault(table.CodeGroup);
        int counterRequired = SeedCodeRules.LargestNumber(table.SeedCodes);

        IReadOnlyList<SeedCodeMove> moves;

        try
        {
            moves = SeedCodeMatcher.Plan(
                table.Table,
                table.CodeGroup,
                table.SeedCodes,
                [.. rows.Select(row => new SeedCodeRow(row.Id, row.Code))]);
        }
        catch (SeedCodeMismatchException problem)
        {
            return new SeedCodeTableResult(table.Table, table.CodeGroup, rows.Count, [], [], counterBefore, counterRequired, problem.Message);
        }

        Dictionary<int, SeedCodeRowKeys> rowsById = rows.ToDictionary(row => row.Id);
        List<SeedCodeKeyChange> keys = [];

        foreach (SeedCodeMove move in moves)
        {
            SeedCodeRowKeys row = rowsById[move.RowId];
            IReadOnlyList<string?> oldKeys = table.KeysOf(move.OldCode);
            IReadOnlyList<string?> newKeys = table.KeysOf(move.NewCode);

            for (int index = 0; index < table.KeyColumns.Count; index++)
            {
                // A key the row holds and the old code builds is renamed; a key set by hand stays.
                if (row.Keys[index] is { } key && newKeys[index] is { } newKey && string.Equals(key, oldKeys[index], StringComparison.Ordinal))
                {
                    keys.Add(new SeedCodeKeyChange(move.RowId, table.KeyColumns[index], key, newKey));
                }
            }
        }

        return new SeedCodeTableResult(table.Table, table.CodeGroup, rows.Count, moves, keys, counterBefore, counterRequired, null);
    }

    private async Task WriteTableAsync(SeedCodeTableResult result, CancellationToken cancellationToken)
    {
        ISeedCodeTable table = _tables.First(candidate => string.Equals(candidate.Table, result.Table, StringComparison.Ordinal));
        Dictionary<(int RowId, string Column), string> newKeys = result.Keys.ToDictionary(key => (key.RowId, key.Column), key => key.NewKey);

        // A key that is not renamed arrives as null and keeps the value of the row.
        await table.WriteAsync(
            [
                .. result.Moves.Select(move => new SeedCodeRowChange(
                    move.RowId,
                    move.NewCode,
                    [.. table.KeyColumns.Select(column => newKeys.GetValueOrDefault((move.RowId, column)))])),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(int Renamed, int Dropped)> RenameTextsAsync(SeedCodeReport plan, CancellationToken cancellationToken)
    {
        string[] oldKeys = [.. plan.Tables.SelectMany(table => table.Keys).Select(key => key.OldKey)];
        string[] newKeys = [.. plan.Tables.SelectMany(table => table.Keys).Select(key => key.NewKey)];

        if (oldKeys.Length == 0)
        {
            return (0, 0);
        }

        // A text whose new key already has a text in the same language is dropped: the text of the
        // new key stays, so the unique key of the table is never broken.
        int dropped = await _dbContext.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM localization_text AS old_text
            USING unnest({oldKeys}, {newKeys}) AS renamed(old_key, new_key)
            WHERE old_text.key = renamed.old_key
              AND EXISTS (SELECT 1 FROM localization_text AS new_text WHERE new_text.key = renamed.new_key AND new_text.language = old_text.language)
            """,
            cancellationToken).ConfigureAwait(false);

        // The version rises, so the pack of the texts changes for the client.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        int renamed = await _dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE localization_text AS renamed_text
            SET key = renamed.new_key, version = renamed_text.version + 1, updated_at = {now}, updated_by = {ChangedBy}
            FROM unnest({oldKeys}, {newKeys}) AS renamed(old_key, new_key)
            WHERE renamed_text.key = renamed.old_key
            """,
            cancellationToken).ConfigureAwait(false);

        return (renamed, dropped);
    }
}
