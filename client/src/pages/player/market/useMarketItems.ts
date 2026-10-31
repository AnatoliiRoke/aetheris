import { useCallback, useMemo } from 'react';
import type { GameIconName } from '../../../components/GameIcon';
import { resourceIconClass, resourceIconName } from '../../../components/resourceIcon';
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
 * The look of the items of a lot, of a delivery or of a deal. The names and the groups come from the
 * reference tables of the server, so a new resource or part shows up without a change of the client.
 *
 * @returns A function that gives the look of a resource or of a part.
 */
export function useMarketItems(): (resourceTypeId: number | null, partTypeId: number | null) => ItemView {
  const { production } = useApi();
  const { text } = useTexts();
  const reference = useWorldReference();
  const loadParts = useCallback(() => production.characteristicParts(), [production]);
  const parts = useRead(loadParts);

  const resources = useMemo(() => new Map((reference?.resources ?? []).map((resource) => [resource.id, resource])), [reference]);
  const partNames = useMemo(() => new Map((parts.data ?? []).map((part) => [part.partTypeId, part.nameKey])), [parts.data]);

  return useCallback(
    (resourceTypeId, partTypeId) => {
      if (resourceTypeId !== null) {
        const resource = resources.get(resourceTypeId);

        return {
          name: resource ? text(resource.nameKey) ?? resource.nameKey : String(resourceTypeId),
          iconName: resourceIconName(resource?.groupCode),
          iconClass: resourceIconClass(resource?.groupCode),
        };
      }

      const key = partTypeId === null ? undefined : partNames.get(partTypeId);

      return { name: key ? text(key) ?? key : String(partTypeId), iconName: 'shapeShard', iconClass: 'game-icon game-icon--made' };
    },
    [resources, partNames, text],
  );
}
