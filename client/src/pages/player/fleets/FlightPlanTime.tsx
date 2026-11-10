import { useCallback, useEffect, useMemo, useState } from 'react';
import type { Fleet, ShipDesign } from '../../../api/fleetApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { useRead } from '../market/useRead';
import { formatDuration } from '../planetText';
import { readAxes, type TargetKind } from './flightPlanParts';
import { fleetSpeeds, flightMinutes } from './flightPlanSpeed';

/**
 * The place the time of the flight is counted to (task 00050).
 */
export interface FlightTimeTarget {
  /** The kind of the target: the coordinates of a point, a planet or an object. */
  kind: TargetKind;
  /** The sector of the target. */
  sectorId: number | null;
  /** The system of the target. */
  systemId: number | null;
  /** The body of the target, as the map knows it: the identifier of the object. */
  objectId: number | null;
  /** The body of the target, as the planet of the player: the identifier of the planet. */
  planetId: number | null;
  /** The coordinates of the target, when the target is a point of the space. */
  point: string;
}

/**
 * The time of the flight of a fleet to the target of an order (task 00050): the time with an empty
 * hold and the time with a full one.
 *
 * The distance comes from the map of the world: the server counts it from the body the fleet stands
 * on. The speed comes from the ships of the fleet: the fleet flies at the speed of its slowest ship.
 *
 * @param fleet The fleet the plan is made for.
 * @param designs The projects of the ships of the fleet.
 * @param target The place the time is counted to.
 */
export function FlightPlanTime({
  fleet,
  designs,
  target,
}: {
  fleet: Fleet | null;
  designs: readonly ShipDesign[];
  target: FlightTimeTarget;
}) {
  const { t, language } = useI18n();
  const { map } = useApi();

  const { kind, sectorId, systemId, objectId, planetId, point } = target;

  // The body the fleet stands on now: the server counts the distance from it.
  const fromPlanetId = fleet?.planetId ?? null;

  // The distance to a body of a system: the map of the system gives it. The body is found by the
  // identifier of the planet when the target names a planet of the player, and by the identifier of
  // the object otherwise.
  const loadSystem = useCallback(
    async (): Promise<number | null> => {
      if (systemId === null || fromPlanetId === null) {
        return null;
      }

      const view = await map.system(systemId, fromPlanetId);
      const object =
        planetId === null
          ? view.objects.find((entry) => entry.id === objectId)
          : view.objects.find((entry) => entry.planetId === planetId);

      return object?.distance ?? null;
    },
    [map, systemId, objectId, planetId, fromPlanetId],
  );
  const systemRead = useRead(loadSystem, kind === 'planet' && systemId !== null, false);

  // The distance to a free field of a sector: the map of the sector gives it.
  const loadSector = useCallback(
    async (): Promise<number | null> => {
      if (sectorId === null || fromPlanetId === null) {
        return null;
      }

      const view = await map.sector(sectorId, fromPlanetId);
      const field = view.fields.find((entry) => entry.objectId === objectId);

      return field?.distance ?? null;
    },
    [map, sectorId, objectId, fromPlanetId],
  );
  const sectorRead = useRead(loadSector, kind === 'object' && sectorId !== null && objectId !== null, false);

  // The distance to a point of the space: the map counts it to the point.
  const axes = useMemo(() => readAxes(point), [point]);

  const loadPoint = useCallback(
    async (): Promise<number | null> => {
      if (axes === null || fromPlanetId === null || sectorId === null) {
        return null;
      }

      const info = await map.target(sectorId, axes[0], axes[1], axes[2], fromPlanetId);

      return info.distance;
    },
    [map, axes, sectorId, fromPlanetId],
  );
  const pointRead = useRead(loadPoint, kind === 'point' && axes !== null && sectorId !== null, false);

  // The distance comes from the source that answers the kind of the target that is chosen now. A
  // read keeps its last answer when it is turned off, so taking the first non-empty one would show
  // the time of the target that was chosen before.
  const distance = kind === 'planet' ? systemRead.data : kind === 'object' ? sectorRead.data : pointRead.data;

  // The answer of the server belongs to the target that was chosen when it was asked for. When the
  // target changes, the old answer is not the answer anymore: the time waits for the new one.
  const key = `${kind}:${sectorId ?? 0}:${systemId ?? 0}:${objectId ?? 0}:${planetId ?? 0}:${point}`;
  const [answer, setAnswer] = useState<{ key: string; distance: number } | null>(null);

  // The answer is only the answer when it came for the target that is chosen now.
  const current = answer !== null && answer.key === key ? answer.distance : null;

  useEffect(() => {
    if (distance !== null) {
      setAnswer({ key, distance });
    }
  }, [key, distance]);

  const speeds = useMemo(() => (fleet === null ? null : fleetSpeeds(fleet, designs)), [fleet, designs]);

  const minutes = useMemo(() => {
    if (current === null || speeds === null) {
      return null;
    }

    return flightMinutes(current, speeds.empty, speeds.full);
  }, [current, speeds]);

  if (minutes === null) {
    return null;
  }

  return (
    <div className="pl-order__row">
      <span className="fl-label">{t('plan.flightTime')}</span>
      <span className="pl-order__time">
        {t('plan.flightTimeRange', {
          from: formatDuration(language, minutes.empty * 60),
          to: formatDuration(language, minutes.full * 60),
        })}
      </span>
    </div>
  );
}
