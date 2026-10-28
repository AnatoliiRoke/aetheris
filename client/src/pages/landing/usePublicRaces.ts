import { useCallback } from 'react';
import type { Race } from '../../api/gameApi';
import { useApi } from '../../api/useApi';
import { useAsync, type AsyncState } from './useAsync';

/**
 * The races of the game from the server, for the landing page (task 00022): their number, names,
 * pictures and colours are data.
 */
export function usePublicRaces(): AsyncState<Race[]> {
  const { game } = useApi();
  const load = useCallback(() => game.publicRaces(), [game]);

  return useAsync(load);
}
