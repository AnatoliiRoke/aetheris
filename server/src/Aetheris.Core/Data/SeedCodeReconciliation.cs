using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Core.Data;

/// <summary>
/// One table of a module whose rows come from the starting data, as the command that brings a
/// database to the codes of the starting data sees it (task 00052). The module reads and writes
/// its own table; the host never touches it.
/// </summary>
public interface ISeedCodeTable
{
    /// <summary>
    /// Gets the table of the module, in snake case.
    /// </summary>
    string Table { get; }

    /// <summary>
    /// Gets the group of the counter of the server that gives the codes of the table to the rows
    /// of the admin panel, for example <c>part_group</c>.
    /// </summary>
    string CodeGroup { get; }

    /// <summary>
    /// Gets the codes of the rows of the starting data, in the order of the file.
    /// </summary>
    IReadOnlyList<string> SeedCodes { get; }

    /// <summary>
    /// Gets the columns of the table that hold the localisation keys built from the code, for
    /// example <c>name_key</c>.
    /// </summary>
    IReadOnlyList<string> KeyColumns { get; }

    /// <summary>
    /// Builds the localisation keys of a row from its code, in the order of <see cref="KeyColumns"/>.
    /// </summary>
    /// <param name="code">The code of the row.</param>
    /// <returns>The keys.</returns>
    IReadOnlyList<string?> KeysOf(string code);

    /// <summary>
    /// Reads every row of the table, in the order of the identifier, in one query.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<SeedCodeRowKeys>> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes the new codes and keys of the rows in the transaction of the request, in one save.
    /// </summary>
    /// <param name="changes">The rows to change.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    Task WriteAsync(IReadOnlyList<SeedCodeRowChange> changes, CancellationToken cancellationToken);
}

/// <summary>
/// A module's part of the match between its tables and the starting data (task 00052). A module
/// tells which of its tables the starting data fills, and the host does the rest: it reads the
/// rows, finds the ones whose codes differ from the file, and renames them with their texts in one
/// transaction.
/// </summary>
public interface ISeedCodeSource
{
    /// <summary>
    /// Gets the tables of the module the starting data fills.
    /// </summary>
    IReadOnlyList<ISeedCodeTable> Tables { get; }
}

/// <summary>
/// A row of a table with its code and the localisation keys it holds.
/// </summary>
/// <param name="Id">The identifier of the row.</param>
/// <param name="Code">The code the row has in the database.</param>
/// <param name="Keys">The keys the row holds, in the order of the key columns; an entry is <see langword="null"/> when the row has none.</param>
public sealed record SeedCodeRowKeys(int Id, string Code, IReadOnlyList<string?> Keys);

/// <summary>
/// The new code and keys of a row.
/// </summary>
/// <param name="Id">The identifier of the row.</param>
/// <param name="Code">The code of the starting data.</param>
/// <param name="Keys">The new keys, in the order of the key columns; an entry is <see langword="null"/> when the key of the row stays.</param>
public sealed record SeedCodeRowChange(int Id, string Code, IReadOnlyList<string?> Keys);

