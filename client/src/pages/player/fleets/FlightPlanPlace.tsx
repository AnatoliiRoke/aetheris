import { useCallback, useMemo } from 'react';
import type { MapSectorView, MapSystemView } from '../../../api/mapApi';
import { useApi } from '../../../api/useApi';
import { useRead } from '../market/useRead';

/**
 * The place an order of a flight plan names, as the panel shows it (task 00050).
 *
 * The target of a flight is either a point of the space or a place of the world: a planet of a star
 * system or a free asteroid field of a sector. The name of the place is read from the world, so it
 * is the same name the picker shows.
 *
 * @param sectorId The sector of the target, when the target names one.
 * @param systemId The system of the target, when the target names one.
 * @param objectId The body of the target, when the target names one.
 * @returns The name of the place, or nothing when the world does not name it.
 */
export function FlightPlanPlace({ sectorId, systemId, objectId }: { sectorId: number; systemId: number; objectId: number }) {
  const { map } = useApi();

  // The sector of the target, so the name of its system and of its free fields may be read.
  const loadSector = useCallback(
    (): Promise<MapSectorView | null> => (sectorId <= 0 ? Promise.resolve(null) : map.sector(sectorId, null)),
    [map, sectorId],
  );
  const sectorRead = useRead(loadSector, sectorId > 0, false);

  // The name of the system of the target. The name is taken from the whole list of the systems of the
  // sector, not only from the visible ones: an order of a plan that was written earlier keeps the
  // name of its system even when the radar of the player does not reach it now.
  const systemName = useMemo(() => {
    const systems = sectorRead.data?.systems ?? [];
    const system = systems.find((entry) => entry.id === systemId);

    return system === undefined || system.name === null ? null : system.name;
  }, [sectorRead.data, systemId]);

  // The name of a free field or of a nebula of the sector: both belong to no system.
  const fieldName = useMemo(() => {
    const fields = sectorRead.data?.fields ?? [];
    const field = fields.find((entry) => entry.objectId === objectId);

    if (field !== undefined) {
      return field.name;
    }

    const nebula = (sectorRead.data?.nebulas ?? []).find((entry) => entry.objectId === objectId);

    return nebula === undefined ? null : nebula.name;
  }, [sectorRead.data, objectId]);

  // The system of the target, so the name of its body may be read.
  const loadSystem = useCallback(
    (): Promise<MapSystemView | null> => (systemId <= 0 ? Promise.resolve(null) : map.system(systemId, null)),
    [map, systemId],
  );
  const systemRead = useRead(loadSystem, systemId > 0, false);

  // The name of the body of the target. It is taken from the whole list of the bodies of the system,
  // not only from the visible ones, for the same reason as the name of the system.
  const objectName = useMemo(() => {
    const objects = systemRead.data?.objects ?? [];
    const object = objects.find((entry) => entry.id === objectId);

    return object === undefined || object.name === null ? null : object.name;
  }, [systemRead.data, objectId]);

  return <>{objectName ?? fieldName ?? systemName}</>;
}
