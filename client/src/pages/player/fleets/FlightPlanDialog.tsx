import { useCallback, useMemo, useState } from 'react';
import type { FlightPlan, FlightPlanOrder, FlightPlanOrderInput, Fleet } from '../../../api/fleetApi';
import { useApi } from '../../../api/useApi';
import { Alert } from '../../../components/Alert';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import type { MessageKey } from '../../../i18n/uk';
import { useRead } from '../market/useRead';
import { useBodies } from '../useBodies';
import { FleetDialog } from './FleetDialog';
import { FlightMineTime } from './FlightMineTime';
import { FlightPlanTime } from './FlightPlanTime';
import { FlightTargetFields } from './FlightTargetFields';
import { FlightCargoFields } from './FlightCargoFields';
import { fleetExtracts, fleetOpenedOrders, fleetScanCapabilities } from './fleetExtraction';
import { formatDuration } from '../planetText';
import { cargoItemsValid, cargoMass, holdBefore, holdContentOf, holdMass, readCargoItems, unloadDefaults } from './flightPlanCargo';
import { readAxes, toOrderInputs, type DraftOrder, type FlightTimeTarget, type TargetKind } from './flightPlanParts';

/**
 * The types of an order of a flight plan, as the client sends them (task 00050). Only the type of
 * an order decides what the player has to fill in: the flights name a target and last a while,
 * and the return names the home body of the fleet and lasts a while. The order of extraction names
 * no target (task 00054): it has the time only and takes from the place where the fleet stands.
 *
 * The order of extraction is offered only to a fleet that may extract: one of its ships has a
 * module of extraction.
 */
const orderTypes: readonly { code: string; label: MessageKey; fields: OrderFields }[] = [
  { code: 'fly', label: 'plan.order.fly', fields: { target: true, duration: true } },
  { code: 'hyperfly', label: 'plan.order.hyperfly', fields: { target: true, duration: true } },
  { code: 'jump', label: 'plan.order.jump', fields: { target: true, duration: true } },
  { code: 'mine', label: 'plan.order.mine', fields: { duration: true } },
  { code: 'scan', label: 'plan.order.scan', fields: {} },
  { code: 'load', label: 'plan.order.load', fields: { cargo: true } },
  { code: 'unload', label: 'plan.order.unload', fields: { cargo: true } },
  { code: 'wait', label: 'plan.order.wait', fields: {} },
  { code: 'return', label: 'plan.order.return', fields: { duration: true } },
];

/**
 * The orders that a module of the fleet opens: the fleet is offered them only when it has the module (task 00058).
 */
const moduleOrders: readonly string[] = ['hyperfly', 'jump', 'scan'];

/**
 * What the player has to fill in for an order (task 00050): a field that is not named is not shown.
 */
interface OrderFields {
  /** Whether the order names a target. */
  target?: boolean;
  /** Whether the order lasts a while. */
  duration?: boolean;
  /** Whether the order carries a list of what to load or to put out (task 00057). */
  cargo?: boolean;
}

/**
 * The target the map gave to the window of the flight plan (task 00050).
 */
export interface FlightPlanSeed {
  /** The kind of the target the map gave: `point`, `planet` or `object`. */
  targetKind?: TargetKind;
  /** The sector of the target, when the map gave one. */
  targetSectorId?: number;
  /** The system of the target, when the map gave one. */
  targetSystemId?: number;
  /** The object of the target, when the map gave one. */
  targetObjectId?: number;
  /** The first axis of the target, when the map gave a point. */
  targetX?: number;
  /** The second axis of the target, when the map gave a point. */
  targetY?: number;
  /** The third axis of the target, when the map gave a point. */
  targetZ?: number;
}

/**
 * The window that writes the flight plan of a fleet (task 00050): the fleet the plan is made for,
 * the orders of the plan with their type, their target and how long they last. The window is
 * opened from the screen of the fleets and from the card of the map, and it is the same window in
 * both places.
 *
 * @param fleets The fleets of the player; the plan is made for one of them.
 * @param sectorId The sector the fleets stand in; the places of the sector may be named as targets.
 * @param plan The plan that is being changed, or `null` when a new one is written.
 * @param initialFleetId The fleet selected on the fleets screen, if any.
 * @param seed The target the map gave, or `null`.
 * @param busy Whether a command is under way.
 * @param error The refusal of the last attempt, or `null`.
 * @param onSave Writes the plan.
 * @param onExecute Puts the plan under way.
 * @param onClose Closes the window.
 */
