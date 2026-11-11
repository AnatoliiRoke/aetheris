import type { FleetApi, FlightEstimateTarget, FlightPlan, FlightPlanOrderInput } from '../../../api/fleetApi';
import { ApiError } from '../../../api/http';

/**
 * The refusal of the server that says the fleet has a flight plan already.
 */
const planExistsCode = 'fleet_flight_plan_exists';

/**
 * Writes the flight plan of a fleet: a new plan, or the new orders of the plan the fleet already has
 * (task 00053). The window of the plan and the card of the map write the plan the same way, and a plan that is
 * carried out at once is written first.
 *
 * @param api The fleet API.
 * @param fleetId The fleet.
 * @param orders The orders of the plan.
 * @returns The plan after the write.
 */
export async function writeFlightPlan(api: FleetApi, fleetId: number, orders: FlightPlanOrderInput[]): Promise<FlightPlan> {
  try {
    return await api.createFlightPlan({ fleetId, orders });
  } catch (error) {
    if (error instanceof ApiError && error.code === planExistsCode) {
      return api.saveFlightPlan({ fleetId, orders });
    }

    throw error;
  }
}

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
    // The order of extraction names no target (task 00054): it takes from the place where the fleet stands.
    if (order.typeCode === 'mine') {
      return {
        typeCode: order.typeCode,
        targetKind: 'none',
        targetSectorId: 0,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: Math.max(0, Math.round((Number(order.durationMinutes) || 0) * 60)),
        parameters: '{}',
      };
    }

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

/**
 * The place the time of the flight is counted to (task 00050, task 00053).
 */
export interface FlightTimeTarget {
  /** The kind of the target: the coordinates of a point, a planet or an object. */
  kind: TargetKind;
  /** The sector of the target. */
  sectorId: number | null;
  /** The body or the object of the target, as the map knows it: the identifier of the object. */
  objectId: number | null;
  /** The coordinates of the target, when the target is a point of the space. */
  point: string;
}

/**
 * Turns a target of the window into the place the server counts the flight to, or `null` when the
 * target is not named whole yet: a point needs a sector and three numbers, a place needs its object.
 *
 * @param target The target of the window.
 * @returns The place for the request, or `null`.
 */
export function estimateTargetOf(target: FlightTimeTarget): FlightEstimateTarget | null {
  if (target.kind === 'point') {
    const axes = readAxes(target.point);

    return axes === null || target.sectorId === null
      ? null
      : { kind: 'point', sectorId: target.sectorId, objectId: 0, x: axes[0], y: axes[1], z: axes[2] };
  }

  return target.objectId === null ? null : { kind: target.kind, sectorId: target.sectorId ?? 0, objectId: target.objectId, x: 0, y: 0, z: 0 };
}
