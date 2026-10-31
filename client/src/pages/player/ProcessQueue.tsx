import type { ReactNode } from 'react';
import type { ActiveOrder, ActiveOrders } from '../../api/activeOrders';
import type { ProcessActiveOrder } from '../../api/processesApi';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { orderEntry } from './orderEntries';
import { QueuePanel } from './QueuePanel';

/**
 * The queue of the processes of a body in the standard queue panel (task 00020, section 7.5):
 * the orders of all the buildings of the body that run a process together, in the same form as
 * the production queue. The server sends the orders it holds and the names of the recipes, so
 * the component filters nothing and knows no kind of a process.
 *
 * @param orders The orders of all the buildings with the places.
 * @param receivedAt The moment the answer of the server came.
 * @param timeMultiplier How many game seconds pass in one real second on the body.
 * @param busy Whether a command is running.
 * @param onCancel What to do when the player cancels an order.
 * @param onFinished What to do when the timer of a unit runs out.
 * @param action A link beside the heading.
 * @param footnote A short note under the queue.
 */
export function ProcessQueue({
  orders,
  receivedAt,
  timeMultiplier,
  busy,
  onCancel,
  onFinished,
  action,
  footnote,
}: {
  orders: ActiveOrders<ProcessActiveOrder>;
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
      kindName: order.processTypeNameKey ? (text(order.processTypeNameKey) ?? order.processTypeNameKey) : '',
      batchText: (item) => t('processes.batch', { made: item.producedQuantity, total: item.quantity }),
      blockedText: t('processes.blocked'),
      cancelLabel: (name) => t('processes.cancelLabel', { name }),
    }),
  );

  return (
    <QueuePanel
      action={action}
      busy={busy}
      emptyText={t('processes.queueEmpty')}
      entries={entries}
      footnote={footnote}
      id="process-queue"
      onCancel={(id) => {
        const order = orders.orders.find((candidate) => candidate.orderId === id);

        if (order) {
          onCancel(order);
        }
      }}
      onFinished={onFinished}
      title={t('processes.queue')}
      total={orders.queueSlots}
      underWayLabel={t('processes.queueUnderWay')}
      used={orders.queueUsedSlots}
      waitingLabel={t('processes.queueWaiting')}
    />
  );
}
