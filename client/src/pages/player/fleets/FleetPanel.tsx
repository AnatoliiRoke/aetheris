import { useState, type FormEvent } from 'react';
import type { Fleet, FlightPlan } from '../../../api/fleetApi';
import { GameIcon } from '../../../components/GameIcon';
import type { Language } from '../../../i18n/context';
import { useI18n } from '../../../i18n/useI18n';
import type { MessageKey } from '../../../i18n/uk';
import { routes } from '../../../router/context';
import { Link } from '../../../router/Router';
import { formatNumber, hasKey } from '../planetText';
import { PlanetAvatar, StatePill } from './FleetBits';
import { FleetFlightStatus, type FleetsClock } from './FleetFlightStatus';
import { FleetHoldButton } from './FleetHold';
import { FleetPlace } from './FleetPlace';
import { FlightPlanPlace } from './FlightPlanPlace';
import { ProjectList } from './ProjectLists';
import { stateText } from './fleetsParts';
import { useFleetsScene } from './useFleetsScene';

/**
 * The orders of a fleet as the panel shows them: the type of the order (task 00050).
 */
const orderLabels: Record<string, MessageKey> = {
  fly: 'plan.order.fly',
  hyperfly: 'plan.order.hyperfly',
  jump: 'plan.order.jump',
  mine: 'plan.order.mine',
  scan: 'plan.order.scan',
  load: 'plan.order.load',
  unload: 'plan.order.unload',
  wait: 'plan.order.wait',
  return: 'plan.order.return',
};

/**
 * The orders that name a place to fly to (task 00050).
 */
const flightTypes: readonly string[] = ['fly', 'hyperfly', 'jump'];

/**
 * The states of a flight plan as the panel shows them (task 00050).
 */
const planStates: Record<string, MessageKey> = {
  draft: 'plan.state.draft',
  active: 'plan.state.active',
  paused: 'plan.state.paused',
};

/**
 * The panel of the chosen fleet (task 00040, mock-up `AETHERIS_fleets`): the name, which the player may
 * change, the home body and the place, the state, what the fleet carries and deals, the ships by class and
 * project with how many each has, the ways to add, take out and move ships by number, the block of the commander and
 * the flight plan of the fleet. The commander
 * is shown and is off, with the reason beside it, because it does not exist in the game yet.
 *
 * @param fleet The fleet.
 * @param plan The flight plan of the fleet, or `null` when it has none.
 * @param hasCredits Whether the civilization has credits.
 * @param busy Whether a command is under way.
 * @param canMove Whether another fleet stands on the same body, so a ship has somewhere to go.
 * @param onRename Renames the fleet; answers whether the server took the name.
 * @param onRemove Opens the window that takes ships out of the fleet.
 * @param onMove Opens the window that moves ships to another fleet.
 * @param onAdd Opens the window that adds ships.
 * @param onDisband Opens the window that disbands the fleet.
 * @param onPlanCreate Opens the window that writes a flight plan.
 * @param onPlanEdit Opens the window that changes the flight plan.
 * @param onPlanDelete Opens the window that deletes the flight plan.
 * @param onPlanStop Stops the flight plan.
 * @param onPlanExecute Puts the flight plan under way.
 * @param clock The clock of the game as the screen read it, for the time left of a flight.
 * @param onArrived What to do when the time left of a flight runs out.
 */
