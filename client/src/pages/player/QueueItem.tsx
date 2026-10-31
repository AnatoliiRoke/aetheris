import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useI18n } from '../../i18n/useI18n';
import { clockTickMs } from './clock';
import { formatDuration } from './planetText';
import { reportRender } from './renderProbe';

/**
 * A record of a queue as the standard queue shows it. A queue of the buildings and a queue of
 * the researches are mapped to this one shape, so every queue of the game looks and behaves the
 * same (task 00020, section 8.1).
 */
export interface QueueEntry {
  /** The identifier of the record on the server. */
  id: number;
  /** The text of the record, for example the name and the level. */
  title: string;
  /** Whether the record is under way; a waiting record has no timer yet. */
  underWay: boolean;
  /** The moment the record ends on the clock of the browser, in milliseconds; `null` while it waits. */
  deadline: number | null;
  /** How many real seconds the record takes in all. */
  durationSeconds: number;
  /** The costs and other details under the title. */
  details: string;
  /** The label of the cancel button for a screen reader. */
  cancelLabel: string;
  /**
   * Whether the record cannot be cancelled once its time has run out: a building or a research
   * is finished by the server then, while a batch of a factory still has units to cancel.
   */
  locksCancelWhenDue?: boolean;
}

const millisecondsPerSecond = 1000;
const fullPercent = 100;

/**
 * The seconds left to a moment of the clock of the browser. The component that shows the
 * timer owns the tick, so only this component is drawn again every second, never the page.
 * When the time runs out it tells it once, so the page asks the server again.
 *
 * @param deadline The moment the record ends, in milliseconds, or `null` for a waiting record.
 * @param onDone What to do when the time runs out.
 */
function useSecondsLeft(deadline: number | null, onDone: () => void): number | null {
  const [now, setNow] = useState(() => Date.now());
  const reportedFor = useRef<number | null>(null);
  const callback = useRef(onDone);

  useEffect(() => {
    callback.current = onDone;
  }, [onDone]);

  useEffect(() => {
    if (deadline === null) {
      return undefined;
    }

    const timer = window.setInterval(() => setNow(Date.now()), clockTickMs);

    return () => window.clearInterval(timer);
  }, [deadline]);

  const secondsLeft = deadline === null ? null : (deadline - now) / millisecondsPerSecond;

  useEffect(() => {
    if (deadline !== null && secondsLeft !== null && secondsLeft <= 0 && reportedFor.current !== deadline) {
      reportedFor.current = deadline;
      callback.current();
    }
  }, [deadline, secondsLeft]);

  return secondsLeft;
}

/**
 * A record of a queue: the text, the timer, the progress and the cancel.
 *
 * @param entry The record.
 * @param underWayLabel The word for a record under way.
 * @param waitingLabel The word for a waiting record.
 * @param busy Whether a command is running, so the button waits.
 * @param onCancel What to do when the player cancels the record.
 * @param onFinished What to do when the timer runs out.
 */
