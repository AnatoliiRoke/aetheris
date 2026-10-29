namespace Aetheris.Research.Services;

/// <summary>
/// The subjects of another module that the effect «opens» of a technology can name: the
/// recipes of the production module, for example (task 00023, decision 7; task 00013).
/// </summary>
/// <remarks>
/// The research module does not know the modules that own the subjects, so each of them
/// registers this contract for its own kind. The technology closes the subject through the
/// common requirements module, and the owner drops its copy of the requirements after the
/// change of the technology is committed.
/// </remarks>
public interface ITechnologyUnlockTargets
{
    /// <summary>
    /// Gets the kind of the subjects, one of <see cref="TechnologyUnlockKinds"/>.
    /// </summary>
    string UnlockKind { get; }

    /// <summary>
    /// Gets the subject type of the requirements that close a subject of the kind.
    /// </summary>
    string RequirementSubjectType { get; }

    /// <summary>
    /// Gets the subjects that exist, the archived ones included, with the localisation keys of
    /// their names.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The keys of the names by subject.</returns>
    Task<IReadOnlyDictionary<int, string>> GetNameKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Drops the copy of the subjects and their requirements kept in memory, so a new
    /// requirement of a subject takes effect at once.
    /// </summary>
    void InvalidateCatalog();
}
