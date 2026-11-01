/**
 * The pure geometry of the map screen (task 00034): the view (the shift and the scale of the
 * drawn world), the zoom around a point, the way from a pixel to a place of the world and back,
 * the grouping of near stars and the places of the orbits of a system. It knows nothing of the
 * game: the sizes of a sector and the times of a flight come from the server.
 */

/** The view of the world: a point of the world `(wx, wy)` is drawn at `(x + wx * k, y + wy * k)` pixels. */
export interface MapView {
  x: number;
  y: number;
  k: number;
}

/** A rectangle of the world. */
export interface Bounds {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

/** The size of the area the map is drawn in, in pixels. */
export interface Viewport {
  width: number;
  height: number;
}

/** The smallest and the largest scale a view may have, relative to the scale that fits the whole world. */
export const zoomLimits = { minRelative: 0.5, maxRelative: 24 } as const;

/** The step of one press of a zoom button or of one wheel notch. */
export const zoomStep = 1.25;

/** The size of a part of a drawn star system, in the units of its own world: the world is a square of this side. */
export const systemWorldSize = 1000;

/** The share of the half of the system world the outermost orbit takes. */
const outerOrbitShare = 0.92;

/** The golden angle in degrees: it spreads the places of the objects round the orbits without a pattern. */
const goldenAngle = 137.50776405;

const fullTurn = 360;

/**
 * The view that fits a rectangle of the world into an area, with a margin round it.
 *
 * @param bounds The rectangle of the world.
 * @param viewport The area in pixels.
 * @param margin The margin in pixels.
 */
export function fitView(bounds: Bounds, viewport: Viewport, margin: number): MapView {
  const width = Math.max(bounds.maxX - bounds.minX, 1);
  const height = Math.max(bounds.maxY - bounds.minY, 1);
  const k = Math.max(Math.min((viewport.width - margin * 2) / width, (viewport.height - margin * 2) / height), 1e-6);

  return {
    k,
    x: (viewport.width - width * k) / 2 - bounds.minX * k,
    y: (viewport.height - height * k) / 2 - bounds.minY * k,
  };
}

/**
 * The view that puts a place of the world in the middle of the area, keeping the scale.
 *
 * @param view The view now.
 * @param world The place of the world.
 * @param viewport The area in pixels.
 */
export function centerOn(view: MapView, world: { x: number; y: number }, viewport: Viewport): MapView {
  return { k: view.k, x: viewport.width / 2 - world.x * view.k, y: viewport.height / 2 - world.y * view.k };
}

/**
 * Changes the scale of a view keeping the place under a pixel where it is.
 *
 * @param view The view now.
 * @param factor How many times the scale grows.
 * @param pixel The pixel that stays still, in the area.
 * @param limits The smallest and the largest scale.
 */
export function zoomAt(view: MapView, factor: number, pixel: { x: number; y: number }, limits: { min: number; max: number }): MapView {
  const k = Math.min(Math.max(view.k * factor, limits.min), limits.max);
  const ratio = k / view.k;

  return { k, x: pixel.x - (pixel.x - view.x) * ratio, y: pixel.y - (pixel.y - view.y) * ratio };
}

/**
 * The place of the world under a pixel of the area.
 *
 * @param view The view.
 * @param pixel The pixel in the area.
 */
export function screenToWorld(view: MapView, pixel: { x: number; y: number }): { x: number; y: number } {
  return { x: (pixel.x - view.x) / view.k, y: (pixel.y - view.y) / view.k };
}

/**
 * The pixel of the area where a place of the world is drawn.
 *
 * @param view The view.
 * @param world The place of the world.
 */
export function worldToScreen(view: MapView, world: { x: number; y: number }): { x: number; y: number } {
  return { x: view.x + world.x * view.k, y: view.y + world.y * view.k };
}

/** A group of stars that lie so near on the screen that they are drawn as one bubble. */
export interface Cluster<T> {
  /** The middle of the group in the world. */
  x: number;
  y: number;
  members: T[];
}

/**
 * Groups the points that fall into one cell of a grid; a cell holds one group. A cell of the size
 * of a few pixels of the screen (the size in the world is the pixels divided by the scale) makes the
 * stars merge when the map is far and split when it comes near, with the cost of one pass.
 *
 * @param items The points of the world.
 * @param cellSize The side of a cell in the units of the world.
 * @param isFixed Whether a point must stay alone: a place of the player is never hidden in a bubble.
 */
export function clusterPoints<T extends { x: number; y: number }>(items: readonly T[], cellSize: number, isFixed: (item: T) => boolean): Cluster<T>[] {
  const cells = new Map<string, T[]>();
  const result: Cluster<T>[] = [];

  for (const item of items) {
    if (isFixed(item) || cellSize <= 0) {
      result.push({ x: item.x, y: item.y, members: [item] });

      continue;
    }

    const key = `${Math.floor(item.x / cellSize)}:${Math.floor(item.y / cellSize)}`;
    const cell = cells.get(key);

    if (cell) {
      cell.push(item);
    } else {
      cells.set(key, [item]);
    }
  }

  for (const members of cells.values()) {
    result.push({
      x: members.reduce((sum, member) => sum + member.x, 0) / members.length,
      y: members.reduce((sum, member) => sum + member.y, 0) / members.length,
      members,
    });
  }

  return result;
}

/**
 * The radius of an orbit in the world of a drawn star system.
 *
 * @param orbit The number of the orbit, from 1.
 * @param orbitCount How many orbits the system has.
 */
export function orbitRadius(orbit: number, orbitCount: number): number {
  return (orbit / Math.max(orbitCount, 1)) * (systemWorldSize / 2) * outerOrbitShare;
}

/**
 * The place of an object of a star system: on its orbit at an angle taken from its identifier, so
 * the map is the same on every visit (the map is static, the bodies do not move).
 *
 * @param id The identifier of the object.
 * @param orbit The orbit.
 * @param orbitCount How many orbits the system has.
 */
export function orbitPlace(id: number, orbit: number, orbitCount: number): { x: number; y: number } {
  const angle = (((id * goldenAngle) % fullTurn) * Math.PI) / (fullTurn / 2);
  const radius = orbitRadius(orbit, orbitCount);
  const middle = systemWorldSize / 2;

  return { x: middle + radius * Math.cos(angle), y: middle + radius * Math.sin(angle) };
}

/**
 * The distance between two places in the world, for the nearest place under a pointer.
 *
 * @param first The first place.
 * @param second The second place.
 */
export function distanceBetween(first: { x: number; y: number }, second: { x: number; y: number }): number {
  return Math.hypot(first.x - second.x, first.y - second.y);
}