export function QueueItem({
  entry,
  underWayLabel,
  waitingLabel,
  busy,
  onCancel,
  onFinished,
  fillClass = 'f-accent',
  mark,
  clockOnly = false,
}: {
  entry: QueueEntry;
  underWayLabel: string;
  waitingLabel: string;
  busy: boolean;
  onCancel: (id: number) => void;
  onFinished: () => void;
  /** The fill of the progress. A research uses the research colour of the design. */
  fillClass?: string;
  /** The icon in front of the name. The research screen draws one; the other queues do not. */
  mark?: ReactNode;
  /** The research screen shows the clock alone. The other queues keep the "time left" sentence. */
  clockOnly?: boolean;
}) {
  reportRender('QueueItem');

  const { t, language } = useI18n();
  const secondsLeft = useSecondsLeft(entry.deadline, onFinished);
  const total = Math.max(entry.durationSeconds, 1);
  // A record whose time has run out is being finished by the server: it cannot be cancelled any more.
  const isFinishing = entry.locksCancelWhenDue === true && secondsLeft !== null && secondsLeft <= 0;
  const percent = secondsLeft === null ? 0 : Math.max(0, Math.min(fullPercent, (1 - secondsLeft / total) * fullPercent));

  return (
    <li className={entry.underWay ? 'queue-item queue-item--active' : 'queue-item'}>
      <div className="queue-item__identity">
        {mark ? (
          <span aria-hidden="true" className="queue-item__mark">
            {mark}
          </span>
        ) : null}
        <strong>{entry.title}</strong>
        <span className={entry.underWay ? 'queue-item__status queue-item__status--active' : 'queue-item__status'}>
          {entry.underWay ? underWayLabel : waitingLabel}
        </span>
      </div>
      {secondsLeft === null ? (
        <span className="queue-item__track" />
      ) : (
        <div aria-label={entry.title} aria-valuemax={fullPercent} aria-valuemin={0} aria-valuenow={Math.round(percent)} className="track queue-item__track" role="progressbar">
          <i className={fillClass} style={{ width: `${percent}%` }} />
        </div>
      )}
      <span className="queue-item__time num">
        {secondsLeft === null ? (
          formatDuration(language, entry.durationSeconds)
        ) : secondsLeft > 0 ? (
          <span role="timer">{clockOnly ? formatDuration(language, secondsLeft) : t('planet.timeLeft', { time: formatDuration(language, secondsLeft) })}</span>
        ) : (
          <span>{t('planet.finishing')}</span>
        )}
      </span>
      <button
        aria-label={entry.cancelLabel}
        className="btn btn--secondary"
        disabled={busy || isFinishing}
        title={isFinishing ? t('queue.finishingNoCancel') : entry.cancelLabel}
        type="button"
        onClick={() => onCancel(entry.id)}
      >
        {t('planet.queueCancel')}
      </button>
      {entry.details ? <span className="queue-item__details meta">{entry.details}</span> : null}
    </li>
  );
}

/**
 * The time left to a moment, ticking on its own, for the tile of a technology that is under
 * way. The record of the queue above it asks the server when the time runs out, so this one
 * only shows the time.
 *
 * @param deadline The moment the record ends on the clock of the browser, in milliseconds.
 */
export function Countdown({ deadline }: { deadline: number }) {
  const { t, language } = useI18n();
  const secondsLeft = useSecondsLeft(deadline, noop);

  return secondsLeft !== null && secondsLeft > 0 ? (
    <span className="num" role="timer">
      {formatDuration(language, secondsLeft)}
    </span>
  ) : (
    <span>{t('planet.finishing')}</span>
  );
}

function noop() {
  // The record of the queue reads the server again; the tile only shows the time.
}

/**
 * The progress and the clock of a technology that is being researched, inside its plate.
 * The queue above asks the server when the time runs out, so this one only shows them.
 *
 * @param deadline The moment the research ends on the clock of the browser, in milliseconds.
 * @param durationSeconds How many real seconds the research takes in all.
 * @param label The word beside the clock.
 */
export function ResearchProgress({
  deadline,
  durationSeconds,
  label,
}: {
  deadline: number;
  durationSeconds: number;
  label: string;
}) {
  const { t, language } = useI18n();
  const secondsLeft = useSecondsLeft(deadline, noop);
  const total = Math.max(durationSeconds, 1);
  const left = secondsLeft === null ? total : Math.max(secondsLeft, 0);
  const percent = Math.max(0, Math.min(fullPercent, (1 - left / total) * fullPercent));

  return (
    <>
      <div aria-valuemax={fullPercent} aria-valuemin={0} aria-valuenow={Math.round(percent)} className="track tech-card__track" role="progressbar">
        <i className="f-accent" style={{ width: `${percent}%` }} />
      </div>
      <div className="tech-card__time">
        <span>{label}</span>
        {secondsLeft !== null && secondsLeft > 0 ? (
          <span className="num" role="timer">
            {formatDuration(language, secondsLeft)}
          </span>
        ) : (
          <span>{t('planet.finishing')}</span>
        )}
      </div>
    </>
  );
}
