using Aetheris.Core.Commands;
using Aetheris.Host.Data.Entities;

namespace Aetheris.Host.Data;

/// <summary>
/// Adds change records to the database context of the current request, so they are saved
/// in the same transaction as the change.
/// </summary>
public sealed class EfChangeJournal : IChangeJournal
{
    private readonly AetherisDbContext _dbContext;
    private readonly CommandExecutionScope _scope;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfChangeJournal"/> class.
    /// </summary>
    /// <param name="dbContext">The database context of the request.</param>
    /// <param name="scope">The command that is being executed.</param>
    /// <param name="timeProvider">The clock.</param>
    public EfChangeJournal(AetherisDbContext dbContext, CommandExecutionScope scope, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _scope = scope;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public void Record(ChangeRecord change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (_scope.ActorId is null)
        {
            throw new InvalidOperationException("A change can only be recorded inside a command or an admin action.");
        }

        _dbContext.ChangeJournal.Add(new ChangeJournalEntity
        {
            At = _timeProvider.GetUtcNow(),
            ActorId = _scope.ActorId,
            RequestId = _scope.RequestId,
            ObjectType = change.ObjectType,
            ObjectId = change.ObjectId,
            Field = change.Field,
            OldValue = change.OldValue,
            NewValue = change.NewValue,
            Reason = change.Reason,
        });
    }
}

/// <summary>
/// The actor and the request identifier of the command executed in the current request.
/// </summary>
public sealed class CommandExecutionScope
{
    /// <summary>
    /// Gets or sets the identifier of the player or administrator.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Gets or sets the request identifier.
    /// </summary>
    public string? RequestId { get; set; }
}
