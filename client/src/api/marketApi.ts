import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * The scopes of the levels of the market: who sees the lots of a level. They are the codes the
 * server sends, so the client names a level by its scope and never by a row of the table.
 */
export const marketVisibilities = { clan: 'clan', sector: 'sector', galactic: 'galactic' } as const;

/**
 * A level of the market (task 00024).
 */
export interface MarketLevel {
  id: number;
  visibility: string;
  commissionPercent: number;
  isActive: boolean;
  sortOrder: number;
}

/**
 * How many lots a civilization has on the market and how many its trade centres allow.
 */
export interface LotLimit {
  used: number;
  limit: number;
}

/**
 * Whether a body of the player has what the market asks for.
 */
export interface MarketPlanet {
  planetId: number;
  hasTradeCentre: boolean;
  hasPortal: boolean;
}

/**
 * What the screen of the market needs to start: the levels, the currencies and the limit of the lots.
 */
export interface MarketOverview {
  levels: MarketLevel[];
  creditsResourceTypeId: number;
  tradeResourceTypeId: number;
  /** The resource of the delivery fee, or `null` when the data has none. */
  energyResourceTypeId: number | null;
  lotLimit: LotLimit;
  /** The bodies of the player with the buildings of the market on them. */
  planets: MarketPlanet[];
  /** The moment of game time of the answer. */
  gameNow: string;
  /** How many game seconds pass in one real second. */
  timeMultiplier: number;
}

/**
 * A player as the other players see them.
 */
export interface PlayerCard {
  playerId: string;
  nickname: string;
  /** The localisation key of the name of the race, or `null` when the player has no civilization. */
  raceNameKey: string | null;
  /** The colour key of the race, which names the glyph, or `null`. */
  raceColorKey: string | null;
}

/**
 * A lot in the list of the lots a player may buy.
 */
export interface MarketLot {
  id: number;
  seller: PlayerCard;
  sellerPlanetId: number;
  marketLevelId: number;
  resourceTypeId: number | null;
  partTypeId: number | null;
  quantity: number;
  pricePerUnit: number;
  totalPrice: number;
  createdAt: string;
  partConditionPercent: number | null;
  /** The delivery time to the chosen body in seconds, or `null` when no body was chosen. */
  deliverySeconds: number | null;
  /** The delivery fee in energy rods for the chosen body, or `null` when no body was chosen. */
  energyRodAmount: number | null;
  /** The code of the reason the player cannot buy the lot now, or `null`. */
  unavailableReason: string | null;
}

/**
 * The lots a player may buy and what the player needs to read them.
 */
export interface MarketLots {
  items: MarketLot[];
  /** How many lots match, all the pages together. */
  total: number;
  offset: number;
  /** How many lots a page may hold. */
  limit: number;
  /** The energy rods on the chosen body, or `null` when no body was chosen. */
  energyRodBalance: number | null;
  /** Whether the chosen body has a portal, or `null` when no body was chosen. */
  destinationHasPortal: boolean | null;
}

/**
 * The orders the lots may be listed in; the server sorts, so the order holds over every page.
 */
export type LotSort = 'created' | 'price' | 'total' | 'quantity' | 'item' | 'time' | 'rods';

/**
 * What a player looks for on the market, in what order and which page. A body of the player makes
 * the answer carry the cost of the delivery to it.
 */
export interface LotsQuery {
  marketLevelId?: number;
  /** The resources to show; without both lists every item is shown. */
  resourceTypeIds?: number[];
  /** The parts to show. */
  partTypeIds?: number[];
  minPrice?: number;
  maxPrice?: number;
  maxSum?: number;
  offset?: number;
  limit?: number;
  planetId?: number;
  sort?: LotSort;
  descending?: boolean;
  /** The items in the order of their names (`r12`, `p3`), for the sort by item. */
  itemOrder?: string[];
}

/**
 * The lots of one item: how many there are, the lowest price of a unit and the whole quantity.
 */
export interface LotGroup {
  resourceTypeId: number | null;
  partTypeId: number | null;
  lotCount: number;
  minPricePerUnit: number;
  totalQuantity: number;
}

