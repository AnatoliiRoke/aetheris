import type { ActiveOrder, ActiveOrders } from './activeOrders';
import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * A building of a body that runs a process (task 00014).
 */
export interface ProcessBuildingSummary {
  planetId: number;
  planetBuildingId: number;
  buildingTypeId: number;
  level: number;
  nameKey: string;
}

/**
 * A building of a body with the recipes of the processes it runs and its queue.
 */
export interface ProcessBuilding {
  planetId: number;
  planetBuildingId: number;
  buildingTypeId: number;
  buildingLevel: number;
  recipes: ProcessRecipe[];
  queue: ProcessOrder[];
  gameNow: string;
  fullConditionPercent: number;
}

/**
 * A recipe of a process with the reasons why it is not available.
 */
export interface ProcessRecipe {
  recipeId: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  processTypeNameKey: string;
  buildingTypeId: number | null;
  sourceGroupId: number | null;
  targetGroupId: number | null;
  durationSeconds: number;
  needsShip: boolean;
  partSubject: ProcessPartSubject;
  isAvailable: boolean;
  reasons: ProcessReason[];
  imageKey: string | null;
  ingredients: ProcessIngredient[];
}

/**
 * The part an order of a process names, as the server says: none, a part of the storage in
 * any condition, a damaged one or a new one.
 */
export type ProcessPartSubject = 'none' | 'any' | 'damaged' | 'new';

/**
 * An ingredient of one unit of a process: a resource or a part with its amount.
 */
export interface ProcessIngredient {
  resourceTypeId: number | null;
  partTypeId: number | null;
  amount: number;
}

/**
 * A reason why a recipe of a process is not available.
 */
export interface ProcessReason {
  code: string;
  targetId: number;
  targetLevel: number;
  targetNameKey: string | null;
}

/**
 * An order of a process in the queue of a building.
 */
export interface ProcessOrder {
  orderId: number;
  recipeId: number;
  quantity: number;
  producedQuantity: number;
  position: number;
  startsAt: string | null;
  nextUnitAt: string | null;
  finishesAt: string | null;
  isBlocked: boolean;
}

/**
 * An order of the queue of a process of a body with the name of the kind of the process
 * (task 00020, section 7.5).
 */
export interface ProcessActiveOrder extends ActiveOrder {
  processTypeNameKey: string;
}

/**
 * What the order needs besides the recipe and the number of units: the resource the process
 * takes and gives and the part it works on with its condition.
 */
export interface ProcessOrderRequest {
  sourceResourceTypeId?: number | null;
  targetResourceTypeId?: number | null;
  partTypeId?: number | null;
  partConditionPercent?: number | null;
}

/**
 * The terraforming a ship may start (task 00014, sections 9 and 11): the free bodies that may be
 * settled with a ship of the player by them, the devices in the holds of those ships, and the
 * range of the multiplier every deposit rolls in. It comes only from a ship, so no colony is named.
 */
export interface TerraformingState {
  targets: TerraformingTarget[];
  multiplierMin: number;
  multiplierMax: number;
}

/**
 * A free body a device may be turned on at, with the ships of the player that stand by it and
 * carry a device.
 */
export interface TerraformingTarget {
  planetId: number;
  systemId: number;
  objectTypeId: number;
  orbit: number | null;
  name: string;
  isTerraformed: boolean;
  shipIds: TerraformingShip[];
}

/**
 * A ship of the player by a body with the one-shot devices in its hold.
 */
export interface TerraformingShip {
  shipId: number;
  devices: TerraformingDevice[];
}

/**
 * A one-shot device of the terraforming in the hold of a ship.
 */
export interface TerraformingDevice {
  partTypeId: number;
  nameKey: string;
  markLevel: number;
  quantity: number;
}

/**
 * A deposit that became richer, with the multiplier of its own roll.
 */
export interface TerraformedDeposit {
  resourceTypeId: number;
  oldCoefficient: number;
  newCoefficient: number;
}

/**
 * The outcome of a device turned on at a free body, with the terraforming after the change.
 */
export interface TerraformingResult {
  targetPlanetId: number;
  shipId: number;
  partTypeId: number;
  changedDeposits: number;
  depositIds: TerraformedDeposit[];
  terraforming: TerraformingState;
}

/**
 * The endpoints of the processes module: the reads of the buildings and the commands of the
 * queue, and the terraforming started by a ship. The server checks every rule; the client only
 * shows them.
 */
export interface ProcessesApi {
  buildings(planetId: number): Promise<ProcessBuildingSummary[]>;
  building(planetId: number, planetBuildingId: number): Promise<ProcessBuilding>;
  /** Every order of every building of the body that runs a process, in one list (task 00020, section 7.5). */
  orders(planetId: number): Promise<ActiveOrders<ProcessActiveOrder>>;
  enqueue(
    planetId: number,
    planetBuildingId: number,
    recipeId: number,
    quantity: number,
    request?: ProcessOrderRequest,
  ): Promise<ProcessBuilding>;
  cancel(planetId: number, planetBuildingId: number, orderId: number): Promise<ProcessBuilding>;
  terraforming(): Promise<TerraformingState>;
  terraform(targetPlanetId: number, shipId: number, partTypeId: number): Promise<TerraformingResult>;
}

const prefix = '/api/v1/processes';

/**
 * The endpoints of the processes module over HTTP.
 */
export function createProcessesApi(http: HttpClient): ProcessesApi {
  return {
    async buildings(planetId) {
      return camelize<ProcessBuildingSummary[]>(await http.get<unknown>(`${prefix}/buildings?planet_id=${planetId}`));
    },
    async building(planetId, planetBuildingId) {
      return camelize<ProcessBuilding>(await http.get<unknown>(`${prefix}/buildings/${planetBuildingId}?planet_id=${planetId}`));
    },
    async orders(planetId) {
      return camelize<ActiveOrders<ProcessActiveOrder>>(await http.get<unknown>(`${prefix}/orders?planet_id=${planetId}`));
    },
    async enqueue(planetId, planetBuildingId, recipeId, quantity, request) {
      return camelize<ProcessBuilding>(
        await http.command<unknown>(`${prefix}/enqueue`, {
          planet_id: planetId,
          planet_building_id: planetBuildingId,
          recipe_id: recipeId,
          quantity,
          source_resource_type_id: request?.sourceResourceTypeId ?? undefined,
          target_resource_type_id: request?.targetResourceTypeId ?? undefined,
          part_type_id: request?.partTypeId ?? undefined,
          part_condition_percent: request?.partConditionPercent ?? undefined,
        }),
      );
    },
    async cancel(planetId, planetBuildingId, orderId) {
      return camelize<ProcessBuilding>(
        await http.command<unknown>(`${prefix}/cancel`, {
          planet_id: planetId,
          planet_building_id: planetBuildingId,
          order_id: orderId,
        }),
      );
    },
    async terraforming() {
      return camelize<TerraformingState>(await http.get<unknown>(`${prefix}/terraforming`));
    },
    async terraform(targetPlanetId, shipId, partTypeId) {
      return camelize<TerraformingResult>(
        await http.command<unknown>(`${prefix}/terraform`, {
          target_planet_id: targetPlanetId,
          ship_id: shipId,
          part_type_id: partTypeId,
        }),
      );
    },
  };
}
