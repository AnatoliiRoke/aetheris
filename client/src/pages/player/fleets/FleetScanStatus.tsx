import type { FlightPlanOrder } from '../../../api/fleetApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatDuration } from '../planetText';
import { SecondsLeftText } from '../QueueItem';
import type { FleetsClock } from './FleetFlightStatus';

/** The countdown of a running scan, isolated from the fleet screen. */
export function FleetScanStatus({ order, clock, onDone }: { order: FlightPlanOrder; clock: FleetsClock | null; onDone: () => void }) {
  const { t, language } = useI18n();
  let start = Number.NaN;
  try {
    const state = JSON.parse(order.parameters) as { StartedAt?: string };
    start = Date.parse(state.StartedAt ?? '');
  } catch {
    // An unreadable state cannot provide a deadline.
  }
  const deadline = clock && Number.isFinite(start)
    ? clock.receivedAt + ((start + order.durationSeconds * 1000 - Date.parse(clock.gameNow)) / (clock.timeMultiplier > 0 ? clock.timeMultiplier : 1))
    : null;
  return <span className="fl-row fl-row--soft"><SecondsLeftText deadline={deadline} onDone={onDone} render={(seconds) => t('plan.scanning', { time: seconds === null ? '—' : formatDuration(language, seconds) })} /></span>;
}
