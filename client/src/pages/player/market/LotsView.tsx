import { useCallback, useMemo, useState } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import type { MarketLot } from '../../../api/marketApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { useStock } from '../useStock';
import { formatDuration, formatInteger, parseWhole } from './marketFormat';
import { ItemCell, EmptyBox, RodIcon, SellerPill, TradeIcon } from './MarketBits';
import { ListSkeleton, ReadError } from './MarketStates';
import { lotReasonKeys } from './lotReasons';
import { useIsPhone } from './useIsPhone';
import type { ItemView } from './useMarketItems';
import { useRead } from './useRead';

type Kind = 'all' | 'res' | 'prod' | 'part';
type SortKey = 'item' | 'qty' | 'price' | 'total' | 'time' | 'rods';

const kinds: readonly Kind[] = ['all', 'res', 'prod', 'part'];

/**
 * A lot with what the screen needs to show it: its look, its seller and the reason it cannot be bought.
 */
interface LotRow {
  lot: MarketLot;
  view: ItemView;
  reason: string | null;
}

/**
 * The tab "Buy": the filters, the table of the lots (cards on a phone) and the button that opens the
 * dialog of the purchase. The server counts the delivery time, the fee and the reason a lot cannot
 * be bought for the body that is chosen; the client only sorts and filters what it got.
 *
 * @param levelId The chosen level of the market.
 * @param planetId The body of the player the goods would arrive at.
 * @param itemView How an item looks.
 * @param onBuy What to do when the player opens the purchase of a lot.
 * @param reloadKey A number that changes when the lots must be read again, after a command.
 */
