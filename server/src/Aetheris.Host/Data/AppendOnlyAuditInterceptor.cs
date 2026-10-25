using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aetheris.Host.Data;

/// <summary>
/// Refuses to save a change or a removal of an administrator audit record.
/// </summary>
/// <remarks>
/// The audit log is append-only (documentation/tech/05, section 8). Three layers keep it
/// so: the application has no endpoint that edits it, this interceptor stops the change in
/// the application, and a database trigger stops it in PostgreSQL for any other client.
/// </remarks>
public sealed class AppendOnlyAuditInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        EnsureAppendOnly(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        EnsureAppendOnly(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnsureAppendOnly(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        bool tampered = context.ChangeTracker
            .Entries<AdminAuditLogEntity>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (tampered)
        {
            throw new InvalidOperationException("The administrator audit log is append-only.");
        }
    }
}
