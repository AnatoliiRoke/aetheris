import type { HttpClient } from './http';
import { camelize } from './planetApi';

/**
 * The population of a body at a moment of game time (task 00015).
 */
export interface PopulationFacts {
  planetId: number;
  amount: number;
  maximum: number;
  /** How many inhabitants the population grows with in an hour; it is negative when it dies down. */
  growthPerHour: number;
  /** Whether the population is above the largest population the body may have. */
  overLimit: boolean;
  /**
   * The time to the largest population as the server sends it: `[-][d.]hh:mm:ss`. It is
   * missing when the population will never reach the largest one.
   */
  timeToMaximum: string | null;
}

/**
 * The balance of a currency of a civilization at a moment of game time (task 00015).
 */
export interface CurrencyBalanceFacts {
  playerId: string;
  /** The currency: a resource of the scope of the civilization. */
  resourceTypeId: number;
  amount: number;
  /** How much the balance grows with in an hour. */
  incomePerHour: number;
}

/**
 * The economy of a player: the populations of the bodies and the balances of the currencies.
 */
export interface EconomyOverview {
  populations: PopulationFacts[];
  balances: CurrencyBalanceFacts[];
  gameNow: string;
}

/**
 * The endpoints of the economy module: the overview of the economy of a player and the
 * population of one body. The server checks every rule; the client only shows them.
 */
export interface EconomyApi {
  overview(): Promise<EconomyOverview>;
  population(planetId: number): Promise<PopulationFacts>;
}

const prefix = '/api/v1/economy';

/**
 * The endpoints of the economy module over HTTP.
 */
export function createEconomyApi(http: HttpClient): EconomyApi {
  return {
    async overview() {
      return camelize<EconomyOverview>(await http.get<unknown>(`${prefix}/`));
    },
    async population(planetId) {
      return camelize<PopulationFacts>(await http.get<unknown>(`${prefix}/population?planet_id=${planetId}`));
    },
  };
}
