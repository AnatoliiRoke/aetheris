import type { RaceId } from '../api/contentApi';

/**
 * The sign of a race: a shape and a colour, so the race is never told by colour alone
 * (documentation/ui-art/01, accessibility).
 */
export function Glyph({ race, small = false }: { race: RaceId; small?: boolean }) {
  return <span className={`glyph glyph--${race}${small ? ' glyph--sm' : ''}`} aria-hidden="true" />;
}
