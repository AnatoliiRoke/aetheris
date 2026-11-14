import type { FleetHold } from '../../../api/fleetApi';

/**
 * The kinds of an item of an order of a load or an unload (task 00057).
 */
export type CargoKind = 'resource' | 'part';

/**
 * One item of an order of a load or an unload as the window holds it while the player writes it
 * (task 00057): what to carry and how much of it. The amount is a text, because the player is typing it.
 */
export interface CargoDraftItem {
  kind: CargoKind;
  /** The resource or the kind of the module; `null` until the player chooses. */
  id: number | null;
  /** The mark level of a module; one for a resource. */
  markLevel: number;
  /** Whether a module carries the bonus of its race. */
  hasRaceBonus: boolean;
  /** How much of the resource, or how many modules, as the player wrote it. */
  amount: string;
}

/**
 * What the holds of a fleet carry, or are meant to carry after some orders of the plan: the amount by the
 * key of the thing carried (task 00057).
 */
export type HoldContent = ReadonlyMap<string, HoldEntry>;

/**
 * One thing the holds carry.
 */
export interface HoldEntry {
  kind: CargoKind;
  id: number;
  markLevel: number;
  hasRaceBonus: boolean;
  amount: number;
}

/**
 * The key of a thing carried: a resource by its identifier, a module by its kind, mark and bonus.
 *
 * @param item The thing.
 * @returns The key.
 */
export function cargoKey(item: Pick<CargoDraftItem, 'kind' | 'id' | 'markLevel' | 'hasRaceBonus'>): string {
  return item.kind === 'resource' ? `r:${item.id}` : `p:${item.id}:${item.markLevel}:${item.hasRaceBonus ? 1 : 0}`;
}

/**
 * An empty item that the player fills in.
 *
 * @returns The item.
 */
export function emptyCargoItem(): CargoDraftItem {
  return { kind: 'resource', id: null, markLevel: 1, hasRaceBonus: false, amount: '' };
}

/**
 * The amount of an item as a number, or `null` when it is not a whole number above zero. The amount is
 * obligatory (task 00057).
 *
 * @param item The item.
 * @returns The amount.
 */
export function cargoAmount(item: Pick<CargoDraftItem, 'amount'>): number | null {
  const amount = Number(item.amount);

  return item.amount.trim() !== '' && Number.isInteger(amount) && amount > 0 ? amount : null;
}

/**
 * Whether the items of an order are written whole: there is at least one, and every one names a thing and a
 * whole amount above zero.
 *
 * @param items The items.
 * @returns `true` when the order can be sent.
 */
export function cargoItemsValid(items: readonly CargoDraftItem[]): boolean {
  return items.length > 0 && items.every((item) => item.id !== null && cargoAmount(item) !== null);
}

/**
 * Writes the items of an order as the document of its parameters, as the server reads it. An item that is
 * not written whole is left out, so the server never gets an amount that is not a number.
 *
 * @param items The items.
 * @returns The document.
 */
export function cargoParameters(items: readonly CargoDraftItem[]): string {
  const written = items.flatMap((item) => {
    const amount = cargoAmount(item);

    if (item.id === null || amount === null) {
      return [];
    }

    return item.kind === 'resource'
      ? [{ kind: 'resource', id: item.id, amount }]
      : [{ kind: 'part', id: item.id, mark_level: item.markLevel, has_race_bonus: item.hasRaceBonus, amount }];
  });

  return JSON.stringify({ items: written });
}

/**
 * Reads the items of an order out of the document of its parameters.
 *
 * @param parameters The document.
 * @returns The items; empty when the document names none.
 */
export function readCargoItems(parameters: string): CargoDraftItem[] {
  try {
    const document = JSON.parse(parameters) as { items?: unknown };

    if (!Array.isArray(document.items)) {
      return [];
    }

    return document.items.flatMap((entry: unknown) => {
      const item = entry as { kind?: string; id?: number; mark_level?: number; has_race_bonus?: boolean; amount?: number };

      if ((item.kind !== 'resource' && item.kind !== 'part') || typeof item.id !== 'number' || typeof item.amount !== 'number') {
        return [];
      }

      return [
        {
          kind: item.kind,
          id: item.id,
          markLevel: item.kind === 'part' ? (item.mark_level ?? 1) : 1,
          hasRaceBonus: item.kind === 'part' && item.has_race_bonus === true,
          amount: String(item.amount),
        } satisfies CargoDraftItem,
      ];
    });
  } catch {
    return [];
  }
}

