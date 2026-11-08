import asteroidPicture from '../../../assets/images/map/asteroid.png';
import nebulaPicture from '../../../assets/images/map/nebula.png';
import star01 from '../../../assets/images/map/star-01.png';
import star02 from '../../../assets/images/map/star-02.png';
import star03 from '../../../assets/images/map/star-03.png';
import star04 from '../../../assets/images/map/star-04.png';

/** The four star pictures. A system keeps one of them, chosen from its identifier. */
const starPictures = [star01, star02, star03, star04];

/** The picture of a nebula. */
export const nebulaMark = nebulaPicture;

/** The picture of one clump of an asteroid field. A field draws several of these. */
export const asteroidMark = asteroidPicture;

/** The sizes of the clumps of one field, in pixels of the screen. */
const fieldRockSizes = [28, 18, 14, 22, 12];

/** How far a clump may sit from the middle of the field, in pixels of the screen. */
const fieldRockSpread = 16;

/** One clump of an asteroid field. */
export interface FieldRock {
  size: number;
  offsetX: number;
  offsetY: number;
}

/**
 * A stable mix of an identifier, so the same system or field always looks the same.
 *
 * @param seed The identifier.
 * @returns A non-negative mix of the identifier.
 */
function mix(seed: number): number {
  let mixed = seed | 0;
  mixed = Math.imul(mixed ^ (mixed >>> 16), 0x7feb352d);
  mixed = Math.imul(mixed ^ (mixed >>> 15), 0x846ca68b);
  return (mixed ^ (mixed >>> 16)) >>> 0;
}

/**
 * The picture of a star system. The four pictures are spread by the identifier, so neighbours differ.
 *
 * @param systemId The star system.
 * @returns The address of the picture.
 */
export function starPicture(systemId: number): string {
  return starPictures[mix(systemId) % starPictures.length] ?? starPictures[0];
}

/**
 * The clumps of an asteroid field: the same picture several times, each a different size and place.
 *
 * @param seed The field, so the cluster does not jump between draws.
 * @returns The clumps.
 */
export function fieldRocks(seed: number): FieldRock[] {
  return fieldRockSizes.map((size, index) => {
    const mixed = mix(seed + index + 1);
    const angle = ((mixed % 360) * Math.PI) / 180;
    const distance = (mix(seed + index + fieldRockSizes.length) % fieldRockSpread) + 4;

    return {
      size,
      offsetX: Math.round(Math.cos(angle) * distance),
      offsetY: Math.round(Math.sin(angle) * distance),
    };
  });
}
