import type { HttpClient } from './http';

/**
 * The amount of a resource.
 */
export interface ResourceAmount {
  resourceTypeId: number;
  amount: number;
}

/**
 * An effect of a building level (task 00011, decision 10).
 */
export interface BuildingEffect {
  effectType: string;
  resourceTypeId: number | null;
  resourceGroupId: number | null;
  value: number;
}

/**
 * A requirement that is not met, with the reason for the hint.
 */
export interface UnmetRequirement {
  requirementId: number;
  requirementType: string;
  targetId: number;
  targetLevel: number;
  reasonCode: string;
  targetNameKey: string | null;
  /** Whether the requirement is out of reach of the next step, so the building is shown as closed. */
  isClosed: boolean;
}

/**
 * The next level of a building and whether the server would accept it now.
 */
export interface NextLevel {
  level: number;
  durationSeconds: number | null;
  costs: ResourceAmount[];
  effects: BuildingEffect[];
  energyBalanceAfter: number;
  /** The energy the body produces once the queue and this level are done. */
  energyOutputAfter: number;
  /** The energy the body consumes once the queue and this level are done. */
  energyConsumptionAfter: number;
  canEnqueue: boolean;
  reasonCodes: string[];
  unmetRequirements: UnmetRequirement[];
  missingResources: ResourceAmount[];
}

/**
 * A building type on a body.
 */
export interface Building {
  buildingTypeId: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  level: number;
  queuedLevel: number;
  maxLevel: number;
  effects: BuildingEffect[];
  next: NextLevel;
  /** The category of the building, a row of the data: the group of its tile on the building page (task 00020). */
  categoryId: number;
}

/**
 * A category of the buildings: the group of the tiles on the building page. The name comes from
 * the texts of the game data by `nameKey`; the order is the one of the data.
 */
export interface BuildingCategory {
  id: number;
  code: string;
  nameKey: string;
  sortOrder: number;
  imageKey: string | null;
}

/**
 * A record of the construction queue.
 */
export interface QueueItem {
  id: number;
  buildingTypeId: number;
  targetLevel: number;
  position: number;
  durationSeconds: number;
  startedAt: string | null;
  finishesAt: string | null;
  costs: ResourceAmount[];
}

/**
 * The amount of a resource on a body now with the limit of the resource: every resource has the
 * capacity the storage of its group gives, and a full resource stops its own production only.
 */
export interface PlanetResource {
  resourceTypeId: number;
  resourceGroupId: number;
  amount: number;
  ratePerHour: number;
  /** How much of this resource the storage of the body holds. */
  capacity: number;
  /** Whether the resource is full and its production stands still. */
  isFull: boolean;
  /** The moment of game time the resource is or will be full, or `null` when it does not fill. */
  fullAt: string | null;
  /** The weight of one unit in tonnes (task 00027). Absent answers count as one tonne. */
  weight?: number;
  /** How much of the resource lies on the body, in tonnes (task 00027). */
  storedTonnes?: number;
}

/**
 * A row of a reference table with its localisation key.
 */
export interface NamedItem {
  id: number;
  code: string;
  nameKey: string;
  resourceGroupId: number | null;
}

/**
 * The place of a body in the galaxy (task 00020).
 */
export interface PlanetCoordinates {
  sectorId: number;
  systemId: number;
  orbit: number | null;
  /** The name of the star system. Absent when the world has no name for it. */
  systemName?: string | null;
  /** The name of the sector. Absent when the world has no name for it. */
  sectorName?: string | null;
}

/**
 * The richness of a deposit of a body: the resource and the coefficient of it (task 00020).
 */
export interface DepositRichness {
  resourceTypeId: number;
  coefficient: number;
}

/**
 * Everything the client shows about a body (`GET /api/v1/planets/{id}`).
 */
export interface Planet {
  planetId: number;
  gameNow: string;
  timeMultiplier: number;
  resources: PlanetResource[];
  energy: { output: number; consumption: number; balance: number; queuedBalance: number };
  queue: { slotCount: number; items: QueueItem[] };
  buildings: Building[];
  /** The categories of the buildings with their order; the building page groups the tiles by them (task 00020). */
  buildingCategories: BuildingCategory[];
  populationMax: number;
  resourceTypes: NamedItem[];
  resourceGroups: NamedItem[];
  /** The name the owner gave the body (task 00020). */
  name: string | null;
  /** The type of the body by identifier; the client reads the name and the picture from the reference table. */
  typeId: number | null;
  /** The place of the body in the galaxy (task 00020). */
  coordinates: PlanetCoordinates | null;
  /** The population of the body now (task 00020). */
  population: number;
  /** How much the population grows with in an hour; it is negative when it dies down (task 00020). */
  populationGrowthPerHour: number;
  /** The time to the largest population, as the server sends it (task 00020). */
  timeToMaximum: string | null;
  /** The richness coefficients of the deposits by resource (task 00020). */
  depositRichness: DepositRichness[];
}

/**
 * The endpoints of a body: the read and the commands of the construction queue. The server
 * checks every rule; the client only shows them.
 */
export interface PlanetApi {
  planet(planetId: number): Promise<Planet>;
  enqueue(planetId: number, buildingTypeId: number): Promise<Planet>;
  cancel(planetId: number, queueItemId: number): Promise<Planet>;
}

const prefix = '/api/v1/planets';

/**
 * Turns the snake_case answer of the server into the camelCase of the client, deeply.
 */
export function camelize<T>(value: unknown): T {
  if (Array.isArray(value)) {
    return value.map((item) => camelize(item)) as T;
  }

  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key.replace(/_([a-z])/g, (_, letter: string) => letter.toUpperCase()), camelize(item)]),
    ) as T;
  }

  return value as T;
}

/**
 * The endpoints of a body over HTTP.
 */
export function createPlanetApi(http: HttpClient): PlanetApi {
  return {
    async planet(planetId) {
      return camelize<Planet>(await http.get<unknown>(`${prefix}/${planetId}`));
    },
    async enqueue(planetId, buildingTypeId) {
      return camelize<Planet>(await http.command<unknown>(`${prefix}/construction/enqueue`, { planet_id: planetId, building_type_id: buildingTypeId }));
    },
    async cancel(planetId, queueItemId) {
      return camelize<Planet>(await http.command<unknown>(`${prefix}/construction/cancel`, { planet_id: planetId, queue_item_id: queueItemId }));
    },
  };
}
