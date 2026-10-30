import { useEffect, useMemo, useState } from 'react';
import type { QueueItem } from '../../api/planetApi';
import type { OwnedPlanet } from '../../api/worldApi';
import { useApi } from '../../api/useApi';
import { images } from '../../assets/images';
import { Alert } from '../../components/Alert';
import { GameIcon, type GameIconName } from '../../components/GameIcon';
import { LangSwitch } from '../../components/LangSwitch';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { BodyIdentity, Deposits, FactBar } from './CivilizationParts';
import { EmailBanner } from './EmailBanner';
import { EnergyPanel, QueuePanel } from './PlanetPanels';
import { formatNumber, namer } from './planetText';
import { ProcessesPanel } from './ProcessesPanel';
import { ProductionPanel } from './ProductionPanel';
import { ResearchPanel } from './ResearchPanel';
import { TerraformingPanel } from './TerraformingPanel';
import { useBodies } from './useBodies';
import { useEconomy } from './useEconomy';
import { useNow, usePlanet } from './usePlanet';
import { useResourceGroups } from './useResourceGroups';
import { useResourceNames } from './useResourceNames';

/**
 * The items of the menu of the game. The items that no task has made yet stay in the menu
 * but are not active, with the reason in the hint (task 00010, section 2.7).
 */
const menuItems: { id: string; icon: GameIconName; label: MessageKey; route?: string; soon?: boolean }[] = [
  { id: 'overview', icon: 'overview', label: 'civ.nav.overview', route: routes.game },
  { id: 'planet', icon: 'planet', label: 'civ.nav.planet', route: routes.planet },
  { id: 'production', icon: 'production', label: 'civ.nav.production' },
  { id: 'shipyard', icon: 'shipyard', label: 'civ.nav.shipyard', soon: true },
  { id: 'fleets', icon: 'fleets', label: 'civ.nav.fleets', soon: true },
  { id: 'map', icon: 'map', label: 'civ.nav.map', soon: true },
  { id: 'research', icon: 'research', label: 'civ.nav.research' },
  { id: 'market', icon: 'market', label: 'civ.nav.market', soon: true },
  { id: 'ranking', icon: 'ranking', label: 'civ.nav.ranking', soon: true },
  { id: 'battle', icon: 'battle', label: 'civ.nav.battle', soon: true },
  { id: 'clan', icon: 'clan', label: 'civ.nav.clan', soon: true },
  { id: 'mail', icon: 'mail', label: 'civ.nav.mail', soon: true },
  { id: 'help', icon: 'help', label: 'civ.nav.help' },
  { id: 'settings', icon: 'settings', label: 'civ.nav.settings', route: routes.account },
];

/**
 * The tabs of the phone: the same sections of the menu, five of them.
 */
const tabItems: { id: string; icon: GameIconName; label: MessageKey; route?: string }[] = [
  { id: 'overview', icon: 'overview', label: 'civ.nav.overview', route: routes.game },
  { id: 'planet', icon: 'planet', label: 'civ.nav.planet', route: routes.planet },
  { id: 'production', icon: 'production', label: 'civ.nav.production' },
  { id: 'map', icon: 'map', label: 'civ.nav.map' },
];

/**
 * The screen of the civilization (task 00010): the bodies of the player with their real
 * resources, their storage, their energy, their population and their queues.
 *
 * Everything the tasks 00011–00015 have made is shown with the data of the server. The blocks
 * that no task has made yet are visual placeholders with the shape of the real content and
 * the word "Soon", never a made-up number, a made-up name or a made-up timer.
 */
