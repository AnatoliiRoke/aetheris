using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The terraforming: a ship turns a one-shot device on at a free body, and the deposits of the
/// body become rich (task 00014, section 9).
/// </summary>
/// <remarks>
/// <para>
/// The terraforming is no process of the queue of a building: it has no kind, no recipe and
/// no place in a queue, and the engine of the processes does not know it. The ship starts it
/// at once, and the device is spent at the start; nothing gives it back.
/// </para>
/// <para>
/// Until the task of the flights the device comes from the storage of a colony of the player
/// and the ship by the target is the stub <see cref="IShipsOnOrbit"/>, as before the change.
/// Only a free body without a colony is a target, and only once: the world marks the body as
/// terraformed, and the body stays free for whoever colonizes it.
/// </para>
/// <para>
/// A part is a device when it has the attribute of the multiplier of the deposits; the
/// multiplier must lie in the range of the configuration. The type of the body, its size and
/// its buildings do not change.
/// </para>
/// </remarks>
public sealed class TerraformingHandler
{
    /// <summary>
    /// The reason why the deposits of a body become rich, in the journal of the world.
    /// </summary>
    public const string Reason = "terraforming";

    private readonly IWorldModule _world;
    private readonly IShipsOnOrbit _ships;
    private readonly PartStockService _parts;
    private readonly ProcessesUnitOfWork _unitOfWork;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerraformingHandler"/> class.
    /// </summary>
    /// <param name="world">The world module, for the bodies and their deposits.</param>
    /// <param name="ships">The ships on the orbit of the bodies.</param>
    /// <param name="parts">The storage of the parts of the production module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="config">The configuration of the game, for the range of the multiplier.</param>
    public TerraformingHandler(
        IWorldModule world,
        IShipsOnOrbit ships,
        PartStockService parts,
        ProcessesUnitOfWork unitOfWork,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(config);

        _world = world;
        _ships = ships;
        _parts = parts;
        _unitOfWork = unitOfWork;
        _config = config;
    }

    /// <summary>
    /// Reads the terraforming a player may start from a colony: the devices of its storage and
    /// the free bodies of its sector the player sees.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The colony the ship takes the device from.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The terraforming of the colony.</returns>
    /// <exception cref="GameException">The body is not a colony of the player.</exception>
    public async Task<TerraformingDto> ReadAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts colony = await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        ProcessesSettings settings = ProcessesSettings.From(_config);

