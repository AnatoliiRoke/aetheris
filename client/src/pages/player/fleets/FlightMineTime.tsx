import { useI18n } from '../../../i18n/useI18n';

/**
 * The time of the extraction of a fleet (task 00054).
 *
 * The order of extraction names no place: the player gives only the time the fleet works for, and the
 * fleet takes from the place where it stands when the order begins. The one-line hint under the
 * field says so.
 *
 * @param id The identifier of the order; the fields of the order are named after it.
 * @param durationMinutes The time the player asked for, in minutes.
 * @param onChange Answers the new time of the order.
 */
export function FlightMineTime({
  id,
  durationMinutes,
  onChange,
}: {
  id: string;
  durationMinutes: string;
  onChange: (durationMinutes: string) => void;
}) {
  const { t } = useI18n();

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

      <p className="fl-note">{t('plan.mineHint')}</p>
    </>
  );
}
