using Aetheris.Core.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Aetheris.Host.Admin;

/// <summary>
/// What the current administrator action changed, filled by the action and written to the
/// audit log by <see cref="AdminAuditFilter"/>.
/// </summary>
public sealed class AdminAuditScope : IAdminAuditRecorder
{
    private readonly List<Func<Task>> _afterCommit = [];

    /// <summary>
    /// Gets the object the action targeted.
    /// </summary>
    public string? Target { get; private set; }

    /// <summary>
    /// Gets the value before the change.
    /// </summary>
    public string? OldValue { get; private set; }

    /// <summary>
    /// Gets the value after the change.
    /// </summary>
    public string? NewValue { get; private set; }

    /// <summary>
    /// Gets the work to do once the change and its audit record are committed.
    /// </summary>
    public IReadOnlyList<Func<Task>> AfterCommit => _afterCommit;

    /// <inheritdoc />
    public void Record(string target, string? oldValue, string? newValue)
    {
        Target = target;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <inheritdoc />
    public void OnCommitted(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _afterCommit.Add(action);
    }
}

/// <summary>
/// Writes an audit record for every administrator request (documentation/tech/05, section 8).
/// </summary>
/// <remarks>
/// The filter is added to the whole admin group, so no admin endpoint can be added without
/// an audit record. An action only stages its changes in the database context; the filter
/// adds the audit record and saves both in one transaction, so a change is never stored
/// without its record.
/// </remarks>
public sealed class AdminAuditFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        HttpContext httpContext = context.HttpContext;
        IServiceProvider services = httpContext.RequestServices;

        object? result = await next(context).ConfigureAwait(false);

        AdminAuditScope scope = services.GetRequiredService<AdminAuditScope>();
        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        TimeProvider timeProvider = services.GetRequiredService<TimeProvider>();

        bool rejected = result is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status400BadRequest };
        string action = httpContext.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? httpContext.Request.Path.ToString();

        dbContext.AdminAuditLogs.Add(new AdminAuditLogEntity
        {
            AdminId = httpContext.User.GetAdminId() ?? "unknown",
            Action = rejected ? action + ".rejected" : action,
            Target = scope.Target,
            OldValue = rejected ? null : scope.OldValue,
            NewValue = rejected ? null : scope.NewValue,
            At = timeProvider.GetUtcNow(),
        });

        await dbContext.SaveChangesAsync(httpContext.RequestAborted).ConfigureAwait(false);

        if (!rejected)
        {
            foreach (Func<Task> afterCommit in scope.AfterCommit)
            {
                await afterCommit().ConfigureAwait(false);
            }
        }

        return result;
    }
}
