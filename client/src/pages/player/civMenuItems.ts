import type { GameIconName } from '../../components/GameIcon';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';

/**
 * A section of the menu of the game.
 */
export interface MenuItem {
  id: string;
  icon: GameIconName;
  label: MessageKey;
  /** The screen the section opens; a section without one has no screen yet and stays inactive. */
  route?: string;
  /** Every address that belongs to the section, so its item is the current one on all of them. */
  belongsTo?: string[];
  /** A line is drawn above the item, as in the design. */
  separated?: boolean;
}

/**
 * The sections of the menu of the game in the order of the design
 * (`documentation/design/AETHERIS_4_pages`, `documentation/design/aetheris-civ`) with the
 * sections of the storage and of the construction that the task 00020 adds (the design has no
 * item of the construction, but without it a player cannot reach the screen that builds a mine or a city). The sections that no task has made yet have
 * no screen: they stay in the menu, inactive, with the reason in the hint (task 00010, section 2.7).
 *
 * The menu and the tabs of the phone are made from this one list, so they never differ.
 */
export const menuItems: readonly MenuItem[] = [
  { id: 'overview', icon: 'overview', label: 'civ.nav.overview', route: routes.game },
  { id: 'planet', icon: 'planet', label: 'civ.nav.planet', route: routes.planet },
  { id: 'build', icon: 'house', label: 'civ.nav.build', route: routes.planetBuild },
  { id: 'storage', icon: 'box', label: 'civ.nav.storage', route: routes.planetStorage },
  { id: 'production', icon: 'production', label: 'civ.nav.production' },
  { id: 'shipyard', icon: 'shipyard', label: 'civ.nav.shipyard' },
  { id: 'fleets', icon: 'fleets', label: 'civ.nav.fleets' },
  { id: 'map', icon: 'map', label: 'civ.nav.map' },
  { id: 'research', icon: 'research', label: 'civ.nav.research', route: routes.planetResearch },
  { id: 'market', icon: 'market', label: 'civ.nav.market' },
  { id: 'ranking', icon: 'ranking', label: 'civ.nav.ranking' },
  { id: 'battle', icon: 'battle', label: 'civ.nav.battle' },
  { id: 'clan', icon: 'clan', label: 'civ.nav.clan' },
  { id: 'mail', icon: 'mail', label: 'civ.nav.mail', separated: true },
  { id: 'help', icon: 'help', label: 'civ.nav.help' },
  { id: 'settings', icon: 'settings', label: 'civ.nav.settings', route: routes.account },
];

/**
 * The tabs of the phone: the sections of the design of the phone (the construction takes the place
 * of the shipyard, which has no screen yet) and the button that opens the rest of the menu.
 */
export const tabItemIds: readonly string[] = ['overview', 'planet', 'build', 'map'];

/**
 * The section of the menu an address belongs to, or `null` when no section does.
 *
 * @param path The address of the screen.
 */
export function activeItemId(path: string): string | null {
  const normalized = path.length > 1 ? path.replace(/\/+$/, '') : path;

  return menuItems.find((item) => (item.belongsTo ?? (item.route ? [item.route] : [])).includes(normalized))?.id ?? null;
}
