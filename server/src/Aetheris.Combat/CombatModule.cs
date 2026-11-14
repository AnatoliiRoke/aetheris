using Aetheris.Combat.Services;

namespace Aetheris.Combat;

/// <summary>
/// The implementation of <see cref="ICombatModule"/>.
/// </summary>
/// <remarks>
/// The module keeps tables of its own, so it works on the context of the request (task 00062).
/// </remarks>
public sealed class CombatModule : ICombatModule
{
    private readonly CombatBootstrapService _bootstrap;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the reference rows.</param>
    public CombatModule(CombatBootstrapService bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        _bootstrap = bootstrap;
    }

    /// <inheritdoc />
    public string ModuleName => "Combat";

    /// <inheritdoc />
    public Task EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);
}
