import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { Alert } from '../../components/Alert';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { useTexts } from '../../texts/useTexts';
import { adminRoutes } from '../adminRoutes';
import { errorKey, useAdmin } from '../adminContext';
import type { AdminMessageKey } from '../i18n/uk';
import { formatMoment, formatNumber, useAdminText } from '../i18n/useAdminText';
import type {
  BalanceRow,
  BuildingRow,
  ColonyView,
  ConstructionRow,
  MarketDelivery,
  MarketView,
  PartStackRow,
  PlanetLabel,
  PlayerCard,
  PlayerRow,
  ProcessOrderRow,
  ProductionOrderRow,
  ProductionView,
  ResearchRow,
  ShipDesign,
  StockRow,
  TechnologyRow,
  TechnologyView,
} from '../playerAdminApi';

type TabId =
  | 'identity'
  | 'finances'
  | 'stock'
  | 'population'
  | 'buildings'
  | 'construction'
  | 'technologies'
  | 'parts'
  | 'production'
  | 'processes'
  | 'designs'
  | 'market';

const colonyTabIds: TabId[] = ['stock', 'population', 'buildings', 'construction'];

const tabs: { id: TabId; label: AdminMessageKey; permission: string | null; needsPlanet: boolean }[] = [
  { id: 'identity', label: 'admin.players.identity', permission: null, needsPlanet: false },
  { id: 'finances', label: 'admin.players.finances', permission: 'admin.economy_balances_read', needsPlanet: false },
  { id: 'stock', label: 'admin.players.stock', permission: 'admin.player_colony_read', needsPlanet: true },
  { id: 'population', label: 'admin.players.population', permission: 'admin.player_colony_read', needsPlanet: true },
  { id: 'buildings', label: 'admin.players.buildings', permission: 'admin.player_colony_read', needsPlanet: true },
  { id: 'construction', label: 'admin.players.construction', permission: 'admin.player_colony_read', needsPlanet: true },
  { id: 'technologies', label: 'admin.players.technologies', permission: 'admin.player_technologies_read', needsPlanet: true },
  { id: 'parts', label: 'admin.players.parts', permission: 'admin.player_parts_read', needsPlanet: true },
  { id: 'production', label: 'admin.players.production', permission: 'admin.player_production_read', needsPlanet: true },
  { id: 'processes', label: 'admin.players.processes', permission: 'admin.player_process_read', needsPlanet: true },
  { id: 'designs', label: 'admin.players.designs', permission: 'admin.player_designs_read', needsPlanet: false },
  { id: 'market', label: 'admin.players.market', permission: 'admin.player_market_read', needsPlanet: false },
];

type Area = 'card' | 'balances' | 'colony' | 'technologies' | 'production' | 'processes' | 'designs' | 'market';

/**
 * The player list and the card of one player. The body itself is not edited: a planet is only
 * the label of the stock, the buildings and the technologies that sit on it.
 */
export function PlayersScreen() {
  const { path } = useRouter();
  const prefix = `${adminRoutes.players}/`;
  const raw = path.startsWith(prefix) ? path.slice(prefix.length) : '';
  const userId = raw ? decodeURIComponent(raw.split('/')[0] ?? '') : '';

  return userId ? <PlayerCard userId={userId} /> : <PlayerList />;
}