export function FlightPlanDialog({
  fleets,
  sectorId,
  plan,
  initialFleetId,
  seed,
  busy,
  error,
  onSave,
  onExecute,
  onClose,
}: {
  fleets: readonly Fleet[];
  sectorId: number | null;
  plan: FlightPlan | null;
  initialFleetId?: number | null;
  seed: FlightPlanSeed | null;
  busy: boolean;
  error: string | null;
  onSave: (fleetId: number, orders: FlightPlanOrderInput[]) => void;
  onExecute: (fleetId: number, orders: FlightPlanOrderInput[]) => void;
  onClose: () => void;
}) {
  const { t, language } = useI18n();
  const { map, fleet: fleetApi } = useApi();
  const { bodies } = useBodies();
  const [fleetId, setFleetId] = useState<number | null>(() => plan?.fleetId ?? initialFleetId ?? (fleets.length === 1 ? fleets[0]?.id ?? null : null));
  const [coordinatesBad, setCoordinatesBad] = useState(false);
  const [targetBad, setTargetBad] = useState(false);
  const [cargoBad, setCargoBad] = useState(false);
  const [cargoOverflow, setCargoOverflow] = useState(false);

  // The sectors of the galaxy, so the player may choose the sector of the target. The list of the
  // sectors does not change, so the tab of the browser does not read it again.
  const loadGalaxy = useCallback(() => map.galaxy(), [map]);
  const galaxyRead = useRead(loadGalaxy, true, false);

  // The fleet the plan is made for: its ships give the speed of the flight.
  const chosenFleet = useMemo(() => fleets.find((entry) => entry.id === fleetId) ?? null, [fleets, fleetId]);

  // The projects of the ships of the fleet: they give the mass and the push of every ship, and so
  // the speed of the fleet. The list of the projects does not change, so the tab of the browser does
  // not read it again.
  const loadDesigns = useCallback(() => fleetApi.designs(), [fleetApi]);
  const designsRead = useRead(loadDesigns, chosenFleet !== null, false);

  // The catalog of the ships: it names the group of every module, and so it tells whether a ship
  // of the fleet carries a module of extraction. The catalog does not change, so the tab of the
  // browser does not read it again.
  const loadCatalog = useCallback(
    () => (chosenFleet === null ? Promise.resolve(null) : fleetApi.shipCatalog(chosenFleet.homePlanetId)),
    [fleetApi, chosenFleet],
  );
  const catalogRead = useRead(loadCatalog, chosenFleet !== null, false);

  // What the holds of the fleet carry now: an unload starts with it, and a load is checked against its room.
  // The hold is read once, when the fleet is chosen (task 00057).
  const loadHold = useCallback(() => (fleetId === null ? Promise.resolve(null) : fleetApi.fleetHold(fleetId)), [fleetApi, fleetId]);
  const holdRead = useRead(loadHold, fleetId !== null, false);
  const baseHold = useMemo(() => holdContentOf(holdRead.data), [holdRead.data]);

  // The names and the weights of the modules, from the catalog, for the lists of the load and of the unload.
  const partNames = useMemo(() => new Map((catalogRead.data?.modules ?? []).map((module) => [module.id, module.nameKey])), [catalogRead.data]);
  const partWeights = useMemo(() => new Map((catalogRead.data?.modules ?? []).map((module) => [module.id, module.weight])), [catalogRead.data]);
  const holdCapacity = holdRead.data?.capacity ?? chosenFleet?.holdCapacity ?? null;

  // Whether the fleet may extract: one of its ships has a module of extraction. The order of
  // extraction is offered only then.
  const mayExtract = useMemo(
    () => fleetExtracts(chosenFleet, designsRead.data ?? [], catalogRead.data),
    [chosenFleet, designsRead.data, catalogRead.data],
  );
  const scanCapabilities = useMemo(() => fleetScanCapabilities(chosenFleet, designsRead.data ?? [], catalogRead.data), [chosenFleet, designsRead.data, catalogRead.data]);

  // The sector the fleet stands in: it is the sector of the target of every new order.
  const fleetSectorId = useMemo(() => chosenFleet?.sectorId ?? sectorId, [chosenFleet, sectorId]);

  const [orders, setOrders] = useState<DraftOrder[]>(() => draftOf(plan, seed, fleetSectorId));

  // The orders the modules of the fleet open: the hyperflight, the jump and the scan are offered only to a fleet that
  // has the module (task 00058).
  const openedOrders = useMemo(
    () => fleetOpenedOrders(chosenFleet, designsRead.data ?? [], catalogRead.data),
    [chosenFleet, designsRead.data, catalogRead.data],
  );

  // The types of the orders the player may choose: the order of extraction is hidden from a fleet that may not
  // extract, and an order a module opens is hidden from a fleet without the module. A plan that is being changed
  // keeps the type of an order it holds, so a type stays in the list as long as the plan holds an order of it.
  const offeredTypes = useMemo(
    () =>
      orderTypes.filter((type) => {
        if (orders.some((order) => order.typeCode === type.code)) {
          return true;
        }

        if (type.code === 'mine') {
          return mayExtract;
        }

        return !moduleOrders.includes(type.code) || openedOrders.has(type.code);
      }),
    [mayExtract, openedOrders, orders],
  );

  // The home body of the fleet: the return of the fleet goes back to it.
  const homeName = useMemo(() => {
    const entry = fleets.find((item) => item.id === fleetId);
    const home = entry === undefined ? undefined : bodies.find((item) => item.planetId === entry.homePlanetId);

    return home?.name?.trim() || (entry === undefined ? null : String(entry.homePlanetId));
  }, [bodies, fleetId, fleets]);

  const needsFleet = fleetId === null;
  const ready = fleetId !== null && orders.length > 0;

  const addOrder = () => {
    setOrders((current) => [
      ...current,
      {
        typeCode: 'fly',
        targetKind: 'point',
        // The sector of the fleet is the sector of the target by default.
        targetSectorId: fleetSectorId,
        targetSystemId: null,
        targetObjectId: null,
        target: '',
        durationMinutes: '',
        items: [],
      },
    ]);
  };

  const changeOrder = (index: number, patch: Partial<DraftOrder>) => {
    setOrders((current) =>
      current.map((order, place) => {
        if (place !== index) {
          return order;
        }

        const next = { ...order, ...patch };
        const fields = orderTypes.find((type) => type.code === next.typeCode)?.fields;

        // A new type of a load or an unload starts its list anew: an unload with everything the holds carry (or are
        // meant to carry after the loads before it), a load with nothing, and any other type with no list (task 00057).
        if (patch.typeCode !== undefined && patch.typeCode !== order.typeCode) {
          next.items = patch.typeCode === 'unload' ? unloadDefaults(holdBefore(current, index, baseHold)) : [];
        }

        // The fields of the type that are gone carry nothing: an order of a type without a target
        // names no target, and an order that does not last has no duration.
        if (fields && !fields.target) {
          next.targetKind = 'point';
          next.target = '';
          next.targetSectorId = null;
          next.targetSystemId = null;
          next.targetObjectId = null;
        }

        if (fields && !fields.duration) {
          next.durationMinutes = '';
        }

        if (fields && !fields.cargo) {
          next.items = [];
        }

        return next;
      }),
    );
  };

  const removeOrder = (index: number) => {
    setOrders((current) => current.filter((_, place) => place !== index));
  };

  const moveOrder = (index: number, step: number) => {
    setOrders((current) => {
      const next = [...current];
      const to = index + step;

      if (to < 0 || to >= next.length) {
        return current;
      }

      const moved = next[index]!;

      next[index] = next[to]!;
      next[to] = moved;

      return next;
    });
  };

  const write = (done: (fleetId: number, orders: FlightPlanOrderInput[]) => void) => {
    if (fleetId === null) {
      return;
    }

    // Only the type of an order decides what is checked: a type without a target names none.
    const needsTarget = (order: DraftOrder): boolean => {
      const type = orderTypes.find((entry) => entry.code === order.typeCode);

      return type === undefined || type.fields.target === true;
    };

    // A point of the space is written as three numbers, so the text is read before the plan is sent.
    const bad = orders.some(
      (order) => needsTarget(order) && order.targetKind === 'point' && order.target !== '' && readAxes(order.target) === null,
    );

    // A target of a place names the place: a planet needs a sector, a system and a body, and an
    // object needs a sector and a body.
    const unknown = orders.some((order) => {
      if (!needsTarget(order) || order.targetKind === 'point') {
        return false;
      }

      if (order.targetKind === 'planet') {
        return order.targetSectorId === null || order.targetSystemId === null || order.targetObjectId === null;
      }

      return order.targetSectorId === null || order.targetObjectId === null;
    });

    // An order of a load and of an unload needs its list whole: every item names a thing and an amount (task 00057).
    const cargoOrders = orders.filter((order) => order.typeCode === 'load' || order.typeCode === 'unload');
    const cargoWrong = cargoOrders.some((order) => !cargoItemsValid(order.items));

    // A load is checked against the room that is left in the holds, as far as the orders before it tell.
    const overflow =
      holdCapacity !== null &&
      orders.some((order, index) => {
        if (order.typeCode !== 'load') {
          return false;
        }

        const free = Math.max(0, holdCapacity - holdMass(holdBefore(orders, index, baseHold), (entry) => (entry.kind === 'resource' ? 1 : (partWeights.get(entry.id) ?? 0))));

        return cargoMass(order.items, partWeights) > free;
      });

    setCoordinatesBad(bad);
    setTargetBad(unknown);
    setCargoBad(cargoWrong);
    setCargoOverflow(overflow);

    if (!bad && !unknown && !cargoWrong && !overflow) {
      done(fleetId, toOrderInputs(orders));
    }
  };

  const orderRows = useMemo(
    () =>
      orders.map((order, index) => {
        // The fields of the type of the order are looked up in the whole list of the types, not in
        // the offered ones: a plan that is being changed may hold an order of extraction even when
        // the fleet may not extract now.
        const type = orderTypes.find((entry) => entry.code === order.typeCode);

        return {
          order,
          index,
          fields: type?.fields ?? { target: true },
        };
      }),
    [orders],
  );

  return (
    <FleetDialog
      actions={
        <>
          <button className="btn fl-btn" onClick={onClose} type="button">
            {t('fleets.cancel')}
          </button>
          <button className="btn btn--secondary" disabled={busy || !ready} onClick={() => write(onExecute)} title={ready ? undefined : t('plan.executeNeedsOrders')} type="button">
            {t('plan.execute')}
          </button>
          <button className="btn btn--primary" disabled={busy || !ready} onClick={() => write(onSave)} type="button">
            {plan === null ? t('plan.save') : t('plan.edit')}
          </button>
        </>
      }
      onClose={onClose}
      title={plan ? t('plan.editTitle') : t('plan.createTitle')}
    >
      <div className="fl-field fl-field--row">
        <label className="fl-label" htmlFor="plan-fleet">
          {t('plan.chooseFleet')}
        </label>
        {fleets.length === 0 ? (
          <p className="fl-note">{t('plan.noFleets')}</p>
        ) : (
          <select
            className="fl-input"
            id="plan-fleet"
            onChange={(event) => setFleetId(Number(event.target.value))}
            value={fleetId ?? ''}
          >
            {fleetId === null ? <option disabled value="">{t('plan.chooseFleet')}</option> : null}
            {fleets.map((fleet) => (
              <option key={fleet.id} value={fleet.id}>
                {fleet.name} · {t('fleets.shipsCount', { count: fleet.ships.length })}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="fl-field">
        <span className="fl-label">{t('plan.orders')}</span>
        {orders.length === 0 ? (
          <p className="fl-note">{t('plan.noOrders')}</p>
        ) : (
          <ul className="pl-orders">
            {orderRows.map(({ order, index, fields }) => (
              <li className="pl-order" key={index}>
                <div className="pl-order__fields">
                  <div className="pl-order__row">
                    <label className="fl-label" htmlFor={`plan-order-type-${index}`}>
                      {t('plan.orderType')}
                    </label>
                    <select
                      className="fl-input"
                      id={`plan-order-type-${index}`}
                      onChange={(event) => changeOrder(index, { typeCode: event.target.value })}
                      value={order.typeCode}
                    >
                      {offeredTypes.map((type) => (
                        <option key={type.code} value={type.code}>
                          {t(type.label)}
                        </option>
                      ))}
                    </select>
                  </div>

                  {order.typeCode === 'return' ? (
                    <div className="pl-order__row">
                      <label className="fl-label" htmlFor={`plan-order-home-${index}`}>
                        {t('plan.home')}
                      </label>
                      <input
                        className="fl-input"
                        id={`plan-order-home-${index}`}
                        readOnly
                        value={homeName ?? ''}
                      />
                    </div>
                  ) : null}

                  {order.typeCode === 'return' ? (
                    <FlightPlanTime fleet={chosenFleet} from={placeBefore(orders, index)} typeCode="return" />
                  ) : null}

                  {order.typeCode === 'mine' ? (
                    <FlightMineTime
                      durationMinutes={order.durationMinutes}
                      id={String(index)}
                      onChange={(durationMinutes) => changeOrder(index, { durationMinutes })}
                    />
                  ) : null}
                  {order.typeCode === 'scan' && scanCapabilities ? <p className="fl-note">{t('plan.scanDetails', { radius: scanCapabilities.radius, duration: formatDuration(language, scanCapabilities.duration) })}</p> : null}

                  {fields.cargo && (order.typeCode === 'load' || order.typeCode === 'unload') ? (
                    <FlightCargoFields
                      capacity={holdCapacity}
                      content={holdBefore(orders, index, baseHold)}
                      id={String(index)}
                      items={order.items}
                      onChange={(items) => changeOrder(index, { items })}
                      partNames={partNames}
                      partWeights={partWeights}
                      planetId={planetBefore(orders, index, chosenFleet, bodies)}
                      typeCode={order.typeCode}
                    />
                  ) : null}

                  {fields.target ? (
                    <FlightTargetFields
                      fleet={chosenFleet}
                      from={placeBefore(orders, index)}
                      id={String(index)}
                      onChange={(patch) => changeOrder(index, patch)}
                      sectors={galaxyRead.data?.sectors ?? []}
                      value={{
                        targetKind: order.targetKind,
                        targetSectorId: order.targetSectorId,
                        targetSystemId: order.targetSystemId,
                        targetObjectId: order.targetObjectId,
                        target: order.target,
                      }}
                    />
                  ) : null}
                </div>

                <div className="pl-order__tools">
                  <button
                    aria-label={t('plan.moveUp')}
                    className="btn fl-btn fl-btn--small pl-tool"
                    disabled={index === 0}
                    onClick={() => moveOrder(index, -1)}
                    type="button"
                  >
                    <GameIcon name="arrowUp" size={14} />
                  </button>
                  <button
                    aria-label={t('plan.moveDown')}
                    className="btn fl-btn fl-btn--small pl-tool"
                    disabled={index === orders.length - 1}
                    onClick={() => moveOrder(index, 1)}
                    type="button"
                  >
                    <GameIcon name="arrowDown" size={14} />
                  </button>
                  <button
                    aria-label={t('plan.removeOrder')}
                    className="btn fl-btn fl-btn--small pl-tool fl-btn--danger-solid"
                    onClick={() => removeOrder(index)}
                    type="button"
                  >
                    <GameIcon name="close" size={14} />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
        <button className="btn fl-btn fl-btn--small" onClick={addOrder} type="button">
          <GameIcon name="mapPlus" size={16} />
          {t('plan.addOrder')}
        </button>
        {coordinatesBad ? <p className="pl-error">{t('plan.coordinatesBad')}</p> : null}
        {targetBad ? <p className="pl-error">{t('plan.targetUnknown')}</p> : null}
        {cargoBad ? <p className="pl-error">{t('plan.cargo.itemsRequired')}</p> : null}
        {cargoOverflow ? <p className="pl-error">{t('plan.cargo.overflowSave')}</p> : null}
      </div>

      {needsFleet ? <p className="fl-note">{t('plan.fleetRequired')}</p> : null}
      {error ? <Alert kind="error">{error}</Alert> : null}
    </FleetDialog>
  );
}

/**
 * The place the fleet is at when an order begins, as far as the orders before it tell (task 00053): the
 * target of the last flight before it; an extraction leaves the fleet where it is (task 00054). The return
 * and an order that moves the fleet to a place the window cannot name leave the place unknown, and the time
 * is then counted from the place the fleet is at now.
 *
 * @param orders The orders of the window.
 * @param index The position of the order.
 * @returns The place before the order, or `null` when it is the place the fleet is at now or is unknown.
 */
function placeBefore(orders: readonly DraftOrder[], index: number): FlightTimeTarget | null {
  for (let place = index - 1; place >= 0; place -= 1) {
    const order = orders[place]!;

    if (order.typeCode === 'return') {
      return null;
    }

    if (order.typeCode === 'fly' || order.typeCode === 'hyperfly' || order.typeCode === 'jump') {
      return {
        kind: order.targetKind,
        sectorId: order.targetSectorId,
        objectId: order.targetObjectId,
        point: order.target,
      };
    }
  }

  return null;
}

/**
 * The body the fleet stands on when an order begins, as far as the orders before it tell (task 00057): the home
 * body after a return, a body of the player the last flight goes to, and the body the fleet stands on now when no
 * order before moves it. A flight to a point or to a free object, or to a body that is not the player's, leaves the
 * body unknown: a fleet there cannot load, and the window shows the whole list of resources.
 *
 * @param orders The orders of the window.
 * @param index The position of the order.
 * @param fleet The fleet the plan is made for.
 * @param bodies The bodies of the player.
 * @returns The body, or `null` when the window does not know it.
 */
function planetBefore(
  orders: readonly DraftOrder[],
  index: number,
  fleet: Fleet | null,
  bodies: readonly { planetId: number }[],
): number | null {
  for (let place = index - 1; place >= 0; place -= 1) {
    const order = orders[place]!;

    if (order.typeCode === 'return') {
      return fleet?.homePlanetId ?? null;
    }

    if (order.typeCode === 'fly' || order.typeCode === 'hyperfly' || order.typeCode === 'jump') {
      return order.targetKind === 'planet' && order.targetObjectId !== null && bodies.some((body) => body.planetId === order.targetObjectId)
        ? order.targetObjectId
        : null;
    }
  }

  return fleet !== null && fleet.planetId > 0 ? fleet.planetId : null;
}

/**
 * The orders the window starts with: the orders of the plan that is being changed, or the one
 * order the map gave.
 *
 * @param plan The plan that is being changed, or `null`.
 * @param seed The target the map gave, or `null`.
 * @param sectorId The sector the fleets stand in; it is the sector of the target by default.
 * @returns The orders the window holds.
 */
function draftOf(plan: FlightPlan | null, seed: FlightPlanSeed | null, sectorId: number | null): DraftOrder[] {
  if (plan) {
    return plan.orders.map((order: FlightPlanOrder) => ({
      typeCode: order.typeCode,
      targetKind: order.targetKind === 'point' ? 'point' : order.targetKind === 'planet' ? 'planet' : 'object',
      targetSectorId: order.targetSectorId > 0 ? order.targetSectorId : sectorId,
      targetSystemId: order.targetSystemId > 0 ? order.targetSystemId : null,
      targetObjectId: order.targetObjectId > 0 ? order.targetObjectId : null,
      target: order.targetKind === 'point' ? `${order.targetX}, ${order.targetY}, ${order.targetZ}` : '',
      durationMinutes: order.durationSeconds > 0 ? String(Math.round(order.durationSeconds / 60)) : '',
      items: order.typeCode === 'load' || order.typeCode === 'unload' ? readCargoItems(order.parameters) : [],
    }));
  }

  if (seed) {
    const isPoint = seed.targetObjectId === undefined && seed.targetSystemId === undefined;
    const kind: TargetKind = seed.targetKind ?? (isPoint ? 'point' : seed.targetSystemId === undefined ? 'object' : 'planet');

    return [
      {
        typeCode: 'fly',
        targetKind: kind,
        targetSectorId: seed.targetSectorId ?? sectorId,
        targetSystemId: seed.targetSystemId ?? null,
        targetObjectId: seed.targetObjectId ?? null,
        target: isPoint ? `${seed.targetX ?? 0}, ${seed.targetY ?? 0}, ${seed.targetZ ?? 0}` : '',
        durationMinutes: '',
        items: [],
      },
    ];
  }

  return [];
}
