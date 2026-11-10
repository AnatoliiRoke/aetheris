import type { FlightPlanOrderInput } from '../../../api/fleetApi';

/**
 * One order of a flight plan as the window holds it while the player writes it (task 00050).
 */
export interface DraftOrder {
  /** The type of the order: `fly`, `hyperfly`, `jump`, `mine`, `scan`, `load`, `unload`, `wait` or `return`. */
  typeCode: string;
  /** The kind of the target: the coordinates of a point, a planet or an object. */
  targetKind: TargetKind;
  /** The sector the player chose, or `null` when the target is a point of the space. */
  targetSectorId: number | null;
  /** The system the player chose, or `null` when the target is a point of the space. */
  targetSystemId: number | null;
  /** The body the player chose, or `null` when the target is a point of the space. */
  targetObjectId: number | null;
  /** The target as the player wrote it: the three coordinates of a point of the space. */
  target: string;
  /** How long the order lasts, in minutes, as the game counted it. */
  durationMinutes: string;
}

/**
 * The kinds of the target of an order (task 00050): the coordinates of a point of the space, a
 * planet of a star system, or an object that belongs to no system — a free asteroid field.
 */
export type TargetKind = 'point' | 'planet' | 'object';

/**
 * Reads the three coordinates out of what the player wrote: three numbers separated by spaces or
 * commas.
 *
 * @param text The text.
 * @returns The three axes, or `null` when the text is not three numbers.
 */
export function readAxes(text: string): [number, number, number] | null {
  const parts = text
    .trim()
    .split(/[\s,]+/)
    .filter((part) => part !== '');

  if (parts.length !== 3) {
    return null;
  }

  const axes = parts.map((part) => Number(part));

  return axes.every((axis) => Number.isFinite(axis)) ? [axes[0]!, axes[1]!, axes[2]!] : null;
}

/**
 * Turns the orders of the window into the orders the server reads (task 00050).
 *
 * The target is either the three coordinates of a point of the space, a planet of a system or an
 * object that belongs to no system. A planet and an object are named by their identifiers, and the
 * sector and the system say where they are.
 *
 * @param orders The orders of the window.
 * @returns The orders of the request.
 */
export function toOrderInputs(orders: readonly DraftOrder[]): FlightPlanOrderInput[] {
  return orders.map((order) => {
    const isPoint = order.targetKind === 'point';
    const axes = isPoint ? readAxes(order.target) : null;

    return {
      typeCode: order.typeCode,
      targetKind: isPoint ? 'point' : order.targetKind === 'planet' ? 'planet' : 'object',
      targetSectorId: order.targetSectorId ?? 0,
      targetSystemId: order.targetSystemId ?? 0,
      targetObjectId: order.targetObjectId ?? 0,
      targetX: axes ? axes[0] : 0,
      targetY: axes ? axes[1] : 0,
      targetZ: axes ? axes[2] : 0,
      durationSeconds: Math.max(0, Math.round((Number(order.durationMinutes) || 0) * 60)),
      parameters: '{}',
    };
  });
}
