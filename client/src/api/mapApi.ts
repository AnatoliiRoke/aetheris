import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * What the player knows about a place on the map (task 00034): open (with details), known (only
 * that it is there) or unknown (nothing).
 */
export type MapState = 'open' | 'known' | 'unknown';

/** A sector on the map of the galaxy. */
export interface MapSector {
  id: number;
  name: string;
  x: number;
  y: number;
  z: number;
  isNewbie: boolean;
  systemCount: number;
  ownPlanetCount: number;
  /** What the player knows of the sector: `open`, `known` or `unknown`. */
  state: MapState;
}

/** The map of the galaxy. */
export interface MapGalaxy {
  sectors: MapSector[];
  /** The width and the height of a sector in the units of the map. */
  sectorSize: number;
  /** Whether the player sees nothing but their own bodies. */
  seesOnlyOwn: boolean;
}

/** A star system on the map of a sector. */
export interface MapSystem {
  id: number;
  number: number;
  /** The name; `null` for a star the player only knows of. */
  name: string | null;
  x: number;
  y: number;
  z: number;
  inNebula: boolean;
  state: MapState;
  objectCount: number;
  ownPlanetCount: number;
  distance: number | null;
  travelSeconds: number | null;
}

/** A nebula of a sector. */
export interface MapNebula {
  id: number;
  objectId: number;
  name: string;
  x: number;
  y: number;
  z: number;
  radius: number;
  blocksJumpDrive: boolean;
}

/** A free asteroid field the player sees in the space of a sector. */
export interface MapField {
  objectId: number;
  objectTypeId: number;
  name: string;
  x: number;
  y: number;
  z: number;
  mass: number;
  distance: number | null;
  travelSeconds: number | null;
}

/** The range of a radar of a body of the player. */
export interface MapRadar {
  planetId: number;
  x: number;
  y: number;
  z: number;
  radius: number;
}

/** The map of a sector. */
export interface MapSectorView {
  sectorId: number;
  name: string;
  sectorSize: number;
  systems: MapSystem[];
  nebulas: MapNebula[];
  fields: MapField[];
  radars: MapRadar[];
  seesOnlyOwn: boolean;
}

/** The owner of a body as the map shows them. */
export interface MapOwner {
  nickname: string;
  raceCode: string;
  raceNameKey: string;
  raceColorKey: string | null;
}

/** A deposit of a body or a share of a field. */
export interface MapResourceShare {
  resourceId: number;
  coefficient: number;
}

/** An object of a star system. */
export interface MapObject {
  id: number;
  state: MapState;
  orbit: number | null;
  /** The class of the type, or `unknown` for a place the player only knows of. */
  objectClass: string;
  objectTypeId: number | null;
  name: string | null;
  planetId: number | null;
  sizeClassId: number | null;
  size: number | null;
  isOwn: boolean;
  isFree: boolean;
  isTerraformed: boolean;
  owner: MapOwner | null;
  stationLimit: number | null;
  mass: number | null;
  resources: MapResourceShare[];
  distance: number | null;
  travelSeconds: number | null;
}

/** The map of a star system. */
export interface MapSystemView {
  system: MapSystem;
  sectorName: string;
  orbitCount: number;
  objects: MapObject[];
}

/** A result of the search on the map. */
export interface MapSearchResult {
  kind: 'system' | 'object' | 'player';
  sectorId: number;
  systemId: number | null;
  objectId: number | null;
  objectTypeId: number | null;
  name: string;
  systemName: string | null;
  orbit: number | null;
}

/** A point the player pointed at as the target of a flight. */
export interface MapTargetInfo {
  kind: 'system' | 'object' | 'point';
  targetId: number | null;
  sectorId: number;
  x: number;
  y: number;
  z: number;
  name: string | null;
  nearSystemId: number | null;
  nearSystemName: string | null;
  inNebula: boolean;
  jumpForbidden: boolean;
  state: MapState;
  distance: number | null;
  travelSeconds: number | null;
  errors: string[];
}

/**
 * The reads of the map screen (task 00034). The server decides what the player sees, counts the
 * distances and the times and checks every rule; the client only draws the answers.
 */
export interface MapApi {
  galaxy(): Promise<MapGalaxy>;
  sector(sectorId: number, fromPlanetId: number | null): Promise<MapSectorView>;
  system(systemId: number, fromPlanetId: number | null): Promise<MapSystemView>;
  search(query: string): Promise<MapSearchResult[]>;
  target(sectorId: number, x: number, y: number, z: number, fromPlanetId: number | null): Promise<MapTargetInfo>;
}

const prefix = '/api/v1/world/map';

const from = (fromPlanetId: number | null) => (fromPlanetId === null ? '' : `from_planet_id=${fromPlanetId}`);

const withQuery = (path: string, query: string) => (query === '' ? path : `${path}?${query}`);

/** The reads of the map over HTTP. */
export function createMapApi(http: HttpClient): MapApi {
  return {
    async galaxy() {
      return camelize<MapGalaxy>(await http.get<unknown>(`${prefix}/galaxy`));
    },
    async sector(sectorId, fromPlanetId) {
      return camelize<MapSectorView>(await http.get<unknown>(withQuery(`${prefix}/sectors/${sectorId}`, from(fromPlanetId))));
    },
    async system(systemId, fromPlanetId) {
      return camelize<MapSystemView>(await http.get<unknown>(withQuery(`${prefix}/systems/${systemId}`, from(fromPlanetId))));
    },
    async search(query) {
      return camelize<MapSearchResult[]>(await http.get<unknown>(`${prefix}/search?q=${encodeURIComponent(query)}`));
    },
    async target(sectorId, x, y, z, fromPlanetId) {
      const origin = from(fromPlanetId);

      return camelize<MapTargetInfo>(await http.get<unknown>(`${prefix}/target?sector_id=${sectorId}&x=${x}&y=${y}&z=${z}${origin ? `&${origin}` : ''}`));
    },
  };
}
