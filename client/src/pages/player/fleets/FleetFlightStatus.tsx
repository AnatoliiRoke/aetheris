import type { FleetFlight } from '../../../api/fleetApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatDuration } from '../planetText';
import { SecondsLeftText } from '../QueueItem';
import { FlightPlanPlace } from './FlightPlanPlace';

/**
 * What the screen knows about the clock of the game when it read the fleets (task 00053): the game time
 * of the answer, how fast the game time runs and the moment the answer came on the clock of the browser.
 */
export interface FleetsClock {
  /** The game time of the answer. */
  gameNow: string;
  /** How many game seconds pass in a real second. */
  timeMultiplier: number;
  /** The moment the answer came, on the clock of the browser, in milliseconds. */
  receivedAt: number;
}

/**
 * The state of a fleet that is under way (task 00053): where it flies and how long it takes to arrive.
 *
 * The time left counts down in this component alone, so only it is drawn again every second, never the
 * screen; when the time runs out it tells it once, and the screen reads the fleets again.
 *
 * @param flight The flight of the fleet.
 * @param clock The clock of the game as the screen read it.
 * @param onArrived What to do when the time left runs out.
 */
export function FleetFlightStatus({ flight, clock, onArrived }: { flight: FleetFlight; clock: FleetsClock | null; onArrived: () => void }) {
  const { t, language } = useI18n();

  // The moment of the arrival on the clock of the browser: the game seconds that are left, over the speed
  // of the game time, from the moment the answer came.
  const deadline =
    clock === null
      ? null
      : clock.receivedAt +
        ((Date.parse(flight.arrivesAt) - Date.parse(clock.gameNow)) / (clock.timeMultiplier > 0 ? clock.timeMultiplier : 1));

  return (
    <p className="fl-row fl-row--soft fl-flight">
      <span>
        {t('plan.flightTo')}{' '}
        {flight.toObjectId === null ? (
          <b>
            {Math.round(flight.toX)} / {Math.round(flight.toY)} / {Math.round(flight.toZ)}
          </b>
        ) : (
          <b>
            <FlightPlanPlace objectId={flight.toObjectId} sectorId={flight.toSectorId} systemId={flight.toSystemId ?? 0} />
          </b>
        )}
      </span>
      <span>
        <SecondsLeftText
          deadline={deadline}
          onDone={onArrived}
          render={(secondsLeft) =>
            secondsLeft === null
              ? t('plan.arrivesAt', { time: new Date(flight.arrivesAt).toLocaleString(language) })
              : t('plan.arrivesIn', { time: formatDuration(language, secondsLeft) })
          }
        />
      </span>
    </p>
  );
}
