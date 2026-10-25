using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.World.Services;

namespace Aetheris.World.Api;

/// <summary>
/// The command of a newcomer: take the start body.
/// </summary>
/// <remarks>
/// It runs through the command pipeline of the core: a signed-in player, the command rate
/// limit, the request identifier and one transaction. The allocation itself is atomic and
/// happens once per player.
/// </remarks>
public sealed class AllocateStartPlanetCommand : Command<AllocateStartPlanetCommand.Request, AllocateStartPlanetCommand.Response>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/world/start-planet";

    private readonly StartPlanetAllocator _allocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocateStartPlanetCommand"/> class.
    /// </summary>
    /// <param name="allocator">The start body allocator.</param>
    public AllocateStartPlanetCommand(StartPlanetAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(allocator);

        _allocator = allocator;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        StartPlanetResult result = await _allocator.AllocateAsync(context.PlayerId, cancellationToken).ConfigureAwait(false);

        return new Response(result.PlanetId, result.CelestialObjectId, result.SystemId, result.SectorId, result.AlreadyAllocated);
    }

    /// <summary>
    /// The request: the intent only, without any choice.
    /// </summary>
    public sealed record Request;

    /// <summary>
    /// The start body.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="CelestialObjectId">Its celestial object.</param>
    /// <param name="SystemId">The star system.</param>
    /// <param name="SectorId">The sector.</param>
    /// <param name="AlreadyAllocated">Whether the player had it before.</param>
    public sealed record Response(
        [property: JsonPropertyName("planet_id")] long PlanetId,
        [property: JsonPropertyName("celestial_object_id")] long CelestialObjectId,
        [property: JsonPropertyName("system_id")] long SystemId,
        [property: JsonPropertyName("sector_id")] int SectorId,
        [property: JsonPropertyName("already_allocated")] bool AlreadyAllocated);
}
