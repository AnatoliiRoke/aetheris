import { createContext } from 'react';
import type { AuthApi } from './authApi';
import type { ContentApi } from './contentApi';
import type { GameApi } from './gameApi';
import type { PlanetApi } from './planetApi';
import type { ProductionApi } from './productionApi';
import type { ResearchApi } from './researchApi';
import type { TextsApi } from './textsApi';

/**
 * The server interfaces the pages use. Tests pass their own implementations.
 */
export interface Api {
  auth: AuthApi;
  content: ContentApi;
  game: GameApi;
  planets: PlanetApi;
  production: ProductionApi;
  research: ResearchApi;
  texts: TextsApi;
}

export const ApiContext = createContext<Api | null>(null);
