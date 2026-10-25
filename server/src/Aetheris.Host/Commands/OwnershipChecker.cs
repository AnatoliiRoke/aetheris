using Aetheris.Core.Security;

namespace Aetheris.Host.Commands;

/// <summary>
/// The single ownership check of the server: a player may act on and subscribe to their
/// own objects only (task 00002, section 2.2).
/// </summary>
public sealed class OwnershipChecker
{
    private readonly Dictionary<string, IOwnedObjectResolver> _resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnershipChecker"/> class.
    /// </summary>
    /// <param name="resolvers">The resolvers of every registered object type.</param>
    public OwnershipChecker(IEnumerable<IOwnedObjectResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);

        _resolvers = resolvers.ToDictionary(resolver => resolver.ObjectType, StringComparer.Ordinal);
    }

    /// <summary>
    /// Checks that a player owns an object.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="objectType">The object type.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the player owns the object. An unknown type, an unknown
    /// object and an object of another player all give <see langword="false"/>, so the
    /// answer does not tell which objects exist.
    /// </returns>
    public async Task<bool> IsOwnerAsync(
        string playerId,
        string objectType,
        string objectId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (string.IsNullOrEmpty(objectType)
            || string.IsNullOrEmpty(objectId)
            || !_resolvers.TryGetValue(objectType, out IOwnedObjectResolver? resolver))
        {
            return false;
        }

        string? ownerId = await resolver.GetOwnerIdAsync(objectId, cancellationToken).ConfigureAwait(false);

        return string.Equals(ownerId, playerId, StringComparison.Ordinal);
    }
}

/// <summary>
/// The built-in object type <c>player</c>: every player owns their own player object, for
/// example their personal event channel.
/// </summary>
public sealed class PlayerObjectResolver : IOwnedObjectResolver
{
    /// <summary>
    /// The object type of a player.
    /// </summary>
    public const string Type = "player";

    /// <inheritdoc />
    public string ObjectType => Type;

    /// <inheritdoc />
    public Task<string?> GetOwnerIdAsync(string objectId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(objectId);
}
