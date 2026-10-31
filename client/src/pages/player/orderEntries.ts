import type { ActiveOrder } from '../../api/activeOrders';
import type { EntityText } from '../../texts/context';
import type { QueueEntry } from './QueueItem';
import { deadlineFor } from './planetText';

/**
 * What the screen knows about the orders of a body when it shows them in the standard queue.
 */
export interface OrderEntryContext {
  /** The moment of game time of the answer of the server. */
  gameNow: string;
  /** How many game seconds pass in one real second on the body. */
  timeMultiplier: number;
  /** The moment the answer of the server came, in milliseconds. */
  receivedAt: number;
  /** The names of the game data by their localisation keys. */
  text: EntityText;
  /** The text of the count of the units made, for example "Made 1 of 3". */
  batchText: (order: ActiveOrder) => string;
  /** The text of an order that waits for a free place. */
  blockedText: string;
  /** The label of the cancel button for a screen reader. */
  cancelLabel: (name: string) => string;
  /** The name of the kind of the order, when the orders have kinds; shown before the count of the units. */
  kindName?: string;
}

/**
 * Turns an order of a queue of the production or of the processes into a record of the
 * standard queue. The timer counts to the next unit, and the units made come from the server,
 * so the client counts nothing of its own.
 *
 * @param order The order.
 * @param context What the screen knows about the answer and the language.
 * @returns The record of the queue.
 */
export function orderEntry(order: ActiveOrder, context: OrderEntryContext): QueueEntry {
  const multiplier = context.timeMultiplier > 0 ? context.timeMultiplier : 1;
  const name = order.nameKey ? (context.text(order.nameKey) ?? order.nameKey) : String(order.recipeId);
  const buildingName = order.buildingNameKey ? (context.text(order.buildingNameKey) ?? order.buildingNameKey) : '';
  const underWay = order.nextUnitAt !== null;
  const unitDeadline = order.isBlocked ? null : deadlineFor(context.gameNow, context.timeMultiplier, order.nextUnitAt, context.receivedAt);
  // A unit that is already late has no timer: the next event of the page reads the server, so a
  // slow server is not asked again and again.
  const deadline = unitDeadline !== null && unitDeadline > context.receivedAt ? unitDeadline : null;
  const unitSeconds = order.unitSeconds / multiplier;

  return {
    id: order.orderId,
    title: name,
    underWay,
    deadline,
    durationSeconds: underWay ? unitSeconds : unitSeconds * order.quantity,
    details: [context.kindName ?? '', context.batchText(order), buildingName, order.isBlocked ? context.blockedText : ''].filter((part) => part !== '').join(' · '),
    cancelLabel: context.cancelLabel(name),
  };
}
