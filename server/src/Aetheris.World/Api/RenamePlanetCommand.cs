using System.Globalization;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.World;
using Aetheris.World.Services;
using FluentValidation;

namespace Aetheris.World.Api;

/// <summary>
/// Renames a body the player owns. The rules are the ones of the name chosen at the first step.
/// </summary>
public sealed class RenamePlanetCommand : Command<RenamePlanetCommand.Request, PlanetNameDto>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/world/planets/rename";

    /// <summary>The action code of the command.</summary>
    public const string Code = "world.planet_rename";

    private readonly CivilizationService _civilizations;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenamePlanetCommand"/> class.
    /// </summary>
    /// <param name="civilizations">The civilization service.</param>
    public RenamePlanetCommand(CivilizationService civilizations)
    {
        ArgumentNullException.ThrowIfNull(civilizations);

        _civilizations = civilizations;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.PlanetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Name).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) =>
        request.PlanetId is long planetId
            ? new OwnedObjectReference(PlanetOwnerResolver.Type, planetId.ToString(CultureInfo.InvariantCulture))
            : null;

    /// <inheritdoc />
    public override Task<PlanetNameDto> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return _civilizations.RenameAsync(context.PlayerId, request.PlanetId!.Value, request.Name!, cancellationToken);
    }

    /// <summary>
    /// The request: the body and the new name.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="Name">The new name.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("name")] string? Name);
}
