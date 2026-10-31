import type { ReactNode } from 'react';
import type { ActiveOrder, ActiveOrders } from '../../api/activeOrders';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { orderEntry } from './orderEntries';
import { QueuePanel } from './QueuePanel';

/**
 * The production queue of a body in the standard queue panel (task 00020, section 7.5): the
 * batches of all the factories of the body together, each with the units made out of the whole
 * batch, the time to the next unit and the cancel. The server sends the places and the names, so
 * the component knows no factory and no product.
 *
 * @param orders The batches of all the factories with the places.
 * @param receivedAt The moment the answer of the server came.
 * @param timeMultiplier How many game seconds pass in one real second on the body.
 * @param busy Whether a command is running.
 * @param onCancel What to do when the player cancels a batch.
 * @param onFinished What to do when the timer of a unit runs out.
 * @param action A link beside the heading.
 * @param footnote A short note under the queue.
 */
export function ProductionQueue({
  orders,
  receivedAt,
  timeMultiplier,
  busy,
  onCancel,
  onFinished,
  action,
  footnote,
}: {
  orders: ActiveOrders;
  receivedAt: number;
  timeMultiplier: number;
  busy: boolean;
  onCancel: (order: ActiveOrder) => void;
  onFinished: () => void;
  action?: ReactNode;
  footnote?: string;
}) {
  const { t } = useI18n();
  const { text } = useTexts();

  const entries = orders.orders.map((order) =>
    orderEntry(order, {
      gameNow: orders.gameNow,
      timeMultiplier,
      receivedAt,
      text,
      batchText: (item) => t('production.batch', { made: item.producedQuantity, total: item.quantity }),
      blockedText: t('production.blocked'),
      cancelLabel: (name) => t('production.cancelLabel', { name }),
    }),
  );

  return (
    <QueuePanel
      action={action}
      busy={busy}
      emptyText={t('production.queueEmpty')}
      entries={entries}
      footnote={footnote}
      id="production-queue"
      onCancel={(id) => {
        const order = orders.orders.find((candidate) => candidate.orderId === id);

        if (order) {
          onCancel(order);
        }
      }}
      onFinished={onFinished}
      title={t('production.queue')}
      total={orders.queueSlots}
      underWayLabel={t('production.queueUnderWay')}
      used={orders.queueUsedSlots}
      waitingLabel={t('production.queueWaiting')}
    />
  );
}
