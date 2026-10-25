import { useContext } from 'react';
import { ApiContext, type Api } from './context';

/**
 * The server interfaces.
 */
export function useApi(): Api {
  const api = useContext(ApiContext);

  if (!api) {
    throw new Error('useApi must be used inside ApiContext.Provider.');
  }

  return api;
}