        List<PartFacts> devices =
        [
            .. (await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false)).Values
                .Where(part => part.TerraformingMultiplier is not null)
                .OrderBy(part => part.PartTypeId),
        ];

        IReadOnlyList<PartStackInfo> stacks = await _parts
            .ReadAsync(planetId, [.. devices.Select(device => device.PartTypeId)], cancellationToken)
            .ConfigureAwait(false);

        // A device of the storage is a new part: the start takes a new one, like the processes do.
        List<TerraformingDeviceDto> stored =
        [
            .. devices
                .Select(device => new TerraformingDeviceDto(
                    device.PartTypeId,
                    device.NameKey,
                    device.MarkLevel,
                    device.TerraformingMultiplier!.Value,
                    stacks.Where(stack => stack.PartTypeId == device.PartTypeId && stack.ConditionPercent >= PartCondition.Full).Sum(stack => stack.Quantity)))
                .Where(device => device.Quantity > 0),
        ];

        IReadOnlyList<FreePlanetDto> free = await _world.GetFreePlanetsAsync(playerId, colony.SectorId, cancellationToken).ConfigureAwait(false);

        return new TerraformingDto(
            planetId,
            stored,
            [.. free.Select(body => new TerraformingTargetDto(body.PlanetId, body.SystemId, body.ObjectTypeId, body.Orbit, body.Name, body.IsTerraformed))],
            settings.TerraformingMultiplierMin,
            settings.TerraformingMultiplierMax);
    }

    /// <summary>
    /// Turns a device of the storage of a colony on at a free body: the device is spent and the
    /// deposits of the body become rich, in one transaction.
    /// </summary>
    /// <param name="playerId">The player, from the session.</param>
    /// <param name="planetId">The colony the ship takes the device from.</param>
    /// <param name="targetPlanetId">The free body.</param>
    /// <param name="partTypeId">The device.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome with the terraforming of the colony after the change.</returns>
    /// <exception cref="GameException">A rule of the terraforming is not met; nothing changes.</exception>
    public async Task<TerraformingResultDto> LaunchAsync(
        string playerId,
        long planetId,
        long targetPlanetId,
        int partTypeId,
        CancellationToken cancellationToken)
    {
        (decimal multiplier, int changed) = await _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureOwnedAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                // The world checks the target again under the lock of its row; this check only
                // gives the player the reason before anything is taken.
                PlanetFacts target = await _world.GetPlanetFactsAsync(targetPlanetId, cancellationToken).ConfigureAwait(false)
                    ?? throw new GameException(ProcessErrorCodes.TargetUnknown, "The body does not exist.");

                if (target.OwnerPlayerId is not null)
                {
                    throw new GameException(ProcessErrorCodes.TargetColonized, "Only a free body is terraformed.");
                }

                if (target.IsTerraformed)
                {
                    throw new GameException(ProcessErrorCodes.TargetTerraformed, "The body is terraformed already.");
                }

                // The ship that turns the device on stands by the body.
                if (await _ships.FindAsync(playerId, targetPlanetId, cancellationToken).ConfigureAwait(false) is null)
                {
                    throw new GameException(ProcessErrorCodes.NoShipOnOrbit, "No ship of the player stands by the body.");
                }

                IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

                if (!parts.TryGetValue(partTypeId, out PartFacts? device) || device.TerraformingMultiplier is not { } value)
                {
                    throw new GameException(ProcessErrorCodes.DeviceMissing, "The part is no device of the terraforming.");
                }

                ProcessesSettings settings = ProcessesSettings.From(_config);

                if (value <= settings.TerraformingMultiplierMin || value > settings.TerraformingMultiplierMax)
                {
                    throw new GameException(ProcessErrorCodes.ParamMissing, "The multiplier of the device is out of the range of the configuration.");
                }

                // The device is spent at the start: the storage is locked first, then the body,
                // so two starts of one colony wait for each other and never cross.
                try
                {
                    await _parts.TakeStackAsync(planetId, partTypeId, PartCondition.Full, 1, cancellationToken).ConfigureAwait(false);
                }
                catch (GameException exception) when (exception.Code == ProductionErrorCodes.PartMissing)
                {
                    throw new GameException(ProcessErrorCodes.PartMissing, "The storage of the colony holds no such device.");
                }

                TerraformResult result = await _world
                    .TerraformAsync(targetPlanetId, value, playerId, Reason, cancellationToken)
                    .ConfigureAwait(false);

                // A refusal of the world rolls the transaction back, so the device stays.
                return result.Outcome switch
                {
                    TerraformOutcomes.Done => (value, result.ChangedDeposits),
                    TerraformOutcomes.Colonized => throw new GameException(ProcessErrorCodes.TargetColonized, "Only a free body is terraformed."),
                    TerraformOutcomes.AlreadyTerraformed => throw new GameException(ProcessErrorCodes.TargetTerraformed, "The body is terraformed already."),
                    _ => throw new GameException(ProcessErrorCodes.TargetUnknown, "The body does not exist."),
                };
            },
            cancellationToken).ConfigureAwait(false);

        TerraformingDto terraforming = await ReadAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        return new TerraformingResultDto(targetPlanetId, partTypeId, multiplier, changed, terraforming);
    }

    /// <summary>
    /// Checks that the body is a colony of the player, from the facts of the world and never
    /// from the word of the client.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts of the body.</returns>
    /// <exception cref="GameException">The body is not a colony of the player.</exception>
    private async Task<PlanetFacts> EnsureOwnedAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(ProcessErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }

        return facts;
    }
}
