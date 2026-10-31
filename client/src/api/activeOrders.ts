/**
 * An order of the queue of one building of a body, as the server sends it for the queue of the
 * production and of the processes (task 00020, section 7.5). The two kinds of the orders have
 * one shape, so one queue component shows both.
 */
export interface ActiveOrder {
  orderId: number;
  /** The building the order runs in, so the order is cancelled in its own queue. */
  planetBuildingId: number;
  buildingNameKey: string;
  recipeId: number;
  /** The localisation key of the name of the recipe. */
  nameKey: string;
  quantity: number;
  producedQuantity: number;
  /** The place of the order in the queue of its building. */
  position: number;
  /** The time of one unit, in game seconds. */
  unitSeconds: number;
  startsAt: string | null;
  nextUnitAt: string | null;
  finishesAt: string | null;
  isBlocked: boolean;
}

/**
 * Every order of every building of a body of one kind with the places of all those queues.
 */
export interface ActiveOrders<TOrder extends ActiveOrder = ActiveOrder> {
  planetId: number;
  queueSlots: number;
  queueUsedSlots: number;
  orders: TOrder[];
  gameNow: string;
}
