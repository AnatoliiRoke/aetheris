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
  /** The resources some recipe makes: the produced resources, shown apart from the raw ones on the storage screen. */
  producedResourceTypeIds: number[];
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
 * A group of the parts with one of its characteristics (task 00027, section 3.2).
 */
export interface CharacteristicGroup {
  /** The group of the parts. */
  partGroupId: number;
  /** The neutral code of the group. */
  code: string;
  /** The localisation key of the name of the group. */
  nameKey: string;
  /** The order of the characteristic in the list of the group. */
  sortOrder: number;
  /** Whether a part of the group must have the value of the characteristic. */
  isRequired: boolean;
}

/**
 * A characteristic of the parts with the groups that have it (task 00027, section 3.2).
 */
export interface Characteristic {
  /** The characteristic. */
  characteristicId: number;
  /** The neutral code of the characteristic. */
  code: string;
  /** The localisation key of the name of the characteristic. */
  nameKey: string;
  /** The localisation key of the description of the characteristic. */
  descriptionKey: string;
  /** The localisation key of the unit of measure, or `null` when it has none. */
  unitKey: string | null;
  /** The key of the picture of the characteristic, or `null`. */
  imageKey: string | null;
  /** The order in the lists. */
  sortOrder: number;
  /** Whether the characteristic is archived. */
  isArchived: boolean;
  /** The groups of the parts that have the characteristic. */
  groups: CharacteristicGroup[];
}

/**
 * The value of a characteristic of a part (task 00027, section 3.2).
 */
export interface PartCharacteristicValue {
  /** The characteristic. */
  characteristicId: number;
  /** The value of the characteristic. */
  numberValue: number;
  /** The values of the characteristic by the level of the part, when it has them. */
  levels: Record<string, number>;
}

/**
 * The weight and the characteristics of a part (task 00027, section 3.2).
 */
export interface PartCharacteristics {
  /** The part. */
  partTypeId: number;
  /** The neutral code of the part. */
  code: string;
  /** The localisation key of the name of the part. */
  nameKey: string;
  /** The group of the part. */
  partGroupId: number;
  /** The weight of one part in tonnes. */
  weight: number;
  /** The values of the characteristics of the part. */
  characteristics: PartCharacteristicValue[];
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
  /** The characteristics of the parts with the groups that have them (task 00027). */
  characteristics(): Promise<Characteristic[]>;
  /** The weight and the characteristics of every part (task 00027). */
  characteristicParts(): Promise<PartCharacteristics[]>;
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
    async characteristics() {
      return camelize<Characteristic[]>(await http.get<unknown>(`${prefix}/characteristics`));
    },
    async characteristicParts() {
      return camelize<PartCharacteristics[]>(await http.get<unknown>(`${prefix}/characteristics/parts`));
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