export function GamePage() {
  const { t } = useI18n();
  const { session } = useSession();

  if (session.status !== 'signed-in') {
    return null;
  }

  return (
    <div className="civ">
      <CivHeader nickname={session.me.nickname} />
      <EmailBanner />
      <div className="civ__body">
        <CivMenu />
        <main className="civ__main">
          <img alt="" aria-hidden="true" className="civ__bg" src={images.nebula} />
          <CivBodies nickname={session.me.nickname} />
        </main>
      </div>
      <CivFooter />
      <nav aria-label={t('civ.gameMenu')} className="civ-tabbar">
        {tabItems.map((item, index) =>
          item.route ? (
            <Link aria-current={index === 0 ? 'page' : undefined} key={item.id} to={item.route}>
              <GameIcon name={item.icon} size={22} />
              <span>{t(item.label)}</span>
            </Link>
          ) : (
            <span aria-disabled="true" className="is-off" key={item.id}>
              <GameIcon name={item.icon} size={22} />
              <span>{t(item.label)}</span>
            </span>
          ),
        )}
      </nav>
    </div>
  );
}

/**
 * The header of the screen: the logo, the nickname, the resources of the body and the
 * balances of the currencies.
 */
function CivHeader({ nickname }: { nickname: string }) {
  const { t, language } = useI18n();
  const { session } = useSession();
  const { balances } = useEconomy(null);
  const { planet } = usePlanet(session.status === 'signed-in' ? session.me.civilization?.planetId ?? null : null);
  const { text } = useTexts();
  const home = session.status === 'signed-in' ? session.me.civilization : null;

  // The names of the currencies come from the server with the texts of the game data, so the
  // client knows no currency by name.
  const names = useMemo(() => {
    const byId = new Map((planet?.resourceTypes ?? []).map((resource) => [resource.id, resource.nameKey]));

    return (id: number): string => {
      const key = byId.get(id);

      return key ? text(key) ?? key : String(id);
    };
  }, [planet, text]);

  return (
    <header className="civ-topbar">
      <div className="civ-topbar__id">
        <Link aria-label={t('common.logoHome')} className="brand" to={routes.home}>
          <img alt="" height={28} src={images.logoMark} width={28} />
          <b>AETHERIS</b>
        </Link>
        <span className="nick">
          <span aria-hidden="true" className="glyph glyph--race-1" />
          {nickname}
        </span>
        {home?.planetName ? <span className="civ-home">{home.planetName}</span> : null}
      </div>
      <div className="civ-topbar__res">
        <ul aria-label={t('civ.resources')} className="civ-res">
          {planet?.resources.slice(0, 6).map((resource) => (
            <li key={resource.resourceTypeId}>
              <span className="num">{formatNumber(resource.amount, language)}</span>
            </li>
          ))}
        </ul>
        {balances.length === 0 ? (
          <span className="badge" title={t('economy.credits')}>
            —
          </span>
        ) : (
          <div aria-label={t('economy.credits')} className="civ-balances">
            {balances.map((balance) => (
              <span
                className="badge"
                key={balance.resourceTypeId}
                title={`${names(balance.resourceTypeId)}: ${formatNumber(balance.amount, language)} (${t('economy.income')}: ${balance.incomePerHour > 0 ? '+' : ''}${formatNumber(balance.incomePerHour, language)})`}
              >
                <span>{names(balance.resourceTypeId)}</span> <span className="num">{formatNumber(balance.amount, language)}</span>{' '}
                <span className="meta num">
                  ({balance.incomePerHour > 0 ? '+' : ''}
                  {formatNumber(balance.incomePerHour, language)}
                  {t('economy.perHourShort')})
                </span>
              </span>
            ))}
          </div>
        )}
      </div>
      <div className="civ-topbar__tools">
        <LangSwitch />
      </div>
    </header>
  );
}

/**
 * The menu of the game on the left: the sections that exist are links, the others are shown
 * with the reason in the hint.
 */
function CivMenu() {
  const { t } = useI18n();
  const { session } = useSession();
  const confirmed = session.status === 'signed-in' && session.me.emailConfirmed;

  return (
    <nav aria-label={t('civ.gameMenu')} className="civ-side">
      {menuItems.map((item) =>
        item.route ? (
          <Link className="civ-nav" key={item.id} to={item.route}>
            <GameIcon name={item.icon} />
            <span className="civ-nav__label">{t(item.label)}</span>
          </Link>
        ) : (
          <span className="civ-nav-wrap" key={item.id}>
            <button
              className="civ-nav is-off"
              disabled={item.soon === true || !confirmed}
              title={item.soon ? t('civ.soon') : t('player.game.locked')}
              type="button"
            >
              <GameIcon name={item.icon} />
              <span className="civ-nav__label">{t(item.label)}</span>
            </button>
            {item.soon ? <span className="civ-nav__tag">{t('civ.soon')}</span> : null}
          </span>
        ),
      )}
    </nav>
  );
}

