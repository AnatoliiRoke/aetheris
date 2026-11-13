import { useCallback, useMemo, useState } from 'react';
import type { FleetCargoItem, FleetHold } from '../../../api/fleetApi';
import { useApi } from '../../../api/useApi';
import { Alert } from '../../../components/Alert';
import { ResourceIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { useRead } from '../market/useRead';
import { formatNumber } from '../planetText';
import { useShellSelector } from '../useShell';
import { FleetDialog } from './FleetDialog';
import { FlightCargoFields } from './FlightCargoFields';
import { cargoItemsValid, holdContentOf, type CargoDraftItem } from './flightPlanCargo';

/**
 * The button «Show the hold» of a fleet with the window it opens (task 00056). The window is the only
 * thing that reads the hold, and it reads it when it opens, so the screen asks the server for nothing
 * until the player asks to look; opening and closing the window draws this part and nothing else.
 *
 * @param fleetId The fleet.
 * @param homePlanetId The home body of the fleet, to read the catalog that names the modules in the holds (task 00057).
 * @param planetId The body the fleet stands on now; the hold may be loaded and unloaded only on a body of its owner (task 00073).
 */
export function FleetHoldButton({ fleetId, homePlanetId, planetId }: { fleetId: number; homePlanetId?: number; planetId?: number }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button className="btn fl-btn fl-btn--block" onClick={() => setOpen(true)} type="button">
        {t('fleets.showHold')}
      </button>
      {open ? (
        <FleetHoldDialog fleetId={fleetId} homePlanetId={homePlanetId} planetId={planetId} onClose={() => setOpen(false)} />
      ) : null}
    </>
  );
}

/**
 * The window of the hold of a fleet: the room that is taken against the room there is, and what is in
 * the holds, the greatest amount first and then by name, each with the icon of its resource group.
 *
 * @param fleetId The fleet.
 * @param homePlanetId The home body of the fleet, for the catalog that names the modules.
 * @param planetId The body the fleet stands on now, or `undefined` when it is not known.
 * @param onClose Closes the window.
 */
