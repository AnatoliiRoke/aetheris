import { useCallback, useMemo } from 'react';
import type { Fleet, FlightEstimateTarget } from '../../../api/fleetApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { useRead } from '../market/useRead';
import { formatDuration } from '../planetText';
import { estimateTargetOf, type FlightTimeTarget } from './flightPlanParts';

/**
 * The time of the flight of a fleet to the target of an order (task 00053): the time with an empty
 * hold and the time with a full one, and the time with the cargo the fleet carries when it differs.
 *
 * The time is counted by the server: the distance comes from the world and the speed from the ships
 * of the fleet, so the window shows the same time the fleet will take. The window does not count
 * anything itself.
 *
 * @param fleet The fleet the plan is made for.
 * @param typeCode `fly`, `hyperfly` or `jump` for a flight to the target, counted with the drives of the order (task 00072); `return` for the flight to the home body of the fleet.
 * @param target The place the flight goes to; it is not used for the return.
 * @param from The place the flight starts at, when an order before it brings the fleet elsewhere.
 */
export function FlightPlanTime({
  fleet,
  typeCode,
  target,
  from,
}: {
  fleet: Fleet | null;
  typeCode: string;
  target?: FlightTimeTarget;
  from?: FlightTimeTarget | null;
}) {
  const fleetId = fleet?.id ?? null;
  const place = useMemo(() => (target === undefined ? null : estimateTargetOf(target)), [target]);
  const start = useMemo(() => (from === undefined || from === null ? null : estimateTargetOf(from)), [from]);

  // The return names no target: it goes to the home body of the fleet. A flight needs its target whole.
  if (fleetId === null || (typeCode !== 'return' && place === null)) {
    return null;
  }

  // The answer belongs to the question it was asked for: when the place changes, the line is made anew, so the
  // time of the place that was chosen before never stays on the screen.
  const key = JSON.stringify([fleetId, typeCode, place, start]);

  return <FlightEstimateLine fleetId={fleetId} from={start} key={key} place={place} typeCode={typeCode} />;
}

/**
 * The lines of the time of one question to the server (task 00053).
 */
function FlightEstimateLine({
  fleetId,
  typeCode,
  place,
  from,
}: {
  fleetId: number;
  typeCode: string;
  place: FlightEstimateTarget | null;
  from: FlightEstimateTarget | null;
}) {
  const { t, language } = useI18n();
  const { fleet: fleetApi } = useApi();

  const load = useCallback(
    () => fleetApi.flightEstimate({ fleetId, typeCode, target: place ?? undefined, from: from ?? undefined }),
    [fleetApi, fleetId, typeCode, place, from],
  );
  const read = useRead(load, true, false);
  const estimate = read.data;

  // The window never hides the time without a word: when the server did not answer, it says so.
  if (estimate === null) {
    return read.failed ? <p className="fl-note">{t('plan.flightTimeFailed')}</p> : null;
  }

  if (!estimate.canFly) {
    return <p className="fl-note">{t('plan.noDrive')}</p>;
  }

  return (
    <>
      <div className="pl-order__row">
        <span className="fl-label">{t('plan.flightTime')}</span>
        <span className="pl-order__time">
          {t('plan.flightTimeRange', {
            from: formatDuration(language, estimate.emptySeconds),
            to: formatDuration(language, estimate.fullSeconds),
          })}
        </span>
      </div>
      {estimate.currentSeconds !== estimate.emptySeconds ? (
        <div className="pl-order__row">
          <span className="fl-label">{t('plan.flightTimeCurrent')}</span>
          <span className="pl-order__time">{formatDuration(language, estimate.currentSeconds)}</span>
        </div>
      ) : null}
    </>
  );
}
