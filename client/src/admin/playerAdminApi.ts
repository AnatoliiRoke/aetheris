import type { HttpClient } from '../api/http';
import { adminPrefix, type Page } from './adminApi';

/** One player in the admin list. */
export interface PlayerRow {
  user_id: string;
  nickname: string;
  email: string | null;
  email_confirmed: boolean;
  registered_at: string;
  race_name_key: string | null;
  planet_count: number;
}

/** A body shown only so the administrator knows which colony is open. */
export interface PlanetLabel {
  planet_id: number;
  sector_id: number;
  name: string | null;
}

/** The civilization of a player. The race is not edited here. */
export interface CivilizationCard {
  race_id: number;
  race_code: string;
  race_name_key: string;
  planet_id: number;
  planet_name: string | null;
  system_id: number;
  sector_id: number;
  founded_at: string;
}

/** The card of a player. */
export interface PlayerCard {
  user_id: string;
  nickname: string;
  email: string | null;
  email_confirmed: boolean;
  registered_at: string;
  civilization: CivilizationCard | null;
  planets: PlanetLabel[];
}

/** A currency of the civilization. */
export interface BalanceRow {
  player_id: string;
  resource_type_id: number;
  amount: number;
  income_per_hour: number;
  version: number;
  name_key: string | null;
}

/** Energy counted from the buildings. It is not stored. */
export interface EnergyView {
  output: number;
  consumption: number;
  balance: number;
  queued_balance: number;
}

/** One stock of a colony. */
export interface StockRow {
  resource_type_id: number;
  name_key: string;
  group_name_key: string | null;
  amount: number;
}

/** One building of a colony. */
export interface BuildingRow {
  building_type_id: number;
  name_key: string;
  level: number;
  max_level: number;
  queued_level: number;
}

/** One record of the construction queue. */
export interface ConstructionRow {
  id: number;
  building_type_id: number;
  name_key: string;
  target_level: number;
  position: number;
  finishes_at: string | null;
}

/** The colony state an administrator may change. The body itself is not here. */
export interface ColonyView {
  planet_id: number;
  population: number;
  population_maximum: number;
  population_growth_per_hour: number;
  extra_queue_slots: number;
  energy: EnergyView;
  stocks: StockRow[];
  buildings: BuildingRow[];
  construction_queue: ConstructionRow[];
}

/** One technology of a body. */
export interface TechnologyRow {
  technology_id: number;
  name_key: string;
  branch_id: number;
  branch_name_key: string;
  level: number;
  max_level: number;
}

/** One record of the research queue of the whole civilization. */
export interface ResearchRow {
  id: number;
  planet_id: number;
  technology_id: number;
  name_key: string;
  target_level: number;
  position: number;
  finishes_at: string | null;
}

/** Technologies of one body and the research queue of the player. */
export interface TechnologyView {
  technologies: TechnologyRow[];
  queue: ResearchRow[];
}

/** A stack of parts. */
export interface PartStackRow {
  id: number;
  part_type_id: number;
  name_key: string;
  mark_level: number;
  has_race_bonus: boolean;
  condition_percent: number;
  quantity: number;
}

/** A part the administrator may grant. */
export interface PartTypeRow {
  id: number;
  name_key: string;
  mark_level: number;
  is_archived: boolean;
}

/** An order of a factory. */
export interface ProductionOrderRow {
  id: number;
  planet_building_id: number;
  recipe_id: number;
  name_key: string;
  quantity: number;
  produced_quantity: number;
  position: number;
  finishes_at: string | null;
}

/** Parts and factory orders of a body. */
export interface ProductionView {
  stacks: PartStackRow[];
  part_types: PartTypeRow[];
  orders: ProductionOrderRow[];
}

/** An order of a process. */
export interface ProcessOrderRow {
  id: number;
  planet_building_id: number;
  process_recipe_id: number;
  name_key: string;
  quantity: number;
  produced_quantity: number;
  position: number;
  finishes_at: string | null;
}

