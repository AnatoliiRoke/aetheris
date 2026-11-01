import type { CSSProperties } from 'react';

/**
 * The place of an element of the world as custom properties. The stylesheet turns them into the
 * pixels of the screen with the shift and the scale the area keeps (`--tx`, `--ty`, `--k`), so the
 * element is drawn at its own size and its text and pictures stay sharp at any zoom; only the vector
 * shapes of the map are scaled, and a vector stays sharp when it grows.
 *
 * @param x The place of the world.
 * @param y The place of the world.
 * @param width The size of the element in the units of the world, for an area that grows with the map.
 * @param height The size of the element in the units of the world.
 */
export function worldStyle(x: number, y: number, width?: number, height?: number): CSSProperties {
  const style: Record<string, number> = { '--wx': x, '--wy': y };

  if (width !== undefined) {
    style['--ww'] = width;
  }

  if (height !== undefined) {
    style['--wh'] = height;
  }

  return style as CSSProperties;
}
