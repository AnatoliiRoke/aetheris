namespace Aetheris.Core.Errors;

/// <summary>
/// Raised when a command targets an object that the player does not own.
/// </summary>
public sealed class NotYourObjectException : GameException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotYourObjectException"/> class.
    /// </summary>
    /// <param name="objectType">The type of the object, for example <c>test_object</c>.</param>
    /// <param name="objectId">The identifier of the object.</param>
    public NotYourObjectException(string objectType, string objectId)
        : base(
            GameErrorCodes.NotYourObject,
            $"The {objectType} '{objectId}' does not belong to this player.",
            new Dictionary<string, object?>
            {
                ["objectType"] = objectType,
                ["objectId"] = objectId,
            })
    {
        ArgumentException.ThrowIfNullOrEmpty(objectType);
        ArgumentException.ThrowIfNullOrEmpty(objectId);

        ObjectType = objectType;
        ObjectId = objectId;
    }

    /// <summary>
    /// Gets the type of the object.
    /// </summary>
    public string ObjectType { get; }

    /// <summary>
    /// Gets the identifier of the object.
    /// </summary>
    public string ObjectId { get; }
}