function FleetHoldDialog({
  fleetId,
  homePlanetId,
  planetId,
  onClose,
}: {
  fleetId: number;
  homePlanetId?: number;
  planetId?: number;
  onClose: () => void;
}) {
  const { t, language } = useI18n();
  const { fleet: fleetApi } = useApi();
  const { name } = useTexts();
  const reference = useShellSelector((state) => state.reference);
  const load = useCallback(() => fleetApi.fleetHold(fleetId), [fleetApi, fleetId]);

  // The hold is read once, when the window opens: it is not read again when the tab becomes visible.
  const read = useRead<FleetHold>(load, true, false);
  const resources = useMemo(() => new Map((reference?.resources ?? []).map((resource) => [resource.id, resource])), [reference]);

  // The modules in the holds are named by the catalog of the ships; it is read once, only when the holds carry a module.
  const hasParts = (read.data?.parts?.length ?? 0) > 0;
  const loadCatalog = useCallback(
    () => (homePlanetId === undefined ? Promise.resolve(null) : fleetApi.shipCatalog(homePlanetId)),
    [fleetApi, homePlanetId],
  );
  const catalogRead = useRead(loadCatalog, hasParts && homePlanetId !== undefined, false);
  const partNames = useMemo(() => new Map((catalogRead.data?.modules ?? []).map((module) => [module.id, module.nameKey])), [catalogRead.data]);
  const partWeights = useMemo(() => new Map((catalogRead.data?.modules ?? []).map((module) => [module.id, module.weight])), [catalogRead.data]);
  const partRows = useMemo(
    () =>
      (read.data?.parts ?? []).map((part) => ({
        key: `${part.partTypeId}:${part.markLevel}:${part.hasRaceBonus ? 1 : 0}`,
        quantity: part.quantity,
        label: `${name(partNames.get(part.partTypeId), String(part.partTypeId))} · ${t('shipyard.mark', { mark: part.markLevel })}${part.hasRaceBonus ? ' ★' : ''}`,
      })),
    [name, partNames, read.data, t],
  );

  const rows = useMemo(
    () =>
      (read.data?.items ?? [])
        .map((item) => {
          const resource = resources.get(item.resourceId);

          return {
            resourceId: item.resourceId,
            amount: item.amount,
            groupCode: resource?.groupCode ?? null,
            label: name(resource?.nameKey, String(item.resourceId)),
          };
        })
        .sort((first, second) => second.amount - first.amount || first.label.localeCompare(second.label, language)),
    [language, name, read.data, resources],
  );

  // The commands of the hold (task 00073): the hold is loaded and unloaded only when the fleet stands
  // on a body of its own, and the fleet is under way nowhere else.
  const onOwnBody = planetId !== undefined && planetId > 0;
  const [cargoMode, setCargoMode] = useState<'load' | 'unload' | null>(null);
  const [cargoItems, setCargoItems] = useState<CargoDraftItem[]>([]);
  const [cargoBusy, setCargoBusy] = useState(false);
  const [cargoError, setCargoError] = useState<string | null>(null);

  const baseHold = useMemo(() => holdContentOf(read.data), [read.data]);

  const openCargo = (mode: 'load' | 'unload') => {
    setCargoError(null);
    setCargoItems([]);
    setCargoMode(mode);
  };

  const sendCargo = async () => {
    if (cargoMode === null || !cargoItemsValid(cargoItems)) {
      return;
    }

    const items: FleetCargoItem[] = cargoItems.flatMap((item) => {
      const amount = Number(item.amount);

      if (item.id === null || !Number.isInteger(amount) || amount <= 0) {
        return [];
      }

      return [{ kind: item.kind, id: item.id, markLevel: item.markLevel, hasRaceBonus: item.hasRaceBonus, amount }];
    });

    setCargoBusy(true);
    setCargoError(null);

    try {
      const request = { fleetId, items };

      if (cargoMode === 'load') {
        await fleetApi.cargoLoad(request);
      } else {
        await fleetApi.cargoUnload(request);
      }

      setCargoMode(null);
      setCargoItems([]);

      // The hold of the fleet has changed, so the window reads it again.
      void read.reload();
    } catch (error) {
      setCargoError(error instanceof Error ? error.message : t('fleets.holdFailed'));
    } finally {
      setCargoBusy(false);
    }
  };

  return (
    <FleetDialog
      lead={
        read.data
          ? t('fleets.holdUsed', {
              used: formatNumber(read.data.carried, language),
              capacity: formatNumber(read.data.capacity, language),
            })
          : null
      }
      onClose={onClose}
      title={t('fleets.holdTitle')}
    >
      {read.failed && !read.data ? <Alert kind="error">{t('fleets.holdFailed')}</Alert> : null}
      {read.loading ? <p className="fl-note">{t('fleets.holdLoading')}</p> : null}
      {read.data && rows.length === 0 && partRows.length === 0 ? <p className="fl-note">{t('fleets.holdEmpty')}</p> : null}
      {rows.length > 0 ? (
        <ul aria-label={t('fleets.holdTitle')} className="fl-hold">
          {rows.map((row) => (
            <li className="fl-hold__item" key={row.resourceId}>
              <span className="fl-hold__name">
                <ResourceIcon groupCode={row.groupCode} />
                <span>{row.label}</span>
              </span>
              <b className="fl-hold__amount">{t('fleets.tonnes', { amount: formatNumber(row.amount, language) })}</b>
            </li>
          ))}
        </ul>
      ) : null}
      {partRows.length > 0 ? (
        <ul aria-label={t('fleets.holdParts')} className="fl-hold">
          {partRows.map((row) => (
            <li className="fl-hold__item" key={row.key}>
              <span className="fl-hold__name">
                <span>{row.label}</span>
              </span>
              <b className="fl-hold__amount">×{formatNumber(row.quantity, language)}</b>
            </li>
          ))}
        </ul>
      ) : null}

      {/*
      The commands of the hold (task 00073): the same rules as the orders of the plan, so the window
      shows the same form of the list of what to carry.
      */}
      <div className="fl-row fl-row--wrap">
        <button className="btn fl-btn fl-btn--small" disabled={!onOwnBody} onClick={() => openCargo('load')} type="button">
          {t('fleets.holdLoad')}
        </button>
        <button className="btn fl-btn fl-btn--small" disabled={!onOwnBody} onClick={() => openCargo('unload')} type="button">
          {t('fleets.holdUnload')}
        </button>
      </div>

      {cargoMode !== null ? (
        <div className="fl-field">
          <span className="fl-label">{cargoMode === 'load' ? t('fleets.holdLoadTitle') : t('fleets.holdUnloadTitle')}</span>
          <FlightCargoFields
            capacity={read.data?.capacity ?? null}
            content={baseHold}
            id="hold"
            items={cargoItems}
            onChange={setCargoItems}
            partNames={partNames}
            partWeights={partWeights}
            planetId={onOwnBody ? planetId! : null}
            typeCode={cargoMode}
          />
          {cargoError !== null ? <p className="pl-error">{cargoError}</p> : null}
          <div className="fl-row fl-row--wrap">
            <button className="btn fl-btn fl-btn--small" disabled={cargoBusy} onClick={() => setCargoMode(null)} type="button">
              {t('fleets.cancel')}
            </button>
            <button className="btn btn--primary" disabled={cargoBusy || !cargoItemsValid(cargoItems)} onClick={sendCargo} type="button">
              {t('fleets.holdCargoDone')}
            </button>
          </div>
        </div>
      ) : null}
    </FleetDialog>
  );
}