/**
 * The bodies of the player: the cards with the real data of every body.
 */
function CivBodies({ nickname }: { nickname: string }) {
  const { t } = useI18n();
  const { text } = useTexts();
  const { session } = useSession();
  const [selected, setSelected] = useState<number | null>(null);
  const { bodies, reference, loaded, failed } = useBodies();
  const confirmed = session.status === 'signed-in' && session.me.emailConfirmed;

  const names = useResourceNames(reference, text);
  const groups = useResourceGroups(reference);
  const types = useMemo(() => new Map((reference?.objectTypes ?? []).map((type) => [type.id, type])), [reference]);
  const current = selected ?? bodies[0]?.planetId ?? null;

  return (
    <div className="civ-main__in">
      <div className="civ-main__head">
        <h1>{t('player.game.title', { nickname })}</h1>
        <p className="lead">{t('civ.lead')}</p>
        <h2 className="civ-main__title">
          {t('civ.planets')} <span>{bodies.length}</span>
        </h2>
        {confirmed ? (
          <Link className="btn btn--secondary" to={routes.planet}>
            {t('player.game.build')}
          </Link>
        ) : (
          <button className="btn btn--secondary" disabled title={t('player.game.locked')} type="button">
            {t('player.game.build')}
          </button>
        )}
      </div>

      {failed ? <Alert kind="error">{t('civ.loadFailed')}</Alert> : null}

      {!loaded ? (
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      ) : bodies.length === 0 ? (
        <div className="civ-empty">
          <p>{t('civ.noBodies')}</p>
        </div>
      ) : (
        <div className="civ-bodies">
          {bodies.map((planet) => (
            <BodyCard
              current={current === planet.planetId}
              groups={groups}
              key={planet.planetId}
              names={names}
              onSelect={() => setSelected(planet.planetId)}
              planet={planet}
              type={types.get(planet.objectTypeId) ?? null}
            />
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * A card of a body: the picture with the type and the place, the real data of the body and
 * the queues and the panels that the server has.
 */
function BodyCard({
  planet,
  type,
  names,
  groups,
  current,
  onSelect,
}: {
  planet: OwnedPlanet;
  type: { id: number; nameKey: string; imageKey: string | null } | null;
  names: (id: number) => string;
  groups: (id: number) => string | null;
  current: boolean;
  onSelect: () => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { planets } = useApi();
  const { session } = useSession();
  const { planet: data, receivedAt, apply } = usePlanet(planet.planetId);
  const { population } = useEconomy(planet.planetId);
  const now = useNow();
  const confirmed = session.status === 'signed-in' && session.me.emailConfirmed;

  const onCancel = (item: QueueItem) =>
    void planets.cancel(planet.planetId, item.id).then((answer) => apply(answer));

  const typeName = type ? text(type.nameKey) ?? type.nameKey : t('civ.unknownType');
  const storage = data?.storageGroups[0] ?? null;
  const stored = storage?.stored ?? 0;
  const capacity = storage?.capacity ?? 0;
  const energy = data?.energy ?? null;
  const balance = energy?.balance ?? 0;
  const consumption = energy?.consumption ?? 0;

  return (
    <article aria-label={planet.name ?? typeName} className={current ? 'civ-body is-current' : 'civ-body'}>
      <div className="civ-body__id">
        <BodyIdentity
          onSelect={onSelect}
          planet={planet}
          selected={current}
          typeImageKey={type?.imageKey ?? null}
          typeName={typeName}
        />
        <div className="civ-body__deposits">
          <span className="civ-label">{t('civ.deposits')}</span>
          <Deposits groups={groups} names={names} planet={planet} />
        </div>
      </div>

      <div className="civ-body__col">
        <span className="civ-label">{t('civ.facts')}</span>
        <div className="civ-bars">
          <FactBar
            label={t('civ.population')}
            percent={population && population.maximum > 0 ? (population.amount / population.maximum) * 100 : 0}
            tone="accent"
            value={
              population
                ? `${formatNumber(population.amount, language)} / ${formatNumber(population.maximum, language)}`
                : t('civ.noData')
            }
          />
          <FactBar
            label={t('civ.energy')}
            percent={consumption > 0 ? (balance / consumption) * 100 : balance > 0 ? 100 : 0}
            tone="energy"
            value={
              energy
                ? `${formatNumber(balance, language)} / ${formatNumber(consumption, language)}${balance < consumption ? ` · ${t('civ.deficit')} ${formatNumber(consumption - balance, language)}` : ''}`
                : t('civ.noData')
            }
            warning={energy !== null && balance < consumption}
          />
          <FactBar
            label={t('civ.storage')}
            percent={capacity > 0 ? (stored / capacity) * 100 : 0}
            tone="store"
            value={
              storage
                ? `${formatNumber(stored, language)} / ${formatNumber(capacity, language)}${storage.isFull ? ` · ${t('civ.full')}` : ''}`
                : t('civ.noData')
            }
            warning={storage?.isFull === true}
          />
        </div>
      </div>

      <div className="civ-body__col">
        {data && confirmed ? (
          <>
            <EnergyPanel planet={data} />
            <QueuePanel
              busy={false}
              names={namer(data, language, text)}
              now={now}
              onCancel={onCancel}
              planet={data}
              receivedAt={receivedAt}
            />
          </>
        ) : (
          <p className="lead" role="status">
            {t('common.loading')}
          </p>
        )}

        <span className="civ-label">{t('civ.resources')}</span>
        {data && data.resources.length > 0 ? (
          <ul className="civ-yields">
            {data.resources
              .filter((resource) => resource.ratePerHour > 0)
              .map((resource) => (
                <li className="civ-yield" key={resource.resourceTypeId}>
                  <span>{names(resource.resourceTypeId)}</span>
                  <span className="num">
                    +{formatNumber(resource.ratePerHour, language)} {t('civ.perHour')}
                  </span>
                </li>
              ))}
          </ul>
        ) : (
          <p className="meta">{t('civ.noData')}</p>
        )}
      </div>

      <div className="civ-body__col civ-body__col--wide">
        {confirmed ? (
          <>
            <ResearchPanel planetId={planet.planetId} />
            <ProductionPanel planetId={planet.planetId} />
            <ProcessesPanel planetId={planet.planetId} />
            <TerraformingPanel />
          </>
        ) : (
          <p className="meta">{t('civ.lockedUntilConfirmed')}</p>
        )}
      </div>
    </article>
  );
}

/**
 * The bottom bar of the screen: the switcher of the bodies and the place of the chat, which
 * no task has made yet.
 */
function CivFooter() {
  const { t } = useI18n();
  const { world } = useApi();
  const [bodies, setBodies] = useState<OwnedPlanet[]>([]);

  useEffect(() => {
    let cancelled = false;

    void world.planets().then((answer) => {
      if (!cancelled) {
        setBodies(answer);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [world]);

  return (
    <footer className="civ-bottombar">
      <div className="civ-bottombar__switch">
        {bodies.map((planet) => (
          <span className="civ-thumb" key={planet.planetId} title={planet.name ?? String(planet.planetId)}>
            <span className="civ-thumb__letter">{planet.name?.slice(0, 1) ?? '·'}</span>
          </span>
        ))}
        <span className="civ-current">
          <small>{t('civ.current')}</small>
          <strong>{t('civ.bodiesCount', { count: bodies.length })}</strong>
        </span>
      </div>
      <p className="meta civ-chat-soon">{t('civ.chatSoon')}</p>
    </footer>
  );
}