/** What a ship project gives. The slots stay as the constructor saved them. */
export interface DesignSummary {
  total_slots: number;
  used_slots: number;
  free_slots: number;
  weight: number;
  energy_use: number;
  energy_power: number;
  crew: number;
  hold_capacity: number;
  integrity: number;
  drive_push: number;
  energy_damage: number;
  kinetic_damage: number;
  shield_capacity: number;
  has_drive: boolean;
  has_reactor: boolean;
  /** How much integrity the fitted armor adds. */
  armor?: number;
  /** How fast the shields recover, with the shield restorers. */
  shield_regen?: number;
  /** The least kinetic damage the weapons make in one round. */
  kinetic_damage_min?: number;
  /** The greatest kinetic damage the weapons make in one round. */
  kinetic_damage_max?: number;
  /** The characteristics of the fitted modules that the plaque adds up. */
  facts?: DesignFact[];
}

/** One summed characteristic of the fitted modules. */
export interface DesignFact {
  kind: string;
  name_key: string;
  unit_key: string | null;
  amount: number;
}

/** A slot of a ship project, shown and not edited. */
export interface DesignSlot {
  slot_index: number;
  part_type_id: number;
  mark_level: number;
  has_race_bonus: boolean;
}

/** The finished ships of one project. Zero is a real count. */
export interface PlayerShipQuantity {
  design_id: number;
  name: string;
  planet_id: number;
  is_archived: boolean;
  quantity: number;
}

/** A ship project. */
export interface ShipDesign {
  id: number;
  name: string;
  planet_id: number;
  hull_part_type_id: number;
  has_race_bonus_slot: boolean;
  is_archived: boolean;
  created_at: string;
  updated_at: string;
  version: number;
  slots: DesignSlot[];
  summary: DesignSummary;
}

/** A name of a resource or a part on the market screen. */
export interface MarketName {
  id: number;
  name_key: string;
}

/** An open lot. */
export interface MarketLot {
  id: number;
  seller_player_id: string;
  seller_planet_id: number;
  market_level_id: number;
  resource_type_id: number | null;
  part_type_id: number | null;
  quantity: number;
  price_per_unit: number;
  total_price: number;
  state: string;
  created_at: string;
  part_mark_level: number | null;
  part_has_race_bonus: boolean | null;
  part_condition_percent: number | null;
}

/** The other side of a deal or a delivery. */
export interface MarketPlayer {
  player_id: string;
  nickname: string;
  race_name_key: string | null;
  race_color_key: string | null;
}

/** One deal in the history. */
export interface MarketDeal {
  id: number;
  direction: string;
  counterparty: MarketPlayer;
  resource_type_id: number | null;
  part_type_id: number | null;
  quantity: number;
  price_per_unit: number;
  sum: number;
  commission: number;
  created_at: string;
}

/** One page of deals. */
export interface MarketDeals {
  items: MarketDeal[];
  total: number;
  offset: number;
  limit: number;
}

/** One delivery. */
export interface MarketDelivery {
  id: number;
  counterparty: MarketPlayer;
  source_planet_id: number;
  destination_planet_id: number;
  resource_type_id: number | null;
  part_type_id: number | null;
  quantity: number;
  energy_rod_amount: number;
  started_at: string;
  arrives_at: string;
  state: string;
}

/** Lots, deals and deliveries of a player. */
export interface MarketView {
  lots: MarketLot[];
  deals: MarketDeals;
  deliveries: { incoming: MarketDelivery[]; outgoing: MarketDelivery[] };
  resources: MarketName[];
  parts: MarketName[];
}

/** A grant of parts. */
export interface PartGrant {
  part_type_id: number;
  quantity: number;
  has_race_bonus: boolean;
  condition_percent: number | null;
  reason: string;
}

/**
 * The player screen talks to the admin group through the same client as the rest of the
 * panel. It is not part of {@link AdminApi}: the existing screens and their tests do not
 * grow a required method.
 */
