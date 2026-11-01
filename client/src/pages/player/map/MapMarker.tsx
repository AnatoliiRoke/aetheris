import type { ReactNode } from 'react';

/**
 * A mark of the map: a button at a place of the world that keeps its size whatever the zoom is
 * (the style takes the scale from `--k`), with a symbol and, if there is room, a label. It is a
 * button, so it has the focus, the keyboard and the name for readers, and a click on it does not
 * count as a click on an empty place.
 *
 * @param x The place of the world.
 * @param y The place of the world.
 * @param label The text under the symbol, or `null`.
 * @param name What readers hear.
 * @param tone The look of the mark: the ownership or the state.
 * @param selected Whether the mark is the chosen one.
 * @param onSelect What to do on a click.
 * @param onOpen What to do on a double click, or `undefined`.
 * @param children The symbol.
 */
export function MapMarker({
  x,
  y,
  label,
  name,
  tone,
  selected,
  onSelect,
  onOpen,
  children,
}: {
  x: number;
  y: number;
  label: string | null;
  name: string;
  tone: string;
  selected: boolean;
  onSelect: () => void;
  onOpen?: () => void;
  children: ReactNode;
}) {
  return (
    <button
      aria-label={name}
      aria-pressed={selected}
      className={`map-marker map-marker--${tone}${selected ? ' is-selected' : ''}`}
      data-map-hit=""
      style={{ left: x, top: y }}
      type="button"
      onClick={onSelect}
      onDoubleClick={onOpen}
    >
      <span aria-hidden="true" className="map-marker__symbol">
        {children}
      </span>
      {label ? (
        <span aria-hidden="true" className="map-marker__label">
          {label}
        </span>
      ) : null}
    </button>
  );
}
