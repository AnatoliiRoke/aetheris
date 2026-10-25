namespace Aetheris.Chat;

/// <summary>
/// Marker contract for the Chat module.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01,
/// section 8). This interface is the module's public surface; everything else in
/// <c>Aetheris.Chat</c> is internal to the module.
/// </remarks>
public interface IChatModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }
}
