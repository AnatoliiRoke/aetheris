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
 * The terraforming a ship may start from a colony (task 00014, section 9): the devices of the
 * storage of the colony, the free bodies the player sees and the range of the multiplier.
 */
export interface TerraformingState {
  planetId: number;
  devices: TerraformingDevice[];
  targets: TerraformingTarget[];
  multiplierMin: number;
  multiplierMax: number;
}

/**
 * A one-shot device of the terraforming in the storage of the colony.
 */
export interface TerraformingDevice {
  partTypeId: number;
  nameKey: string;
  markLevel: number;
  multiplier: number;
  quantity: number;
}

/**
 * A free body a device may be turned on at.
 */
export interface TerraformingTarget {
  planetId: number;
  systemId: number;
  objectTypeId: number;
  orbit: number | null;
  name: string;
  isTerraformed: boolean;
}

/**
 * The outcome of a device turned on at a free body, with the terraforming after the change.
 */
export interface TerraformingResult {
  targetPlanetId: number;
  partTypeId: number;
  multiplier: number;
  changedDeposits: number;
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
  enqueue(
    planetId: number,
    planetBuildingId: number,
    recipeId: number,
    quantity: number,
    request?: ProcessOrderRequest,
  ): Promise<ProcessBuilding>;
  cancel(planetId: number, planetBuildingId: number, orderId: number): Promise<ProcessBuilding>;
  terraforming(planetId: number): Promise<TerraformingState>;
  terraform(planetId: number, targetPlanetId: number, partTypeId: number): Promise<TerraformingResult>;
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
    async terraforming(planetId) {
      return camelize<TerraformingState>(await http.get<unknown>(`${prefix}/terraforming?planet_id=${planetId}`));
    },
    async terraform(planetId, targetPlanetId, partTypeId) {
      return camelize<TerraformingResult>(
        await http.command<unknown>(`${prefix}/terraform`, {
          planet_id: planetId,
          target_planet_id: targetPlanetId,
          part_type_id: partTypeId,
        }),
      );
    },
  };
}
