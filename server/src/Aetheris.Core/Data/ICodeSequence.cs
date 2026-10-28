namespace Aetheris.Core.Data;

/// <summary>
/// Gives the neutral codes of the reference rows: the group of the code and a number,
/// <c>group_1</c>, <c>group_2</c> and so on (task 00022, decision 1).
/// </summary>
/// <remarks>
/// The server gives the number, never the administrator. A number is given once: it is not
/// reused after the row is deleted, and two parallel requests never get the same one. The code
/// carries no meaning and is not shown to a player; the rows link to each other by their
/// numeric identifiers.
/// </remarks>
public interface ICodeSequence
{
    /// <summary>
    /// Takes the next code of a group, in the transaction of the request.
    /// </summary>
    /// <param name="group">The group, lower-case letters and underscores, for example <c>race</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The code, for example <c>race_5</c>.</returns>
    Task<string> NextAsync(string group, CancellationToken cancellationToken);

    /// <summary>
    /// Makes sure the next code of a group is above a number that is already taken, for example
    /// by the starting data that brings its own codes.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="takenNumber">The largest number already taken.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the counter is raised.</returns>
    Task RaiseAsync(string group, int takenNumber, CancellationToken cancellationToken);
}
