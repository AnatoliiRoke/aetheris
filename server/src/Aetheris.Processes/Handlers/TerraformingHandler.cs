using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Services;
using Aetheris.Production.Services;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The terraforming: a ship turns a one-shot device from its hold on at a free body, and the
/// deposits of the body become rich (task 00014, sections 9 and 11).
/// </summary>
/// <remarks>
/// <para>
/// The terraforming is no process of the queue of a building: it has no kind, no recipe and
/// no place in a queue, and the engine of the processes does not know it. The ship starts it
/// at once, and the device is spent at the start; nothing gives it back.
/// </para>
/// <para>
/// The device is cargo of one named ship that stands by the target, never a part of the
/// storage of a colony (task 00010, the general rule of the cargo). Until the task of the
/// fleet the ships are the stub <see cref="IShipsOnOrbit"/>. Only a free body that may be
/// settled is a target, so a gas giant is refused by the data of its type, and only once: the
/// world marks the body as terraformed, and the body stays free for whoever colonizes it.
/// </para>
/// <para>
/// A part is a device when it carries the mark of the device in its attributes. The multiplier
/// is no attribute of the part: the world rolls one for every deposit in the range of the
/// configuration. The type of the body, its size and its buildings do not change.
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
    /// <param name="ships">The ships on the orbit of the bodies, with their holds.</param>
    /// <param name="parts">The facts of the parts of the production module, to tell a device.</param>
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
    /// Reads the terraforming a player may start: the free bodies that may be settled with a
    /// ship of the player by them, and the devices in the holds of those ships. The terraforming
    /// belongs to the ships, not to a colony, so nothing here names a colony.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The terraforming of the player.</returns>
    public async Task<TerraformingDto> ReadAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        ProcessesSettings settings = ProcessesSettings.From(_config);
        IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

        // The bodies the ships of the player stand by; a ship sees the body it stands by.
        IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>> shipsByBody = await _ships
            .ListAllAsync(playerId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<FreePlanetDto> free = await _world
            .GetFreePlanetsByIdsAsync([.. shipsByBody.Keys], cancellationToken)
            .ConfigureAwait(false);

        List<TerraformingTargetDto> targets = [];

        foreach (FreePlanetDto body in free)
        {
            List<TerraformingShipDto> ships = [];

            foreach (ShipOnOrbit ship in shipsByBody.GetValueOrDefault(body.PlanetId) ?? [])
            {
                // The hold carries many things; only the devices are the terraforming's.
                List<TerraformingDeviceDto> devices =
                [
                    .. ship.Cargo
                        .Where(cargo => cargo.Quantity > 0 && parts.TryGetValue(cargo.PartTypeId, out PartFacts? facts) && facts.IsTerraformingDevice)
                        .OrderBy(cargo => cargo.PartTypeId)
                        .Select(cargo => new TerraformingDeviceDto(
                            cargo.PartTypeId,
                            parts[cargo.PartTypeId].NameKey,
                            parts[cargo.PartTypeId].MarkLevel,
                            cargo.Quantity)),
                ];

                if (devices.Count > 0)
                {
                    ships.Add(new TerraformingShipDto(ship.ShipId, devices));
                }
            }

            targets.Add(new TerraformingTargetDto(
                body.PlanetId,
                body.SystemId,
                body.ObjectTypeId,
                body.Orbit,
                body.Name,
                body.IsTerraformed,
                ships));
        }

        return new TerraformingDto(targets, settings.TerraformingMultiplierMin, settings.TerraformingMultiplierMax);
    }

    /// <summary>
    /// Turns a device from the hold of a ship on at the free body the ship stands by: the
    /// device is spent and every deposit of the body rolls its own multiplier, in one
    /// transaction.
    /// </summary>
    /// <param name="playerId">The player, from the session.</param>
    /// <param name="targetPlanetId">The free body.</param>
    /// <param name="shipId">The ship whose hold carries the device.</param>
    /// <param name="partTypeId">The device.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome with the terraforming of the colony after the change.</returns>
    /// <exception cref="GameException">A rule of the terraforming is not met; nothing changes.</exception>
    public async Task<TerraformingResultDto> LaunchAsync(
        string playerId,
        long targetPlanetId,
        long shipId,
        int partTypeId,
        CancellationToken cancellationToken)
    {
        TerraformResult result = await _unitOfWork.RunAsync(
            async () =>
            {
                // The world checks the target again under the lock of its row; this check only
                // gives the player the reason before anything is taken.
                PlanetFacts target = await _world.GetPlanetFactsAsync(targetPlanetId, cancellationToken).ConfigureAwait(false)
                    ?? throw new GameException(ProcessErrorCodes.TargetUnknown, "The body does not exist.");

                if (!target.IsColonizable)
                {
                    throw new GameException(ProcessErrorCodes.TargetNotColonizable, "Only a body that may be settled is terraformed.");
                }

                if (target.OwnerPlayerId is not null)
                {
                    throw new GameException(ProcessErrorCodes.TargetColonized, "Only a free body is terraformed.");
                }

                if (target.IsTerraformed)
                {
                    throw new GameException(ProcessErrorCodes.TargetTerraformed, "The body is terraformed already.");
                }

                // The ship that turns the device on is one of the player's own and stands by
                // the body; the device is in its hold and nowhere else.
                IReadOnlyDictionary<long, IReadOnlyList<ShipOnOrbit>> shipsByBody = await _ships
                    .ListAsync(playerId, [targetPlanetId], cancellationToken)
                    .ConfigureAwait(false);

                ShipOnOrbit ship = shipsByBody.GetValueOrDefault(targetPlanetId)?.FirstOrDefault(entry => entry.ShipId == shipId)
                    ?? throw new GameException(ProcessErrorCodes.NoShipOnOrbit, "The ship of the player does not stand by the body.");

                IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

                if (!parts.TryGetValue(partTypeId, out PartFacts? device) || !device.IsTerraformingDevice)
                {
                    throw new GameException(ProcessErrorCodes.DeviceMissing, "The part is no device of the terraforming.");
                }

                if (ship.Cargo.Where(cargo => cargo.PartTypeId == partTypeId).Sum(cargo => cargo.Quantity) < 1)
                {
                    throw new GameException(ProcessErrorCodes.DeviceNotInHold, "The hold of the ship carries no such device.");
                }

                ProcessesSettings settings = ProcessesSettings.From(_config);

                if (settings.TerraformingMultiplierMin <= decimal.Zero || settings.TerraformingMultiplierMax < settings.TerraformingMultiplierMin)
                {
                    throw new GameException(ProcessErrorCodes.ParamMissing, "The range of the multiplier of the configuration is not valid.");
                }

                // The world locks the body first, so two starts on one body wait for each other;
                // the device leaves the hold only when the body took the terraforming, and a
                // refusal of either rolls the transaction back, so nothing is half done.
                TerraformResult terraformed = await _world
                    .TerraformAsync(targetPlanetId, settings.TerraformingMultiplierMin, settings.TerraformingMultiplierMax, playerId, Reason, cancellationToken)
                    .ConfigureAwait(false);

                switch (terraformed.Outcome)
                {
                    case TerraformOutcomes.Done:
                        break;
                    case TerraformOutcomes.NotColonizable:
                        throw new GameException(ProcessErrorCodes.TargetNotColonizable, "Only a body that may be settled is terraformed.");
                    case TerraformOutcomes.Colonized:
                        throw new GameException(ProcessErrorCodes.TargetColonized, "Only a free body is terraformed.");
                    case TerraformOutcomes.AlreadyTerraformed:
                        throw new GameException(ProcessErrorCodes.TargetTerraformed, "The body is terraformed already.");
                    default:
                        throw new GameException(ProcessErrorCodes.TargetUnknown, "The body does not exist.");
                }

                if (!await _ships.TakeCargoAsync(playerId, targetPlanetId, shipId, partTypeId, 1, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(ProcessErrorCodes.DeviceNotInHold, "The hold of the ship carries no such device.");
                }

                return terraformed;
            },
            cancellationToken).ConfigureAwait(false);

        TerraformingDto terraforming = await ReadAsync(playerId, cancellationToken).ConfigureAwait(false);

        return new TerraformingResultDto(
            targetPlanetId,
            shipId,
            partTypeId,
            result.ChangedDeposits,
            [.. result.Deposits.Select(deposit => new TerraformedDepositDto(deposit.ResourceTypeId, deposit.OldCoefficient, deposit.NewCoefficient))],
            terraforming);
    }
}
