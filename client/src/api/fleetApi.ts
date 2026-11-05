import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * A hull of the catalog of the ships (task 00038).
 */
export interface ShipHull {
  id: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  imageKey: string | null;
  partGroupCode: string;
  /** The role of the hull: what it carries. */
  role: string;
  /** How many slots the hull has. */
  slots: number;
  /** How much the hold of the hull takes. */
  holdCapacity: number;
  /** How strong the hull is. */
  integrity: number;
  /** The weight of the hull in tonnes. */
  weight: number;
  /** Whether the race of the player gives the hull one more slot. */
  allowsRaceBonusSlot: boolean;
  /** How many of the hull the body has on its stock. */
  stock: number;
}

/**
 * A module of the catalog of the ships (task 00038).
 */
export interface ShipModule {
  id: number;
  code: string;
  nameKey: string;
  descriptionKey: string;
  imageKey: string | null;
  partGroupCode: string;
  /** The role of the module: what it may be put on. */
  role: string;
  /** The kind of the component the module is: the drive, the reactor or the life support. */
  component: string | null;
  /** The hulls the module fits. */
  compatibleHullIds: number[];
  /** The races the module belongs to. */
  raceIds: number[];
  /** How much energy the module takes. */
  energyUse: number;
  /** How much power the module gives, for a reactor. */
  power: number;
  /** How many people the module needs. */
  crew: number;
  /** The weight of one module in tonnes. */
  weight: number;
  /** The marks of the module that the catalog holds. */
  markLevels: number[];
  /** How many of the module the body has on its stock. */
  stock: number;
}

/**
 * The catalog of the ships for the constructor (task 00038).
 */
export interface ShipCatalog {
  hulls: ShipHull[];
  modules: ShipModule[];
  /** How many projects the player may keep. */
  designLimit: number;
  /** How many projects the player has. */
  designCount: number;
}

/**
 * A slot of a project of a ship (task 00038).
 */
export interface ShipDesignSlot {
  slotIndex: number;
  partTypeId: number;
  markLevel: number;
  hasRaceBonus: boolean;
}

/**
 * What a project of a ship gives (task 00038).
 */
export interface ShipDesignSummary {
  totalSlots: number;
  usedSlots: number;
  freeSlots: number;
  weight: number;
  energyUse: number;
  energyPower: number;
  crew: number;
  holdCapacity: number;
  integrity: number;
  hasDrive: boolean;
  hasReactor: boolean;
  hasLifeSupport: boolean;
}

/**
 * A project of a ship (task 00038).
 */
export interface ShipDesign {
  id: number;
  name: string;
  planetId: number;
  hullPartTypeId: number;
  hasRaceBonusSlot: boolean;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
  version: number;
  slots: ShipDesignSlot[];
  summary: ShipDesignSummary;
}

/**
 * A slot as the client sends it (task 00038).
 */
export interface ShipDesignSlotInput {
  slotIndex: number;
  partTypeId: number;
  markLevel: number;
  hasRaceBonus: boolean;
}

/**
 * The endpoints of the fleet module over HTTP.
 */
export interface FleetApi {
  /** The catalog of the ships of the constructor. */
  shipCatalog(planetId: number): Promise<ShipCatalog>;
  /** The projects of the player. */
  designs(planetId?: number, includeArchived?: boolean): Promise<ShipDesign[]>;
  /** One project of the player. */
  design(designId: number): Promise<ShipDesign>;
  /** Creates a project of a ship. */
  createDesign(request: { planetId: number; name: string; hullPartTypeId: number; hasRaceBonusSlot: boolean }): Promise<ShipDesign>;
  /** Renames a project of a ship. */
  renameDesign(designId: number, name: string): Promise<ShipDesign>;
  /** Saves the hull and the slots of a project of a ship. */
  saveDesign(request: {
    designId: number;
    hullPartTypeId: number;
    hasRaceBonusSlot: boolean;
    slots: ShipDesignSlotInput[];
  }): Promise<ShipDesign>;
  /** Archives a project of a ship. */
  archiveDesign(designId: number): Promise<ShipDesign>;
}

const prefix = '/api/v1/fleet';

/**
 * The endpoints of the fleet module over HTTP.
 */
export function createFleetApi(http: HttpClient): FleetApi {
  return {
    async shipCatalog(planetId) {
      return camelize<ShipCatalog>(await http.get<unknown>(`${prefix}/ship-catalog?planet_id=${planetId}`));
    },
    async designs(planetId, includeArchived) {
      const query = new URLSearchParams();
      if (planetId !== undefined) {
        query.set('planet_id', String(planetId));
      }
      if (includeArchived) {
        query.set('include_archived', 'true');
      }
      const suffix = query.size > 0 ? `?${query.toString()}` : '';

      return camelize<ShipDesign[]>(await http.get<unknown>(`${prefix}/designs${suffix}`));
    },
    async design(designId) {
      return camelize<ShipDesign>(await http.get<unknown>(`${prefix}/designs/${designId}`));
    },
    async createDesign(request) {
      return camelize<ShipDesign>(
        await http.command<unknown>(`${prefix}/designs`, {
          planet_id: request.planetId,
          name: request.name,
          hull_part_type_id: request.hullPartTypeId,
          has_race_bonus_slot: request.hasRaceBonusSlot,
        }),
      );
    },
    async renameDesign(designId, name) {
      return camelize<ShipDesign>(await http.command<unknown>(`${prefix}/designs/rename`, { design_id: designId, name }));
    },
    async saveDesign(request) {
      return camelize<ShipDesign>(
        await http.command<unknown>(`${prefix}/designs/save`, {
          design_id: request.designId,
          hull_part_type_id: request.hullPartTypeId,
          has_race_bonus_slot: request.hasRaceBonusSlot,
          slots: request.slots.map((slot) => ({
            slot_index: slot.slotIndex,
            part_type_id: slot.partTypeId,
            mark_level: slot.markLevel,
            has_race_bonus: slot.hasRaceBonus,
          })),
        }),
      );
    },
    async archiveDesign(designId) {
      return camelize<ShipDesign>(await http.command<unknown>(`${prefix}/designs/archive`, { design_id: designId }));
    },
  };
}
