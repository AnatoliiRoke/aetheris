import { useState, type FormEvent } from 'react';
import type { Fleet } from '../../../api/fleetApi';
import { GameIcon, type GameIconName } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import type { MessageKey } from '../../../i18n/uk';
import { routes } from '../../../router/context';
import { Link } from '../../../router/Router';
import { formatNumber } from '../planetText';
import { PlanetAvatar, StatePill } from './FleetBits';
import { ProjectList } from './ProjectLists';
import { stateText } from './fleetsParts';
import { useFleetsScene } from './useFleetsScene';

/**
 * The orders of a fleet the mock-up shows. They are there and switched off with the reason: the
 * flights are a task of a later stage.
 */
const orders: readonly { id: string; icon: GameIconName; label: MessageKey }[] = [
  { id: 'fly', icon: 'orderFly', label: 'fleets.order.fly' },
  { id: 'mine', icon: 'orderMine', label: 'fleets.order.mine' },
  { id: 'attack', icon: 'orderAttack', label: 'fleets.order.attack' },
  { id: 'escort', icon: 'orderEscort', label: 'fleets.order.escort' },
  { id: 'colonize', icon: 'orderColonize', label: 'fleets.order.colonize' },
  { id: 'return', icon: 'orderHome', label: 'fleets.order.return' },
];

/**
 * The panel of the chosen fleet (task 00040, mock-up `AETHERIS_fleets`): the name, which the player may
 * change, the home body and the place, the state, what the fleet carries and deals, the ships by class and
 * project with how many each has, the ways to add, take out and move ships by number, the block of the commander and the block of the orders. The commander
 * and the orders are shown and are off, with the reason beside them, because neither exists in the game
 * yet.
 *
 * @param fleet The fleet.
 * @param hasCredits Whether the civilization has credits.
 * @param busy Whether a command is under way.
 * @param canMove Whether another fleet stands on the same body, so a ship has somewhere to go.
 * @param onRename Renames the fleet; answers whether the server took the name.
 * @param onRemove Opens the window that takes ships out of the fleet.
 * @param onMove Opens the window that moves ships to another fleet.
 * @param onAdd Opens the window that adds ships.
 * @param onDisband Opens the window that disbands the fleet.
 */
export function FleetPanel({
  fleet,
  hasCredits,
  busy,
  canMove,
  onRename,
  onRemove,
  onMove,
  onAdd,
  onDisband,
}: {
  fleet: Fleet;
  hasCredits: boolean;
  busy: boolean;
  canMove: boolean;
  onRename: (name: string) => Promise<boolean>;
  onRemove: () => void;
  onMove: () => void;
  onAdd: () => void;
  onDisband: () => void;
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
  const mapLink = body ? `${routes.map}?sector=${body.sectorId}&system=${body.systemId}&object=${fleet.planetId}` : routes.map;
  const facts: readonly { label: string; value: string }[] = [
    { label: t('fleets.statCargo'), value: t('fleets.tonnes', { amount: formatNumber(fleet.holdCapacity, language) }) },
    {
      label: t('fleets.statDamage'),
      value:
        fleet.damageMin === fleet.damageMax
          ? formatNumber(fleet.damageMax, language)
          : t('fleets.damageRange', { min: formatNumber(fleet.damageMin, language), max: formatNumber(fleet.damageMax, language) }),
    },
    { label: t('fleets.statIntegrity'), value: formatNumber(fleet.integrity, language) },
    { label: t('fleets.statShield'), value: formatNumber(fleet.shieldCapacity, language) },
    { label: t('fleets.statCrew'), value: formatNumber(fleet.crew, language) },
    { label: t('fleets.statUpkeep'), value: t('fleets.upkeepValue', { amount: formatNumber(fleet.upkeepPerHour, language) }) },
  ];

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
        <span className="fl-row fl-row--soft">{t('fleets.place', { place: scene.placeOf(fleet.planetId) })}</span>
        <div className="fl-row fl-row--wrap">
          <StatePill tone={hasCredits ? 'ok' : 'warn'}>{stateText(language, t, fleet, hasCredits)}</StatePill>
          <Link className="btn fl-btn fl-btn--small" to={mapLink}>
            {t('fleets.showMap')}
          </Link>
        </div>
      </div>

      <dl className="fl-facts">
        {facts.map((fact) => (
          <div key={fact.label}>
            <dt>{fact.label}</dt>
            <dd>{fact.value}</dd>
          </div>
        ))}
      </dl>

      <section aria-labelledby="fleet-ships" className="fl-section">
        <h3 className="fl-section__title" id="fleet-ships">
          {t('fleets.shipsTitle')}
        </h3>
        <ProjectList ships={fleet.ships} />
      </section>

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

      <section aria-labelledby="fleet-orders" className="fl-section">
        <h3 className="fl-section__title" id="fleet-orders">
          {t('fleets.ordersTitle')}
        </h3>
        <div className="fl-orders">
          {orders.map((order) => (
            <button className="btn fl-btn fl-btn--order" disabled key={order.id} title={t('fleets.ordersSoon')} type="button">
              <GameIcon name={order.icon} size={18} />
              {t(order.label)}
            </button>
          ))}
        </div>
        <p className="fl-note">{t('fleets.ordersSoon')}</p>
      </section>

      <div className="fl-panel__buttons">
        <button className="btn fl-btn fl-btn--block" disabled={busy} onClick={onAdd} type="button">
          {t('fleets.addShips')}
        </button>
        <button className="btn fl-btn fl-btn--block" disabled={busy} onClick={onRemove} type="button">
          {t('fleets.removeShips')}
        </button>
        <button
          className="btn fl-btn fl-btn--block"
          disabled={busy || !canMove}
          onClick={onMove}
          title={canMove ? undefined : t('fleets.noFleetsHere')}
          type="button"
        >
          {t('fleets.moveShips')}
        </button>
        <button className="btn fl-btn fl-btn--block fl-btn--danger" disabled={busy} onClick={onDisband} type="button">
          {t('fleets.disband')}
        </button>
      </div>
    </aside>
  );
}