/**
 * A lot of the player: what the player put up and for how much.
 */
export interface MyLot {
  id: number;
  sellerPlanetId: number;
  marketLevelId: number;
  resourceTypeId: number | null;
  partTypeId: number | null;
  quantity: number;
  pricePerUnit: number;
  totalPrice: number;
  state: string;
  createdAt: string;
  partConditionPercent: number | null;
}

/**
 * A delivery of the market: the goods that travel through a portal.
 */
export interface MarketDelivery {
  id: number;
  counterparty: PlayerCard;
  sourcePlanetId: number;
  destinationPlanetId: number;
  resourceTypeId: number | null;
  partTypeId: number | null;
  quantity: number;
  energyRodAmount: number;
  startedAt: string;
  arrivesAt: string;
  state: string;
}

/**
 * The deliveries of a player.
 */
export interface MarketDeliveries {
  incoming: MarketDelivery[];
  outgoing: MarketDelivery[];
}

/**
 * A deal in the history of a player.
 */
export interface MarketDeal {
  id: number;
  /** `bought` or `sold`. */
  direction: string;
  counterparty: PlayerCard;
  resourceTypeId: number | null;
  partTypeId: number | null;
  quantity: number;
  pricePerUnit: number;
  sum: number;
  commission: number;
  createdAt: string;
}

/**
 * What a purchase costs, counted by the server without doing it.
 */
export interface MarketQuote {
  sum: number;
  commission: number;
  energyResourceTypeId: number;
  energyRodAmount: number;
  deliverySeconds: number;
}

/**
 * The rate of the exchange now: credits for one unit of the trade currency.
 */
export interface MarketRate {
  rate: number;
  commissionPercent: number;
  creditsResourceTypeId: number;
  tradeResourceTypeId: number;
}

/**
 * The rate at the end of a day.
 */
export interface RatePoint {
  day: string;
  rate: number;
}

/**
 * The history of the rate for the chart.
 */
export interface RateHistory {
  points: RatePoint[];
  /** How the rate changed in a day, in per cent, or `null` when there is nothing to compare. */
  dayChangePercent: number | null;
}

/**
 * The lowest and the highest price of one unit of an item.
 */
export interface PriceLimit {
  min: number;
  max: number;
}

/**
 * An exchange of the credits and the trade currency.
 */
export interface MarketExchange {
  id: number;
  fromResourceTypeId: number;
  toResourceTypeId: number;
  fromAmount: number;
  toAmount: number;
  rate: number;
  commissionPercent: number;
  createdAt: string;
}

/**
 * What a seller offers.
 */
export interface ListLotRequest {
  planetId: number;
  marketLevelId: number;
  quantity: number;
  pricePerUnit: number;
  resourceTypeId?: number;
  partTypeId?: number;
  partConditionPercent?: number;
}

/**
 * The endpoints of the market module: the reads of the screen and the commands of the lots and
 * of the exchange. The server checks every rule; the client only shows them.
 */
export interface MarketApi {
  overview(): Promise<MarketOverview>;
  lots(query: LotsQuery): Promise<MarketLots>;
  lotGroups(query: LotsQuery): Promise<LotGroup[]>;
  myLots(): Promise<MyLot[]>;
  deliveries(): Promise<MarketDeliveries>;
  deals(offset?: number): Promise<MarketDeal[]>;
  rate(): Promise<MarketRate>;
  rateHistory(): Promise<RateHistory>;
  quote(lotId: number, planetId: number): Promise<MarketQuote>;
  priceLimit(resourceTypeId: number | null, partTypeId: number | null): Promise<PriceLimit>;
  listLot(request: ListLotRequest): Promise<MyLot[]>;
  cancelLot(lotId: number): Promise<MyLot>;
  buy(lotId: number, destinationPlanetId: number): Promise<MarketDeal>;
  exchange(giveResourceTypeId: number, amount: number): Promise<MarketExchange>;
}

const prefix = '/api/v1/market';

/**
 * Builds the query string of a read from the values that are set; a list is written with commas.
 */
