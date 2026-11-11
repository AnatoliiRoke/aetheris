import type { Fleet } from '../../../api/fleetApi';
import { useI18n } from '../../../i18n/useI18n';
import { FlightPlanPlace } from './FlightPlanPlace';
import { useFleetsScene } from './useFleetsScene';

/**
 * The place a fleet stands at, with its label (task 00053): «in orbit» of a body with the system and the
 * orbit of it, or «at» the object of the map the fleet stands at, or at the coordinates of the point of the
 * space it hangs at.
 *
 * @param fleet The fleet.
 * @param compact Whether to leave the name of the object out and write its coordinates: the cards of the
 * list stay cheap and do not read the map of every fleet.
 */
export function FleetPlace({ fleet, compact = false }: { fleet: Fleet; compact?: boolean }) {
  const { t } = useI18n();
  const scene = useFleetsScene();

  if (fleet.planetId > 0) {
    return <>{t('fleets.place', { place: scene.placeOf(fleet.planetId) })}</>;
  }

  if (!compact && fleet.objectId !== null && fleet.objectId !== undefined) {
    return (
      <>
        {t('fleets.placeAt')}{' '}
        <FlightPlanPlace objectId={fleet.objectId} sectorId={fleet.sectorId} systemId={fleet.systemId ?? 0} />
      </>
    );
  }

  if (fleet.positionX !== null && fleet.positionX !== undefined) {
    return (
      <>
        {t('fleets.placeAt')} {Math.round(fleet.positionX)} / {Math.round(fleet.positionY ?? 0)} / {Math.round(fleet.positionZ ?? 0)}
      </>
    );
  }

  return <>{t('fleets.placeUnknown')}</>;
}
