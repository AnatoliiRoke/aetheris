import type { FlightFuelLeg, FlightPlanOrder } from '../../../api/fleetApi';

/**
 * One row of the flight plan as the list of the screen shows it (task 00072).
 *
 * A hyperflight or a jump is spread by the server over two or three legs that share one key, so
 * the screen shows them as one row with the legs under it. The other orders stand alone.
 */
export interface PlanRow {
  /** The key that binds the legs of one command together; `null` for an order that stands alone. */
  groupKey: string | null;
  /** The command of the player the legs come from: `hyperfly` or `jump`. */
  command: string | null;
  /** The orders of the row: one for an order that stands alone, the legs for a command. */
  orders: FlightPlanOrder[];
  /** Whether the row may be opened: only a command with legs may be. */
  expandable: boolean;
}

/**
 * Groups the orders of a flight plan into the rows of the list (task 00072).
 *
 * The legs of a command share one key and come one after another, so the rows keep the order of the
 * plan: the first leg of a command opens the row and the other legs go into it.
 *
 * @param orders The orders of the plan, in the order they are carried out in.
 * @returns The rows of the list.
 */
export function groupPlanOrders(orders: readonly FlightPlanOrder[]): PlanRow[] {
  const rows: PlanRow[] = [];

  for (const order of orders) {
    const key = order.groupKey ?? null;
    const last = rows[rows.length - 1];

    // The leg belongs to the row that is open right now: the legs of one command come one after
    // another, so the row is the last one.
    if (key !== null && last !== undefined && last.groupKey === key) {
      last.orders.push(order);

      continue;
    }

    rows.push({
      groupKey: key,
      command: order.command ?? null,
      orders: [order],
      expandable: key !== null,
    });
  }

  return rows;
}

/** The orders that fly on their own, as one ordinary leg of the route. */
const plainFlights: readonly string[] = ['fly', 'return'];

/**
 * Gives every row of the plan the legs of the route the server counted for it (task 00072).
 *
 * The estimate of the plan lists the legs of the whole route one after another: one ordinary leg
 * for a flight that stands alone, and for a hyperflight or a jump its leg to the edge (when the
 * fleet stands in a system), its leg between the systems and its leg from the edge (when the
 * target lies in a system). The legs are counted from the real places, so they carry the real end
 * of every leg and its time; the rows take them in the order of the route.
 *
 * @param rows The rows of the plan.
 * @param legs The legs of the route from the estimate of the server.
 * @returns The legs of every row, in the order of the rows; an empty list for a row that does not fly.
 */
export function legsOfRows(rows: readonly PlanRow[], legs: readonly FlightFuelLeg[]): FlightFuelLeg[][] {
  let cursor = 0;

  return rows.map((row) => {
    const first = row.orders[0];

    if (first === undefined) {
      return [];
    }

    if (row.expandable) {
      const taken: FlightFuelLeg[] = [];

      if (legs[cursor]?.leg === 'to_edge') {
        taken.push(legs[cursor++]!);
      }

      if (legs[cursor]?.leg === 'ftl') {
        taken.push(legs[cursor++]!);
      }

      if (legs[cursor]?.leg === 'from_edge') {
        taken.push(legs[cursor++]!);
      }

      return taken;
    }

    if (plainFlights.includes(first.typeCode) && legs[cursor]?.leg === 'normal') {
      return [legs[cursor++]!];
    }

    return [];
  });
}
