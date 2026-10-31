import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import type { MarketLot, MyLot } from '../../../api/marketApi';
import { useApi } from '../../../api/useApi';
import { useDocumentTitle } from '../../../components/useDocumentTitle';
import { useI18n } from '../../../i18n/useI18n';
import { useSession } from '../../../session/useSession';
import { noticeFromError } from '../screenNotice';
import { useCurrentPlanetId, useShell, useShellSelector } from '../useShell';
import { BuyDialog } from './BuyDialog';
import { DeliveriesView } from './DeliveriesView';
import { ExchangeView } from './ExchangeView';
import { HistoryView } from './HistoryView';
import { LevelSwitch } from './LevelSwitch';
import { levelLabelKeys } from './levelLabels';
import { LotsView } from './LotsView';
import { useMarketPlanets } from './marketPlanets';
import { formatDuration } from './marketFormat';
import { MarketTabs, type MarketTab } from './MarketTabs';
import { ListSkeleton, ReadError } from './MarketStates';
import { MineView } from './MineView';
import { SellView, type SellItem } from './SellView';
import { useGameClock } from './useGameClock';
import { useMarketItems } from './useMarketItems';
import { useRead } from './useRead';

const tabIds = ['buy', 'sell', 'mine', 'del', 'ex', 'hist'] as const;
type TabId = (typeof tabIds)[number];

const tabLabelKeys = {
  buy: 'market.tBuy',
  sell: 'market.tSell',
  mine: 'market.tMine',
  del: 'market.tDel',
  ex: 'market.tEx',
  hist: 'market.tHist',
} as const;

/**
 * How long the message about a done command stays on the screen, in milliseconds.
 */
const flashMs = 6000;

/**
 * The prefixes of the refusals of the server that have their own text for the player here.
 */
const reasonPrefixes = ['market_', 'planets_', 'economy_'];

/**
 * The screen of the market (task 00026): the level of the market, the tabs Buy, Sell, My lots,
 * Deliveries, Exchange and History, the dialog of the purchase and the message of the last command.
 * Every number is the answer of the server of the market (task 00024): the page sorts and filters
 * what it got and never decides a rule.
 *
 * The frame of the screen is the common one of every screen of the game.
 */
