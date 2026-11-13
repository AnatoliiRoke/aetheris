import { useCallback, useState } from 'react';
import type { Fleet, FlightPlan } from '../../../api/fleetApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { FleetDialog } from '../fleets/FleetDialog';
import { useRead } from '../market/useRead';
import { noticeFromError } from '../screenNotice';

/**
 * The choice of the fleet that colonizes a planet (task 00073, the button «Колонізувати» of the map).
 *
 * The window lists the fleets of the player that stand at a place and carry a colony module in a hold;
 * the server says which ones do. The fleet the player chooses gets the plan of the colonization from
 * the server, a draft with the flight to the planet and the colonization, and the screen opens it in
 * the window of the flight plan for the player to review and send.
 *
 * @param fleets The fleets of the player.
 * @param planetId The planet to colonize.
 * @param onClose What to do when the player closes the window.
 * @param onPlanned What to do with the plan the server wrote for the chosen fleet.
 */
export function ColonizeFleetDialog({
  fleets,
  planetId,
  onClose,
  onPlanned,
}: {
  fleets: readonly Fleet[];
  planetId: number;
  onClose: () => void;
  onPlanned: (plan: FlightPlan) => void;
}) {
  const { t, language } = useI18n();
  const { fleet: fleetApi } = useApi();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The fleets that stand at a place and carry a colony module: the hold of every one of them says
  // whether its cargo opens the colonization. A player has a few fleets, so they are read at once.
  const loadCandidates = useCallback(async () => {
    const standing = fleets.filter((fleet) => fleet.stateCode === 'stationed');
    const holds = await Promise.all(standing.map((fleet) => fleetApi.fleetHold(fleet.id)));

    return standing.filter((_, index) => holds[index]?.availableOrders?.includes('colonize') === true);
  }, [fleetApi, fleets]);
  const candidatesRead = useRead(loadCandidates, true, false);

  const choose = async (fleetId: number) => {
    setBusy(true);
    setError(null);

    try {
      onPlanned(await fleetApi.colonizeFlightPlan({ fleetId, planetId }));
    } catch (failure) {
      setError(noticeFromError(failure, language, t('fleets.failed'), ['fleet_'])?.text ?? t('fleets.failed'));
    } finally {
      setBusy(false);
    }
  };

  const candidates = candidatesRead.data ?? [];

  return (
    <FleetDialog lead={t('map.colonize.lead')} onClose={onClose} title={t('map.colonize.title')}>
      {error ? (
        <p className="pl-error" role="alert">
          {error}
        </p>
      ) : null}
      {candidatesRead.loading ? <p className="fl-note">{t('map.colonize.loading')}</p> : null}
      {!candidatesRead.loading && candidates.length === 0 ? <p className="fl-note">{t('map.colonize.noFleet')}</p> : null}
      <ul className="map-colonize__fleets">
        {candidates.map((fleet) => (
          <li key={fleet.id}>
            <button className="btn btn--secondary" disabled={busy} onClick={() => void choose(fleet.id)} type="button">
              {fleet.name}
            </button>
          </li>
        ))}
      </ul>
    </FleetDialog>
  );
}
