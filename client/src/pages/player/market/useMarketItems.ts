import { useCallback, useMemo } from 'react';
import type { GameIconName } from '../../../components/GameIcon';
import { useResourceIcons } from '../../../components/resourceIcon';
import { useApi } from '../../../api/useApi';
import { useTexts } from '../../../texts/useTexts';
import { useWorldReference } from '../useWorldReference';
import { useRead } from './useRead';

/**
 * How an item of the market looks: its name from the texts of the game data and its icon from the
 * group of the resource, so the client knows no item by its code.
 */
export interface ItemView {
  name: string;
  iconName: GameIconName;
  iconClass: string;
}

/**
 * An item the market may list, with the name the player reads.
 */
export interface CatalogItem {
  /** `r` and the resource, or `p` and the part: the key the server orders the items by. */
  key: string;
  resourceTypeId: number | null;
  partTypeId: number | null;
  name: string;
}

/**
 * What the screens of the market need to show items.
 */
export interface MarketItems {
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  catalog: CatalogItem[];
}

/**
 * The look of the items of a lot, of a delivery or of a deal. The names and the groups come from the
 * reference tables of the server, so a new resource or part shows up without a change of the client.
 *
 * @returns The function that gives the look of a resource or of a part, and the catalog of every item
 * the market may list, named and in the order of the names.
 */
export function useMarketItems(): MarketItems {
  const { production } = useApi();
  const { text } = useTexts();
  const reference = useWorldReference();
  const icons = useResourceIcons();
  const loadParts = useCallback(() => production.characteristicParts(), [production]);
  const parts = useRead(loadParts);

  const resources = useMemo(() => new Map((reference?.resources ?? []).map((resource) => [resource.id, resource])), [reference]);
  const partNames = useMemo(() => new Map((parts.data ?? []).map((part) => [part.partTypeId, part.nameKey])), [parts.data]);

  const itemView = useCallback(
    (resourceTypeId: number | null, partTypeId: number | null): ItemView => {
      if (resourceTypeId !== null) {
        const resource = resources.get(resourceTypeId);

        return {
          name: resource ? text(resource.nameKey) ?? resource.nameKey : String(resourceTypeId),
          iconName: icons.byId(resource?.groupId).name,
          iconClass: icons.byId(resource?.groupId).className,
        };
      }

      const key = partTypeId === null ? undefined : partNames.get(partTypeId);

      return { name: key ? text(key) ?? key : String(partTypeId), iconName: 'shapeShard', iconClass: 'game-icon game-icon--made' };
    },
    [resources, partNames, text, icons],
  );

  const catalog = useMemo<CatalogItem[]>(
    () =>
      [
        ...(reference?.resources ?? []).map((resource) => ({ key: `r${resource.id}`, resourceTypeId: resource.id, partTypeId: null, name: text(resource.nameKey) ?? resource.nameKey })),
        ...(parts.data ?? []).map((part) => ({ key: `p${part.partTypeId}`, resourceTypeId: null, partTypeId: part.partTypeId, name: text(part.nameKey) ?? part.nameKey })),
      ].sort((a, b) => a.name.localeCompare(b.name)),
    [reference, parts.data, text],
  );

  return { itemView, catalog };
}
