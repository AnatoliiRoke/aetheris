import type { ReactNode } from 'react';
import { useI18n } from '../../i18n/useI18n';
import { QueueItem, type QueueEntry } from './QueueItem';

/**
 * The standard queue of the game (task 00020, section 8.1): the same panel on every screen and
 * for every kind of queue. The panel shows the slots, the records in their order with the timer
 * of the one under way, the free slots and the cancel; the screen gives it the records and
 * says what a record is called.
 *
 * @param id The identifier of the heading, so the panel is a named region.
 * @param title The heading of the panel.
 * @param used How many slots are taken.
 * @param total How many slots there are.
 * @param entries The records in the order of the queue.
 * @param emptyText What to say when the queue is empty.
 * @param underWayLabel The word for a record under way.
 * @param waitingLabel The word for a waiting record.
 * @param busy Whether a command is running.
 * @param onCancel What to do when the player cancels a record.
 * @param onFinished What to do when the timer of a record runs out.
 * @param action A link or a button beside the heading, for example the way to choose a record.
 * @param footnote A short note under the queue.
 */
export function QueuePanel({
  id,
  title,
  used,
  total,
  entries,
  emptyText,
  underWayLabel,
  waitingLabel,
  busy,
  onCancel,
  onFinished,
  action,
  footnote,
}: {
  id: string;
  title: string;
  used: number;
  total: number;
  entries: QueueEntry[];
  emptyText: string;
  underWayLabel: string;
  waitingLabel: string;
  busy: boolean;
  onCancel: (id: number) => void;
  onFinished: () => void;
  action?: ReactNode;
  footnote?: string;
}) {
  const { t } = useI18n();
  const freeSlots = Math.max(0, total - entries.length);

  return (
    <section aria-labelledby={id} className="panel queue-panel">
      <div className="panel__head">
        <h2 className="panel__title" id={id}>
          {title}
        </h2>
        <span className="meta">{t('planet.queueSlots', { used, total })}</span>
      </div>
      {entries.length === 0 ? <p className="lead">{emptyText}</p> : null}
      <ol className="queue-list">
        {entries.map((entry) => (
          <QueueItem
            busy={busy}
            entry={entry}
            key={entry.id}
            onCancel={onCancel}
            onFinished={onFinished}
            underWayLabel={underWayLabel}
            waitingLabel={waitingLabel}
          />
        ))}
        {Array.from({ length: freeSlots }, (_, index) => (
          <li className="queue-item queue-item--free" key={`free-${index}`}>
            <span className="meta">{t('queue.freeSlot')}</span>
          </li>
        ))}
      </ol>
      {action}
      {footnote ? <p className="meta">{footnote}</p> : null}
    </section>
  );
}
