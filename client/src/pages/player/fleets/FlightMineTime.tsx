import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ExtractionEstimate, Fleet } from '../../../api/fleetApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { useRead } from '../market/useRead';
import { formatDuration } from '../planetText';

/**
 * The three times of the extraction of a fleet (task 00050).
 *
 * The player names the place the fleet works at, and the window tells him how long the work will
 * last: the time he asked for, the time the holds of the fleet will hold the yield, and the time
 * the place itself will last. The two last times come from the server, which knows the modules of
 * the ships and the mass of the place.
 *
 * @param id The identifier of the order; the fields of the order are named after it.
 * @param fleet The fleet the plan is made for.
 * @param objectId The object of the map the fleet works at.
 * @param durationMinutes The time the player asked for, in minutes.
 * @param onChange Answers the new time of the order.
 */
export function FlightMineTime({
  id,
  fleet,
  objectId,
  durationMinutes,
  onChange,
}: {
  id: string;
  fleet: Fleet | null;
  objectId: number | null;
  durationMinutes: string;
  onChange: (durationMinutes: string) => void;
}) {
  const { t, language } = useI18n();
  const { fleet: fleetApi } = useApi();

  // The estimate of the server: it counts the modules of the ships of the fleet and the mass of the
  // place. The answer belongs to the place it was asked for: when the place changes, the old answer
  // is not the answer anymore.
  const loadEstimate = useCallback(
    (): Promise<ExtractionEstimate | null> =>
      fleet === null || objectId === null ? Promise.resolve(null) : fleetApi.extractionEstimate(fleet.id, objectId),
    [fleetApi, fleet, objectId],
  );
  const estimateRead = useRead(loadEstimate, fleet !== null && objectId !== null, false);

  const key = `${fleet?.id ?? 0}:${objectId ?? 0}`;
  const [answer, setAnswer] = useState<{ key: string; estimate: ExtractionEstimate } | null>(null);

  useEffect(() => {
    if (estimateRead.data !== null) {
      setAnswer({ key, estimate: estimateRead.data });
    }
  }, [key, estimateRead.data]);

  const estimate = answer !== null && answer.key === key ? answer.estimate : null;

  // The time the player asked for: it is his own, not a reading of the server.
  const asked = useMemo(() => {
    const minutes = Number(durationMinutes);

    return Number.isFinite(minutes) && minutes > 0 ? minutes : null;
  }, [durationMinutes]);

  return (
    <>
      <div className="pl-order__row">
        <label className="fl-label" htmlFor={`plan-order-mine-duration-${id}`}>
          {t('plan.mineDuration')}
        </label>
        <input
          autoComplete="off"
          className="fl-input"
          id={`plan-order-mine-duration-${id}`}
          inputMode="numeric"
          min={0}
          onChange={(event) => onChange(event.target.value)}
          type="number"
          value={durationMinutes}
        />
      </div>

      <div className="pl-order__row">
        <span className="fl-label">{t('plan.mineTimeSet')}</span>
        <span className="pl-order__time">{asked === null ? '—' : formatDuration(language, asked * 60)}</span>
      </div>

      <div className="pl-order__row">
        <span className="fl-label">{t('plan.mineTimeHold')}</span>
        <span className="pl-order__time">
          {estimate === null ? '—' : formatDuration(language, estimate.holdFullSeconds)}
        </span>
      </div>

      <div className="pl-order__row">
        <span className="fl-label">{t('plan.mineTimePlace')}</span>
        <span className="pl-order__time">
          {estimate === null ? '—' : formatDuration(language, estimate.placeEmptySeconds)}
        </span>
      </div>

      {estimate === null ? <p className="fl-note">{t('plan.mineUnknown')}</p> : null}

      {estimate === null ? null : (
        <div className="pl-order__row pl-order__row--places-2">
          <span className="fl-label">{t('plan.mineYield')}</span>
          <span className="pl-order__time">{estimate.resourceTonsPerCycle}</span>

          <span className="fl-label">{t('plan.mineHoldFree')}</span>
          <span className="pl-order__time">{estimate.freeHoldTons}</span>

          <span className="fl-label">{t('plan.mineMass')}</span>
          <span className="pl-order__time">{estimate.placeMassTons}</span>
        </div>
      )}
    </>
  );
}