function queryString(values: Record<string, number | string | readonly number[] | readonly string[] | undefined>): string {
  const parts = Object.entries(values)
    .filter((entry): entry is [string, number | string | readonly number[] | readonly string[]] => entry[1] !== undefined)
    .map(([key, value]) => `${key}=${encodeURIComponent(Array.isArray(value) ? value.join(',') : String(value))}`);

  return parts.length === 0 ? '' : `?${parts.join('&')}`;
}

/**
 * The query of the lots as the server reads it.
 */
function lotsParameters(query: LotsQuery): Record<string, number | string | readonly number[] | readonly string[] | undefined> {
  return {
    market_level_id: query.marketLevelId,
    resource_type_ids: query.resourceTypeIds,
    part_type_ids: query.partTypeIds,
    min_price: query.minPrice,
    max_price: query.maxPrice,
    max_sum: query.maxSum,
    offset: query.offset,
    limit: query.limit,
    planet_id: query.planetId,
    sort: query.sort,
    direction: query.descending === undefined ? undefined : query.descending ? 'desc' : 'asc',
    item_order: query.itemOrder,
  };
}

/**
 * The endpoints of the market module over HTTP.
 */
export function createMarketApi(http: HttpClient): MarketApi {
  return {
    async overview() {
      return camelize<MarketOverview>(await http.get<unknown>(`${prefix}/overview`));
    },
    async lots(query) {
      return camelize<MarketLots>(await http.get<unknown>(`${prefix}/lots${queryString(lotsParameters(query))}`));
    },
    async lotGroups(query) {
      return camelize<LotGroup[]>(await http.get<unknown>(`${prefix}/lot-groups${queryString({ ...lotsParameters(query), sort: undefined, direction: undefined, item_order: undefined, offset: undefined, limit: undefined })}`));
    },
    async myLots() {
      return camelize<MyLot[]>(await http.get<unknown>(`${prefix}/my-lots`));
    },
    async deliveries() {
      return camelize<MarketDeliveries>(await http.get<unknown>(`${prefix}/deliveries`));
    },
    async deals(offset) {
      return camelize<MarketDeal[]>(await http.get<unknown>(`${prefix}/deals${queryString({ offset })}`));
    },
    async rate() {
      return camelize<MarketRate>(await http.get<unknown>(`${prefix}/rate`));
    },
    async rateHistory() {
      return camelize<RateHistory>(await http.get<unknown>(`${prefix}/rate-history`));
    },
    async quote(lotId, planetId) {
      return camelize<MarketQuote>(await http.get<unknown>(`${prefix}/quote?lot_id=${lotId}&planet_id=${planetId}`));
    },
    async priceLimit(resourceTypeId, partTypeId) {
      return camelize<PriceLimit>(
        await http.get<unknown>(
          `${prefix}/price-limit${queryString({ resource_type_id: resourceTypeId ?? undefined, part_type_id: partTypeId ?? undefined })}`,
        ),
      );
    },
    async listLot(request) {
      return camelize<MyLot[]>(
        await http.command<unknown>(`${prefix}/orders`, {
          planet_id: request.planetId,
          market_level_id: request.marketLevelId,
          quantity: request.quantity,
          price_per_unit: request.pricePerUnit,
          resource_type_id: request.resourceTypeId,
          part_type_id: request.partTypeId,
          part_condition_percent: request.partConditionPercent,
        }),
      );
    },
    async cancelLot(lotId) {
      return camelize<MyLot>(await http.command<unknown>(`${prefix}/orders/cancel`, { lot_id: lotId }));
    },
    async buy(lotId, destinationPlanetId) {
      return camelize<MarketDeal>(
        await http.command<unknown>(`${prefix}/orders/buy`, { lot_id: lotId, destination_planet_id: destinationPlanetId }),
      );
    },
    async exchange(giveResourceTypeId, amount) {
      return camelize<MarketExchange>(
        await http.command<unknown>('/api/v1/exchange', { give_resource_type_id: giveResourceTypeId, amount }),
      );
    },
  };
}
