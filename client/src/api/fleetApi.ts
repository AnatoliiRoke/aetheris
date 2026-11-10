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
  /** The localisation key of the name of the group of the hull: the class of the hull. */
  partGroupNameKey: string;
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
  /** How many extra slots the race gives the hull. */
  raceBonusSlots: number;
  /** How many people the hull needs. The seed sets this to the number of slots. */
  crew: number;
}

/**
 * One characteristic of a module that the plaque of a project adds up.
 */
export interface ShipModuleFact {
  /** The kind of the characteristic. */
  kind: string;
  /** The localisation key of the name of the characteristic. */
  nameKey: string;
  /** The localisation key of the unit, or `null`. */
  unitKey: string | null;
  /** The amount the module adds. */
  amount: number;
  /** The order of the characteristic in the lists. */
  sortOrder: number;
}

/**
 * One summed characteristic of the fitted modules.
 */
export interface ShipDesignFact {
  /** The kind of the characteristic. */
  kind: string;
  /** The localisation key of the name of the characteristic. */
  nameKey: string;
  /** The localisation key of the unit, or `null`. */
  unitKey: string | null;
  /** The amount the fitted modules add together. */
  amount: number;
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
  /** The neutral code of the group of the module: its type. */
  partGroupCode: string;
  /** The localisation key of the name of the group of the module: its type. */
  partGroupNameKey: string;
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
  /** The push the module gives, for a drive. */
  drivePush: number;
  /** The damage the module makes, for a weapon. */
  damage: number;
  /** The kind of the damage the module makes: the energy or the kinetic one. */
  damageKind: string | null;
  /** The capacity of the shield of the module, for a shield. */
  shieldCapacity: number;
  /** How much cargo the module adds to the hold, in tonnes. */
  cargoCapacity?: number;
  /** How much integrity the armor of the module adds. */
  armor?: number;
  /** How fast the shield of the module recovers. */
  shieldRegen?: number;
  /** How many shield units the module restores over time. */
  shieldRestore?: number;
  /** How many shots the weapon makes in one round. */
  rate?: number;
  /** The size of the launcher, or zero when the weapon has none. */
  launcherSize?: number;
  /** The ammunition the weapon fires. A weapon that fires ammunition has no damage of its own. */
  ammunitionIds?: number[];
  /** The characteristics the plaque adds up. */
  facts?: ShipModuleFact[];
  /** The weight of one module in tonnes. */
  weight: number;
  /** The marks of the module that the catalog holds. */
  markLevels: number[];
  /** Whether the module is ammunition of a weapon: it is loaded, not fitted. */
  isAmmunition: boolean;
  /** Whether the module is a capsule of inhabitants: it is used on a body, not fitted. */
  isCapsule: boolean;
}

/**
 * One round of ammunition a kinetic weapon may fire. It is not a module of a ship.
 */
export interface ShipAmmunition {
  id: number;
  /** The damage of one round. */
  damage: number;
  /** The kind of the damage: the energy or the kinetic one. */
  damageKind: string | null;
  /** The size of the launcher the round fits, or zero when it has none. */
  launcherSize: number;
}

/**
 * The catalog of the ships for the constructor (task 00038).
 */
export interface ShipCatalog {
  hulls: ShipHull[];
  modules: ShipModule[];
  /** The types of the modules, for the filters of the picker. */
  moduleGroups: ShipModuleGroup[];
  /** How many projects the player may keep. */
  designLimit: number;
  /** How many projects the player has. */
  designCount: number;
  /** The ammunition a kinetic weapon may fire. */
  ammunition?: ShipAmmunition[];
}

/**
 * A type of a module: the group of the module, with the name of the group and how many modules
 * of it the catalog holds (task 00038).
 */
