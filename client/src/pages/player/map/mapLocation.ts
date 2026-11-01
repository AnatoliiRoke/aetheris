/**
 * The place the map shows and what is chosen on it, written in the address of the page (task
 * 00034), so a link opens the same place: `?sector=3&system=12&object=40&view=list&target=system:12`.
 */

/** The target of a flight a player pointed at (task 00034): a system, an object or a free point of a sector. */
export type MapTarget =
  | { kind: 'system'; systemId: number }
  | { kind: 'object'; objectId: number; systemId: number | null; sectorId: number | null }
  | { kind: 'point'; sectorId: number; x: number; y: number; z: number };

/** The way the places are shown: the drawn map or the table. */
export type MapMode = 'map' | 'list';

/** The place and the choice of the map screen. */
export interface MapLocation {
  /** Whether the whole galaxy is shown (no sector is chosen). */
  galaxy: boolean;
  sectorId: number | null;
  systemId: number | null;
  objectId: number | null;
  mode: MapMode;
  target: MapTarget | null;
}

const positive = (value: string | null): number | null => {
  const number = value === null ? Number.NaN : Number(value);

  return Number.isInteger(number) && number > 0 ? number : null;
};

/**
 * Writes a target as the text of the address.
 *
 * @param target The target.
 */
export function targetToText(target: MapTarget): string {
  switch (target.kind) {
    case 'system':
      return `system:${target.systemId}`;
    case 'object':
      return `object:${target.objectId}:${target.systemId ?? ''}:${target.sectorId ?? ''}`;
    case 'point':
      return `point:${target.sectorId}:${target.x}:${target.y}:${target.z}`;
  }
}

/**
 * Reads a target from the text of the address; a text that is not a target gives `null`.
 *
 * @param text The text.
 */
export function targetFromText(text: string | null): MapTarget | null {
  const parts = (text ?? '').split(':');
  const kind = parts[0];

  if (kind === 'system') {
    const systemId = positive(parts[1] ?? null);

    return systemId === null ? null : { kind, systemId };
  }

  if (kind === 'object') {
    const objectId = positive(parts[1] ?? null);

    return objectId === null ? null : { kind, objectId, systemId: positive(parts[2] ?? null), sectorId: positive(parts[3] ?? null) };
  }

  if (kind === 'point') {
    const sectorId = positive(parts[1] ?? null);
    const [x, y, z] = [parts[2], parts[3], parts[4]].map((value) => (value === undefined || value === '' ? Number.NaN : Number(value)));

    return sectorId === null || !Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z) ? null : { kind, sectorId, x: x!, y: y!, z: z! };
  }

  return null;
}

/**
 * Reads the place and the choice of the map screen from the address.
 *
 * @param query The query of the address.
 */
export function parseLocation(query: URLSearchParams): MapLocation {
  return {
    galaxy: query.get('level') === 'galaxy',
    sectorId: positive(query.get('sector')),
    systemId: positive(query.get('system')),
    objectId: positive(query.get('object')),
    mode: query.get('view') === 'list' ? 'list' : 'map',
    target: targetFromText(query.get('target')),
  };
}

/**
 * Writes the place and the choice of the map screen as the query of the address.
 *
 * @param location The place and the choice.
 */
export function buildQuery(location: MapLocation): string {
  const query = new URLSearchParams();

  if (location.galaxy) {
    query.set('level', 'galaxy');
  }

  if (location.sectorId !== null) {
    query.set('sector', String(location.sectorId));
  }

  if (location.systemId !== null) {
    query.set('system', String(location.systemId));
  }

  if (location.objectId !== null) {
    query.set('object', String(location.objectId));
  }

  if (location.mode === 'list') {
    query.set('view', 'list');
  }

  if (location.target !== null) {
    query.set('target', targetToText(location.target));
  }

  const text = query.toString();

  return text === '' ? '' : `?${text}`;
}
