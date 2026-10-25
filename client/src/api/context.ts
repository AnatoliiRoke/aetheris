import { createContext } from 'react';
import type { AuthApi } from './authApi';
import type { ContentApi } from './contentApi';

/**
 * The server interfaces the pages use. Tests pass their own implementations.
 */
export interface Api {
  auth: AuthApi;
  content: ContentApi;
}

export const ApiContext = createContext<Api | null>(null);
