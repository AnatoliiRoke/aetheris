namespace Aetheris.Core.Security;

/// <summary>
/// Tells who owns an object of one type.
/// </summary>
/// <remarks>
/// This is the single point of the ownership check (task 00002, section 2.2): commands and
/// SignalR subscriptions that target an object ask the resolver of its type, and a player
/// is let through only for their own objects. A feature that introduces a new kind of object
/// registers one resolver with
/// <c>services.AddOwnedObjectType&lt;TResolver&gt;()</c>.
/// </remarks>
public interface IOwnedObjectResolver
{
    /// <summary>
    /// Gets the object type this resolver answers for, for example <c>player</c>.
    /// </summary>
    string ObjectType { get; }

    /// <summary>
    /// Finds the owner of an object.
    /// </summary>
    /// <param name="objectId">The identifier of the object.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier of the owning player, or <see langword="null"/> when the object does not exist.</returns>
    Task<string?> GetOwnerIdAsync(string objectId, CancellationToken cancellationToken);
}
