import { useCallback, useMemo } from 'react';
import type { Fleet } from '../../../api/fleetApi';
import type { MapSector, MapSectorView, MapSystemView } from '../../../api/mapApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { useRead } from '../market/useRead';
import { FlightPlanTime } from './FlightPlanTime';
import type { DraftOrder, FlightTimeTarget, TargetKind } from './flightPlanParts';
import { objectText, planetBodies, systemText, visibleSystems } from './flightPlanTarget';

/**
 * The fields of the target of an order (task 00050).
 *
 * The player names the target of a flight in one of three ways: the three coordinates of a point of
 * the space, a planet of a star system, or an object that belongs to no system — a free asteroid
 * field. The kind of the target is chosen in the field «Ціль».
 *
 * @param id The identifier of the order; the fields of the order are named after it.
 * @param value The target as the order holds it.
 * @param sectors The sectors the player knows of.
 * @param fleet The fleet the plan is made for.
 * @param from The place the fleet is at when the order begins, when an order before it moves the fleet.
 * @param typeCode The type of the order, so the time is counted with the drives of the order (task 00072).
 * @param onChange Answers the new target of the order.
 */
export function FlightTargetFields({
  id,
  value,
  sectors,
  fleet,
  from,
  typeCode,
  onChange,
}: {
  id: string;
  value: Pick<DraftOrder, 'targetKind' | 'targetSectorId' | 'targetSystemId' | 'targetObjectId' | 'target'>;
  sectors: readonly MapSector[];
  fleet: Fleet | null;
  from: FlightTimeTarget | null;
  typeCode: string;
  onChange: (patch: Partial<DraftOrder>) => void;
}) {
  const { t } = useI18n();
  const { map } = useApi();

  // The sectors the player knows of: the ones a radar of the player reaches and the ones with a body
  // of the player in them.
  const knownSectors = useMemo(() => sectors.filter((sector) => sector.state === 'open' || sector.state === 'known'), [sectors]);

  // The systems of the chosen sector: the ones a radar of the player reaches and the ones with a
  // body of the player in them.
  const loadSector = useCallback(
    (): Promise<MapSectorView | null> => (value.targetSectorId === null ? Promise.resolve(null) : map.sector(value.targetSectorId, null)),
    [map, value.targetSectorId],
  );
  const sectorRead = useRead(loadSector, value.targetSectorId !== null, false);

  const systems = useMemo(() => (sectorRead.data === null ? [] : visibleSystems(sectorRead.data.systems)), [sectorRead.data]);

  // The bodies of the chosen system: the ones the radar shows and the ones he has explored.
  const loadSystem = useCallback(
    (): Promise<MapSystemView | null> => (value.targetSystemId === null ? Promise.resolve(null) : map.system(value.targetSystemId, null)),
    [map, value.targetSystemId],
  );
  const systemRead = useRead(loadSystem, value.targetSystemId !== null, false);

  // The planets of the chosen system and the asteroid field of it, when it has one.
  const objects = useMemo(() => (systemRead.data === null ? [] : planetBodies(systemRead.data.objects)), [systemRead.data]);

  // The objects of the chosen sector that belong to no system: the free fields and the nebulas a
  // radar of the player reaches (the server sends only the fields a radar sees).
  const fields = useMemo(
    () => [
      ...(sectorRead.data?.fields ?? []).map((field) => ({ objectId: field.objectId, name: field.name })),
      ...(sectorRead.data?.nebulas ?? [])
        .filter((nebula) => nebula.state === 'open')
        .map((nebula) => ({ objectId: nebula.objectId, name: nebula.name })),
    ],
    [sectorRead.data],
  );

  // A change of the kind of the target drops what was chosen for the other kind.
  const changeKind = (kind: TargetKind) => {
    onChange({
      targetKind: kind,
      target: '',
      targetSectorId: kind === 'point' ? null : value.targetSectorId,
      targetSystemId: null,
      targetObjectId: null,
    });
  };

  // A change of the sector or of the system drops what was chosen below it, for the choice of a
  // system of another sector makes no sense.
  const changeSector = (nextSectorId: number) => {
    onChange({ targetSectorId: nextSectorId, targetSystemId: null, targetObjectId: null });
  };

  const changeSystem = (nextSystemId: number) => {
    onChange({ targetSystemId: nextSystemId, targetObjectId: null });
  };

  return (
    <>
      <div className="pl-order__row">
        <label className="fl-label" htmlFor={`plan-order-target-kind-${id}`}>
          {t('plan.target')}
        </label>
        <select
          className="fl-input"
          id={`plan-order-target-kind-${id}`}
          onChange={(event) => changeKind(event.target.value as TargetKind)}
          value={value.targetKind}
        >
          <option value="point">{t('plan.targetPoint')}</option>
          <option value="planet">{t('plan.targetPlanet')}</option>
          <option value="object">{t('plan.targetObject')}</option>
          <option value="system">{t('plan.targetEdge')}</option>
        </select>
      </div>

      {value.targetKind === 'point' ? (
        <div className="pl-order__row">
          <label className="fl-label" htmlFor={`plan-order-target-${id}`}>
            {t('plan.coordinates')}
          </label>
          <input
            autoComplete="off"
            className="fl-input"
            id={`plan-order-target-${id}`}
            onChange={(event) => onChange({ target: event.target.value })}
            placeholder={t('plan.coordinatesHint')}
            value={value.target}
          />
        </div>
      ) : null}

      {value.targetKind === 'planet' || value.targetKind === 'system' ? (
        <>
          <div className="pl-order__row pl-order__row--places">
            <label className="fl-label" htmlFor={`plan-order-sector-${id}`}>
              {t('plan.sector')}
            </label>
            <select
              className="fl-input"
              id={`plan-order-sector-${id}`}
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

            <label className="fl-label" htmlFor={`plan-order-system-${id}`}>
              {t('plan.system')}
            </label>
            <select
              className="fl-input"
              disabled={value.targetSectorId === null}
              id={`plan-order-system-${id}`}
              onChange={(event) => changeSystem(Number(event.target.value))}
              value={value.targetSystemId ?? ''}
            >
              <option value="">{t('plan.systemAny')}</option>
              {systems.map((system) => (
                <option key={system.id} value={system.id}>
                  {systemText(system)}
                </option>
              ))}
            </select>

            {value.targetKind === 'planet' ? (
              <>
                <label className="fl-label" htmlFor={`plan-order-object-${id}`}>
                  {t('plan.planet')}
                </label>
                <select
                  className="fl-input"
                  disabled={value.targetSystemId === null}
                  id={`plan-order-object-${id}`}
                  onChange={(event) => onChange({ targetObjectId: event.target.value === '' ? null : Number(event.target.value) })}
                  value={value.targetObjectId ?? ''}
                >
                  <option value="">{t('plan.planetAny')}</option>
                  {objects.map((object) => (
                    <option key={object.id} value={object.id}>
                      {objectText(object)}
                    </option>
                  ))}
                </select>
              </>
            ) : null}
          </div>
        </>
      ) : null}

      {value.targetKind === 'object' ? (
        <div className="pl-order__row pl-order__row--places pl-order__row--places-2">
          <label className="fl-label" htmlFor={`plan-order-field-sector-${id}`}>
            {t('plan.sector')}
          </label>
          <select
            className="fl-input"
            id={`plan-order-field-sector-${id}`}
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

          <label className="fl-label" htmlFor={`plan-order-field-${id}`}>
            {t('plan.field')}
          </label>
          <select
            className="fl-input"
            disabled={value.targetSectorId === null}
            id={`plan-order-field-${id}`}
            onChange={(event) => onChange({ targetObjectId: event.target.value === '' ? null : Number(event.target.value) })}
            value={value.targetObjectId ?? ''}
          >
            <option value="">{t('plan.fieldAny')}</option>
            {fields.map((field) => (
              <option key={field.objectId} value={field.objectId}>
                {field.name}
              </option>
            ))}
          </select>
        </div>
      ) : null}

      <FlightPlanTime
        fleet={fleet}
        from={from}
        target={{
          kind: value.targetKind,
          sectorId: value.targetSectorId,
          objectId: value.targetObjectId,
          point: value.target,
          systemId: value.targetSystemId,
        }}
        typeCode={typeCode}
      />
    </>
  );
}
