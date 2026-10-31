import { useCallback, useEffect, useMemo, useState } from 'react';
import type { Characteristic, ProductionApi } from '../../api/productionApi';
import { useApi } from '../../api/useApi';
import { useTexts } from '../../texts/useTexts';
import { buildCatalog, characteristicRows, type CharacteristicCatalog, type CharacteristicRow } from './useCharacteristics.helpers';

/**
 * What the screens know about the characteristics of the parts (task 00027, section 3.4): the
 * catalog of the characteristics, the names and the units of them, the values of a part and the
 * weight of a part. Every name and every unit comes from the texts of the server, so the client
 * knows no characteristic by its code.
 */
export interface CharacteristicsState {
  /** Whether the catalog of the characteristics has arrived. */
  loaded: boolean;
  /** Whether the read failed. */
  failed: boolean;
  /** The characteristic with its identifier, or `null` when the catalog does not have it. */
  characteristic: (characteristicId: number) => Characteristic | null;
  /** The name of a characteristic, from the texts of the game data. */
  name: (characteristicId: number) => string;
  /** The unit of a characteristic, from the texts of the game data; `null` when it has none. */
  unit: (characteristicId: number) => string | null;
  /** The value of a characteristic of a part; `null` when the part has none. */
  value: (partTypeId: number, characteristicId: number) => number | null;
  /** The weight of a part in tonnes; `null` when the catalog does not know the part. */
  weight: (partTypeId: number) => number | null;
  /** The characteristics of a part with their values, in the order of the group of the part. */
  rows: (partTypeId: number) => CharacteristicRow[];
}

/**
 * The catalog of the characteristics of the parts. Every screen of the game reads the same
 * catalog and it changes only when the admin panel changes a row, so the server is asked once
 * for the session and the answer stays in memory.
 */
let cached: CharacteristicCatalog | null = null;

/**
 * The read that is under way, so two screens that open at once ask the server once.
 */
let pending: Promise<CharacteristicCatalog> | null = null;

/**
 * Forgets the catalog of the characteristics, so the next read asks the server again. The tests
 * use it; the game never empties the catalog on its own.
 */
export function resetCharacteristicsCache(): void {
  cached = null;
  pending = null;
}

/**
 * Reads the catalog of the characteristics of the parts, once for the session.
 *
 * @param production The endpoints of the production module.
 * @returns The catalog of the characteristics and the parts.
 */
function readCatalog(production: ProductionApi): Promise<CharacteristicCatalog> {
  if (cached) {
    return Promise.resolve(cached);
  }

  if (pending) {
    return pending;
  }

  pending = Promise.all([production.characteristics(), production.characteristicParts()])
    .then(([characteristics, parts]) => {
      cached = buildCatalog(characteristics, parts);

      return cached;
    })
    .finally(() => {
      pending = null;
    });

  return pending;
}

/**
 * Reads the characteristics of the parts and keeps them for the session: the catalog of the
 * characteristics, the names and the units of them, the value of a characteristic of a part and
 * the weight of a part. The screens ask the server once and share the answer.
 */
export function useCharacteristics(): CharacteristicsState {
  const { production } = useApi();
  const { text } = useTexts();
  const [catalog, setCatalog] = useState<CharacteristicCatalog | null>(cached);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;

    void readCatalog(production)
      .then((answer) => {
        if (!cancelled) {
          setCatalog(answer);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [production]);

  const characteristic = useCallback(
    (characteristicId: number): Characteristic | null => catalog?.characteristics.get(characteristicId) ?? null,
    [catalog],
  );

  const name = useCallback(
    (characteristicId: number): string => {
      const found = catalog?.characteristics.get(characteristicId);

      return found ? text(found.nameKey) ?? found.nameKey : String(characteristicId);
    },
    [catalog, text],
  );

  const unit = useCallback(
    (characteristicId: number): string | null => {
      const key = catalog?.characteristics.get(characteristicId)?.unitKey;

      return key ? text(key) ?? key : null;
    },
    [catalog, text],
  );

  const value = useCallback(
    (partTypeId: number, characteristicId: number): number | null => {
      const part = catalog?.parts.get(partTypeId);

      return part?.characteristics.find((entry) => entry.characteristicId === characteristicId)?.numberValue ?? null;
    },
    [catalog],
  );

  const weight = useCallback((partTypeId: number): number | null => catalog?.parts.get(partTypeId)?.weight ?? null, [catalog]);

  const rows = useCallback(
    (partTypeId: number): CharacteristicRow[] => (catalog ? characteristicRows(catalog, partTypeId, text) : []),
    [catalog, text],
  );

  return useMemo(
    () => ({ loaded: catalog !== null, failed, characteristic, name, unit, value, weight, rows }),
    [catalog, failed, characteristic, name, unit, value, weight, rows],
  );
}
