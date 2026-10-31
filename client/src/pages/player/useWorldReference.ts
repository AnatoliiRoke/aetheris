import { useEffect } from 'react';
import type { WorldReference } from '../../api/worldApi';
import { useShell, useShellSelector } from './useShell';

/**
 * Reads the reference tables of the world from the state the frame shares: the names and the
 * pictures of the resources and the types of the bodies. The tables do not change while a screen
 * is open, so the server is asked once for all the screens.
 *
 * @returns The tables, or `null` while they have not arrived or when the read failed.
 */
export function useWorldReference(): WorldReference | null {
  const { actions } = useShell();
  const reference = useShellSelector((state) => state.reference);

  useEffect(() => {
    void actions.loadBodies();
  }, [actions]);

  return reference;
}
