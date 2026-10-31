import type { ReactNode } from 'react';
import type { ResearchQueueItem, ResearchTree } from '../../api/researchApi';
import { useI18n } from '../../i18n/useI18n';
import { QueuePanel } from './QueuePanel';
import type { QueueEntry } from './QueueItem';
import { deadlineFor, formatNumber } from './planetText';

/**
 * The research queue of the player in the standard queue panel (task 00020, section 8.1): the
 * same panel as the construction queue, with the records of the researches. Every screen that
 * shows the research queue shows it with this component.
 *
 * @param tree The tree of the technologies with the queue.
 * @param receivedAt The moment the answer of the server came.
 * @param technologyName The name of a technology by its localisation key.
 * @param resourceName The name of a resource by its identifier.
 * @param busy Whether a command is running.
 * @param onCancel What to do when the player cancels a record.
 * @param onFinished What to do when the timer of a record runs out.
 * @param action A link beside the heading.
 * @param footnote A short note under the queue.
 */
export function ResearchQueue({
  tree,
  receivedAt,
  technologyName,
  resourceName,
  busy,
  onCancel,
  onFinished,
  action,
  footnote,
}: {
  tree: ResearchTree;
  receivedAt: number;
  technologyName: (key: string) => string;
  resourceName: (id: number) => string;
  busy: boolean;
  onCancel: (item: ResearchQueueItem) => void;
  onFinished: () => void;
  action?: ReactNode;
  footnote?: string;
}) {
  const { t, language } = useI18n();
  const multiplier = tree.timeMultiplier > 0 ? tree.timeMultiplier : 1;

  const entries: QueueEntry[] = tree.queue.map((item) => {
    const name = technologyName(item.technologyNameKey);
    const level = tree.technologies.find((technology) => technology.id === item.technologyId)?.levels.find((candidate) => candidate.level === item.targetLevel);

    return {
      id: item.id,
      title: t('research.queueItem', { name, level: item.targetLevel }),
      underWay: item.finishesAt !== null,
      deadline: deadlineFor(tree.gameNow, tree.timeMultiplier, item.finishesAt, receivedAt),
      durationSeconds: (level?.durationSeconds ?? 0) / multiplier,
      details: item.costs.map((cost) => `${resourceName(cost.resourceTypeId)} ${formatNumber(cost.amount, language)}`).join(', '),
      cancelLabel: t('research.cancelLabel', { name, level: item.targetLevel }),
    };
  });

  return (
    <QueuePanel
      action={action}
      busy={busy}
      emptyText={t('research.queueEmpty')}
      entries={entries}
      footnote={footnote}
      id="research-queue"
      onCancel={(id) => {
        const item = tree.queue.find((candidate) => candidate.id === id);

        if (item) {
          onCancel(item);
        }
      }}
      onFinished={onFinished}
      title={t('research.queueTitle')}
      total={tree.queueSlots}
      underWayLabel={t('research.underWay')}
      used={tree.queueUsedSlots}
      waitingLabel={t('research.waiting')}
    />
  );
}
