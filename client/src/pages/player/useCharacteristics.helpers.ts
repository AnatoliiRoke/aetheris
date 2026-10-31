import type { Characteristic, PartCharacteristics } from '../../api/productionApi';
import type { EntityText } from '../../texts/context';

/**
 * A characteristic of a part as the screens show it: the name and the unit of the
 * characteristic, both from the texts of the game data, and the value the part has.
 */
export interface CharacteristicRow {
  /** The characteristic. */
  characteristicId: number;
  /** The name of the characteristic, from the texts of the game data. */
  name: string;
  /** The unit of the characteristic, from the texts of the game data; `null` when it has none. */
  unit: string | null;
  /** The value of the part; `null` when the part has none. */
  value: number | null;
  /** The values by the level of the part, when the characteristic depends on the level. */
  levels: CharacteristicLevel[];
}

/**
 * One step of a characteristic that depends on the level of the part (task 00027).
 */
export interface CharacteristicLevel {
  /** The level of the part. */
  level: number;
  /** The value at that level. */
  value: number;
}

/**
 * The catalog of the characteristics of the parts: the characteristics by their identifier and
 * the parts with their weight and their values by their identifier. The client keeps no list of
 * its own: every name and every unit comes from the server.
 */
export interface CharacteristicCatalog {
  /** The characteristics of the parts by their identifier. */
  readonly characteristics: ReadonlyMap<number, Characteristic>;
  /** The parts with their weight and their values by their identifier. */
  readonly parts: ReadonlyMap<number, PartCharacteristics>;
}

/**
 * The steps of a characteristic that depends on the level of the part, in the order of the level.
 * A step whose level is not a number is dropped.
 *
 * @param levels The values by level, as the server sends them.
 * @returns The steps.
 */
function levelSteps(levels: Record<string, number>): CharacteristicLevel[] {
  const steps: CharacteristicLevel[] = [];

  for (const [levelText, value] of Object.entries(levels)) {
    const level = Number(levelText);

    if (Number.isFinite(level) && Number.isFinite(value)) {
      steps.push({ level, value });
    }
  }

  return steps.sort((first, second) => first.level - second.level);
}

/**
 * Builds the catalog of the characteristics out of the answers of the server.
 *
 * @param characteristics The characteristics of the parts with their groups.
 * @param parts The parts with their weight and their values.
 * @returns The catalog the screens read.
 */
export function buildCatalog(characteristics: Characteristic[], parts: PartCharacteristics[]): CharacteristicCatalog {
  return {
    characteristics: new Map(characteristics.map((characteristic) => [characteristic.characteristicId, characteristic])),
    parts: new Map(parts.map((part) => [part.partTypeId, part])),
  };
}

/**
 * The characteristics of a part as the screens show them: every characteristic of the group of
 * the part, in the order of the group, with the value the part has. A characteristic the part
 * has no value for shows only when the group needs it, and an archived one shows only when the
 * part keeps its value.
 *
 * @param catalog The catalog of the characteristics.
 * @param partTypeId The part.
 * @param text The texts of the game data.
 * @returns The rows of the characteristics of the part.
 */
export function characteristicRows(catalog: CharacteristicCatalog, partTypeId: number, text: EntityText): CharacteristicRow[] {
  const part = catalog.parts.get(partTypeId);

  if (!part) {
    return [];
  }

  const values = new Map(part.characteristics.map((value) => [value.characteristicId, value]));
  const rows: { row: CharacteristicRow; sortOrder: number }[] = [];

  for (const characteristic of catalog.characteristics.values()) {
    const link = characteristic.groups.find((group) => group.partGroupId === part.partGroupId);

    if (!link) {
      continue;
    }

    const value = values.get(characteristic.characteristicId);

    if (!value && (characteristic.isArchived || !link.isRequired)) {
      continue;
    }

    rows.push({
      sortOrder: link.sortOrder,
      row: {
        characteristicId: characteristic.characteristicId,
        name: text(characteristic.nameKey) ?? characteristic.nameKey,
        unit: characteristic.unitKey ? text(characteristic.unitKey) ?? characteristic.unitKey : null,
        value: value ? value.numberValue : null,
        levels: levelSteps(value?.levels ?? {}),
      },
    });
  }

  return rows
    .sort((first, second) => first.sortOrder - second.sortOrder || first.row.characteristicId - second.row.characteristicId)
    .map((entry) => entry.row);
}
