import { createContext } from 'react';
import type { AuthApi } from './authApi';
import type { ContentApi } from './contentApi';
import type { EconomyApi } from './economyApi';
import type { FleetApi } from './fleetApi';
import type { GameApi } from './gameApi';
import type { MailApi } from './mailApi';
import type { MapApi } from './mapApi';
import type { MarketApi } from './marketApi';
import type { PlanetApi } from './planetApi';
import type { ProcessesApi } from './processesApi';
import type { ProductionApi } from './productionApi';
import type { ResearchApi } from './researchApi';
import type { TextsApi } from './textsApi';
import type { WorldApi } from './worldApi';

/**
 * The server interfaces the pages use. Tests pass their own implementations.
 */
export interface Api {
  auth: AuthApi;
  content: ContentApi;
  economy: EconomyApi;
  fleet: FleetApi;
  game: GameApi;
  mail: MailApi;
  map: MapApi;
  market: MarketApi;
  planets: PlanetApi;
  processes: ProcessesApi;
  production: ProductionApi;
  research: ResearchApi;
  texts: TextsApi;
  world: WorldApi;
}

export const ApiContext = createContext<Api | null>(null);