/**
 * What the holds of a fleet carry now, as the server says.
 *
 * @param hold The hold of the fleet, or `null` when it is not read yet.
 * @returns The content of the holds.
 */
export function holdContentOf(hold: FleetHold | null | undefined): HoldContent {
  const content = new Map<string, HoldEntry>();

  for (const item of hold?.items ?? []) {
    if (item.amount > 0) {
      content.set(`r:${item.resourceId}`, { kind: 'resource', id: item.resourceId, markLevel: 1, hasRaceBonus: false, amount: item.amount });
    }
  }

  for (const part of hold?.parts ?? []) {
    if (part.quantity > 0) {
      const entry: HoldEntry = { kind: 'part', id: part.partTypeId, markLevel: part.markLevel, hasRaceBonus: part.hasRaceBonus, amount: part.quantity };

      content.set(cargoKey(entry), entry);
    }
  }

  return content;
}

/**
 * What the holds are meant to carry before an order of the plan (task 00057): what they carry now, with every
 * load before the order taken as made whole and every unload before it as put out as far as the holds
 * carried. It is a guess of the window, not a promise: the extraction fills the holds with what the window
 * cannot know, and a load that the body cannot give is refused when the plan is carried out.
 *
 * @param orders The orders of the window; only the type and the items of an order matter.
 * @param index The position of the order.
 * @param base What the holds carry now.
 * @returns The content of the holds before the order.
 */
export function holdBefore(
  orders: readonly { typeCode: string; items: readonly CargoDraftItem[] }[],
  index: number,
  base: HoldContent,
): HoldContent {
  const content = new Map<string, HoldEntry>([...base].map(([key, entry]) => [key, { ...entry }]));

  for (let place = 0; place < index; place += 1) {
    const order = orders[place]!;

    if (order.typeCode !== 'load' && order.typeCode !== 'unload') {
      continue;
    }

    for (const item of order.items) {
      const amount = cargoAmount(item);

      if (item.id === null || amount === null) {
        continue;
      }

      const key = cargoKey(item);
      const entry = content.get(key);

      if (order.typeCode === 'load') {
        content.set(key, { kind: item.kind, id: item.id, markLevel: item.markLevel, hasRaceBonus: item.hasRaceBonus, amount: (entry?.amount ?? 0) + amount });
      } else if (entry) {
        const left = entry.amount - amount;

        if (left > 0) {
          content.set(key, { ...entry, amount: left });
        } else {
          content.delete(key);
        }
      }
    }
  }

  return content;
}

/**
 * The mass of what the holds carry, in tonnes.
 *
 * @param content The content of the holds.
 * @param weightOf The weight of one unit of a thing, in tonnes.
 * @returns The mass.
 */
export function holdMass(content: HoldContent, weightOf: (entry: HoldEntry) => number): number {
  let mass = 0;

  for (const entry of content.values()) {
    mass += entry.amount * weightOf(entry);
  }

  return mass;
}

/**
 * The items an order of an unload starts with: everything the holds carry, in the whole amount (task 00057).
 * The player takes out what they do not want to put out.
 *
 * @param content The content of the holds before the order.
 * @returns The items.
 */
export function unloadDefaults(content: HoldContent): CargoDraftItem[] {
  return [...content.values()].map((entry) => ({
    kind: entry.kind,
    id: entry.id,
    markLevel: entry.markLevel,
    hasRaceBonus: entry.hasRaceBonus,
    amount: String(entry.amount),
  }));
}

/**
 * The weights of one unit of the resources, by the resource, in tonnes, as the hold of the fleet sends them.
 *
 * @param hold The hold of the fleet, or nothing while it is read.
 * @returns The weights.
 */
export function resourceWeightsOf(hold: FleetHold | null | undefined): Map<number, number> {
  return new Map((hold?.resourceWeights ?? []).map((entry) => [entry.resourceId, entry.weight]));
}

/**
 * How many units of a thing a load can take: as many as fit into the room, rounded down, and no more than the
 * source has. A weight that is not above zero is a fault of the data, so nothing is promised.
 *
 * @param free The room the load may take, in tonnes.
 * @param weight The weight of one unit, in tonnes.
 * @param available How much the source has, or `null` when the window does not know.
 * @returns The number of the units, or `null` when it cannot be counted.
 */
export function maxLoadable(free: number, weight: number, available: number | null): number | null {
  if (!(weight > 0)) {
    return null;
  }

  const fits = Math.max(0, Math.floor(free / weight + 1e-9));

  return available === null ? fits : Math.min(fits, Math.max(0, available));
}
