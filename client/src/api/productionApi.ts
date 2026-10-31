import type { ActiveOrders } from './activeOrders';
import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * A factory of a body with its recipes, its queue and the storage of the parts
 * (task 00013).
 */
export interface ProductionFactory {
  planetId: number;
  planetBuildingId: number;
  factoryLevel: number;
  recipes: ProductionRecipe[];
  queue: ProductionOrder[];
  storage: ProductionStorage;
  gameNow: string;
}

/**
 * A factory of a body, without its recipes.
 */
export interface ProductionFactorySummary {
  planetId: number;
  planetBuildingId: number;
  buildingTypeId: number;
  level: number;
  nameKey: string;
}

/**
 * A recipe of a factory with the reasons why it is not available.
 */
export interface ProductionRecipe {
  recipeId: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  buildingTypeId: number | null;
  partTypeId: number | null;
  resourceTypeId: number | null;
  outputQuantity: number;
  durationSeconds: number;
  isAvailable: boolean;
  reasons: ProductionReason[];
  imageKey: string | null;
  unitSeconds: number;
  ingredients: ProductionIngredient[];
}

/**
 * An ingredient of a unit of a recipe: a resource or a part with its name.
 */
export interface ProductionIngredient {
  resourceTypeId: number | null;
  partTypeId: number | null;
  amount: number;
  nameKey: string;
}

/**
 * A reason why a recipe is not available.
 */
export interface ProductionReason {
  code: string;
  targetId: number;
  targetLevel: number;
  targetNameKey?: string | null;
}

/**
 * A batch of a queue.
 */
export interface ProductionOrder {
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
 * The parts of one kind on a body against the room of the kind.
 */
export interface ProductionPartKind {
  partTypeId: number;
  /** How many parts of the kind lie on the body, in every stack of the kind. */
  quantity: number;
  /** How many parts of the kind the storage holds. */
  capacity: number;
  /** Whether the kind has no room left. */
  isFull: boolean;
}

/**
 * The storage of the parts of a body: every kind of part has the same room of its own.
 */
export interface ProductionStorage {
  /** How many parts of every kind the storage holds. */
  capacity: number;
  kinds: ProductionPartKind[];
}

/**
 * The storage of the parts of a body with the stacks it holds.
 */
export interface ProductionStock {
  planetId: number;
  stacks: ProductionStack[];
  storage: ProductionStorage;
  gameNow: string;
}

/**
 * A stack of the parts on a body.
 */
export interface ProductionStack {
  partTypeId: number;
  markLevel: number;
  hasRaceBonus: boolean;
  conditionPercent: number;
  quantity: number;
  nameKey: string;
  imageKey: string | null;
  /** The group of the part, so the storage screen groups the parts by it (task 00020). */
  partGroupId: number;
  /** The localisation key of the name of the group. */
  partGroupNameKey: string | null;
}

/**
 * The endpoints of the production module: the reads of the factories and the stock, and the
 * commands of the queue. The server checks every rule; the client only shows them.
 */
export interface ProductionApi {
  factories(planetId: number): Promise<ProductionFactorySummary[]>;
  factory(planetId: number, planetBuildingId: number): Promise<ProductionFactory>;
  stock(planetId: number): Promise<ProductionStock>;
  /** Every batch of every factory of the body in one list (task 00020, section 7.5). */
  orders(planetId: number): Promise<ActiveOrders>;
  enqueue(planetId: number, planetBuildingId: number, recipeId: number, quantity: number): Promise<ProductionFactory>;
  cancel(planetId: number, planetBuildingId: number, orderId: number): Promise<ProductionFactory>;
}

const prefix = '/api/v1/production';

/**
 * The endpoints of the production module over HTTP.
 */
export function createProductionApi(http: HttpClient): ProductionApi {
  return {
    async factories(planetId) {
      return camelize<ProductionFactorySummary[]>(await http.get<unknown>(`${prefix}/factories?planet_id=${planetId}`));
    },
    async factory(planetId, planetBuildingId) {
      return camelize<ProductionFactory>(await http.get<unknown>(`${prefix}/factories/${planetBuildingId}?planet_id=${planetId}`));
    },
    async stock(planetId) {
      return camelize<ProductionStock>(await http.get<unknown>(`${prefix}/stock?planet_id=${planetId}`));
    },
    async orders(planetId) {
      return camelize<ActiveOrders>(await http.get<unknown>(`${prefix}/orders?planet_id=${planetId}`));
    },
    async enqueue(planetId, planetBuildingId, recipeId, quantity) {
      return camelize<ProductionFactory>(
        await http.command<unknown>(`${prefix}/enqueue`, {
          planet_id: planetId,
          planet_building_id: planetBuildingId,
          recipe_id: recipeId,
          quantity,
        }),
      );
    },
    async cancel(planetId, planetBuildingId, orderId) {
      return camelize<ProductionFactory>(
        await http.command<unknown>(`${prefix}/cancel`, {
          planet_id: planetId,
          planet_building_id: planetBuildingId,
          order_id: orderId,
        }),
      );
    },
  };
}
