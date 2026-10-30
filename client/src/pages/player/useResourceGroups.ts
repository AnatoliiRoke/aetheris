import { useMemo } from 'react';

/**
 * The codes of the groups of the resources by the reference table of the world. The group
 * decides the shape of the icon of a resource, so the client never maps a resource to a
 * picture on its own (task 00010, section 7.3).
 *
 * @param reference The reference tables of the world.
 * @returns A function that gives the code of the group of a resource by its identifier.
 */
export function useResourceGroups(reference: { resources: { id: number; groupCode: string | null }[] } | null) {
  return useMemo(() => {
    const byId = new Map((reference?.resources ?? []).map((resource) => [resource.id, resource]));

    return (id: number): string | null => byId.get(id)?.groupCode ?? null;
  }, [reference]);
}