export function FleetPanel({
  fleet,
  plan,
  hasCredits,
  busy,
  canMove,
  onRename,
  onRemove,
  onMove,
  onAdd,
  onDisband,
  onPlanCreate,
  onPlanEdit,
  onPlanDelete,
  onPlanStop,
  onPlanExecute,
  clock,
  onArrived,
}: {
  fleet: Fleet;
  plan: FlightPlan | null;
  hasCredits: boolean;
  busy: boolean;
  canMove: boolean;
  onRename: (name: string) => Promise<boolean>;
  onRemove: () => void;
  onMove: () => void;
  onAdd: () => void;
  onDisband: () => void;
  onPlanCreate: () => void;
  onPlanEdit: () => void;
  onPlanDelete: () => void;
  onPlanStop: () => void;
  onPlanExecute: () => void;
  clock: FleetsClock | null;
  onArrived: () => void;
}) {
  const { t, language } = useI18n();
  const scene = useFleetsScene();
  const [renaming, setRenaming] = useState(false);
  const [name, setName] = useState(fleet.name);

  const submit = async (event: FormEvent) => {
    event.preventDefault();

    if (await onRename(name.trim())) {
      setRenaming(false);
    }
  };

  const body = scene.bodies.find((entry) => entry.planetId === fleet.planetId);

  // A fleet that is under way, or that stands anywhere but at a body of the player, does not give its ships
  // back: the buttons that change its ships wait until it is at home (the server checks it too).
  const away = fleet.stateCode !== 'stationed' || !scene.bodies.some((entry) => entry.planetId === fleet.planetId);
  const mapLink = body ? `${routes.map}?sector=${body.sectorId}&system=${body.systemId}&object=${fleet.planetId}` : routes.map;

  // The facts of the fleet, one line each. A fact whose value is nothing says nothing, so it is not
  // shown at all; the crew and the integrity are not shown at all.
  const facts: readonly { label: string; value: string; amount: number }[] = [
    {
      label: t('fleets.statCargo'),
      value: t('fleets.tonnes', { amount: formatNumber(fleet.holdCapacity, language) }),
      amount: fleet.holdCapacity,
    },
    {
      label: t('fleets.statDamage'),
      value:
        fleet.damageMin === fleet.damageMax
          ? formatNumber(fleet.damageMax, language)
          : t('fleets.damageRange', { min: formatNumber(fleet.damageMin, language), max: formatNumber(fleet.damageMax, language) }),
      amount: fleet.damageMax,
    },
    { label: t('fleets.statShield'), value: formatNumber(fleet.shieldCapacity, language), amount: fleet.shieldCapacity },
    {
      label: t('fleets.statUpkeep'),
      value: t('fleets.upkeepValue', { amount: formatNumber(fleet.upkeepPerHour, language) }),
      amount: fleet.upkeepPerHour,
    },
  ].filter((fact) => fact.amount > 0);

  return (
    <aside aria-label={t('fleets.details')} className="fl-panel">
      <div className="fl-panel__top">
        <div className="fl-panel__head">
          {renaming ? (
            <form className="fl-panel__rename" onSubmit={(event) => void submit(event)}>
              <label className="fl-label" htmlFor="fleet-rename">
                {t('fleets.fleetName')}
              </label>
              <input autoFocus className="fl-input" id="fleet-rename" onChange={(event) => setName(event.target.value)} value={name} />
              <div className="fl-actions">
                <button className="btn btn--primary" disabled={busy || name.trim() === ''} type="submit">
                  {t('fleets.save')}
                </button>
                <button
                  className="btn fl-btn"
                  onClick={() => {
                    setRenaming(false);
                    setName(fleet.name);
                  }}
                  type="button"
                >
                  {t('fleets.cancel')}
                </button>
              </div>
            </form>
          ) : (
            <>
              <h2 className="fl-panel__title">{fleet.name}</h2>
              <StatePill tone={hasCredits ? 'ok' : 'warn'}>{stateText(language, t, fleet, hasCredits)}</StatePill>
              <button
                aria-label={t('fleets.rename')}
                className="fl-iconbtn"
                onClick={() => {
                  setName(fleet.name);
                  setRenaming(true);
                }}
                title={t('fleets.rename')}
                type="button"
              >
                <GameIcon name="pencil" />
              </button>
            </>
          )}
        </div>
        <span className="fl-row">
          <PlanetAvatar planetId={fleet.homePlanetId} />
          <span>
            {t('fleets.home')}: <b>{scene.nameOf(fleet.homePlanetId)}</b>
          </span>
        </span>
        <span className="fl-row fl-row--soft">
          <FleetPlace fleet={fleet} />
        </span>
        {fleet.flight ? <FleetFlightStatus clock={clock} flight={fleet.flight} onArrived={onArrived} /> : null}
      </div>

      <dl className="fl-facts">
        {facts.map((fact) => (
          <div key={fact.label}>
            <dt>{fact.label}</dt>
            <dd>{fact.value}</dd>
          </div>
        ))}
      </dl>

      <section aria-labelledby="fleet-orders" className="fl-section">
        <h3 className="fl-section__title" id="fleet-orders">
          {t('plan.title')}
        </h3>
        {plan === null ? (
          <>
            <p className="fl-note">{t('plan.empty')}</p>
            <button className="btn fl-btn fl-btn--block" disabled={busy} onClick={onPlanCreate} type="button">
              <GameIcon name="mapRoute" size={18} />
              {t('plan.save')}
            </button>
          </>
        ) : (
          <>
            <p className="fl-note">
              {t(planStates[plan.stateCode] ?? 'plan.state.draft')}
            </p>
            {plan.pauseCode ? (
              <p className="pl-error">{t('plan.pausedBecause', { reason: reasonText(plan.pauseCode, language, t) })}</p>
            ) : null}
            {plan.orders.length === 0 ? (
              <p className="fl-note">{t('plan.noOrders')}</p>
            ) : (
              <ol className="pl-orders pl-orders--plain">
                {plan.orders.map((order) => (
                  <li
                    aria-current={plan.stateCode === 'active' && plan.currentPosition === order.position ? 'step' : undefined}
                    className={
                      plan.stateCode === 'active' && plan.currentPosition === order.position
                        ? 'pl-order pl-order--plain pl-order--current'
                        : 'pl-order pl-order--plain'
                    }
                    key={order.id}
                  >
                    <span className="pl-order__text">{t(orderLabels[order.typeCode] ?? 'plan.order.fly')}</span>
                    {flightTypes.includes(order.typeCode) && order.targetKind === 'point' ? (
                      <span className="pl-order__place">
                        → {order.targetX} / {order.targetY} / {order.targetZ}
                      </span>
                    ) : null}
                    {flightTypes.includes(order.typeCode) && order.targetKind !== 'point' ? (
                      <span className="pl-order__place">
                        →{' '}
                        <FlightPlanPlace
                          objectId={order.targetObjectId}
                          sectorId={order.targetSectorId}
                          systemId={order.targetSystemId}
                        />
                      </span>
                    ) : null}
                    {order.typeCode === 'return' ? <span className="pl-order__place">→ {scene.nameOf(fleet.homePlanetId)}</span> : null}
                  </li>
                ))}
              </ol>
            )}
            <div className="pl-tools">
              <button
                aria-label={t('plan.edit')}
                className="btn fl-btn fl-btn--icon"
                disabled={busy}
                onClick={onPlanEdit}
                title={t('plan.edit')}
                type="button"
              >
                <GameIcon name="pencil" size={18} />
              </button>
              <button
                aria-label={t('plan.execute')}
                className="btn fl-btn fl-btn--icon fl-btn--go"
                disabled={busy || plan.stateCode === 'active' || plan.orders.length === 0}
                onClick={onPlanExecute}
                title={plan.stateCode === 'active' ? t('plan.stateRunning') : plan.orders.length === 0 ? t('plan.executeNeedsOrders') : t('plan.execute')}
                type="button"
              >
                <GameIcon name="orderFly" size={18} />
              </button>
              <button
                aria-label={t('plan.stop')}
                className="btn fl-btn fl-btn--icon"
                disabled={busy || plan.stateCode !== 'active'}
                onClick={onPlanStop}
                title={plan.stateCode === 'active' ? t('plan.stop') : t('plan.stopIdle')}
                type="button"
              >
                <GameIcon name="clock" size={18} />
              </button>
              <button
                aria-label={t('plan.delete')}
                className="btn fl-btn fl-btn--icon fl-btn--danger"
                disabled={busy || plan.stateCode === 'active'}
                onClick={onPlanDelete}
                title={plan.stateCode === 'active' ? t('plan.deleteRunning') : t('plan.delete')}
                type="button"
              >
                <GameIcon name="trash" size={18} />
              </button>
            </div>
          </>
        )}
      </section>

      <section aria-labelledby="fleet-ships" className="fl-section">
        <h3 className="fl-section__title" id="fleet-ships">
          {t('fleets.shipsTitle')}
        </h3>
        <button
          className="btn fl-btn fl-btn--block"
          disabled={busy || away}
          onClick={onAdd}
          title={away ? t('fleets.awayReason') : undefined}
          type="button"
        >
          {t('fleets.addShips')}
        </button>
        <ProjectList ships={fleet.ships} />
      </section>

      {/*
      <section aria-labelledby="fleet-commander" className="fl-section">
        <h3 className="fl-section__title" id="fleet-commander">
          {t('fleets.commander')}
        </h3>
        <div className="fl-row fl-row--wrap fl-row--between">
          <span className="fl-note">{t('fleets.noCommander')}</span>
          <button className="btn fl-btn fl-btn--small" disabled title={t('fleets.commanderSoon')} type="button">
            {t('fleets.assignCommander')}
          </button>
        </div>
        <p className="fl-note">{t('fleets.commanderSoon')}</p>
      </section>
      */}

      <div className="fl-panel__buttons">
        <FleetHoldButton fleetId={fleet.id} />
        <button
          className="btn fl-btn fl-btn--block"
          disabled={busy || away}
          onClick={onRemove}
          title={away ? t('fleets.awayReason') : undefined}
          type="button"
        >
          {t('fleets.removeShips')}
        </button>
        <button
          className="btn fl-btn fl-btn--block"
          disabled={busy || away || !canMove}
          onClick={onMove}
          title={away ? t('fleets.awayReason') : canMove ? undefined : t('fleets.noFleetsHere')}
          type="button"
        >
          {t('fleets.moveShips')}
        </button>
        <Link className="btn fl-btn fl-btn--block" to={mapLink}>
          {t('fleets.showMap')}
        </Link>
        <button
          className="btn fl-btn fl-btn--block fl-btn--danger"
          disabled={busy || away}
          onClick={onDisband}
          title={away ? t('fleets.awayReason') : undefined}
          type="button"
        >
          {t('fleets.disband')}
        </button>
      </div>
    </aside>
  );
}

/**
 * The text of the code a plan stopped with: the refusal of the server that told why the order could not
 * begin, or the code itself when the interface has no words for it (task 00053).
 *
 * @param code The code.
 * @param language The language of the player.
 * @param translate The translator of the interface.
 * @returns The text.
 */
function reasonText(code: string, language: Language, translate: (key: MessageKey) => string): string {
  const key = `reason.${code}`;

  return hasKey(language, key) ? translate(key as MessageKey) : code;
}
