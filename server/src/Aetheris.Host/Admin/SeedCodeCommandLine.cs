using System.Globalization;
using Aetheris.Host.Data;

namespace Aetheris.Host.Admin;

/// <summary>
/// The command of the server that brings a database to the codes of the starting data
/// (task 00052).
/// </summary>
/// <remarks>
/// <c>dotnet run --project src/Aetheris.Host -- reconcile-seed-codes [--check]</c>. Without the
/// option it renames the rows whose codes differ from the starting data, with the keys of their
/// texts, in one transaction; with <c>--check</c> it only shows what differs and writes nothing.
/// The exit code is 0 when the database matches the starting data or the fix succeeded, and 1
/// when <c>--check</c> found a difference or a table cannot be matched. The output holds no
/// connection string and no secret.
/// </remarks>
public static class SeedCodeCommandLine
{
    /// <summary>
    /// The command that brings the codes of a database to the codes of the starting data.
    /// </summary>
    public const string ReconcileCommand = "reconcile-seed-codes";

    /// <summary>
    /// The option that only shows the differences.
    /// </summary>
    public const string CheckOption = "--check";

    /// <summary>
    /// The number of arguments of the command with its option, the command included.
    /// </summary>
    private const int MaxArgumentCount = 2;

    /// <summary>
    /// Checks whether the arguments name this command.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns><see langword="true"/> when the server should run the command and exit.</returns>
    public static bool IsCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], ReconcileCommand, StringComparison.Ordinal);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="args">The command line arguments.</param>
    /// <param name="output">Where the table is written.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        bool check = args.Length == MaxArgumentCount && string.Equals(args[1], CheckOption, StringComparison.Ordinal);

        if (args.Length > MaxArgumentCount || (args.Length == MaxArgumentCount && !check))
        {
            await Console.Error.WriteLineAsync($"Usage: {ReconcileCommand} [{CheckOption}]").ConfigureAwait(false);

            return 1;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        SeedCodeReconciliation reconciliation = scope.ServiceProvider.GetRequiredService<SeedCodeReconciliation>();

        SeedCodeReport report = check
            ? await reconciliation.CheckAsync(cancellationToken).ConfigureAwait(false)
            : await reconciliation.ReconcileAsync(cancellationToken).ConfigureAwait(false);

        await PrintAsync(report, check, output).ConfigureAwait(false);

        if (report.HasProblems)
        {
            return 1;
        }

        // A check that finds a difference fails, so a script can stop on it; a fix that wrote
        // the difference away succeeds.
        return check && report.HasChanges ? 1 : 0;
    }

    private static async Task PrintAsync(SeedCodeReport report, bool check, TextWriter output)
    {
        if (!report.HasProblems && !report.HasChanges)
        {
            await output.WriteLineAsync("Розбіжностей немає: коди бази збігаються з кодами сіду.").ConfigureAwait(false);

            return;
        }

        List<string[]> lines = [["таблиця", "було", "стало", "кількість"]];

        foreach (SeedCodeTableResult table in report.Tables.Where(table => table.Moves.Count > 0))
        {
            lines.Add([
                table.Table,
                Range(table.Moves.Select(move => move.OldCode)),
                Range(table.Moves.Select(move => move.NewCode)),
                table.Moves.Count.ToString(CultureInfo.InvariantCulture),
            ]);
        }

        foreach (SeedCodeTableResult table in report.Tables.Where(table => table.CounterBehind))
        {
            lines.Add([
                "code_sequence/" + table.CodeGroup,
                table.CounterBefore.ToString(CultureInfo.InvariantCulture),
                table.CounterRequired.ToString(CultureInfo.InvariantCulture),
                "1",
            ]);
        }

        if (lines.Count > 1)
        {
            int[] widths = [.. Enumerable.Range(0, lines[0].Length).Select(column => lines.Max(line => line[column].Length))];

            foreach (string[] line in lines)
            {
                await output.WriteLineAsync(string.Join("  ", line.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd()).ConfigureAwait(false);
            }
        }

        int keyCount = report.Tables.Sum(table => table.Keys.Count);

        await output.WriteLineAsync($"Ключів текстів до перейменування: {keyCount.ToString(CultureInfo.InvariantCulture)}.").ConfigureAwait(false);

        foreach (SeedCodeTableResult table in report.Tables.Where(table => table.Problem is not null))
        {
            await output.WriteLineAsync($"Зупинено без змін: {table.Problem}").ConfigureAwait(false);
        }

        if (!check && !report.HasProblems)
        {
            await output.WriteLineAsync($"Виправлено. Текстів перейменовано: {report.TextsRenamed.ToString(CultureInfo.InvariantCulture)}, відкинуто дублів: {report.TextsDropped.ToString(CultureInfo.InvariantCulture)}.").ConfigureAwait(false);
        }
        else if (check && !report.HasProblems)
        {
            await output.WriteLineAsync("Режим перевірки: нічого не записано.").ConfigureAwait(false);
        }
    }

    private static string Range(IEnumerable<string> codes)
    {
        List<string> list = [.. codes];

        return list.Count == 1 ? list[0] : list[0] + " … " + list[^1];
    }
}
