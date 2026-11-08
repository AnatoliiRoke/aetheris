/**
 * The organic outline of a sector of the galaxy (task 00034, the new design of the map).
 *
 * The new design draws a sector not as a rounded rectangle but as an irregular shape with a soft
 * edge, as if a cloud of dust had settled there. The shape is the same on every visit: it is
 * derived from the identifier of the sector, so the map does not change under the player.
 *
 * The outline is a ring of points on a circle whose radius changes from point to point. The
 * points are given in the units of the world, so the shape keeps its proportions at any zoom.
 */

/** The number of points of the outline of a sector. */
const pointCount = 16;

/** The smallest share of the radius a point may have, as a share of the radius of the circle. */
const minRadiusShare = 0.74;

/** The biggest share of the radius a point may have. */
const maxRadiusShare = 1;

/**
 * A small generator of numbers from a seed, so a sector always has the same outline.
 *
 * @param seed The seed of the generator.
 * @returns The next number, from 0 to 1.
 */
function random(seed: number): () => number {
  let state = seed >>> 0;

  return () => {
    // A linear congruential generator: the same seed gives the same row of numbers.
    state = (state * 1664525 + 1013904223) >>> 0;

    return state / 4294967296;
  };
}

/**
 * The outline of a sector as a list of points in the units of the world.
 *
 * @param sectorId The identifier of the sector: the same sector always has the same outline.
 * @param radius The radius of the circle of the points, in the units of the world.
 * @returns The points of the outline, round the shape.
 */
export function sectorOutline(sectorId: number, radius: number): { x: number; y: number }[] {
  const next = random(sectorId * 2654435761);
  const points: { x: number; y: number }[] = [];
  // The first point starts away from the angle of the corner, so the shape has no sharp corner.
  const start = next() * Math.PI * 2;

  for (let index = 0; index < pointCount; index++) {
    const angle = start + (index / pointCount) * Math.PI * 2;
    const share = minRadiusShare + next() * (maxRadiusShare - minRadiusShare);
    const distance = radius * share;

    points.push({ x: Math.cos(angle) * distance, y: Math.sin(angle) * distance });
  }

  return points;
}

/**
 * The outline of a sector as a path of an SVG, in the units of the world. The path is closed and
 * smooth, so the edge of the shape has no sharp corner.
 *
 * @param sectorId The identifier of the sector.
 * @param radius The radius of the circle of the points, in the units of the world.
 * @returns The value of the `d` of the path.
 */
export function sectorPath(sectorId: number, radius: number): string {
  const points = sectorOutline(sectorId, radius);
  const first = points[0];
  const parts: string[] = [`M ${round(first!.x)} ${round(first!.y)}`];

  for (let index = 1; index < points.length; index++) {
    const point = points[index]!;
    const previous = points[index - 1]!;

    // A curve through the middle of two points, so the edge is round.
    const middleX = (previous.x + point.x) / 2;
    const middleY = (previous.y + point.y) / 2;

    parts.push(`Q ${round(previous.x)} ${round(previous.y)} ${round(middleX)} ${round(middleY)}`);
  }

  const last = points[points.length - 1]!;

  parts.push(`Q ${round(last.x)} ${round(last.y)} ${round(first!.x)} ${round(first!.y)}`, 'Z');

  return parts.join(' ');
}

/**
 * The outline of a sector as a `clip-path`, so the browser cuts the shape out of the block. The
 * points are in percentages of the size of the sector, so the shape keeps its proportions.
 *
 * @param sectorId The identifier of the sector.
 * @returns The value of `clip-path` of the sector.
 */
export function sectorClipPath(sectorId: number): string {
  const points = sectorOutline(sectorId, 50);
  const polygon = points.map((point) => `${round(point.x + 50)}% ${round(point.y + 50)}%`).join(', ');

  return `polygon(${polygon})`;
}

/**
 * Rounds a number to two decimals, so the outline of a sector is the same text every time.
 *
 * @param value The number.
 * @returns The rounded number.
 */
function round(value: number): number {
  return Math.round(value * 100) / 100;
}
