import type { HttpClient } from './http';
import { camelize, type ResourceAmount } from './planetApi';

/**
 * An effect of a level of a technology (task 00012).
 */
export interface TechnologyEffect {
  effectType: string;
  unlockKind: string | null;
  unlockTargetId: number | null;
  unlockNameKey: string | null;
  value: number;
}

/**
 * A level of a technology with its time, its costs and its effects.
 */
export interface TechnologyLevel {
  level: number;
  durationSeconds: number;
  costs: ResourceAmount[];
  effects: TechnologyEffect[];
  unmetRequirementIds: number[];
}

/**
 * The state of a technology for a player (task 00012).
 */
export const technologyStatuses = {
  unknown: 'unknown',
  available: 'available',
  queued: 'queued',
  researching: 'researching',
  learned: 'learned',
  unavailable: 'unavailable',
} as const;

/**
 * A technology of the tree with its state for a player.
 */
export interface Technology {
  id: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  branch: string;
  sortOrder: number;
  maxLevel: number;
  learnedLevel: number;
  status: string;
  researchCenterLevel: number;
  availableCenterLevel: number;
  reasonCode: string | null;
  levels: TechnologyLevel[];
}

/**
 * A record of the queue of the researches.
 */
export interface ResearchQueueItem {
  id: number;
  technologyId: number;
  technologyNameKey: string;
  targetLevel: number;
  planetId: number;
  position: number;
  startsAt: string;
  finishesAt: string | null;
  costs: ResourceAmount[];
}

/**
 * A branch of the tree of the technologies.
 */
export interface ResearchBranch {
  branch: string;
  nameKey: string;
}

/**
 * A body of the player a research can start from (task 00012, decisions 5 and 5a).
 */
export interface ResearchPlanet {
  planetId: number;
  name: string | null;
  researchCenterLevel: number;
  isAvailable: boolean;
  reasonCode: string | null;
}

/**
 * The tree of the technologies with the queue of the researches
 * (`GET /api/v1/research`).
 */
export interface ResearchTree {
  branches: ResearchBranch[];
  technologies: Technology[];
  queue: ResearchQueueItem[];
  queueSlots: number;
  queueUsedSlots: number;
  selectedPlanetId: number | null;
  availableCenterLevel: number;
  planets: ResearchPlanet[];
  gameNow: string;
  timeMultiplier: number;
}

/**
 * The endpoints of the research module: the read of the tree and the commands of the queue.
 * The server checks every rule; the client only shows them.
 */
export interface ResearchApi {
  tree(planetId?: number): Promise<ResearchTree>;
  enqueue(planetId: number, technologyId: number, targetLevel: number): Promise<ResearchTree>;
  cancel(queueItemId: number): Promise<ResearchTree>;
}

const prefix = '/api/v1/research';

/**
 * The endpoints of the research module over HTTP.
 */
export function createResearchApi(http: HttpClient): ResearchApi {
  return {
    async tree(planetId) {
      const query = planetId === undefined ? '' : `?planet_id=${planetId}`;
      return camelize<ResearchTree>(await http.get<unknown>(`${prefix}/${query}`));
    },
    async enqueue(planetId, technologyId, targetLevel) {
      return camelize<ResearchTree>(
        await http.command<unknown>(`${prefix}/enqueue`, {
          planet_id: planetId,
          technology_id: technologyId,
          target_level: targetLevel,
        }),
      );
    },
    async cancel(queueItemId) {
      return camelize<ResearchTree>(await http.command<unknown>(`${prefix}/cancel`, { queue_item_id: queueItemId }));
    },
  };
}
