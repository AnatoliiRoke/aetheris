namespace Aetheris.Core.Commands;

/// <summary>
/// A change of a value of an object, written to the change journal.
/// </summary>
/// <param name="ObjectType">The type of the changed object.</param>
/// <param name="ObjectId">The identifier of the changed object.</param>
/// <param name="Field">The name of the changed value.</param>
/// <param name="OldValue">The value before the change.</param>
/// <param name="NewValue">The value after the change.</param>
/// <param name="Reason">Why the value changed, for example the command name.</param>
public sealed record ChangeRecord(
    string ObjectType,
    string ObjectId,
    string Field,
    string? OldValue,
    string? NewValue,
    string Reason);

/// <summary>
/// The journal of value changes (documentation/tech/05, section 5).
/// </summary>
/// <remarks>
/// A record is written in the same transaction as the change itself, so the journal never
/// shows a change that was rolled back, and never misses one that was committed. It is the
/// base for finding and reverting the effects of a defect.
/// </remarks>
public interface IChangeJournal
{
    /// <summary>
    /// Adds a record to the current transaction.
    /// </summary>
    /// <param name="change">The change to record.</param>
    void Record(ChangeRecord change);
}
