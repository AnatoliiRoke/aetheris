import { useEffect, useState } from 'react';
import type { WorldReference } from './adminApi';
import { useAdmin } from './adminContext';

/**
 * The reference tables of the world: resources, object types and size classes with their
 * localisation keys, for the filters and the cards.
 */
export function useReference(): WorldReference | null {
  const { api, onError } = useAdmin();
  const [reference, setReference] = useState<WorldReference | null>(null);

  useEffect(() => {
    let active = true;

    api.reference().then(
      (answer) => {
        if (active) {
          setReference(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [api, onError]);

  return reference;
}
