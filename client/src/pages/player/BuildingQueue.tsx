import type { ReactNode } from 'react';
import type { Planet, QueueItem } from '../../api/planetApi';
import { useI18n } from '../../i18n/useI18n';
import { QueuePanel } from './QueuePanel';
import type { QueueEntry } from './QueueItem';
import { deadlineFor, type namer } from './planetText';

/**
 * The construction queue of a body in the standard queue panel (task 00020, section 8.1): what
 * is built, the timer, what waits and the cancel. Every screen that shows the construction
 * queue shows it with this component.
 *
 * @param planet The body.
 * @param names The names of the resources and the buildings.
 * @param receivedAt The moment the answer of the server came.
 * @param busy Whether a command is running.
 * @param onCancel What to do when the player cancels a record.
 * @param onFinished What to do when the timer of a record runs out.
 * @param action A link beside the heading.
 * @param emptyText What to say when the queue is empty.
 * @param footnote A short note under the queue.
 * @param collect The inactive "collect" button of the design, shown where the server has no such action yet.
 */
export function BuildingQueue({
  planet,
  names,
  receivedAt,
  busy,
  onCancel,
  onFinished,
  action,
  emptyText,
  footnote,
  collect,
  title,
}: {
  planet: Planet;
  names: ReturnType<typeof namer>;
  receivedAt: number;
  busy: boolean;
  onCancel: (item: QueueItem) => void;
  onFinished: () => void;
  action?: ReactNode;
  emptyText?: string;
  footnote?: string;
  collect?: { label: string; reason: string };
  /** The heading. The overview uses the short name; the other screens keep the queue name. */
  title?: string;
}) {
  const { t } = useI18n();
  const { items, slotCount } = planet.queue;
  const multiplier = planet.timeMultiplier > 0 ? planet.timeMultiplier : 1;

  const entries: QueueEntry[] = items.map((item) => {
    const name = names.building(item.buildingTypeId);

    return {
      id: item.id,
      title: t('planet.queueItem', { name, level: item.targetLevel }),
      underWay: item.finishesAt !== null,
      deadline: deadlineFor(planet.gameNow, planet.timeMultiplier, item.finishesAt, receivedAt),
      durationSeconds: item.durationSeconds / multiplier,
      details: names.amounts(item.costs),
      cancelLabel: t('planet.queueCancelLabel', { name, level: item.targetLevel }),
      locksCancelWhenDue: true,
    };
  });

  return (
    <QueuePanel
      action={action}
      busy={busy}
      collect={collect}
      emptyText={emptyText ?? t('queue.emptyShort')}
      entries={entries}
      footnote={footnote}
      id="planet-queue"
      onCancel={(id) => {
        const item = items.find((candidate) => candidate.id === id);

        if (item) {
          onCancel(item);
        }
      }}
      onFinished={onFinished}
      title={title ?? t('planet.queue')}
      total={slotCount}
      underWayLabel={t('planet.queueUnderWay')}
      used={items.length}
      waitingLabel={t('planet.queueWaiting')}
    />
  );
}
