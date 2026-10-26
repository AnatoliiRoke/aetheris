import { createContext } from 'react';
import type { AuthApi } from './authApi';
import type { ContentApi } from './contentApi';
import type { GameApi } from './gameApi';

/**
 * The server interfaces the pages use. Tests pass their own implementations.
 */
export interface Api {
  auth: AuthApi;
  content: ContentApi;
  game: GameApi;
}

export const ApiContext = createContext<Api | null>(null);
