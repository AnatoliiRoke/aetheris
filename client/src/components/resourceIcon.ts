import type { GameIconName } from './GameIcon';

/**
 * The shape of the icon of a resource by the group of the resource. The group comes from the
 * reference table of the world, so the client knows no resource by its code and never maps a
 * resource to a picture on its own (task 00010, section 7.3).
 *
 * The names of the icons are the shapes of the design, not the codes of the resources, so the
 * client names no game entity by an old code (task 00022).
 *
 * @param groupCode The code of the group of the resource, or `null` when it has none.
 * @returns The name of the icon.
 */
export function resourceIconName(groupCode: string | null | undefined): GameIconName {
  switch (groupCode) {
    case 'resource_group_1':
      return 'shapeSquare';
    case 'resource_group_2':
      return 'shapeGem';
    case 'resource_group_3':
      return 'shapeDrop';
    case 'resource_group_4':
      return 'shapeSpark';
    case 'resource_group_5':
      return 'shapeDiamond';
    case 'resource_group_6':
      return 'shapeHexagon';
    default:
      return 'shapeCoin';
  }
}