function PlayerList() {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, onError } = useAdmin();
  const { navigate } = useRouter();
  const [searchText, setSearchText] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [answer, setAnswer] = useState<{ key: string; rows: PlayerRow[]; total: number; pageSize: number; error: AdminMessageKey | null } | null>(null);
  const requestKey = `${page}:${search}`;
  const pending = answer?.key !== requestKey;

  useEffect(() => {
    let active = true;

    players.list(page, search).then(
      (listed) => {
        if (active) {
          setAnswer({ key: requestKey, rows: listed.items, total: listed.total, pageSize: listed.page_size, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setAnswer({ key: requestKey, rows: [], total: 0, pageSize: 1, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, page, search, onError, requestKey]);

  const onSearch = (event: FormEvent) => {
    event.preventDefault();
    setPage(1);
    setSearch(searchText.trim());
  };

  const pages = answer && answer.pageSize > 0 ? Math.max(1, Math.ceil(answer.total / answer.pageSize)) : 1;

  return (
    <section className="admin-table">
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.players.title')}</h1>
        <p className="hint">{t('admin.players.lead')}</p>
      </header>
      <form className="admin-table__tools" onSubmit={onSearch} role="search">
        <label className="admin-field">
          <span>{t('admin.players.search')}</span>
          <input className="input" type="search" value={searchText} onChange={(event) => setSearchText(event.target.value)} />
        </label>
        <button className="btn btn--secondary" type="submit">
          {t('admin.table.find')}
        </button>
      </form>
      {answer?.error ? <Alert kind="error">{t(answer.error)}</Alert> : null}
      {pending ? <p className="hint">{t('admin.loading')}</p> : null}
      {!pending && answer && answer.rows.length === 0 ? <p className="hint">{t('admin.players.empty')}</p> : null}
      {answer && answer.rows.length > 0 ? (
        <div className="admin-table__scroll">
          <table>
            <thead>
              <tr>
                <th>{t('admin.players.nickname')}</th>
                <th>{t('admin.players.email')}</th>
                <th>{t('admin.players.race')}</th>
                <th>{t('admin.players.planets')}</th>
                <th>{t('admin.players.registered')}</th>
              </tr>
            </thead>
            <tbody>
              {answer.rows.map((account) => (
                <tr
                  key={account.user_id}
                  className="admin-table__row--open"
                  tabIndex={0}
                  onClick={() => navigate(`${adminRoutes.players}/${encodeURIComponent(account.user_id)}`)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') {
                      navigate(`${adminRoutes.players}/${encodeURIComponent(account.user_id)}`);
                    }
                  }}
                >
                  <td>{account.nickname}</td>
                  <td>{account.email ?? '—'}</td>
                  <td>{account.race_name_key ? name(account.race_name_key) : '—'}</td>
                  <td>{formatNumber(account.planet_count, language, 0)}</td>
                  <td>{formatMoment(account.registered_at, language)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      <Pager page={page} pages={pages} total={answer?.total ?? 0} onPage={setPage} />
    </section>
  );
}

function PlayerCard({ userId }: { userId: string }) {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, can, onError } = useAdmin();
  const [tab, setTab] = useState<TabId>('identity');
  const [seen, setSeen] = useState<TabId[]>(['identity']);
  const [reason, setReason] = useState('');
  const [notice, setNotice] = useState<AdminMessageKey | null>(null);
  const [failureMessage, setFailureMessage] = useState<AdminMessageKey | null>(null);
  const [busy, setBusy] = useState(false);
  const [stamps, setStamps] = useState<Record<Area, number>>({
    card: 0,
    balances: 0,
    colony: 0,
    technologies: 0,
    production: 0,
    processes: 0,
    designs: 0,
    market: 0,
  });
  const [cardState, setCardState] = useState<{ key: string; card: PlayerCard | null; error: AdminMessageKey | null }>({ key: '', card: null, error: null });
  const [planetId, setPlanetId] = useState<number | null>(null);
  const cardKey = `${userId}:${stamps.card}`;
  const pendingCard = cardState.key !== cardKey;

  useEffect(() => {
    let active = true;

    players.card(userId).then(
      (card) => {
        if (active) {
          setCardState({ key: cardKey, card, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setCardState({ key: cardKey, card: null, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, onError, cardKey]);

  const card = cardState.card;

  useEffect(() => {
    if (!card) {
      return;
    }

    setPlanetId((current) => (current !== null && card.planets.some((planet) => planet.planet_id === current) ? current : (card.planets[0]?.planet_id ?? null)));
  }, [card]);

  const visibleTabs = tabs.filter((entry) => entry.permission === null || can(entry.permission));
  const current = visibleTabs.find((entry) => entry.id === tab) ?? visibleTabs[0];
  const reasonReady = reason.trim().length > 0;

  const commit = (work: () => Promise<unknown>, area: Area) => {
    setNotice(null);
    setFailureMessage(null);
    setBusy(true);

    work().then(
      () => {
        setNotice('admin.players.saved');
        setStamps((currentStamps) => ({ ...currentStamps, [area]: currentStamps[area] + 1 }));
        setBusy(false);
      },
      (failure: unknown) => {
        onError(failure);
        setFailureMessage(errorKey(failure));
        setBusy(false);
      },
    );
  };

  const reloadAll = () => {
    setNotice(null);
    setFailureMessage(null);
    setStamps((currentStamps) => ({
      card: currentStamps.card + 1,
      balances: currentStamps.balances + 1,
      colony: currentStamps.colony + 1,
      technologies: currentStamps.technologies + 1,
      production: currentStamps.production + 1,
      processes: currentStamps.processes + 1,
      designs: currentStamps.designs + 1,
      market: currentStamps.market + 1,
    }));
  };

  return (
    <section className="admin-table">
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{card?.nickname ?? t('admin.players.title')}</h1>
        <p className="hint">{t('admin.players.lead')}</p>
        <div className="admin-links">
          <Link className="btn btn--secondary" to={adminRoutes.players}>{t('admin.players.back')}</Link>
        </div>
      </header>
      {cardState.error ? <Alert kind="error">{t(cardState.error)}</Alert> : null}
      {pendingCard ? <p className="hint">{t('admin.loading')}</p> : null}
      {failureMessage ? <Alert kind="error">{t(failureMessage)}</Alert> : null}
      {notice ? <Alert kind="success">{t(notice)}</Alert> : null}
      {card && current ? (
        <>
          <nav className="admin-tabs" aria-label={t('admin.players.title')}>
            {visibleTabs.map((entry) => (
              <button
                key={entry.id}
                type="button"
                className={entry.id === current.id ? 'admin__link admin__link--active' : 'admin__link'}
                aria-current={entry.id === current.id ? 'page' : undefined}
                onClick={() => {
                  setTab(entry.id);
                  const token: TabId = colonyTabIds.includes(entry.id) ? 'stock' : entry.id;
                  setSeen((currentSeen) => (currentSeen.includes(token) ? currentSeen : [...currentSeen, token]));
                }}
              >
                {t(entry.label)}
              </button>
            ))}
          </nav>
          <div className="admin-form admin-form--inline">
            <label className="admin-field admin-field--wide">
              <span>{t('admin.players.reason')}</span>
              <input className="input" value={reason} onChange={(event) => setReason(event.target.value)} />
              <span className="hint">{t('admin.players.reasonHint')}</span>
            </label>
            <button className="btn btn--secondary" type="button" onClick={reloadAll}>
              {t('admin.players.load')}
            </button>
          </div>
          {current.needsPlanet ? (
            <PlanetSelect planets={card.planets} planetId={planetId} onPlanet={setPlanetId} />
          ) : null}
          {current.needsPlanet && planetId === null ? <p className="hint">{t('admin.players.noPlanet')}</p> : null}
          {seen.includes('identity') ? (
            <div hidden={current.id !== 'identity'}>
              <IdentitySection key={`${card.user_id}:${card.email ?? ''}`} card={card} language={language} name={name} busy={busy} canChangeEmail={can('admin.player_email_changed')} onEmail={(email) => commit(() => players.changeEmail(userId, email), 'card')} />
            </div>
          ) : null}
          {seen.includes('finances') ? (
            <div hidden={current.id !== 'finances'}>
            <BalancesSection
              userId={userId}
              reloadKey={stamps.balances}
              reasonReady={reasonReady}
              busy={busy}
              canSet={can('admin.player_balance_set')}
              onSet={(resourceTypeId, amount, version) => commit(() => players.setBalance(userId, resourceTypeId, amount, reason.trim(), version), 'balances')}
            />
            </div>
          ) : null}
          {seen.includes('stock') && planetId !== null ? (
            <div hidden={!colonyTabIds.includes(current.id)}>
            <ColonySection
              userId={userId}
              planetId={planetId}
              tab={asColonyTab(current.id)}
              reloadKey={stamps.colony}
              reasonReady={reasonReady}
              busy={busy}
              canStock={can('admin.player_stock_updated')}
              canPopulation={can('admin.player_population_updated')}
              canBuilding={can('admin.player_building_updated')}
              canSlots={can('admin.player_slots_updated')}
              canCancel={can('admin.player_construction_cancelled')}
              onStock={(resourceTypeId, amount) => commit(() => players.setStock(userId, planetId, resourceTypeId, amount, reason.trim()), 'colony')}
              onPopulation={(amount) => commit(() => players.setPopulation(userId, planetId, amount, reason.trim()), 'colony')}
              onBuilding={(buildingTypeId, level) => commit(() => players.setBuilding(userId, planetId, buildingTypeId, level, reason.trim()), 'colony')}
              onSlots={(extraQueueSlots) => commit(() => players.setSlots(userId, planetId, extraQueueSlots, reason.trim()), 'colony')}
              onCancel={(queueItemId) => commit(() => players.cancelConstruction(userId, planetId, queueItemId, reason.trim()), 'colony')}
            />
            </div>
          ) : null}
          {seen.includes('technologies') && planetId !== null ? (
            <div hidden={current.id !== 'technologies'}>
            <TechnologiesSection
              userId={userId}
              planetId={planetId}
              reloadKey={stamps.technologies}
              reasonReady={reasonReady}
              busy={busy}
              canSet={can('admin.player_technology_updated')}
              canCancel={can('admin.player_research_cancelled')}
              onSet={(technologyId, level) => commit(() => players.setTechnology(userId, planetId, technologyId, level, reason.trim()), 'technologies')}
              onCancel={(queueItemId) => commit(() => players.cancelResearch(userId, queueItemId, reason.trim()), 'technologies')}
            />
            </div>
          ) : null}
          {seen.includes('parts') && planetId !== null ? (
            <div hidden={current.id !== 'parts'}>
            <PartsSection
              userId={userId}
              planetId={planetId}
              reloadKey={stamps.production}
              reasonReady={reasonReady}
              busy={busy}
              canSet={can('admin.player_part_updated')}
              canGrant={can('admin.player_part_granted')}
              onQuantity={(stackId, quantity) => commit(() => players.setPartQuantity(userId, planetId, stackId, quantity, reason.trim()), 'production')}
              onGrant={(partTypeId, quantity, hasRaceBonus, conditionPercent) =>
                commit(
                  () => players.grantPart(userId, planetId, {
                    part_type_id: partTypeId,
                    quantity,
                    has_race_bonus: hasRaceBonus,
                    condition_percent: conditionPercent,
                    reason: reason.trim(),
                  }),
                  'production',
                )
              }
            />
            </div>
          ) : null}
          {seen.includes('production') && planetId !== null ? (
            <div hidden={current.id !== 'production'}>
            <OrdersSection
              userId={userId}
              planetId={planetId}
              reloadKey={stamps.production}
              kind="production"
              reasonReady={reasonReady}
              busy={busy}
              canCancel={can('admin.player_production_cancelled')}
              onCancel={(orderId) => commit(() => players.cancelProduction(userId, planetId, orderId, reason.trim()), 'production')}
            />
            </div>
          ) : null}
          {seen.includes('processes') && planetId !== null ? (
            <div hidden={current.id !== 'processes'}>
            <OrdersSection
              userId={userId}
              planetId={planetId}
              reloadKey={stamps.processes}
              kind="processes"
              reasonReady={reasonReady}
              busy={busy}
              canCancel={can('admin.player_process_cancelled')}
              onCancel={(orderId) => commit(() => players.cancelProcess(userId, planetId, orderId, reason.trim()), 'processes')}
            />
            </div>
          ) : null}
          {seen.includes('designs') ? (
            <div hidden={current.id !== 'designs'}>
            <DesignsSection
              userId={userId}
              reloadKey={stamps.designs}
              reasonReady={reasonReady}
              busy={busy}
              canRename={can('admin.player_design_updated')}
              canArchive={can('admin.player_design_archived')}
              onRename={(designId, designName) => commit(() => players.renameDesign(userId, designId, designName, reason.trim()), 'designs')}
              onArchive={(designId) => commit(() => players.archiveDesign(userId, designId, reason.trim()), 'designs')}
            />
            </div>
          ) : null}
          {seen.includes('market') ? (
            <div hidden={current.id !== 'market'}>
            <MarketSection
              userId={userId}
              reloadKey={stamps.market}
              reasonReady={reasonReady}
              busy={busy}
              canCancel={can('admin.player_lot_cancelled')}
              onCancel={(lotId) => commit(() => players.cancelLot(userId, lotId, reason.trim()), 'market')}
            />
            </div>
          ) : null}
        </>
      ) : null}
    </section>
  );
}

function PlanetSelect({ planets, planetId, onPlanet }: { planets: PlanetLabel[]; planetId: number | null; onPlanet: (planetId: number) => void }) {
  const { t } = useAdminText();

  if (planets.length === 0) {
    return null;
  }

  return (
    <label className="admin-field">
      <span>{t('admin.players.planet')}</span>
      <select
        className="input"
        value={planetId ?? planets[0].planet_id}
        onChange={(event) => onPlanet(Number(event.target.value))}
      >
        {planets.map((planet) => (
          <option key={planet.planet_id} value={planet.planet_id}>
            {planetCaption(planet, t)}
          </option>
        ))}
      </select>
    </label>
  );
}

function IdentitySection({
  card,
  language,
  name,
  busy,
  canChangeEmail,
  onEmail,
}: {
  card: PlayerCard;
  language: 'uk' | 'en';
  name: (key: string | null | undefined) => string;
  busy: boolean;
  canChangeEmail: boolean;
  onEmail: (email: string) => void;
}) {
  const { t } = useAdminText();
  const [emailText, setEmailText] = useState(card.email ?? '');
  const home = card.civilization;

  return (
    <div className="admin-card__group">
      <dl className="admin-card__facts">
        <dt>{t('admin.players.userId')}</dt>
        <dd>{card.user_id}</dd>
        <dt>{t('admin.players.nickname')}</dt>
        <dd>{card.nickname}</dd>
        <dt>{t('admin.players.registered')}</dt>
        <dd>{formatMoment(card.registered_at, language)}</dd>
        <dt>{t('admin.players.emailConfirmed')}</dt>
        <dd>{card.email_confirmed ? t('admin.table.yes') : t('admin.table.no')}</dd>
        <dt>{t('admin.players.race')}</dt>
        <dd>{home ? name(home.race_name_key) : t('admin.players.noCivilization')}</dd>
        <dt>{t('admin.players.home')}</dt>
        <dd>{home ? planetCaption({ planet_id: home.planet_id, sector_id: home.sector_id, name: home.planet_name }, t) : '—'}</dd>
        <dt>{t('admin.players.founded')}</dt>
        <dd>{home ? formatMoment(home.founded_at, language) : '—'}</dd>
      </dl>
      <div className="admin-inline-save admin-inline-save--text">
        <label className="admin-field">
          <span>{t('admin.players.newEmail')}</span>
          <input className="input" type="email" value={emailText} onChange={(event) => setEmailText(event.target.value)} />
        </label>
        <button className="btn btn--primary" type="button" disabled={busy || !canChangeEmail || emailText.trim() === ''} onClick={() => onEmail(emailText.trim())}>
          {t('admin.players.changeEmail')}
        </button>
      </div>
    </div>
  );
}

function BalancesSection({
  userId,
  reloadKey,
  reasonReady,
  busy,
  canSet,
  onSet,
}: {
  userId: string;
  reloadKey: number;
  reasonReady: boolean;
  busy: boolean;
  canSet: boolean;
  onSet: (resourceTypeId: number, amount: number, version: number) => void;
}) {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, onError } = useAdmin();
  const requestKey = `${userId}:${reloadKey}`;
  const [loaded, setLoaded] = useState<{ key: string; rows: BalanceRow[]; error: AdminMessageKey | null }>({ key: '', rows: [], error: null });

  useEffect(() => {
    let active = true;

    players.balances(userId).then(
      (rows) => {
        if (active) {
          setLoaded({ key: requestKey, rows, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, rows: [], error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, onError, requestKey]);

  return (
    <LoadGate pending={loaded.key !== requestKey} errorMessage={loaded.error}>
      {loaded.rows.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
        <NumberTable
          rows={loaded.rows}
          rowKey={(row) => String(row.resource_type_id)}
          columns={[
            { header: t('admin.players.finances'), cell: (row) => name(row.name_key) },
            { header: t('admin.players.amount'), cell: (row) => formatNumber(row.amount, language, 0) },
            { header: t('admin.players.income'), cell: (row) => formatNumber(row.income_per_hour, language, 2) },
            { header: t('admin.players.version'), cell: (row) => String(row.version) },
          ]}
          initial={(row) => row.amount}
          allowNegative
          canSave={canSet && reasonReady && !busy}
          saveLabel={t('admin.players.save')}
          onSave={(row, amount) => onSet(row.resource_type_id, amount, row.version)}
        />
      )}
    </LoadGate>
  );
}

function ColonySection({
  userId,
  planetId,
  tab,
  reloadKey,
  reasonReady,
  busy,
  canStock,
  canPopulation,
  canBuilding,
  canSlots,
  canCancel,
  onStock,
  onPopulation,
  onBuilding,
  onSlots,
  onCancel,
}: {
  userId: string;
  planetId: number;
  tab: 'stock' | 'population' | 'buildings' | 'construction';
  reloadKey: number;
  reasonReady: boolean;
  busy: boolean;
  canStock: boolean;
  canPopulation: boolean;
  canBuilding: boolean;
  canSlots: boolean;
  canCancel: boolean;
  onStock: (resourceTypeId: number, amount: number) => void;
  onPopulation: (amount: number) => void;
  onBuilding: (buildingTypeId: number, level: number) => void;
  onSlots: (extraQueueSlots: number) => void;
  onCancel: (queueItemId: number) => void;
}) {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, onError } = useAdmin();
  const requestKey = `${userId}:${planetId}:${reloadKey}`;
  const [loaded, setLoaded] = useState<{ key: string; view: ColonyView | null; error: AdminMessageKey | null }>({ key: '', view: null, error: null });

  useEffect(() => {
    let active = true;

    players.colony(userId, planetId).then(
      (view) => {
        if (active) {
          setLoaded({ key: requestKey, view, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, view: null, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, planetId, onError, requestKey]);

  const view = loaded.view;

  return (
    <LoadGate pending={loaded.key !== requestKey} errorMessage={loaded.error}>
      {view ? (
        <>
          <EnergyLine energy={view.energy} />
          {tab === 'stock' ? (
            <NumberTable
              rows={view.stocks}
              rowKey={(row: StockRow) => String(row.resource_type_id)}
              columns={[
                { header: t('admin.players.stock'), cell: (row: StockRow) => (row.group_name_key ? `${name(row.group_name_key)} · ${name(row.name_key)}` : name(row.name_key)) },
                { header: t('admin.players.amount'), cell: (row: StockRow) => formatNumber(row.amount, language, 0) },
              ]}
              initial={(row: StockRow) => row.amount}
              allowNegative={false}
              canSave={canStock && reasonReady && !busy}
              saveLabel={t('admin.players.save')}
              onSave={(row: StockRow, amount) => onStock(row.resource_type_id, amount)}
            />
          ) : null}
          {tab === 'population' ? (
            <div className="admin-card__group">
              <dl className="admin-card__facts">
                <dt>{t('admin.players.populationMaximum')}</dt>
                <dd>{formatNumber(view.population_maximum, language, 0)}</dd>
                <dt>{t('admin.players.populationGrowth')}</dt>
                <dd>{formatNumber(view.population_growth_per_hour, language, 2)}</dd>
              </dl>
              <DraftCell
                label={t('admin.players.population')}
                hideLabel={false}
                initial={view.population}
                allowNegative={false}
                disabled={!(canPopulation && reasonReady && !busy)}
                saveLabel={t('admin.players.save')}
                onSave={onPopulation}
              />
            </div>
          ) : null}
          {tab === 'buildings' ? (
            <NumberTable
              rows={view.buildings}
              rowKey={(row: BuildingRow) => String(row.building_type_id)}
              columns={[
                { header: t('admin.players.buildings'), cell: (row: BuildingRow) => name(row.name_key) },
                { header: t('admin.players.level'), cell: (row: BuildingRow) => formatNumber(row.level, language, 0) },
                { header: t('admin.players.maxLevel'), cell: (row: BuildingRow) => formatNumber(row.max_level, language, 0) },
                { header: t('admin.players.queuedLevel'), cell: (row: BuildingRow) => formatNumber(row.queued_level, language, 0) },
              ]}
              initial={(row: BuildingRow) => row.level}
              allowNegative={false}
              canSave={canBuilding && reasonReady && !busy}
              saveLabel={t('admin.players.save')}
              onSave={(row: BuildingRow, amount) => onBuilding(row.building_type_id, amount)}
            />
          ) : null}
          {tab === 'construction' ? (
            <>
              <div className="admin-card__group">
                <DraftCell
                  label={t('admin.players.slots')}
                  hideLabel={false}
                  initial={view.extra_queue_slots}
                  allowNegative={false}
                  disabled={!(canSlots && reasonReady && !busy)}
                  saveLabel={t('admin.players.save')}
                  onSave={onSlots}
                />
              </div>
              <QueueTable
                rows={view.construction_queue}
                nameOf={(row: ConstructionRow) => name(row.name_key)}
                targetOf={(row: ConstructionRow) => row.target_level}
                positionOf={(row: ConstructionRow) => row.position}
                finishesOf={(row: ConstructionRow) => row.finishes_at}
                canCancel={canCancel && reasonReady && !busy}
                onCancel={(row: ConstructionRow) => onCancel(row.id)}
              />
            </>
          ) : null}
        </>
      ) : null}
    </LoadGate>
  );
}

function EnergyLine({ energy }: { energy: ColonyView['energy'] }) {
  const { t, language } = useAdminText();

  return (
    <p className="hint">
      {t('admin.players.energyReadOnly')} {t('admin.players.output')}: {formatNumber(energy.output, language, 0)}
      {' · '}
      {t('admin.players.consumption')}: {formatNumber(energy.consumption, language, 0)}
      {' · '}
      {t('admin.players.balance')}: {formatNumber(energy.balance, language, 0)}
      {' · '}
      {t('admin.players.queuedBalance')}: {formatNumber(energy.queued_balance, language, 0)}
    </p>
  );
}

function TechnologiesSection({
  userId,
  planetId,
  reloadKey,
  reasonReady,
  busy,
  canSet,
  canCancel,
  onSet,
  onCancel,
}: {
  userId: string;
  planetId: number;
  reloadKey: number;
  reasonReady: boolean;
  busy: boolean;
  canSet: boolean;
  canCancel: boolean;
  onSet: (technologyId: number, level: number) => void;
  onCancel: (queueItemId: number) => void;
}) {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, onError } = useAdmin();
  const requestKey = `${userId}:${planetId}:${reloadKey}`;
  const [loaded, setLoaded] = useState<{ key: string; view: TechnologyView | null; error: AdminMessageKey | null }>({ key: '', view: null, error: null });

  useEffect(() => {
    let active = true;

    players.technologies(userId, planetId).then(
      (view) => {
        if (active) {
          setLoaded({ key: requestKey, view, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, view: null, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, planetId, onError, requestKey]);

  const view = loaded.view;

  return (
    <LoadGate pending={loaded.key !== requestKey} errorMessage={loaded.error}>
      {view ? (
        <>
          <NumberTable
            rows={view.technologies}
            rowKey={(row: TechnologyRow) => String(row.technology_id)}
            columns={[
              { header: t('admin.players.technologies'), cell: (row: TechnologyRow) => name(row.name_key) },
              { header: t('admin.players.branch'), cell: (row: TechnologyRow) => name(row.branch_name_key) },
              { header: t('admin.players.level'), cell: (row: TechnologyRow) => formatNumber(row.level, language, 0) },
              { header: t('admin.players.maxLevel'), cell: (row: TechnologyRow) => formatNumber(row.max_level, language, 0) },
            ]}
            initial={(row: TechnologyRow) => row.level}
            allowNegative={false}
            canSave={canSet && reasonReady && !busy}
            saveLabel={t('admin.players.save')}
            onSave={(row: TechnologyRow, amount) => onSet(row.technology_id, amount)}
          />
          <h3>{t('admin.players.researchQueue')}</h3>
          <QueueTable
            rows={view.queue}
            nameOf={(row: ResearchRow) => `${name(row.name_key)} · ${t('admin.players.planetLabel', { id: row.planet_id })}`}
            targetOf={(row: ResearchRow) => row.target_level}
            positionOf={(row: ResearchRow) => row.position}
            finishesOf={(row: ResearchRow) => row.finishes_at}
            canCancel={canCancel && reasonReady && !busy}
            onCancel={(row: ResearchRow) => onCancel(row.id)}
          />
        </>
      ) : null}
    </LoadGate>
  );
}

interface PartLine {
  key: string;
  stackId: number | null;
  partTypeId: number;
  nameKey: string;
  markLevel: number;
  conditionPercent: number;
  hasRaceBonus: boolean;
  quantity: number;
  archived: boolean;
}

function partLines(view: ProductionView): PartLine[] {
  const stacksByType = new Map<number, PartStackRow[]>();

  for (const stack of view.stacks) {
    const grouped = stacksByType.get(stack.part_type_id);

    if (grouped) {
      grouped.push(stack);
    } else {
      stacksByType.set(stack.part_type_id, [stack]);
    }
  }

  const lines: PartLine[] = [];
  const listedTypeIds = new Set<number>();

  for (const part of view.part_types) {
    listedTypeIds.add(part.id);
    const stacks = stacksByType.get(part.id) ?? [];

    if (stacks.length === 0) {
      lines.push({
        key: `part-${part.id}`,
        stackId: null,
        partTypeId: part.id,
        nameKey: part.name_key,
        markLevel: part.mark_level,
        conditionPercent: 100,
        hasRaceBonus: false,
        quantity: 0,
        archived: part.is_archived,
      });
      continue;
    }

    for (const stack of stacks) {
      lines.push({
        key: `stack-${stack.id}`,
        stackId: stack.id,
        partTypeId: stack.part_type_id,
        nameKey: stack.name_key,
        markLevel: stack.mark_level,
        conditionPercent: stack.condition_percent,
        hasRaceBonus: stack.has_race_bonus,
        quantity: stack.quantity,
        archived: part.is_archived,
      });
    }
  }

  for (const stack of view.stacks) {
    if (listedTypeIds.has(stack.part_type_id)) {
      continue;
    }

    lines.push({
      key: `stack-${stack.id}`,
      stackId: stack.id,
      partTypeId: stack.part_type_id,
      nameKey: stack.name_key,
      markLevel: stack.mark_level,
      conditionPercent: stack.condition_percent,
      hasRaceBonus: stack.has_race_bonus,
      quantity: stack.quantity,
      archived: false,
    });
  }

  return lines;
}

function PartsSection({
  userId,
  planetId,
  reloadKey,
  reasonReady,
  busy,
  canSet,
  canGrant,
  onQuantity,
  onGrant,
}: {
  userId: string;
  planetId: number;
  reloadKey: number;
  reasonReady: boolean;
  busy: boolean;
  canSet: boolean;
  canGrant: boolean;
  onQuantity: (stackId: number, quantity: number) => void;
  onGrant: (partTypeId: number, quantity: number, hasRaceBonus: boolean, conditionPercent: number | null) => void;
}) {
  const viewState = useProduction(userId, planetId, reloadKey, 'parts');
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const view = viewState.view;
  const lines = view ? partLines(view) : [];

  return (
    <LoadGate pending={viewState.pending} errorMessage={viewState.error}>
      {view ? (
        lines.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
          <div className="admin-table__scroll">
            <table>
              <thead>
                <tr>
                  <th>{t('admin.players.part')}</th>
                  <th>{t('admin.players.mark')}</th>
                  <th>{t('admin.players.condition')}</th>
                  <th>{t('admin.players.raceBonus')}</th>
                  <th>{t('admin.players.quantity')}</th>
                  <th>{t('admin.players.amount')}</th>
                </tr>
              </thead>
              <tbody>
                {lines.map((line) => (
                  <tr key={line.key}>
                    <td>
                      {name(line.nameKey)}
                      {line.archived ? ` · ${t('admin.players.archived')}` : ''}
                    </td>
                    <td>{line.markLevel}</td>
                    <td>{line.conditionPercent}</td>
                    <td>{line.hasRaceBonus ? t('admin.table.yes') : t('admin.table.no')}</td>
                    <td>{formatNumber(line.quantity, language, 0)}</td>
                    <td>
                      <DraftCell
                        key={`${line.key}:${line.quantity}`}
                        label={t('admin.players.quantity')}
                        initial={line.quantity}
                        allowNegative={false}
                        requirePositive={line.stackId === null}
                        disabled={line.stackId === null ? !(canGrant && reasonReady && !busy) : !(canSet && reasonReady && !busy)}
                        saveLabel={t('admin.players.save')}
                        onSave={(amount) => {
                          if (line.stackId === null) {
                            if (amount > 0) {
                              onGrant(line.partTypeId, amount, false, null);
                            }

                            return;
                          }

                          onQuantity(line.stackId, amount);
                        }}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      ) : null}
    </LoadGate>
  );
}

function OrdersSection({
  userId,
  planetId,
  reloadKey,
  kind,
  reasonReady,
  busy,
  canCancel,
  onCancel,
}: {
  userId: string;
  planetId: number;
  reloadKey: number;
  kind: 'production' | 'processes';
  reasonReady: boolean;
  busy: boolean;
  canCancel: boolean;
  onCancel: (orderId: number) => void;
}) {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, onError } = useAdmin();
  const requestKey = `${kind}:${userId}:${planetId}:${reloadKey}`;
  const [loaded, setLoaded] = useState<{ key: string; rows: ProcessOrderRow[]; error: AdminMessageKey | null }>({ key: '', rows: [], error: null });

  useEffect(() => {
    let active = true;
    const request = kind === 'production'
      ? players.production(userId, planetId).then((view) => view.orders.map(toProcessShape))
      : players.processes(userId, planetId);

    request.then(
      (rows) => {
        if (active) {
          setLoaded({ key: requestKey, rows, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, rows: [], error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, planetId, kind, onError, requestKey]);

  return (
    <LoadGate pending={loaded.key !== requestKey} errorMessage={loaded.error}>
      {loaded.rows.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
        <div className="admin-table__scroll">
          <table>
            <thead>
              <tr>
                <th>{t('admin.players.order')}</th>
                <th>{t('admin.players.quantity')}</th>
                <th>{t('admin.players.produced')}</th>
                <th>{t('admin.players.position')}</th>
                <th>{t('admin.players.finishes')}</th>
                <th>{t('admin.players.cancel')}</th>
              </tr>
            </thead>
            <tbody>
              {loaded.rows.map((order) => (
                <tr key={order.id}>
                  <td>{name(order.name_key)}</td>
                  <td>{formatNumber(order.quantity, language, 0)}</td>
                  <td>{formatNumber(order.produced_quantity, language, 0)}</td>
                  <td>{order.position}</td>
                  <td>{formatMoment(order.finishes_at, language)}</td>
                  <td>
                    <button className="btn btn--secondary" type="button" disabled={!(canCancel && reasonReady && !busy)} onClick={() => onCancel(order.id)}>
                      {t('admin.players.cancel')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </LoadGate>
  );
}

function DesignsSection({
  userId,
  reloadKey,
  reasonReady,
  busy,
  canRename,
  canArchive,
  onRename,
  onArchive,
}: {
  userId: string;
  reloadKey: number;
  reasonReady: boolean;
  busy: boolean;
  canRename: boolean;
  canArchive: boolean;
  onRename: (designId: number, designName: string) => void;
  onArchive: (designId: number) => void;
}) {
  const { t, language } = useAdminText();
  const { players, onError } = useAdmin();
  const requestKey = `${userId}:${reloadKey}`;
  const [loaded, setLoaded] = useState<{ key: string; rows: ShipDesign[]; error: AdminMessageKey | null }>({ key: '', rows: [], error: null });

  useEffect(() => {
    let active = true;

    players.designs(userId).then(
      (rows) => {
        if (active) {
          setLoaded({ key: requestKey, rows, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, rows: [], error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, onError, requestKey]);

  return (
    <LoadGate pending={loaded.key !== requestKey} errorMessage={loaded.error}>
      <p className="hint">{t('admin.players.slotsReadOnly')}</p>
      {loaded.rows.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : loaded.rows.map((design) => (
        <DesignCard
          key={`${design.id}:${design.version}:${design.name}`}
          design={design}
          language={language}
          canRename={canRename && reasonReady && !busy}
          canArchive={canArchive && reasonReady && !busy && !design.is_archived}
          onRename={(designName) => onRename(design.id, designName)}
          onArchive={() => onArchive(design.id)}
        />
      ))}
    </LoadGate>
  );
}

function DesignCard({
  design,
  language,
  canRename,
  canArchive,
  onRename,
  onArchive,
}: {
  design: ShipDesign;
  language: 'uk' | 'en';
  canRename: boolean;
  canArchive: boolean;
  onRename: (designName: string) => void;
  onArchive: () => void;
}) {
  const { t } = useAdminText();
  const [designName, setDesignName] = useState(design.name);
  const summary = design.summary;

  return (
    <article className="admin-card__group">
      <h3>{design.name}</h3>
      <dl className="admin-card__facts">
        <dt>{t('admin.players.planet')}</dt>
        <dd>{t('admin.players.planetLabel', { id: design.planet_id })}</dd>
        <dt>{t('admin.players.hull')}</dt>
        <dd>{design.hull_part_type_id}</dd>
        <dt>{t('admin.players.archived')}</dt>
        <dd>{design.is_archived ? t('admin.table.yes') : t('admin.table.no')}</dd>
        <dt>{t('admin.col.updatedAt')}</dt>
        <dd>{formatMoment(design.updated_at, language)}</dd>
        <dt>{t('admin.players.freeSlots')}</dt>
        <dd>{summary.free_slots}/{summary.total_slots}</dd>
        <dt>{t('admin.players.weight')}</dt>
        <dd>{formatNumber(summary.weight, language, 2)}</dd>
        <dt>{t('admin.players.crew')}</dt>
        <dd>{formatNumber(summary.crew, language, 2)}</dd>
        <dt>{t('admin.players.hold')}</dt>
        <dd>{formatNumber(summary.hold_capacity, language, 2)}</dd>
        <dt>{t('admin.players.integrity')}</dt>
        <dd>{formatNumber(summary.integrity, language, 2)}</dd>
        <dt>{t('admin.players.energyUse')}</dt>
        <dd>{formatNumber(summary.energy_use, language, 2)}</dd>
        <dt>{t('admin.players.energyPower')}</dt>
        <dd>{formatNumber(summary.energy_power, language, 2)}</dd>
        <dt>{t('admin.players.drive')}</dt>
        <dd>{formatNumber(summary.drive_push, language, 2)}</dd>
        <dt>{t('admin.players.energyDamage')}</dt>
        <dd>{formatNumber(summary.energy_damage, language, 2)}</dd>
        <dt>{t('admin.players.kineticDamage')}</dt>
        <dd>{formatNumber(summary.kinetic_damage, language, 2)}</dd>
        <dt>{t('admin.players.shield')}</dt>
        <dd>{formatNumber(summary.shield_capacity, language, 2)}</dd>
        <dt>{t('admin.players.hasDrive')}</dt>
        <dd>{summary.has_drive ? t('admin.table.yes') : t('admin.table.no')}</dd>
        <dt>{t('admin.players.hasReactor')}</dt>
        <dd>{summary.has_reactor ? t('admin.table.yes') : t('admin.table.no')}</dd>
      </dl>
      {design.slots.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
        <div className="admin-table__scroll">
          <table>
            <thead>
              <tr>
                <th>{t('admin.players.part')}</th>
                <th>{t('admin.players.mark')}</th>
                <th>{t('admin.players.raceBonus')}</th>
              </tr>
            </thead>
            <tbody>
              {design.slots.map((slot) => (
                <tr key={slot.slot_index}>
                  <td>{t('admin.players.slotLine', { index: slot.slot_index, part: slot.part_type_id, mark: slot.mark_level })}</td>
                  <td>{slot.mark_level}</td>
                  <td>{slot.has_race_bonus ? t('admin.table.yes') : t('admin.table.no')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="admin-inline-save admin-inline-save--text">
        <label className="admin-field">
          <span>{t('admin.players.name')}</span>
          <input className="input" value={designName} onChange={(event) => setDesignName(event.target.value)} />
        </label>
        <button className="btn btn--primary" type="button" disabled={!canRename || designName.trim() === '' || design.is_archived} onClick={() => onRename(designName.trim())}>
          {t('admin.players.rename')}
        </button>
        <button className="btn btn--secondary" type="button" disabled={!canArchive} onClick={onArchive}>
          {t('admin.players.archive')}
        </button>
      </div>
    </article>
  );
}

function MarketSection({
  userId,
  reloadKey,
  reasonReady,
  busy,
  canCancel,
  onCancel,
}: {
  userId: string;
  reloadKey: number;
  reasonReady: boolean;
  busy: boolean;
  canCancel: boolean;
  onCancel: (lotId: number) => void;
}) {
  const { t, language } = useAdminText();
  const { name } = useTexts();
  const { players, onError } = useAdmin();
  const [offset, setOffset] = useState(0);
  const requestKey = `${userId}:${reloadKey}:${offset}`;
  const [loaded, setLoaded] = useState<{ key: string; view: MarketView | null; error: AdminMessageKey | null }>({ key: '', view: null, error: null });

  useEffect(() => {
    let active = true;

    players.market(userId, offset).then(
      (view) => {
        if (active) {
          setLoaded({ key: requestKey, view, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, view: null, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, offset, onError, requestKey]);

  const view = loaded.view;
  const goods = (resourceTypeId: number | null, partTypeId: number | null) => {
    if (!view) {
      return '—';
    }

    if (resourceTypeId !== null) {
      return name(view.resources.find((entry) => entry.id === resourceTypeId)?.name_key);
    }

    if (partTypeId !== null) {
      return name(view.parts.find((entry) => entry.id === partTypeId)?.name_key);
    }

    return '—';
  };

  return (
    <LoadGate pending={loaded.key !== requestKey} errorMessage={loaded.error}>
      {view ? (
        <>
          <h3>{t('admin.players.lots')}</h3>
          {view.lots.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
            <div className="admin-table__scroll">
              <table>
                <thead>
                  <tr>
                    <th>{t('admin.players.part')}</th>
                    <th>{t('admin.players.quantity')}</th>
                    <th>{t('admin.players.price')}</th>
                    <th>{t('admin.players.totalPrice')}</th>
                    <th>{t('admin.players.state')}</th>
                    <th>{t('admin.players.cancel')}</th>
                  </tr>
                </thead>
                <tbody>
                  {view.lots.map((lot) => (
                    <tr key={lot.id}>
                      <td>
                        {goods(lot.resource_type_id, lot.part_type_id)}
                        {lot.part_mark_level !== null ? ` · ${t('admin.players.mark')} ${lot.part_mark_level}` : ''}
                        {lot.part_condition_percent !== null ? ` · ${lot.part_condition_percent}%` : ''}
                        {lot.part_has_race_bonus ? ` · ${t('admin.players.raceBonus')}` : ''}
                      </td>
                      <td>{formatNumber(lot.quantity, language, 0)}</td>
                      <td>{formatNumber(lot.price_per_unit, language, 0)}</td>
                      <td>{formatNumber(lot.total_price, language, 0)}</td>
                      <td>{lot.state}</td>
                      <td>
                        <button className="btn btn--secondary" type="button" disabled={!(canCancel && reasonReady && !busy)} onClick={() => onCancel(lot.id)}>
                          {t('admin.players.cancel')}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <h3>{t('admin.players.deals')}</h3>
          {view.deals.items.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
            <div className="admin-table__scroll">
              <table>
                <thead>
                  <tr>
                    <th>{t('admin.players.direction')}</th>
                    <th>{t('admin.players.counterparty')}</th>
                    <th>{t('admin.players.part')}</th>
                    <th>{t('admin.players.quantity')}</th>
                    <th>{t('admin.players.totalPrice')}</th>
                    <th>{t('admin.players.registered')}</th>
                  </tr>
                </thead>
                <tbody>
                  {view.deals.items.map((deal) => (
                    <tr key={deal.id}>
                      <td>{deal.direction === 'bought' ? t('admin.players.bought') : deal.direction === 'sold' ? t('admin.players.sold') : deal.direction}</td>
                      <td>{deal.counterparty.nickname}</td>
                      <td>{goods(deal.resource_type_id, deal.part_type_id)}</td>
                      <td>{formatNumber(deal.quantity, language, 0)}</td>
                      <td>{formatNumber(deal.sum, language, 0)}</td>
                      <td>{formatMoment(deal.created_at, language)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Pager
            page={view.deals.limit > 0 ? Math.floor(view.deals.offset / view.deals.limit) + 1 : 1}
            pages={view.deals.limit > 0 ? Math.max(1, Math.ceil(view.deals.total / view.deals.limit)) : 1}
            total={view.deals.total}
            onPage={(nextPage) => setOffset(Math.max(0, (nextPage - 1) * (view.deals.limit || 1)))}
          />
          <DeliveryTable title="admin.players.incoming" rows={view.deliveries.incoming} goods={goods} />
          <DeliveryTable title="admin.players.outgoing" rows={view.deliveries.outgoing} goods={goods} />
        </>
      ) : null}
    </LoadGate>
  );
}

function DeliveryTable({
  title,
  rows,
  goods,
}: {
  title: AdminMessageKey;
  rows: MarketDelivery[];
  goods: (resourceTypeId: number | null, partTypeId: number | null) => string;
}) {
  const { t, language } = useAdminText();

  return (
    <>
      <h3>{t(title)}</h3>
      {rows.length === 0 ? <p className="hint">{t('admin.players.emptySection')}</p> : (
        <div className="admin-table__scroll">
          <table>
            <thead>
              <tr>
                <th>{t('admin.players.counterparty')}</th>
                <th>{t('admin.players.part')}</th>
                <th>{t('admin.players.quantity')}</th>
                <th>{t('admin.players.state')}</th>
                <th>{t('admin.players.finishes')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((delivery) => (
                <tr key={delivery.id}>
                  <td>{delivery.counterparty.nickname}</td>
                  <td>{goods(delivery.resource_type_id, delivery.part_type_id)}</td>
                  <td>{formatNumber(delivery.quantity, language, 0)}</td>
                  <td>{delivery.state}</td>
                  <td>{formatMoment(delivery.arrives_at, language)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

function useProduction(userId: string, planetId: number, reloadKey: number, source: 'parts' | 'orders') {
  const { players, onError } = useAdmin();
  const requestKey = `${source}:${userId}:${planetId}:${reloadKey}`;
  const [loaded, setLoaded] = useState<{ key: string; view: ProductionView | null; error: AdminMessageKey | null }>({ key: '', view: null, error: null });

  useEffect(() => {
    let active = true;
    const request = source === 'parts' ? players.parts(userId, planetId) : players.production(userId, planetId);

    request.then(
      (view) => {
        if (active) {
          setLoaded({ key: requestKey, view, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setLoaded({ key: requestKey, view: null, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [players, userId, planetId, source, onError, requestKey]);

  return { pending: loaded.key !== requestKey, view: loaded.view, error: loaded.error };
}

function NumberTable<T>({
  rows,
  rowKey,
  columns,
  initial,
  allowNegative,
  canSave,
  saveLabel,
  onSave,
}: {
  rows: T[];
  rowKey: (row: T) => string;
  columns: { header: string; cell: (row: T) => string }[];
  initial: (row: T) => number;
  allowNegative: boolean;
  canSave: boolean;
  saveLabel: string;
  onSave: (row: T, amount: number) => void;
}) {
  const { t } = useAdminText();

  if (rows.length === 0) {
    return <p className="hint">{t('admin.players.emptySection')}</p>;
  }

  return (
    <div className="admin-table__scroll">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.header}>{column.header}</th>
            ))}
            <th>{t('admin.players.amount')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column) => (
                <td key={column.header}>{column.cell(row)}</td>
              ))}
              <td>
                <DraftCell
                  key={`${rowKey(row)}:${initial(row)}`}
                  label={t('admin.players.amount')}
                  initial={initial(row)}
                  allowNegative={allowNegative}
                  disabled={!canSave}
                  saveLabel={saveLabel}
                  onSave={(amount) => onSave(row, amount)}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function QueueTable<T>({
  rows,
  nameOf,
  targetOf,
  positionOf,
  finishesOf,
  canCancel,
  onCancel,
}: {
  rows: T[];
  nameOf: (row: T) => string;
  targetOf: (row: T) => number;
  positionOf: (row: T) => number;
  finishesOf: (row: T) => string | null;
  canCancel: boolean;
  onCancel: (row: T) => void;
}) {
  const { t, language } = useAdminText();

  if (rows.length === 0) {
    return <p className="hint">{t('admin.players.emptySection')}</p>;
  }

  return (
    <div className="admin-table__scroll">
      <table>
        <thead>
          <tr>
            <th>{t('admin.players.name')}</th>
            <th>{t('admin.players.targetLevel')}</th>
            <th>{t('admin.players.position')}</th>
            <th>{t('admin.players.finishes')}</th>
            <th>{t('admin.players.cancel')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={`${positionOf(row)}:${index}`}>
              <td>{nameOf(row)}</td>
              <td>{targetOf(row)}</td>
              <td>{positionOf(row)}</td>
              <td>{formatMoment(finishesOf(row), language)}</td>
              <td>
                <button className="btn btn--secondary" type="button" disabled={!canCancel} onClick={() => onCancel(row)}>
                  {t('admin.players.cancel')}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function DraftCell({
  label,
  hideLabel = true,
  initial,
  allowNegative,
  requirePositive = false,
  disabled,
  saveLabel,
  onSave,
}: {
  label: string;
  hideLabel?: boolean;
  initial: number;
  allowNegative: boolean;
  requirePositive?: boolean;
  disabled: boolean;
  saveLabel: string;
  onSave: (amount: number) => void;
}) {
  const [text, setText] = useState(String(initial));
  const amount = wholeNumber(text);
  const accepted = amount !== null && (allowNegative || amount >= 0) && (!requirePositive || amount > 0);

  return (
    <span className={hideLabel ? 'admin-inline-save' : 'admin-inline-save admin-inline-save--text'}>
      <label className="admin-field">
        <span className={hideLabel ? 'admin-field__label--hidden' : undefined}>{label}</span>
        <input className="input" inputMode="numeric" value={text} onChange={(event) => setText(event.target.value)} />
      </label>
      <button
        className="btn btn--primary"
        type="button"
        disabled={disabled || !accepted}
        onClick={() => {
          if (amount !== null) {
            onSave(amount);
          }
        }}
      >
        {saveLabel}
      </button>
    </span>
  );
}

function Pager({ page, pages, total, onPage }: { page: number; pages: number; total: number; onPage: (page: number) => void }) {
  const { t } = useAdminText();

  return (
    <div className="admin-table__pager">
      <span>{t('admin.table.total', { total })}</span>
      <span>{t('admin.table.page', { page, pages })}</span>
      <button className="btn btn--secondary" type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        {t('admin.table.previous')}
      </button>
      <button className="btn btn--secondary" type="button" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        {t('admin.table.next')}
      </button>
    </div>
  );
}

function LoadGate({ pending, errorMessage, children }: { pending: boolean; errorMessage: AdminMessageKey | null; children: ReactNode }) {
  const { t } = useAdminText();

  if (pending) {
    return <p className="hint">{t('admin.loading')}</p>;
  }

  if (errorMessage) {
    return <Alert kind="error">{t(errorMessage)}</Alert>;
  }

  return children;
}

function asColonyTab(id: TabId): 'stock' | 'population' | 'buildings' | 'construction' {
  return id === 'population' || id === 'buildings' || id === 'construction' ? id : 'stock';
}

function planetCaption(planet: PlanetLabel, t: (key: AdminMessageKey, params?: Record<string, string | number>) => string): string {
  const planetName = planet.name?.trim() ? planet.name : t('admin.players.planetLabel', { id: planet.planet_id });

  return `${planetName} · ${t('admin.players.sector', { id: planet.sector_id })}`;
}

function toProcessShape(order: ProductionOrderRow): ProcessOrderRow {
  return {
    id: order.id,
    planet_building_id: order.planet_building_id,
    process_recipe_id: order.recipe_id,
    name_key: order.name_key,
    quantity: order.quantity,
    produced_quantity: order.produced_quantity,
    position: order.position,
    finishes_at: order.finishes_at,
  };
}

/**
 * A whole number the JSON body can carry. A fraction or a number past the safe integer range is refused.
 */
function wholeNumber(text: string): number | null {
  const trimmed = text.trim();

  if (!/^-?\d+$/.test(trimmed)) {
    return null;
  }

  const parsed = Number(trimmed);

  return Number.isSafeInteger(parsed) ? parsed : null;
}
