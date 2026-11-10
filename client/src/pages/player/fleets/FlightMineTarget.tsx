import { useCallback, useMemo } from 'react';
import type { MapSector, MapSectorView, MapSystemView } from '../../../api/mapApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { useRead } from '../market/useRead';
import { visibleSystems } from './flightPlanTarget';

/**
 * A place a fleet may work at (task 00050).
 */
export interface MinePlace {
  /** The identifier of the object of the map. */
  objectId: number;
  /** The name of the place. */
  name: string;
}

/**
 * The fields of the target of an order of extraction (task 00050).
 *
 * A fleet works at a place that holds something to take: a field of a sector, a nebula or a gas
 * giant of a system. The player names the place, and the fleet takes from it every quarter of an
 * hour.
 *
 * @param id The identifier of the order; the fields of the order are named after it.
 * @param value The target as the order holds it.
 * @param sectors The sectors the player knows of.
 * @param onChange Answers the new target of the order.
 */
export function FlightMineTarget({
  id,
  value,
  sectors,
  onChange,
}: {
  id: string;
  value: { targetSectorId: number | null; targetSystemId: number | null; targetObjectId: number | null };
  sectors: readonly MapSector[];
  onChange: (patch: { targetSectorId: number | null; targetSystemId: number | null; targetObjectId: number | null }) => void;
}) {
  const { t } = useI18n();
  const { map } = useApi();

  // The sectors the player knows of: the ones a radar of the player reaches and the ones with a body
  // of the player in them.
  const knownSectors = useMemo(() => sectors.filter((sector) => sector.state === 'open' || sector.state === 'known'), [sectors]);

  // The sector of the place: it holds the free fields and the nebulas of the place.
  const loadSector = useCallback(
    (): Promise<MapSectorView | null> => (value.targetSectorId === null ? Promise.resolve(null) : map.sector(value.targetSectorId, null)),
    [map, value.targetSectorId],
  );
  const sectorRead = useRead(loadSector, value.targetSectorId !== null, false);

  // The systems of the sector: the gas giants stand in them.
  const systems = useMemo(() => (sectorRead.data === null ? [] : visibleSystems(sectorRead.data.systems)), [sectorRead.data]);

  // The system of the place, so the gas giants of it may be read.
  const loadSystem = useCallback(
    (): Promise<MapSystemView | null> => (value.targetSystemId === null ? Promise.resolve(null) : map.system(value.targetSystemId, null)),
    [map, value.targetSystemId],
  );
  const systemRead = useRead(loadSystem, value.targetSystemId !== null, false);

  // The places the sector holds: the free fields and the nebulas.
  const sectorPlaces = useMemo(() => placesOfSector(sectorRead.data), [sectorRead.data]);

  // The gas giants of the chosen system.
  const systemPlaces = useMemo(() => placesOfSystem(systemRead.data), [systemRead.data]);

  // The places the fleet may work at: the free fields and the nebulas of the sector, and the gas
  // giants of the chosen system.
  const places = useMemo(() => {
    const all = [...sectorPlaces];

    if (value.targetSystemId !== null) {
      all.push(...systemPlaces);
    }

    return all;
  }, [sectorPlaces, systemPlaces, value.targetSystemId]);

  // A change of the sector drops the system and the place below it.
  const changeSector = (nextSectorId: number) => {
    onChange({ targetSectorId: nextSectorId, targetSystemId: null, targetObjectId: null });
  };

  // A change of the system drops the place below it.
  const changeSystem = (nextSystemId: number) => {
    onChange({ targetSectorId: value.targetSectorId, targetSystemId: nextSystemId, targetObjectId: null });
  };

  return (
    <>
      <div className="pl-order__row pl-order__row--places">
        <label className="fl-label" htmlFor={`plan-order-mine-sector-${id}`}>
          {t('plan.sector')}
        </label>
        <select
          className="fl-input"
          id={`plan-order-mine-sector-${id}`}
          onChange={(event) => changeSector(Number(event.target.value))}
          value={value.targetSectorId ?? ''}
        >
          <option value="">{t('plan.sectorAny')}</option>
          {knownSectors.map((sector) => (
            <option key={sector.id} value={sector.id}>
              {sector.name}
            </option>
          ))}
        </select>

        <label className="fl-label" htmlFor={`plan-order-mine-system-${id}`}>
          {t('plan.system')}
        </label>
        <select
          className="fl-input"
          disabled={value.targetSectorId === null}
          id={`plan-order-mine-system-${id}`}
          onChange={(event) => changeSystem(Number(event.target.value))}
          value={value.targetSystemId ?? ''}
        >
          <option value="">{t('plan.systemAny')}</option>
          {systems.map((system) => (
            <option key={system.id} value={system.id}>
              {system.name ?? system.number}
            </option>
          ))}
        </select>

        <label className="fl-label" htmlFor={`plan-order-mine-place-${id}`}>
          {t('plan.minePlace')}
        </label>
        <select
          className="fl-input"
          disabled={value.targetSectorId === null}
          id={`plan-order-mine-place-${id}`}
          onChange={(event) => onChange({ targetSectorId: value.targetSectorId, targetSystemId: value.targetSystemId, targetObjectId: event.target.value === '' ? null : Number(event.target.value) })}
          value={value.targetObjectId ?? ''}
        >
          <option value="">{t('plan.minePlaceAny')}</option>
          {places.map((place) => (
            <option key={place.objectId} value={place.objectId}>
              {place.name}
            </option>
          ))}
        </select>
      </div>
    </>
  );
}

/**
 * The places a sector holds: the free fields of it and the nebulas of it (task 00050).
 *
 * @param sector The sector.
 * @returns The places of the sector.
 */
function placesOfSector(sector: MapSectorView | null): MinePlace[] {
  if (sector === null) {
    return [];
  }

  const fields: MinePlace[] = sector.fields.map((field) => ({
    objectId: field.objectId,
    name: field.name,
  }));

  const nebulas: MinePlace[] = sector.nebulas.map((nebula) => ({
    objectId: nebula.objectId,
    name: nebula.name,
  }));

  return [...fields, ...nebulas];
}

/**
 * The places a system holds: the gas giants of it (task 00050).
 *
 * @param system The system.
 * @returns The places of the system.
 */
function placesOfSystem(system: MapSystemView | null): MinePlace[] {
  if (system === null) {
    return [];
  }

  return system.objects
    .filter((object) => object.objectClass === 'gas_giant' && object.state === 'open')
    .map((object) => ({
      objectId: object.id,
      name: object.name ?? String(object.orbit ?? object.id),
    }));
}