export interface ShipModuleGroup {
  /** The neutral code of the group. */
  code: string;
  /** The localisation key of the name of the group. */
  nameKey: string;
  /** How many modules of the group the catalog holds. */
  count: number;
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
  /** The push the drives of the ship give. */
  drivePush: number;
  /** The energy damage the weapons of the ship make in one round. */
  energyDamage: number;
  /** The greatest kinetic damage the weapons make in one round. */
  kineticDamage: number;
  /** The capacity of the shields of the ship. */
  shieldCapacity: number;
  hasDrive: boolean;
  hasReactor: boolean;
  /** How much integrity the fitted armor adds. */
  armor?: number;
  /** How fast the shields recover, with the shield restorers. */
  shieldRegen?: number;
  /** The least kinetic damage the weapons make in one round. */
  kineticDamageMin?: number;
  /** The greatest kinetic damage the weapons make in one round. */
  kineticDamageMax?: number;
  /** The least damage all the weapons make in one round, as the server counts it. */
  damageMin?: number;
  /** The greatest damage all the weapons make in one round. */
  damageMax?: number;
  /** The characteristics of the fitted modules that the plaque adds up. */
  facts?: ShipDesignFact[];
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
  /** Whether a finished ship of this project exists. The project then cannot be edited or archived. */
  hasBuiltShips?: boolean;
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
  /** The shipyard of a body: the queue, the projects and the finished ships (task 00039). */
  shipyard(planetId: number): Promise<ShipyardView>;
  /** Puts a project into the shipyard queue of a body. */
  enqueueShipyard(request: { planetId: number; planetBuildingId?: number | null; designId: number; quantity: number }): Promise<ShipyardView>;
  /** Cancels a record of a shipyard queue and returns what it paid. */
  cancelShipyard(orderId: number): Promise<ShipyardView>;
  /** The finished ships of a body. */
  ships(planetId: number): Promise<FinishedShip[]>;
  /** Takes a finished ship apart on the shipyard of its body. */
  disassembleShip(shipId: number): Promise<ShipyardView>;
  /** The fleets of the player and the ships that are in no fleet (task 00040). */
  fleets(): Promise<FleetsView>;
  /** Forms a fleet of free ships that stand on one body; the body is the home of the fleet. */
  createFleet(request: { planetId: number; name: string; shipIds: number[] }): Promise<FleetsView>;
  /** Puts free ships into a fleet. */
  addFleetShips(request: { fleetId: number; shipIds: number[] }): Promise<FleetsView>;
  /** Takes ships out of a fleet; they stay on the body as free ships. */
  removeFleetShips(request: { fleetId: number; shipIds: number[] }): Promise<FleetsView>;
  /** Moves ships to another fleet that stands on the same body. */
  transferFleetShips(request: { fleetId: number; targetFleetId: number; shipIds: number[] }): Promise<FleetsView>;
  /** Renames a fleet. */
  renameFleet(request: { fleetId: number; name: string }): Promise<FleetsView>;
  /** Disbands a fleet; its ships stay on the body as free ships. */
  dissolveFleet(fleetId: number): Promise<FleetsView>;
  /** The flight plans of the player (task 00050). */
  flightPlans(): Promise<FlightPlan[]>;
  /** Writes a flight plan for a fleet; the fleet has none yet. */
  createFlightPlan(request: { fleetId: number; orders: FlightPlanOrderInput[] }): Promise<FlightPlan>;
  /** Changes the orders of the flight plan of a fleet. */
  saveFlightPlan(request: { fleetId: number; orders: FlightPlanOrderInput[] }): Promise<FlightPlan>;
  /** Deletes the flight plan of a fleet. */
  deleteFlightPlan(fleetId: number): Promise<void>;
  /** Stops the flight plan of a fleet: the fleet waits where it is. */
  stopFlightPlan(fleetId: number): Promise<FlightPlan>;
  /** Puts the flight plan of a fleet under way. */
  executeFlightPlan(fleetId: number): Promise<FlightPlan>;
  /** Estimates what the extraction of a fleet at a place will take (task 00050). */
  extractionEstimate(fleetId: number, objectId: number): Promise<ExtractionEstimate>;
}

/**
 * A flight plan of a fleet with its orders (task 00050).
 */
export interface FlightPlan {
  id: number;
  /** The fleet the plan is made for. */
  fleetId: number;
  /** The state of the plan: the client turns the code into a word. */
  stateCode: string;
  createdAt: string;
  updatedAt: string;
  version: number;
  orders: FlightPlanOrder[];
}

/**
 * An order of a flight plan (task 00050).
 */
export interface FlightPlanOrder {
  id: number;
  /** The position of the order in the plan, from one. */
  position: number;
  /** The type of the order: `fly`, `mine`, `scan`, `load`, `unload`, `wait` or `return`. */
  typeCode: string;
  /** The kind of the target: `none`, `system`, `object`, `point` or `planet`. */
  targetKind: string;
  targetSectorId: number;
  targetSystemId: number;
  targetObjectId: number;
  targetX: number;
  targetY: number;
  targetZ: number;
  durationSeconds: number;
  /** The parameters of the order as a document. */
  parameters: string;
}

/**
 * An order of a flight plan as the client sends it (task 00050).
 */
export interface FlightPlanOrderInput {
  typeCode: string;
  targetKind: string;
  targetSectorId: number;
  targetSystemId: number;
  targetObjectId: number;
  targetX: number;
  targetY: number;
  targetZ: number;
  durationSeconds: number;
  parameters: string;
}

/**
 * What the extraction of a fleet at a place will take (task 00050).
 */
