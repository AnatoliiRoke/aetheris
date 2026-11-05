namespace Aetheris.Fleet;

/// <summary>
/// The implementation of <see cref="IFleetModule"/>.
/// </summary>
/// <remarks>
/// The module keeps the projects of the ships in its own tables, so it works on the context of
/// the request (task 00038).
/// </remarks>
public sealed class FleetModule : IFleetModule
{
    /// <inheritdoc />
    public string ModuleName => "Fleet";
}