/// <summary>
/// A table of a module backed by one entity set of its database context (task 00052).
/// </summary>
/// <remarks>
/// The module gives the entities, the way to read their code and keys, and the unit of work that
/// joins the transaction of the request; the class gives the rest, so every table of every module
/// is a few lines. The rows are loaded once and saved once, never one query per row.
/// </remarks>
/// <typeparam name="TEntity">The entity of the table.</typeparam>
public sealed class SeedCodeTable<TEntity> : ISeedCodeTable
    where TEntity : class
{
    private readonly Func<string, IReadOnlyList<string?>> _keysOf;
    private readonly Func<CancellationToken, Task<List<TEntity>>> _load;
    private readonly Func<Func<Task<bool>>, CancellationToken, Task<bool>> _inUnitOfWork;
    private readonly Func<TEntity, int> _idOf;
    private readonly Func<TEntity, string> _codeOf;
    private readonly Func<TEntity, IReadOnlyList<string?>> _keysOfEntity;
    private readonly Action<TEntity, string, IReadOnlyList<string?>> _assign;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedCodeTable{TEntity}"/> class.
    /// </summary>
    /// <param name="table">The table of the module, in snake case.</param>
    /// <param name="codeGroup">The group of the counter of the server.</param>
    /// <param name="seedCodes">The codes of the starting data, in the order of the file.</param>
    /// <param name="keyColumns">The columns that hold the keys built from the code.</param>
    /// <param name="keysOf">Builds the keys of a row from its code.</param>
    /// <param name="load">Loads the entities of the table, tracked, in the order of the identifier.</param>
    /// <param name="inUnitOfWork">Runs work in the unit of work of the module, which joins the transaction of the request and saves the context.</param>
    /// <param name="idOf">Reads the identifier of an entity.</param>
    /// <param name="codeOf">Reads the code of an entity.</param>
    /// <param name="keysOfEntity">Reads the keys an entity holds, in the order of the key columns.</param>
    /// <param name="assign">Gives an entity its new code and the keys that are not <see langword="null"/>.</param>
    public SeedCodeTable(
        string table,
        string codeGroup,
        IReadOnlyList<string> seedCodes,
        IReadOnlyList<string> keyColumns,
        Func<string, IReadOnlyList<string?>> keysOf,
        Func<CancellationToken, Task<List<TEntity>>> load,
        Func<Func<Task<bool>>, CancellationToken, Task<bool>> inUnitOfWork,
        Func<TEntity, int> idOf,
        Func<TEntity, string> codeOf,
        Func<TEntity, IReadOnlyList<string?>> keysOfEntity,
        Action<TEntity, string, IReadOnlyList<string?>> assign)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentException.ThrowIfNullOrEmpty(codeGroup);
        ArgumentNullException.ThrowIfNull(seedCodes);
        ArgumentNullException.ThrowIfNull(keyColumns);
        ArgumentNullException.ThrowIfNull(keysOf);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(inUnitOfWork);
        ArgumentNullException.ThrowIfNull(idOf);
        ArgumentNullException.ThrowIfNull(codeOf);
        ArgumentNullException.ThrowIfNull(keysOfEntity);
        ArgumentNullException.ThrowIfNull(assign);

        Table = table;
        CodeGroup = codeGroup;
        SeedCodes = seedCodes;
        KeyColumns = keyColumns;
        _keysOf = keysOf;
        _load = load;
        _inUnitOfWork = inUnitOfWork;
        _idOf = idOf;
        _codeOf = codeOf;
        _keysOfEntity = keysOfEntity;
        _assign = assign;
    }

    /// <inheritdoc />
    public string Table { get; }

    /// <inheritdoc />
    public string CodeGroup { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> SeedCodes { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> KeyColumns { get; }

    /// <inheritdoc />
    public IReadOnlyList<string?> KeysOf(string code) => _keysOf(code);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SeedCodeRowKeys>> ReadAsync(CancellationToken cancellationToken)
    {
        List<SeedCodeRowKeys> rows = [];

        await _inUnitOfWork(
            async () =>
            {
                rows = [.. (await _load(cancellationToken).ConfigureAwait(false))
                    .Select(entity => new SeedCodeRowKeys(_idOf(entity), _codeOf(entity), _keysOfEntity(entity)))];

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        return rows;
    }

    /// <inheritdoc />
    public async Task WriteAsync(IReadOnlyList<SeedCodeRowChange> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Dictionary<int, SeedCodeRowChange> changesById = changes.ToDictionary(change => change.Id);

        // The unit of work saves the context when the work ends, so every row is written by one
        // save and the transaction of the request holds them with the texts.
        await _inUnitOfWork(
            async () =>
            {
                foreach (TEntity entity in await _load(cancellationToken).ConfigureAwait(false))
                {
                    if (changesById.TryGetValue(_idOf(entity), out SeedCodeRowChange? change))
                    {
                        _assign(entity, change.Code, change.Keys);
                    }
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// A row of a table with its identifier and its code.
/// </summary>
/// <param name="Id">The identifier of the row.</param>
/// <param name="Code">The code the row has in the database.</param>
public sealed record SeedCodeRow(int Id, string Code);

/// <summary>
/// A row whose code the command changes.
/// </summary>
/// <param name="RowId">The identifier of the row.</param>
/// <param name="OldCode">The code the row has in the database.</param>
/// <param name="NewCode">The code of the starting data.</param>
public sealed record SeedCodeMove(int RowId, string OldCode, string NewCode);

/// <summary>
/// The rows of a table cannot be matched with the starting data without a guess.
/// </summary>
public sealed class SeedCodeMismatchException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeedCodeMismatchException"/> class.
    /// </summary>
    /// <param name="message">What cannot be matched and why.</param>
    public SeedCodeMismatchException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Matches the rows of a table with the codes of the starting data (task 00052).
/// </summary>
public static class SeedCodeMatcher
{
    /// <summary>
    /// Finds the rows whose code must change to the code of the starting data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A table that already holds every code of the file is left as it is: its other rows belong
    /// to the admin panel. Otherwise the table must be the work of a seeder that gave the rows the
    /// codes of the counter in the order of the file: it holds as many rows as the file, and every
    /// row has either the code of the file at its place or the code of the counter. The k-th row
    /// by its identifier then takes the k-th code of the file.
    /// </para>
    /// <para>
    /// Any other state, such as more rows than the file or a code of neither form, cannot be
    /// matched without a guess, so nothing is changed and the reason is told.
    /// </para>
    /// </remarks>
    /// <param name="table">The table, for the message.</param>
    /// <param name="codeGroup">The group of the counter of the server.</param>
    /// <param name="seedCodes">The codes of the file, in the order of the file.</param>
    /// <param name="rows">The rows of the table.</param>
    /// <returns>The rows whose code changes, in the order of the file.</returns>
    /// <exception cref="SeedCodeMismatchException">The rows cannot be matched without a guess.</exception>
    public static IReadOnlyList<SeedCodeMove> Plan(
        string table,
        string codeGroup,
        IReadOnlyList<string> seedCodes,
        IReadOnlyList<SeedCodeRow> rows)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentException.ThrowIfNullOrEmpty(codeGroup);
        ArgumentNullException.ThrowIfNull(seedCodes);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0 || seedCodes.Count == 0)
        {
            return [];
        }

        HashSet<string> databaseCodes = [.. rows.Select(row => row.Code)];

        if (seedCodes.All(databaseCodes.Contains))
        {
            return [];
        }

        if (rows.Count != seedCodes.Count)
        {
            throw new SeedCodeMismatchException(
                $"The table '{table}' holds {rows.Count.ToString(CultureInfo.InvariantCulture)} rows and the starting data {seedCodes.Count.ToString(CultureInfo.InvariantCulture)}, so the rows cannot be matched with the codes of the file without a guess; nothing was changed.");
        }

        HashSet<string> known = [.. seedCodes];
        Regex counterForm = CounterForm(codeGroup);
        List<SeedCodeRow> ordered = [.. rows.OrderBy(row => row.Id)];
        List<SeedCodeMove> moves = [];
        int previousNumber = 0;

        for (int index = 0; index < ordered.Count; index++)
        {
            SeedCodeRow row = ordered[index];

            if (known.Contains(row.Code))
            {
                if (!string.Equals(row.Code, seedCodes[index], StringComparison.Ordinal))
                {
                    throw new SeedCodeMismatchException(
                        $"The row {row.Id.ToString(CultureInfo.InvariantCulture)} of the table '{table}' has the code '{row.Code}', but the starting data puts '{seedCodes[index]}' at its place; nothing was changed.");
                }

                continue;
            }

            if (!counterForm.IsMatch(row.Code))
            {
                throw new SeedCodeMismatchException(
                    $"The row {row.Id.ToString(CultureInfo.InvariantCulture)} of the table '{table}' has the code '{row.Code}', which is neither a code of the starting data nor a code of the counter '{codeGroup}_<number>'; nothing was changed.");
            }

            int number = SeedCodeRules.TrailingNumber(row.Code);

            if (number <= previousNumber)
            {
                throw new SeedCodeMismatchException(
                    $"The codes of the counter '{codeGroup}' of the table '{table}' do not follow the order of the rows, so the rows cannot be matched with the starting data; nothing was changed.");
            }

            previousNumber = number;
            moves.Add(new SeedCodeMove(row.Id, row.Code, seedCodes[index]));
        }

        return moves;
    }

    private static Regex CounterForm(string codeGroup) =>
        new("^" + Regex.Escape(codeGroup) + "_[0-9]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
}
