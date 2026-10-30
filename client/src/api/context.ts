import { createContext } from 'react';
import type { AuthApi } from './authApi';
import type { ContentApi } from './contentApi';
import type { EconomyApi } from './economyApi';
import type { GameApi } from './gameApi';
import type { PlanetApi } from './planetApi';
import type { ProcessesApi } from './processesApi';
import type { ProductionApi } from './productionApi';
import type { ResearchApi } from './researchApi';
import type { TextsApi } from './textsApi';

/**
 * The server interfaces the pages use. Tests pass their own implementations.
 */
export interface Api {
  auth: AuthApi;
  content: ContentApi;
  economy: EconomyApi;
  game: GameApi;
  planets: PlanetApi;
  processes: ProcessesApi;
  production: ProductionApi;
  research: ResearchApi;
  texts: TextsApi;
}

export const ApiContext = createContext<Api | null>(null);
