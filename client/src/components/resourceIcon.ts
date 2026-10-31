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

/**
 * The colour class of a resource icon. The group comes from the reference table, so one
 * group has one colour of the design. A manufactured resource has no colour of its own in
 * the design and keeps the soft text colour. A balance uses the credits colour: the header
 * draws credits, trade and energy apart, and this icon cannot tell them apart.
 *
 * @param groupCode The code of the group of the resource, or `null` when it has none.
 * @returns The classes of the icon.
 */
export function resourceIconClass(groupCode: string | null | undefined): string {
  switch (groupCode) {
    case 'resource_group_1':
      return 'game-icon game-icon--metal game-icon--fill';
    case 'resource_group_2':
    case 'resource_group_4':
      return 'game-icon game-icon--gem game-icon--fill';
    case 'resource_group_3':
      return 'game-icon game-icon--flame game-icon--fill';
    case 'resource_group_5':
      return 'game-icon game-icon--made';
    case 'resource_group_6':
      return 'game-icon game-icon--credits';
    default:
      return 'game-icon';
  }
}
