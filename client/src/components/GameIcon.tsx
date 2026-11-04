/**
 * The line icons of the game screens: the menu items, the resources, the buildings and the
 * events (task 00010). The contours come from the design of the screen of the civilization
 * (`documentation/design/aetheris-civ/src/icons.js`).
 *
 * A menu icon inherits the colour of the text. A resource icon takes the colour of its
 * group from the design and still has a label beside it, so the colour is never the only
 * carrier of the meaning (ui-art/05, section 7).
 */

import { useResourceIcons } from './resourceIcon';

/**
 * The contours of the icons, by their name. The codes are the keys of the data, not the
 * codes of the game, so the client knows no resource or building by its code.
 */
const paths = {
  overview: 'M3 3h8v8H3z M13 3h8v5h-8z M13 10h8v11h-8z M3 13h8v8H3z',
  planet: 'M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16z M2 13c4 3 16 3 20-2',
  production: 'M3 21V10l6 4V10l6 4V6h6v15z',
  shipyard: 'M12 2l4 8v8l-4 4-4-4v-8z M8 14l-4 3v3h4 M16 14l4 3v3h-4',
  fleets: 'M2 12l8-4v8z M12 5l8-4v8z M12 19l8-4v8z',
  map: 'M3 6l6-3 6 3 6-3v15l-6 3-6-3-6 3z M9 3v15 M15 6v15',
  research: 'M9 3h6 M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3',
  market: 'M3 9l2-5h14l2 5 M3 9h18v11H3z M9 20v-6h6v6',
  ranking: 'M8 21h8 M12 17v4 M6 4h12v4a6 6 0 0 1-12 0z M6 6H3a3 3 0 0 0 3 4 M18 6h3a3 3 0 0 1-3 4',
  battle: 'M14.5 17.5L3 6V3h3l11.5 11.5 M13 19l6-6 M16 16l4 4 M19 21l2-2',
  clan: 'M12 2l8 3v6c0 5-3.5 9-8 11-4.5-2-8-6-8-11V5z',
  mail: 'M3 5h18v14H3z M3 6l9 7 9-7',
  help: 'M12 21a9 9 0 1 0 0-18a9 9 0 1 0 0 18z M9.5 9a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6V14 M12 17.5v.01',
  settings: 'M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8z M12 2v3 M12 19v3 M2 12h3 M19 12h3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M4.9 19.1L7 17 M17 7l2.1-2.1',
  more: 'M5 12h.01 M12 12h.01 M19 12h.01',
  send: 'M3 11l18-8-8 18-2-8z',
  search: 'M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M21 21l-5-5',
  signout: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4 M16 17l5-5-5-5 M21 12H9',
  star: 'M12 2l2.6 6.9L22 9.3l-5.8 4.6 2 7.1L12 17l-6.2 4 2-7.1L2 9.3l7.4-.4z',

  shapeSquare: 'M5 5h14v14H5z',
  shapeHexagon: 'M12 3l8 4.5v9L12 21l-8-4.5v-9z',
  shapeCircle: 'M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16z',
  shapeDiamond: 'M12 3l9 9-9 9-9-9z',
  shapeShard: 'M10 2h4l4 7-6 13-6-13z',
  shapeGem: 'M6 4h12l4 6-10 11L2 10z',
  shapeStar: 'M12 2l7 10-7 10-7-10z',
  shapeTriangle: 'M4 20L12 3l8 17z',
  shapeDrop: 'M12 3c4 5 6 8 6 11a6 6 0 0 1-12 0c0-3 2-6 6-11z',
  shapeFlame: 'M12 2c1 4 6 6 6 12a6 6 0 0 1-12 0c0-3 2-5 3-7 1 2 2 3 3 3-1-3 0-6 0-8z',
  shapeOrbit: 'M12 7a5 5 0 1 0 0 10a5 5 0 1 0 0-10z M12 2v3M12 19v3M2 12h3M19 12h3',
  shapeSpark: 'M12 2l2.6 6.9L22 9.3l-5.8 4.6 2 7.1L12 17l-6.2 4 2-7.1L2 9.3l7.4-.4z',
  shapeCoin: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v10 M9 9.5h4.5a1.5 1.5 0 0 1 0 3H10a1.5 1.5 0 0 0 0 3h5',
  shapeTrade: 'M12 3l8 9-8 9-8-9z M12 8l4 4-4 4-4-4z',
  shapeBolt: 'M13 2L4 14h7l-1 8 9-12h-7z',
  // The line icons of the counters of the top bar (design of the map, task 00033): credits, exchange of the currencies.
  coinCredits: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M15 9.5a3.5 3.5 0 1 0 0 5',
  exchange: 'M4 8h15l-3-3 M20 16H5l3 3',
  shapePeople: 'M12 12a4 4 0 1 0 0-8a4 4 0 1 0 0 8z M4 21a8 8 0 0 1 16 0',

  mine: 'M14 4l6 6 M4 20l9-9 M12 3c3 0 6 1 9 4-3-1-6-1-9 0',
  power: 'M13 2L4 14h7l-1 8 9-12h-7z',
  house: 'M4 21V9l8-6 8 6v12z M9 21v-6h6v6',
  box: 'M3 7l9-4 9 4v10l-9 4-9-4z M3 7l9 4 9-4 M12 11v10',
  turret: 'M4 20h16 M6 20v-5h12v5 M9 15v-3h6v3 M12 12V4l6-1',
  fiber: 'M3 9c4-4 8 4 12 0s4 0 6 0 M3 15c4-4 8 4 12 0s4 0 6 0',
  fuel: 'M4 21V9h6v12 M10 13h4l2-6h4v14H4 M7 9V5',
  lab: 'M9 3h6 M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3 M7.5 14h9',
  radar: 'M12 12l7-7 M12 3a9 9 0 1 0 9 9 M12 8a4 4 0 1 0 4 4',
  bunker: 'M3 20h18 M5 20v-6a7 7 0 0 1 14 0v6 M9 20v-4h6v4',

  done: 'M5 12l4 4 10-10',

  // The marks of the map (task 00034); the contours come from the design of the map (task 00033).
  mapGalaxy: 'M4 6l8-3 8 3v12l-8 3-8-3z M12 3v18',
  mapList: 'M8 6h12 M8 12h12 M8 18h12 M4 6h.01 M4 12h.01 M4 18h.01',
  mapSystem: 'M12 10a2 2 0 1 0 0 4a2 2 0 1 0 0-4z M12 5a7 7 0 1 0 0 14a7 7 0 1 0 0-14z',
  mapCloud: 'M7 18a4 4 0 0 1-.6-8A5.5 5.5 0 0 1 17 8.5a4.5 4.5 0 0 1 .5 9.5z',
  mapField: 'M6 8h.01 M12 6h.01 M17.5 9h.01 M8 14h.01 M14 13h.01 M18.5 16h.01 M11 18.5h.01',
  mapRoute: 'M3 12l7-4v8z M10 12h11 M14 8l4 4-4 4',
  mapLayers: 'M12 3l9 5-9 5-9-5z M3 13l9 5 9-5',
  mapChevron: 'M6 9l6 6 6-6',
  mapFlag: 'M5 21V4 M5 4h12l-2 4 2 4H5',
  mapPlus: 'M12 5v14 M5 12h14',
  mapMinus: 'M5 12h14',
  mapLeaf: 'M5 19c0-8 6-14 14-14 0 8-6 14-14 14z M5 19l7-7',
  mapUnknown: 'M8.5 8.5a3.5 3.5 0 1 1 4.9 3.2c-.8.4-1.4 1.2-1.4 2.3 M12 19v.01',
  mapStation: 'M8 21l2-10h4l2 10 M6 21h12 M12 11V7 M8 7h8 M10 3h4v4h-4z',
  mapInfo: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 11v6 M12 7.5v.01',
  mapCrosshair: 'M12 3v3 M12 18v3 M3 12h3 M18 12h3 M12 7a5 5 0 1 0 0 10a5 5 0 1 0 0-10z M12 11.5v1',
  mapClose: 'M6 6l12 12 M18 6L6 18',
  mapClock: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v5l3 2',
  mapScout: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z M12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6z',
  mapAttack: 'M12 3v4 M12 17v4 M3 12h4 M17 12h4 M12 7a5 5 0 1 0 0 10a5 5 0 1 0 0-10z M12 11v2',
  mapEscort: 'M12 3l7 3v5c0 5-3 8-7 10-4-2-7-5-7-10V6z M9 12l2 2 4-4',
  mapPlanet: 'M12 6a6 6 0 1 0 0 12a6 6 0 1 0 0-12z M3 14c2 1.5 6 2 9 1.5s7-2 9-4.5',
  mapGiant: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M4 9h16 M3.2 13h17.6 M5 17h14',
  mapTarget: 'M12 3a9 9 0 1 0 9 9 M12 7a5 5 0 1 0 5 5 M12 12l6-6 M12 12h.01',
  mapRadar: 'M14 4l6 6-3 3-6-6z M11 7l-8 8 3 3 8-8',
  mapFlagPole: 'M12 3v14 M12 3l6 3-6 3 M5 21h14 M8 17h8',
  fleet: 'M2 12l8-4v8z M10 12h12',
  warn: 'M12 3l10 18H2z M12 10v4 M12 17.5v.01',
  danger: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v3M12 14v3M7 12h3M14 12h3',
  storage: 'M3 7l9-4 9 4v10l-9 4-9-4z M3 7l9 4 9-4 M12 11v10',

  // The marks of the screen of the market (task 00026).
  lock: 'M6 11h12v9H6z M8 11V8a4 4 0 0 1 8 0v3',
  info: 'M12 21a9 9 0 1 0 0-18a9 9 0 1 0 0 18z M12 11v6 M12 7.5v.01',
  close: 'M6 6l12 12 M18 6L6 18',
  check: 'M5 12l5 5L20 7',
  chevronDown: 'M6 9l6 6 6-6',
  sortBoth: 'M7 9l5-5 5 5 M7 15l5 5 5-5',
  arrowUp: 'M12 19V5 M6 11l6-6 6 6',
  arrowDown: 'M12 5v14 M6 13l6 6 6-6',
  clock: 'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v5l3 2',
  history: 'M12 8v4l3 2 M3 12a9 9 0 1 0 3-6.7 M3 4v5h5',
} as const;

/**
 * The names of the icons the client may draw. The values are the keys of the contours.
 */
export type GameIconName = keyof typeof paths;

/**
 * A line icon of the game screens.
 *
 * @param name The name of the icon.
 * @param size The size of the icon in pixels.
 * @param className The class of the icon.
 */
export function GameIcon({ name, size = 20, className }: { name: GameIconName; size?: number; className?: string }) {
  return (
    <svg
      aria-hidden="true"
      className={className}
      focusable="false"
      height={size}
      viewBox="0 0 24 24"
      width={size}
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth={1.8}
    >
      <path d={paths[name]} />
    </svg>
  );
}

/**
 * The icon of a resource, or the neutral one when the group of the resource is unknown. The
 * group comes from the reference table of the world, so the client knows no resource by its
 * code (task 00010, section 7.3).
 *
 * @param groupCode The code of the group of the resource.
 * @param size The size of the icon in pixels.
 */
export function ResourceIcon({ groupCode, size = 14 }: { groupCode: string | null | undefined; size?: number }) {
  const look = useResourceIcons().byCode(groupCode);

  return <GameIcon name={look.name} size={size} className={look.className} />;
}