export interface ExtractionEstimate {
  /** The length of a cycle of extraction, in game seconds. */
  cycleSeconds: number;
  /** The yield of the fleet in one cycle, in tonnes. */
  resourceTonsPerCycle: number;
  /** The mass the fleet takes from the place in one cycle, in tonnes. */
  processedTonsPerCycle: number;
  /** The room that is left in the holds of the fleet, in tonnes. */
  freeHoldTons: number;
  /** The mass that is left in the place, in tonnes. */
  placeMassTons: number;
  /** How long the holds of the fleet will hold the yield, in game seconds. */
  holdFullSeconds: number;
  /** How long the place will last, in game seconds. */
  placeEmptySeconds: number;
}

/**
 * A finished ship as the fleets show it (task 00040).
 */
export interface FleetShip {
  id: number;
  /** The body the ship stands on. */
  planetId: number;
  designId: number;
  /** The name of the project the ship was built from. */
  name: string;
  /** The localisation key of the name of the hull. */
  hullNameKey: string;
  /** The localisation key of the class of the hull. */
  classNameKey: string;
  /** The worst condition of the parts of the ship, in per cent. */
  conditionPercent: number;
  crew: number;
  upkeepPerHour: number;
  /** The fleet the ship is in, or `null` when it is free. */
  fleetId: number | null;
}

/**
 * A fleet with its ships (task 00040).
 */
export interface Fleet {
  id: number;
  name: string;
  /** The home body of the fleet: its base. */
  homePlanetId: number;
  /** The body whose orbit the fleet stands on now. */
  planetId: number;
  sectorId: number;
  /** The state of the fleet; the client turns the code into a word. */
  stateCode: string;
  crew: number;
  upkeepPerHour: number;
  createdAt: string;
  ships: FleetShip[];
  /** The hold of all the ships, as the projects of the ships give it. */
  holdCapacity: number;
  /** The integrity of all the ships. */
  integrity: number;
  /** The least damage all the weapons make in one round. */
  damageMin: number;
  /** The greatest damage all the weapons make in one round. */
  damageMax: number;
  /** The capacity of all the shields. */
  shieldCapacity: number;
}

/**
 * The fleets of the player (task 00040).
 */
export interface FleetsView {
  /** How many fleets the spaceports of the player allow. */
  limit: number;
  /** How many fleets the player has. */
  used: number;
  /** Whether the civilization has credits: without them a fleet does not fly. */
  hasCredits: boolean;
  upkeepPerHour: number;
  fleets: Fleet[];
  /** The finished ships that are in no fleet. */
  freeShips: FleetShip[];
}

/**
 * A resource of an assembly.
 */
export interface ShipyardCost {
  resourceTypeId: number;
  amount: number;
  nameKey: string | null;
  /** How much the player has. Absent on a paid queue record. */
  available?: number | null;
}

/**
 * A stack of parts an assembly takes, or a stack on a finished ship.
 */
export interface ShipyardPart {
  partTypeId: number;
  nameKey: string;
  markLevel: number;
  hasRaceBonus: boolean;
  quantity: number;
  available: number;
  isHull: boolean;
}

/**
 * A record of a shipyard queue.
 */
export interface ShipyardOrder {
  id: number;
  designId: number;
  title: string;
  position: number;
  underWay: boolean;
  finishesAt: string | null;
  durationSeconds: number;
  costs: ShipyardCost[];
  /** How many ships the record assembles. */
  quantity: number;
  /** How many ships of the record are already finished. */
  producedQuantity: number;
}

/**
 * A project as the shipyard shows it.
 */
export interface ShipyardDesign {
  designId: number;
  name: string;
  hullPartTypeId: number;
  hullNameKey: string;
  canEnqueue: boolean;
  reasonCode: string | null;
  requirementNameKey: string | null;
  requirementLevel: number | null;
  crew: number;
  credits: number;
  durationSeconds: number;
  costs: ShipyardCost[];
  parts: ShipyardPart[];
  /** The credits one finished ship of the project costs per hour. */
  upkeepPerHour?: number;
}

/**
 * A finished ship.
 */
export interface FinishedShip {
  id: number;
  planetId: number;
  designId: number;
  name: string;
  /** The localisation key of the name of the hull. */
  hullNameKey: string;
  /** The localisation key of the class of the hull: the group of the hull. */
  classNameKey: string;
  stateCode: string;
  crew: number;
  upkeepPerHour: number;
  modules: ShipyardPart[];
  /** The fleet the ship is in, or `null`/absent when it is free (task 00040). */
  fleetId?: number | null;
}

/**
 * A shipyard of a body with its queue, the projects and the finished ships.
 */
