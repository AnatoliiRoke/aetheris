import { useCallback, useContext, useMemo, useSyncExternalStore } from 'react';
import type { Reference } from '../api/worldApi';
import { ShellContext } from '../pages/player/shellContext';
import type { GameIconName } from './GameIcon';

/**
 * The look of the icon of a resource: the drawing and its colour classes.
 */
export interface ResourceIconLook {
  name: GameIconName;
  className: string;
}

/** The look of a resource whose group is unknown or names no icon. */
export const neutralResourceIcon: ResourceIconLook = { name: 'shapeCoin', className: 'game-icon' };

/** The drawings of the shapes a resource group may name; the shapes are the design, not game data. */
const shapeIcons: Record<string, GameIconName> = {
  square: 'shapeSquare',
  gem: 'shapeGem',
  drop: 'shapeDrop',
  spark: 'shapeSpark',
  diamond: 'shapeDiamond',
  hexagon: 'shapeHexagon',
  coin: 'shapeCoin',
};

/** The colour classes of the tones a resource group may name. */
const toneClasses: Record<string, string> = {
  metal: 'game-icon game-icon--metal game-icon--fill',
  gem: 'game-icon game-icon--gem game-icon--fill',
  flame: 'game-icon game-icon--flame game-icon--fill',
  made: 'game-icon game-icon--made',
  credits: 'game-icon game-icon--credits',
};

/**
 * The look of the icon of a resource group. The shape and the tone come from the reference table
 * of the server, so the client knows no group by its code and a new group needs no change of the
 * client (code audit of 04.11.2026).
 *
 * @param group The group, or `null` when the resource has none.
 * @returns The look of the icon.
 */
export function resourceIconLook(group: Pick<Reference, 'iconShape' | 'iconTone'> | null | undefined): ResourceIconLook {
  if (!group) {
    return neutralResourceIcon;
  }

  return {
    name: (group.iconShape && shapeIcons[group.iconShape]) || neutralResourceIcon.name,
    className: (group.iconTone && toneClasses[group.iconTone]) || neutralResourceIcon.className,
  };
}

/** Subscribes to nothing: the frame is absent, so the groups never change. */
const noSubscription = () => () => {};

/** No groups: the frame is absent. */
const noGroups = (): Reference[] | undefined => undefined;

/**
 * The looks of the icons of the resource groups, read from the reference tables the frame shares.
 * Outside the frame every icon is neutral.
 *
 * @returns The look by the code of a group and by its identifier.
 */
export function useResourceIcons(): { byCode: (groupCode: string | null | undefined) => ResourceIconLook; byId: (groupId: number | null | undefined) => ResourceIconLook } {
  const store = useContext(ShellContext);
  const subscribe = store ? store.subscribe : noSubscription;
  const getGroups = useCallback(() => store?.getState().reference?.resourceGroups, [store]);
  const groups = useSyncExternalStore(subscribe, store ? getGroups : noGroups, store ? getGroups : noGroups);

  return useMemo(() => {
    const byCode = new Map((groups ?? []).map((group) => [group.code, group]));
    const byId = new Map((groups ?? []).map((group) => [group.id, group]));

    return {
      byCode: (groupCode: string | null | undefined) => resourceIconLook(groupCode ? byCode.get(groupCode) : null),
      byId: (groupId: number | null | undefined) => resourceIconLook(groupId === null || groupId === undefined ? null : byId.get(groupId)),
    };
  }, [groups]);
}
