import type { FleetApi, FlightEstimateTarget, FlightPlan, FlightPlanOrderInput } from '../../../api/fleetApi';
import { ApiError } from '../../../api/http';
import { cargoParameters, type CargoDraftItem } from './flightPlanCargo';

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
  /** What a load or an unload carries (task 00057); empty for the other types. */
  items: CargoDraftItem[];
  /**
   * The key of the commands a saved hyperflight or jump was replaced by (task 00072), or `null` for
   * an order of its own: removing the hyperflight removes the flights that go with it.
   */
  groupKey?: string | null;
  /** The role of the command in such a group: `to_edge`, `ftl` or `from_edge`. */
  leg?: string | null;
}

/**
 * The kinds of the target of an order (task 00050): the coordinates of a point of the space, a
 * planet of a star system, an object that belongs to no system — a free asteroid field — or the
 * edge of a star system, its last orbit (task 00072).
 */
export type TargetKind = 'point' | 'planet' | 'object' | 'system';

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
 * The orders that name no target: the extraction takes from the place the fleet stands at (task 00054), the
 * colonization colonizes the planet the fleet stands on (task 00073), the wait and the scan work where the fleet
 * is, and the return flies to the home planet. They are written with the target `none`, whatever the window holds.
 */
const untargetedOrders: readonly string[] = ['mine', 'colonize', 'wait', 'scan', 'return'];

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
    if (untargetedOrders.includes(order.typeCode)) {
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

    // The order of a load and of an unload names no target either (task 00057): it works on the body the fleet
    // stands on when the order begins, and carries the list of what to load or put out.
    if (order.typeCode === 'load' || order.typeCode === 'unload') {
      return {
        typeCode: order.typeCode,
        targetKind: 'none',
        targetSectorId: 0,
        targetSystemId: 0,
        targetObjectId: 0,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        durationSeconds: 0,
        parameters: cargoParameters(order.items),
      };
    }

    const isPoint = order.targetKind === 'point';
    const axes = isPoint ? readAxes(order.target) : null;

    return {
      typeCode: order.typeCode,
      targetKind: isPoint ? 'point' : order.targetKind,
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
  /** The system of the target, when the target is the edge of a system (task 00072). */
  systemId?: number | null;
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

  // The edge of a system is named by the system: the server counts the flight to its last orbit.
  if (target.kind === 'system') {
    return target.systemId === null || target.systemId === undefined
      ? null
      : { kind: 'system', sectorId: target.sectorId ?? 0, objectId: 0, systemId: target.systemId, x: 0, y: 0, z: 0 };
  }

  return target.objectId === null ? null : { kind: target.kind, sectorId: target.sectorId ?? 0, objectId: target.objectId, x: 0, y: 0, z: 0 };
}