export function LotsView({
  levelId,
  planetId,
  itemView,
  onBuy,
  reloadKey,
}: {
  levelId: number | null;
  planetId: number | null;
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  onBuy: (lot: MarketLot) => void;
  reloadKey: number;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const { market } = useApi();
  const phone = useIsPhone();
  const stock = useStock(planetId);
  const [kind, setKind] = useState<Kind>('all');
  const [search, setSearch] = useState('');
  const [minPrice, setMinPrice] = useState('');
  const [maxPrice, setMaxPrice] = useState('');
  const [sort, setSort] = useState<{ key: SortKey; direction: 1 | -1 }>({ key: 'time', direction: 1 });

  const load = useCallback(
    () => market.lots({ marketLevelId: levelId ?? undefined, planetId: planetId ?? undefined }).then((answer) => ({ answer, reloadKey })),
    [market, levelId, planetId, reloadKey],
  );
  const read = useRead(load, levelId !== null);

  const produced = useMemo(() => new Set(stock.stock?.producedResourceTypeIds ?? []), [stock.stock]);

  const rows = useMemo(() => {
    const items = read.data?.answer.items ?? [];
    const needle = search.trim().toLowerCase();
    const low = parseWhole(minPrice);
    const high = parseWhole(maxPrice);
    const decorated: LotRow[] = items.map((lot) => ({
      lot,
      view: itemView(lot.resourceTypeId, lot.partTypeId),
      reason: lot.unavailableReason,
    }));
    const kept = decorated.filter(({ lot, view }) => {
      const lotKind: Kind = lot.partTypeId !== null ? 'part' : produced.has(lot.resourceTypeId ?? 0) ? 'prod' : 'res';

      return (
        (kind === 'all' || kind === lotKind) &&
        (needle === '' || view.name.toLowerCase().includes(needle)) &&
        (low === null || lot.pricePerUnit >= low) &&
        (high === null || lot.pricePerUnit <= high)
      );
    });
    const keyOf: Record<SortKey, (row: LotRow) => number | string> = {
      item: (row) => row.view.name,
      qty: (row) => row.lot.quantity,
      price: (row) => row.lot.pricePerUnit,
      total: (row) => row.lot.totalPrice,
      time: (row) => row.lot.deliverySeconds ?? 0,
      rods: (row) => row.lot.energyRodAmount ?? 0,
    };

    return kept.sort((a, b) => {
      const x = keyOf[sort.key](a);
      const y = keyOf[sort.key](b);
      const order = typeof x === 'string' ? x.localeCompare(String(y), language) : x - Number(y);

      return order === 0 ? a.lot.id - b.lot.id : order * sort.direction;
    });
  }, [read.data, itemView, produced, kind, search, minPrice, maxPrice, sort, language]);

  const reasonText = (reason: string | null) =>
    reason === null ? null : reason in lotReasonKeys ? t(lotReasonKeys[reason]) : text(reason) ?? reason;
  const pick = (key: SortKey) => setSort((previous) => (previous.key === key ? { key, direction: previous.direction === 1 ? -1 : 1 } : { key, direction: 1 }));
  const resetFilters = () => {
    setKind('all');
    setSearch('');
    setMinPrice('');
    setMaxPrice('');
  };
  const rodsHave = read.data?.answer.energyRodBalance ?? null;
  const noPortal = read.data?.answer.destinationHasPortal === false;

  return (
    <>
      <div className="mk-toolbar">
        <div className="mk-search">
          <GameIcon className="mk-search__icon" name="search" size={20} />
          <label className="visually-hidden" htmlFor="mk-search">
            {t('market.search')}
          </label>
          <input autoComplete="off" className="mk-input mk-input--search" id="mk-search" onChange={(event) => setSearch(event.target.value)} placeholder={t('market.search')} type="text" value={search} />
        </div>
        <div aria-label={t('market.kindAria')} className="mk-kinds" role="group">
          {kinds.map((id) => (
            <button aria-pressed={kind === id} className="mk-chip" key={id} onClick={() => setKind(id)} type="button">
              {t(`market.k${id === 'all' ? 'All' : id === 'res' ? 'Res' : id === 'prod' ? 'Prod' : 'Part'}` as const)}
            </button>
          ))}
        </div>
        <div aria-label={t('market.priceAria')} className="mk-range" role="group">
          <span className="mk-range__label">{t('market.priceLabel')}</span>
          <label className="visually-hidden" htmlFor="mk-pmin">
            {t('market.from')}
          </label>
          <input autoComplete="off" className="mk-input mk-input--num" id="mk-pmin" inputMode="numeric" onChange={(event) => setMinPrice(event.target.value)} placeholder={t('market.from')} type="text" value={minPrice} />
          <span aria-hidden="true" className="mk-range__dash">
            –
          </span>
          <label className="visually-hidden" htmlFor="mk-pmax">
            {t('market.to')}
          </label>
          <input autoComplete="off" className="mk-input mk-input--num" id="mk-pmax" inputMode="numeric" onChange={(event) => setMaxPrice(event.target.value)} placeholder={t('market.to')} type="text" value={maxPrice} />
        </div>
        {rodsHave !== null ? (
          <span className="mk-rods-have" title={t('market.rodsHave')}>
            <RodIcon size={16} />
            {t('market.rodsHave')}: <b>{formatInteger(rodsHave, language)}</b>
          </span>
        ) : null}
      </div>
      {noPortal ? (
        <div className="mk-warn" role="status">
          <GameIcon name="warn" size={18} />
          <span>{t('market.noPortalBuy')}</span>
        </div>
      ) : null}
      {read.failed && read.data === null ? <ReadError onRetry={() => void read.reload()} /> : null}
      {read.loading && read.data === null && !read.failed ? <ListSkeleton /> : null}
      {read.data !== null && rows.length === 0 ? (
        <EmptyBox icon="market" title={t('market.noLots')}>
          <button className="mk-btn mk-btn--secondary" onClick={resetFilters} type="button">
            {t('market.reset')}
          </button>
        </EmptyBox>
      ) : null}
      {read.data !== null && rows.length > 0 && !phone ? (
        <section className="mk-panel">
          <div aria-label={t('market.lotsAria')} className="mk-table mk-table--buy" role="table">
            <div role="rowgroup">
              <div className="mk-row mk-row--head" role="row">
                {(
                  [
                    ['item', 'market.cItem', 'l'],
                    ['qty', 'market.cQty', 'r'],
                    ['price', 'market.cUnit', 'r'],
                    ['total', 'market.cTotal', 'r'],
                    [null, 'market.cSeller', 'l'],
                    ['time', 'market.cDelivery', 'r'],
                    ['rods', 'market.cRods', 'r'],
                    [null, null, 'r'],
                  ] as const
                ).map(([key, label, align], index) => (
                  <div
                    aria-sort={key && sort.key === key ? (sort.direction === 1 ? 'ascending' : 'descending') : key ? 'none' : undefined}
                    className={`mk-th mk-th--${align}`}
                    key={index}
                    role="columnheader"
                  >
                    {key && label ? (
                      <button aria-label={t('market.sortBy', { column: t(label) })} className={`mk-sort${sort.key === key ? ' is-active' : ''}`} onClick={() => pick(key)} type="button">
                        {t(label)}
                        <GameIcon name={sort.key === key ? (sort.direction === 1 ? 'arrowUp' : 'arrowDown') : 'sortBoth'} size={12} />
                      </button>
                    ) : label ? (
                      <span>{t(label)}</span>
                    ) : null}
                  </div>
                ))}
              </div>
            </div>
            <div role="rowgroup">
              {rows.map(({ lot, view, reason }) => {
                const why = reasonText(reason);
                const label = `${t('market.buy')}: ${view.name} × ${formatInteger(lot.quantity, language)}`;

                return (
                  <div className="mk-row" key={lot.id} role="row">
                    <div className="mk-td mk-td--name" role="rowheader">
                      <ItemCell condition={lot.partConditionPercent !== null ? t('market.conditionChip', { percent: lot.partConditionPercent }) : undefined} view={view} />
                    </div>
                    <div className="mk-td mk-td--r" role="cell">
                      {formatInteger(lot.quantity, language)}
                    </div>
                    <div className="mk-td mk-td--r mk-td--price" role="cell">
                      {lot.pricePerUnit === 0 ? (
                        <span className="mk-free">{t('market.free')}</span>
                      ) : (
                        <span className="mk-money">
                          <TradeIcon />
                          {formatInteger(lot.pricePerUnit, language)}
                        </span>
                      )}
                    </div>
                    <div className="mk-td mk-td--r" role="cell">
                      {lot.pricePerUnit === 0 ? '—' : formatInteger(lot.totalPrice, language)}
                    </div>
                    <div className="mk-td" role="cell">
                      <SellerPill colorKey={lot.seller.raceColorKey} nickname={lot.seller.nickname} raceName={lot.seller.raceNameKey ? t('market.sellerRace', { race: text(lot.seller.raceNameKey) ?? lot.seller.raceNameKey }) : null} />
                    </div>
                    <div className="mk-td mk-td--r" role="cell">
                      {lot.deliverySeconds === null ? '—' : formatDuration(lot.deliverySeconds, t)}
                    </div>
                    <div className="mk-td mk-td--r" role="cell">
                      {lot.energyRodAmount === null ? (
                        '—'
                      ) : (
                        <span className="mk-money">
                          <RodIcon />
                          {formatInteger(lot.energyRodAmount, language)}
                        </span>
                      )}
                    </div>
                    <div className="mk-td mk-td--btn" role="cell">
                      {why === null && planetId !== null ? (
                        <button aria-label={label} className="mk-btn mk-btn--primary mk-btn--cell" onClick={() => onBuy(lot)} type="button">
                          {t('market.buy')}
                        </button>
                      ) : (
                        <button aria-label={`${t('market.buy')}: ${view.name} — ${why ?? ''}`} className="mk-btn mk-btn--primary mk-btn--cell" disabled title={why ?? undefined} type="button">
                          {t('market.buy')}
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </section>
      ) : null}
      {read.data !== null && rows.length > 0 && phone ? (
        <div className="mk-cards">
          <div aria-label={t('market.sortAria')} className="mk-sortchips" role="group">
            {(
              [
                ['price', 'market.cUnit'],
                ['total', 'market.cTotal'],
                ['time', 'market.cDelivery'],
                ['qty', 'market.cQty'],
              ] as const
            ).map(([key, label]) => (
              <button aria-pressed={sort.key === key} className="mk-chip mk-chip--sort" key={key} onClick={() => pick(key)} type="button">
                {t(label)}
                {sort.key === key ? (sort.direction === 1 ? ' ↑' : ' ↓') : ''}
              </button>
            ))}
          </div>
          {rows.map(({ lot, view, reason }) => {
            const why = reasonText(reason);

            return (
              <article className="mk-card" key={lot.id}>
                <div className="mk-card__top">
                  <span className="mk-card__item">
                    <ItemCell condition={lot.partConditionPercent !== null ? t('market.conditionChip', { percent: lot.partConditionPercent }) : undefined} size={26} view={view} />
                  </span>
                  <span className="mk-card__qty">× {formatInteger(lot.quantity, language)}</span>
                </div>
                <div className="mk-card__mid">
                  <span className="mk-card__price">
                    {lot.pricePerUnit === 0 ? (
                      <span className="mk-free">{t('market.free')}</span>
                    ) : (
                      <>
                        <TradeIcon />
                        {formatInteger(lot.pricePerUnit, language)} {t('market.perUnit')}
                      </>
                    )}
                  </span>
                  <span className="mk-card__total">
                    {t('market.total')}: <b>{lot.pricePerUnit === 0 ? '—' : formatInteger(lot.totalPrice, language)}</b>
                  </span>
                </div>
                <div className="mk-card__low">
                  <SellerPill colorKey={lot.seller.raceColorKey} nickname={lot.seller.nickname} raceName={lot.seller.raceNameKey ? t('market.sellerRace', { race: text(lot.seller.raceNameKey) ?? lot.seller.raceNameKey }) : null} />
                  <span className="mk-card__meta">
                    <span>
                      {t('market.cDelivery')}: {lot.deliverySeconds === null ? '—' : formatDuration(lot.deliverySeconds, t)}
                    </span>
                    <span className="mk-money" title={t('market.cRods')}>
                      <RodIcon />
                      {lot.energyRodAmount === null ? '—' : formatInteger(lot.energyRodAmount, language)}
                    </span>
                  </span>
                </div>
                {why === null && planetId !== null ? (
                  <button className="mk-btn mk-btn--primary mk-btn--full" onClick={() => onBuy(lot)} type="button">
                    {t('market.buy')}
                  </button>
                ) : (
                  <div className="mk-card__off">
                    <button className="mk-btn mk-btn--primary mk-btn--full" disabled title={why ?? undefined} type="button">
                      {t('market.buy')}
                    </button>
                    {why ? <span className="mk-card__reason">{why}</span> : null}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      ) : null}
    </>
  );
}
