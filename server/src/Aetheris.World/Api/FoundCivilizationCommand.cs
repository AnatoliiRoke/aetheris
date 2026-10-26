using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.World.Services;
using FluentValidation;

namespace Aetheris.World.Api;

/// <summary>
/// The first step of the game: choose a race and name the home body (task 00006,
/// decision 9).
/// </summary>
/// <remarks>
/// It runs through the command pipeline of the core in one transaction with the stored
/// response. It is a setting of the account and the body, so it is allowed before the
/// e-mail confirmation (through the database list) and, of course, before the onboarding.
/// </remarks>
public sealed class FoundCivilizationCommand : Command<FoundCivilizationCommand.Request, CivilizationSummary>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/world/civilization";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "civilization.setup";

    private readonly CivilizationService _civilizations;

    /// <summary>
    /// Initializes a new instance of the <see cref="FoundCivilizationCommand"/> class.
    /// </summary>
    /// <param name="civilizations">The civilization service.</param>
    public FoundCivilizationCommand(CivilizationService civilizations)
    {
        ArgumentNullException.ThrowIfNull(civilizations);

        _civilizations = civilizations;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override bool RequiresOnboarding => false;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.RaceId).NotNull();
        rules.RuleFor(request => request.PlanetName).NotEmpty();
    }

    /// <inheritdoc />
    public override Task<CivilizationSummary> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return _civilizations.FoundAsync(context.PlayerId, request.RaceId!.Value, request.PlanetName!, cancellationToken);
    }

    /// <summary>
    /// The request: the choice of the player.
    /// </summary>
    /// <param name="RaceId">The race.</param>
    /// <param name="PlanetName">The name of the home body.</param>
    public sealed record Request(
        [property: JsonPropertyName("race_id")] int? RaceId,
        [property: JsonPropertyName("planet_name")] string? PlanetName);
}