export function MarketPage() {
  const { t, language } = useI18n();

  useDocumentTitle(t('meta.market.title'));

  const { market } = useApi();
  const { session } = useSession();
  const { actions } = useShell();
  const panelId = useId();
  const currentPlanetId = useCurrentPlanetId();
  const civilization = session.status === 'signed-in' ? session.me.civilization : null;

  const loadOverview = useCallback(() => market.overview(), [market]);
  const overview = useRead(loadOverview);
  const loadMine = useCallback(() => market.myLots(), [market]);
  const mine = useRead(loadMine);
  const loadDeliveries = useCallback(() => market.deliveries(), [market]);
  const deliveries = useRead(loadDeliveries);
  const planets = useMarketPlanets(overview.data);
  const itemView = useMarketItems();
  const nowMs = useGameClock(overview);
  const balances = useShellSelector((state) => state.balances);

  const [tab, setTab] = useState<TabId>('buy');
  const [levelId, setLevelId] = useState<number | null>(null);
  const [dialogLot, setDialogLot] = useState<MarketLot | null>(null);
  const [flash, setFlash] = useState('');
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [lotsKey, setLotsKey] = useState(0);
  const flashTimer = useRef<number | null>(null);

  const loadRate = useCallback(() => market.rate(), [market]);
  const rate = useRead(loadRate, tab === 'ex');
  const loadHistory = useCallback(() => market.rateHistory(), [market]);
  const rateHistory = useRead(loadHistory, tab === 'ex');
  const loadDeals = useCallback(() => market.deals(), [market]);
  const deals = useRead(loadDeals, tab === 'hist');

  useEffect(
    () => () => {
      if (flashTimer.current !== null) {
        window.clearTimeout(flashTimer.current);
      }
    },
    [],
  );

  const levels = useMemo(() => overview.data?.levels ?? [], [overview.data]);
  const level = levels.find((candidate) => candidate.id === levelId && candidate.isActive) ?? levels.find((candidate) => candidate.isActive) ?? null;
  const sectorName = useShellSelector((state) => {
    const planet = currentPlanetId === null ? undefined : state.planets.get(currentPlanetId)?.planet;

    return planet?.coordinates?.sectorName?.trim() || (planet?.coordinates ? String(planet.coordinates.sectorId) : null);
  });

  const amountOf = (resourceTypeId: number | undefined) => balances.find((balance) => balance.resourceTypeId === resourceTypeId)?.amount ?? 0;
  const tradeBalance = amountOf(overview.data?.tradeResourceTypeId);
  const creditsBalance = amountOf(overview.data?.creditsResourceTypeId);

  const showFlash = (text: string) => {
    setFlash(text);

    if (flashTimer.current !== null) {
      window.clearTimeout(flashTimer.current);
    }

    flashTimer.current = window.setTimeout(() => setFlash(''), flashMs);
  };

  const refreshEverything = (planetId: number | null) => {
    actions.refreshAfterCommand(planetId);
    setLotsKey((value) => value + 1);
    void overview.reload();
    void mine.reload();
    void deliveries.reload();
  };

  /**
   * Runs a command of the market: the refusal of the server becomes a message the player reads, and
   * a command that went through reads again what it changed.
   */
  const run = async (action: () => Promise<void>, planetId: number | null) => {
    setBusy(true);
    setProblem(null);

    try {
      await action();
      refreshEverything(planetId);
    } catch (error) {
      setProblem(noticeFromError(error, language, t('error.generic.text'), reasonPrefixes)?.text ?? null);
    } finally {
      setBusy(false);
    }
  };

  const onBuy = (planetId: number, seconds: number) =>
    dialogLot === null
      ? Promise.resolve()
      : run(async () => {
          await market.buy(dialogLot.id, planetId);
          setDialogLot(null);
          showFlash(t('market.flashBuy', { time: formatDuration(seconds, t) }));
        }, planetId);

  const onList = (request: { planetId: number; item: SellItem; quantity: number; pricePerUnit: number }) =>
    level === null
      ? Promise.resolve()
      : run(async () => {
          await market.listLot({
            planetId: request.planetId,
            marketLevelId: level.id,
            quantity: request.quantity,
            pricePerUnit: request.pricePerUnit,
            resourceTypeId: request.item.resourceTypeId ?? undefined,
            partTypeId: request.item.partTypeId ?? undefined,
            partConditionPercent: request.item.conditionPercent ?? undefined,
          });
          showFlash(t('market.flashList'));
        }, request.planetId);

  const onCancel = (lot: MyLot) =>
    run(async () => {
      await market.cancelLot(lot.id);
      showFlash(t('market.flashCancel'));
    }, lot.sellerPlanetId);

  const onExchange = (giveResourceTypeId: number, amount: number) =>
    run(async () => {
      await market.exchange(giveResourceTypeId, amount);
      void rate.reload();
      void rateHistory.reload();
      showFlash(t('market.flashEx'));
    }, null);

  const closeDialog = () => {
    setDialogLot(null);
    setProblem(null);
  };

  const changeTab = (id: string) => {
    setTab(id as TabId);
    setProblem(null);
    setDialogLot(null);
  };

  const tabs: MarketTab[] = tabIds.map((id) => ({
    id,
    label: t(tabLabelKeys[id]),
    badge: id === 'mine' ? mine.data?.length : id === 'del' ? (deliveries.data?.incoming ?? []).concat(deliveries.data?.outgoing ?? []).filter((delivery) => delivery.state !== 'arrived').length : undefined,
  }));
  const subline = level && levelLabelKeys[level.visibility] === 'market.lvGalaxy' ? t('market.subGalaxy') : t('market.subSector', { sector: sectorName ?? t('market.sectorFallback') });

  if (!civilization) {
    return null;
  }

  return (
    <div className="civ-main__in mk">
      <div className="mk-head">
        <h1 className="player__title">{t('market.title')}</h1>
        <p className="meta">{subline}</p>
      </div>
      {overview.data !== null ? <LevelSwitch levels={levels} onPick={(id) => { setLevelId(id); setDialogLot(null); }} selectedId={level?.id ?? null} /> : null}
      <MarketTabs active={tab} label={t('market.tabsAria')} onChange={changeTab} panelId={panelId} tabs={tabs} />
      {flash ? (
        <div className="mk-flash" role="status">
          <GameIcon name="check" size={18} />
          <span>{flash}</span>
        </div>
      ) : null}
      <div aria-live="polite" className="mk-panelbox" id={panelId} role="tabpanel">
        {overview.data === null ? (
          overview.failed ? (
            <ReadError onRetry={() => void overview.reload()} />
          ) : (
            <ListSkeleton />
          )
        ) : (
          <>
            {tab === 'buy' ? <LotsView itemView={itemView} levelId={level?.id ?? null} onBuy={(lot) => { setProblem(null); setDialogLot(lot); }} planetId={currentPlanetId} reloadKey={lotsKey} /> : null}
            {tab === 'sell' ? <SellView busy={busy} error={problem} initialPlanetId={currentPlanetId} itemView={itemView} level={level} onList={(request) => void onList(request)} overview={overview.data} planets={planets} /> : null}
            {tab === 'mine' ? <MineView busy={busy} failed={mine.failed} itemView={itemView} levels={levels} lots={mine.data} nowMs={nowMs()} onCancel={(lot) => void onCancel(lot)} onRetry={() => void mine.reload()} /> : null}
            {tab === 'del' ? <DeliveriesView deliveries={deliveries.data} failed={deliveries.failed} itemView={itemView} nowMs={nowMs} onArrive={() => refreshEverything(currentPlanetId)} onRetry={() => void deliveries.reload()} planets={planets} timeMultiplier={overview.data.timeMultiplier} /> : null}
            {tab === 'ex' ? <ExchangeView busy={busy} credits={creditsBalance} error={problem} history={rateHistory.data} onExchange={(id, amount) => void onExchange(id, amount)} overview={overview.data} rate={rate.data} trade={tradeBalance} /> : null}
            {tab === 'hist' ? <HistoryView deals={deals.data} failed={deals.failed} itemView={itemView} onRetry={() => void deals.reload()} /> : null}
          </>
        )}
      </div>
      {dialogLot ? (
        <BuyDialog
          busy={busy}
          error={problem}
          initialPlanetId={currentPlanetId}
          lot={dialogLot}
          onClose={closeDialog}
          onConfirm={(planetId, seconds) => void onBuy(planetId, seconds)}
          planets={planets}
          tradeBalance={tradeBalance}
          view={itemView(dialogLot.resourceTypeId, dialogLot.partTypeId)}
        />
      ) : null}
    </div>
  );
}
