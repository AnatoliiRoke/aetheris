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
  shipStub: string | null;
  isAvailable: boolean;
  reasons: ProcessReason[];
  imageKey: string | null;
  ingredients: ProcessIngredient[];
}

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
 * takes and gives, the part it works on and the stub of the ship on the orbit.
 */
export interface ProcessOrderRequest {
  sourceResourceTypeId?: number | null;
  targetResourceTypeId?: number | null;
  partTypeId?: number | null;
  shipStub?: string | null;
}

/**
 * The endpoints of the processes module: the reads of the buildings and the commands of the
 * queue. The server checks every rule; the client only shows them.
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
          ship_stub: request?.shipStub ?? undefined,
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
  };
}
