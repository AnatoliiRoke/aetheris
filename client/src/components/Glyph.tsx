/**
 * The sign of a race: a shape and a colour, so the race is never told by colour alone
 * (documentation/ui-art/01, accessibility). The race names its style by the colour key of its
 * data; a race without one gets the neutral sign.
 */
export function Glyph({ colorKey, small = false }: { colorKey: string | null | undefined; small?: boolean }) {
  return <span className={`glyph glyph--${colorKey ?? 'neutral'}${small ? ' glyph--sm' : ''}`} aria-hidden="true" />;
}
