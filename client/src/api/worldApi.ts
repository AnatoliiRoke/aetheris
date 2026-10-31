import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * A row of a reference table of the world (task 00010, section 7.3): the client reads the
 * name and the picture of a race, a resource or a body type from here, so it never maps an
 * identifier to a picture on its own.
 */
export interface Reference {
  id: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  groupCode: string | null;
  groupId: number | null;
  imageKey: string | null;
}

/**
 * The reference tables of the world: the resource groups, the resources, the types of the
 * bodies and the size classes.
 */
export interface WorldReference {
  resourceGroups: Reference[];
  resources: Reference[];
  objectTypes: Reference[];
  sizeClasses: Reference[];
}

/**
 * A deposit of a body: the resource and the coefficient of its richness.
 */
export interface OwnedDeposit {
  resourceTypeId: number;
  coefficient: number;
}

/**
 * A body of the player with everything the screen of the civilization shows about it
 * (task 00010, section 7.4).
 */
export interface OwnedPlanet {
  planetId: number;
  name: string | null;
  /** The type of the body; the client reads its name and its picture from the reference table. */
  objectTypeId: number;
  sectorId: number;
  systemId: number;
  orbit: number | null;
  /** The name of the star system. Absent when the world has no name for it. */
  systemName?: string | null;
  /** The name of the sector. Absent when the world has no name for it. */
  sectorName?: string | null;
  sizeClassId: number;
  size: number;
  isTerraformed: boolean;
  deposits: OwnedDeposit[];
}

/**
 * The reads of the world the screen of the civilization needs: the reference tables and the
 * bodies of the player. The server checks every rule; the client only shows them.
 */
export interface WorldApi {
  reference(): Promise<WorldReference>;
  planets(): Promise<OwnedPlanet[]>;
}

const prefix = '/api/v1/world';

/**
 * The reads of the world over HTTP.
 */
export function createWorldApi(http: HttpClient): WorldApi {
  return {
    async reference() {
      return camelize<WorldReference>(await http.get<unknown>(`${prefix}/reference`));
    },
    async planets() {
      return camelize<OwnedPlanet[]>(await http.get<unknown>(`${prefix}/planets`));
    },
  };
}