export interface PlayerAdminApi {
  list(page: number, search: string): Promise<Page<PlayerRow>>;
  card(userId: string): Promise<PlayerCard>;
  changeEmail(userId: string, email: string): Promise<void>;
  balances(userId: string): Promise<BalanceRow[]>;
  setBalance(userId: string, resourceTypeId: number, amount: number, reason: string, version: number): Promise<void>;
  colony(userId: string, planetId: number): Promise<ColonyView>;
  setStock(userId: string, planetId: number, resourceTypeId: number, amount: number, reason: string): Promise<void>;
  setBuilding(userId: string, planetId: number, buildingTypeId: number, level: number, reason: string): Promise<void>;
  setSlots(userId: string, planetId: number, extraQueueSlots: number, reason: string): Promise<void>;
  cancelConstruction(userId: string, planetId: number, queueItemId: number, reason: string): Promise<void>;
  setPopulation(userId: string, planetId: number, amount: number, reason: string): Promise<void>;
  technologies(userId: string, planetId: number): Promise<TechnologyView>;
  setTechnology(userId: string, planetId: number, technologyId: number, level: number, reason: string): Promise<void>;
  cancelResearch(userId: string, queueItemId: number, reason: string): Promise<void>;
  parts(userId: string, planetId: number): Promise<ProductionView>;
  setPartQuantity(userId: string, planetId: number, stackId: number, quantity: number, reason: string): Promise<void>;
  grantPart(userId: string, planetId: number, grant: PartGrant): Promise<void>;
  production(userId: string, planetId: number): Promise<ProductionView>;
  cancelProduction(userId: string, planetId: number, orderId: number, reason: string): Promise<void>;
  processes(userId: string, planetId: number): Promise<ProcessOrderRow[]>;
  cancelProcess(userId: string, planetId: number, orderId: number, reason: string): Promise<void>;
  designs(userId: string): Promise<ShipDesign[]>;
  ships(userId: string): Promise<PlayerShipQuantity[]>;
  setShipQuantity(userId: string, designId: number, quantity: number, reason: string): Promise<void>;
  renameDesign(userId: string, designId: number, name: string, reason: string): Promise<void>;
  archiveDesign(userId: string, designId: number, reason: string): Promise<void>;
  market(userId: string, offset: number): Promise<MarketView>;
  cancelLot(userId: string, lotId: number, reason: string): Promise<void>;
}

/**
 * Builds the player endpoints.
 */
