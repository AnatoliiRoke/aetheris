import { useEffect, useState } from 'react';
import type { WorldReference } from '../../api/worldApi';
import { useApi } from '../../api/useApi';

/**
 * Reads the reference tables of the world once: the names and the pictures of the resources and
 * the types of the bodies. The tables do not change while a screen is open.
 *
 * @returns The tables, or `null` while they have not arrived or when the read failed.
 */
export function useWorldReference(): WorldReference | null {
  const { world } = useApi();
  const [reference, setReference] = useState<WorldReference | null>(null);

  useEffect(() => {
    let cancelled = false;

    world
      .reference()
      .then((tables) => {
        if (!cancelled) {
          setReference(tables);
        }
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, [world]);

  return reference;
}
