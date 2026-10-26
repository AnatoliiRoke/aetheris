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
 * The amount of a resource on a body now.
 */
export interface PlanetResource {
  resourceTypeId: number;
  resourceGroupId: number;
  amount: number;
  ratePerHour: number;
}

/**
 * The storage of a resource group.
 */
export interface StorageGroup {
  resourceGroupId: number;
  capacity: number;
  stored: number;
  isFull: boolean;
  fullAt: string | null;
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
 * Everything the client shows about a body (`GET /api/v1/planets/{id}`).
 */
export interface Planet {
  planetId: number;
  gameNow: string;
  timeMultiplier: number;
  resources: PlanetResource[];
  storageGroups: StorageGroup[];
  energy: { output: number; consumption: number; balance: number; queuedBalance: number };
  queue: { slotCount: number; items: QueueItem[] };
  buildings: Building[];
  populationMax: number;
  resourceTypes: NamedItem[];
  resourceGroups: NamedItem[];
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
