import { useMemo } from 'react';
import type { EntityText } from '../../texts/context';

/**
 * The names of the resources of a body by the reference table of the world. The name comes
 * from the texts of the game data, so the client knows no resource by its code (task 00010,
 * section 7.3).
 *
 * @param reference The reference tables of the world.
 * @param entities The texts of the game data.
 * @returns A function that names a resource by its identifier.
 */
export function useResourceNames(reference: { resources: { id: number; nameKey: string }[] } | null, entities: EntityText) {
  return useMemo(() => {
    const byId = new Map((reference?.resources ?? []).map((resource) => [resource.id, resource]));

    return (id: number): string => {
      const key = byId.get(id)?.nameKey;

      return key ? entities(key) ?? key : String(id);
    };
  }, [reference, entities]);
}