export function createPlayerAdminApi(http: HttpClient): PlayerAdminApi {
  const player = (userId: string) => `${adminPrefix}/players/${encodeURIComponent(userId)}`;
  const colonyPath = (userId: string, planetId: number) =>
    `${adminPrefix}/planets/players/${encodeURIComponent(userId)}/colonies/${planetId}`;
  const researchPlanet = (userId: string, planetId: number) =>
    `${adminPrefix}/research/players/${encodeURIComponent(userId)}/planets/${planetId}`;
  const productionPlanet = (userId: string, planetId: number) =>
    `${adminPrefix}/production/players/${encodeURIComponent(userId)}/planets/${planetId}`;
  const processPlanet = (userId: string, planetId: number) =>
    `${adminPrefix}/processes/players/${encodeURIComponent(userId)}/planets/${planetId}`;

  return {
    list(page, search) {
      const params = new URLSearchParams({ page: String(page) });

      if (search) {
        params.set('search', search);
      }

      return http.get<Page<PlayerRow>>(`${adminPrefix}/players?${params.toString()}`);
    },
    card(userId) {
      return http.get<PlayerCard>(player(userId));
    },
    async changeEmail(userId, email) {
      await http.put(player(userId) + '/email', { email });
    },
    balances(userId) {
      return http.get<BalanceRow[]>(`${adminPrefix}/economy/balances/${encodeURIComponent(userId)}`);
    },
    async setBalance(userId, resourceTypeId, amount, reason, version) {
      await http.put(`${adminPrefix}/economy/players/${encodeURIComponent(userId)}/balances`, {
        resource_type_id: resourceTypeId,
        amount,
        reason,
        version,
      });
    },
    colony(userId, planetId) {
      return http.get<ColonyView>(colonyPath(userId, planetId));
    },
    async setStock(userId, planetId, resourceTypeId, amount, reason) {
      await http.put(`${colonyPath(userId, planetId)}/stock`, { resource_type_id: resourceTypeId, amount, reason });
    },
    async setBuilding(userId, planetId, buildingTypeId, level, reason) {
      await http.put(`${colonyPath(userId, planetId)}/buildings`, { building_type_id: buildingTypeId, level, reason });
    },
    async setSlots(userId, planetId, extraQueueSlots, reason) {
      await http.put(`${colonyPath(userId, planetId)}/queue-slots`, { extra_queue_slots: extraQueueSlots, reason });
    },
    async cancelConstruction(userId, planetId, queueItemId, reason) {
      await http.post(`${colonyPath(userId, planetId)}/construction/${queueItemId}/cancel`, { reason });
    },
    async setPopulation(userId, planetId, amount, reason) {
      await http.put(`${adminPrefix}/economy/players/${encodeURIComponent(userId)}/planets/${planetId}/population`, { amount, reason });
    },
    technologies(userId, planetId) {
      return http.get<TechnologyView>(researchPlanet(userId, planetId));
    },
    async setTechnology(userId, planetId, technologyId, level, reason) {
      await http.put(`${researchPlanet(userId, planetId)}/technologies`, { technology_id: technologyId, level, reason });
    },
    async cancelResearch(userId, queueItemId, reason) {
      await http.post(`${adminPrefix}/research/players/${encodeURIComponent(userId)}/queue/${queueItemId}/cancel`, { reason });
    },
    parts(userId, planetId) {
      return http.get<ProductionView>(`${productionPlanet(userId, planetId)}/parts`);
    },
    async setPartQuantity(userId, planetId, stackId, quantity, reason) {
      await http.put(`${productionPlanet(userId, planetId)}/parts/${stackId}`, { quantity, reason });
    },
    async grantPart(userId, planetId, grant) {
      await http.post(`${productionPlanet(userId, planetId)}/parts`, grant);
    },
    production(userId, planetId) {
      return http.get<ProductionView>(`${productionPlanet(userId, planetId)}/orders`);
    },
    async cancelProduction(userId, planetId, orderId, reason) {
      await http.post(`${productionPlanet(userId, planetId)}/orders/${orderId}/cancel`, { reason });
    },
    processes(userId, planetId) {
      return http.get<ProcessOrderRow[]>(`${processPlanet(userId, planetId)}/orders`);
    },
    async cancelProcess(userId, planetId, orderId, reason) {
      await http.post(`${processPlanet(userId, planetId)}/orders/${orderId}/cancel`, { reason });
    },
    designs(userId) {
      return http.get<ShipDesign[]>(`${adminPrefix}/fleet/players/${encodeURIComponent(userId)}/designs`);
    },
    ships(userId) {
      return http.get<PlayerShipQuantity[]>(`${adminPrefix}/fleet/players/${encodeURIComponent(userId)}/ships`);
    },
    async setShipQuantity(userId, designId, quantity, reason) {
      await http.put(`${adminPrefix}/fleet/players/${encodeURIComponent(userId)}/ships/${designId}`, { quantity, reason });
    },
    async renameDesign(userId, designId, name, reason) {
      await http.put(`${adminPrefix}/fleet/players/${encodeURIComponent(userId)}/designs/${designId}`, { name, reason });
    },
    async archiveDesign(userId, designId, reason) {
      await http.post(`${adminPrefix}/fleet/players/${encodeURIComponent(userId)}/designs/${designId}/archive`, { reason });
    },
    market(userId, offset) {
      return http.get<MarketView>(`${adminPrefix}/market/players/${encodeURIComponent(userId)}?offset=${offset}`);
    },
    async cancelLot(userId, lotId, reason) {
      await http.post(`${adminPrefix}/market/players/${encodeURIComponent(userId)}/lots/${lotId}/cancel`, { reason });
    },
  };
}
