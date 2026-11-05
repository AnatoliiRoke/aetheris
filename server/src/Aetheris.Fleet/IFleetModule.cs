namespace Aetheris.Fleet;

/// <summary>
/// The public contract of the Fleet module (task 00038): the projects of the ships.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8). The
/// module reads the world, the bodies and the production through their contracts and keeps
/// its own tables.
/// </remarks>
public interface IFleetModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }
}