export interface ShipyardView {
  planetId: number;
  planetBuildingId: number | null;
  level: number;
  used: number;
  total: number;
  gameNow: string;
  timeMultiplier: number;
  orders: ShipyardOrder[];
  designs: ShipyardDesign[];
  ships: FinishedShip[];
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
    async shipyard(planetId) {
      return camelize<ShipyardView>(await http.get<unknown>(`${prefix}/shipyards?planet_id=${planetId}`));
    },
    async enqueueShipyard(request) {
      return camelize<ShipyardView>(
        await http.command<unknown>(`${prefix}/shipyard/enqueue`, {
          planet_id: request.planetId,
          planet_building_id: request.planetBuildingId ?? null,
          design_id: request.designId,
          quantity: request.quantity,
        }),
      );
    },
    async cancelShipyard(orderId) {
      return camelize<ShipyardView>(await http.command<unknown>(`${prefix}/shipyard/cancel`, { order_id: orderId }));
    },
    async ships(planetId) {
      return camelize<FinishedShip[]>(await http.get<unknown>(`${prefix}/ships?planet_id=${planetId}`));
    },
    async disassembleShip(shipId) {
      return camelize<ShipyardView>(await http.command<unknown>(`${prefix}/ship/disassemble`, { ship_id: shipId }));
    },
    async fleets() {
      return camelize<FleetsView>(await http.get<unknown>(`${prefix}/fleets`));
    },
    async createFleet(request) {
      return camelize<FleetsView>(
        await http.command<unknown>(`${prefix}/fleets`, { planet_id: request.planetId, name: request.name, ship_ids: request.shipIds }),
      );
    },
    async addFleetShips(request) {
      return camelize<FleetsView>(
        await http.command<unknown>(`${prefix}/fleets/add-ships`, { fleet_id: request.fleetId, ship_ids: request.shipIds }),
      );
    },
    async removeFleetShips(request) {
      return camelize<FleetsView>(
        await http.command<unknown>(`${prefix}/fleets/remove-ships`, { fleet_id: request.fleetId, ship_ids: request.shipIds }),
      );
    },
    async transferFleetShips(request) {
      return camelize<FleetsView>(
        await http.command<unknown>(`${prefix}/fleets/transfer-ships`, {
          fleet_id: request.fleetId,
          target_fleet_id: request.targetFleetId,
          ship_ids: request.shipIds,
        }),
      );
    },
    async renameFleet(request) {
      return camelize<FleetsView>(await http.command<unknown>(`${prefix}/fleets/rename`, { fleet_id: request.fleetId, name: request.name }));
    },
    async dissolveFleet(fleetId) {
      return camelize<FleetsView>(await http.command<unknown>(`${prefix}/fleets/dissolve`, { fleet_id: fleetId }));
    },
    async flightPlans() {
      return camelize<FlightPlan[]>(await http.get<unknown>(`${prefix}/flight-plans`));
    },
    async createFlightPlan(request) {
      return camelize<FlightPlan>(
        await http.command<unknown>(`${prefix}/flight-plans`, {
          fleet_id: request.fleetId,
          orders: request.orders.map((order) => toOrderPayload(order)),
        }),
      );
    },
    async saveFlightPlan(request) {
      return camelize<FlightPlan>(
        await http.command<unknown>(`${prefix}/flight-plans/save`, {
          fleet_id: request.fleetId,
          orders: request.orders.map((order) => toOrderPayload(order)),
        }),
      );
    },
    async deleteFlightPlan(fleetId) {
      await http.command<unknown>(`${prefix}/flight-plans/delete`, { fleet_id: fleetId });
    },
    async stopFlightPlan(fleetId) {
      return camelize<FlightPlan>(await http.command<unknown>(`${prefix}/flight-plans/stop`, { fleet_id: fleetId }));
    },
    async executeFlightPlan(fleetId) {
      return camelize<FlightPlan>(await http.command<unknown>(`${prefix}/flight-plans/execute`, { fleet_id: fleetId }));
    },
    async extractionEstimate(fleetId, objectId) {
      return camelize<ExtractionEstimate>(
        await http.get<unknown>(`${prefix}/extraction/estimate?fleet_id=${fleetId}&object_id=${objectId}`),
      );
    },
  };
}

/**
 * Turns an order of a flight plan into the fields the server reads (task 00050).
 *
 * @param order The order.
 * @returns The fields of the request.
 */
function toOrderPayload(order: FlightPlanOrderInput) {
  return {
    type_code: order.typeCode,
    target_kind: order.targetKind,
    target_sector_id: order.targetSectorId,
    target_system_id: order.targetSystemId,
    target_object_id: order.targetObjectId,
    target_x: order.targetX,
    target_y: order.targetY,
    target_z: order.targetZ,
    duration_seconds: order.durationSeconds,
    parameters: order.parameters,
  };
}
