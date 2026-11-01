import { GameIcon } from '../../../components/GameIcon';
import type { MarketDeal, MarketDeals } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { formatInteger, formatStamp } from './marketFormat';
import { EmptyBox, ItemCell, SellerPill } from './MarketBits';
import { ListSkeleton, ReadError } from './MarketStates';
import { Pager } from './Pager';
import { useIsPhone } from './useIsPhone';
import type { ItemView } from './useMarketItems';

/**
 * The chip of the direction of a deal: an arrow and a word, so the direction is never told by colour alone.
 *
 * @param bought Whether the player bought.
 */
function DirectionChip({ bought }: { bought: boolean }) {
  const { t } = useI18n();

  return (
    <span className={`mk-dir ${bought ? 'mk-dir--bought' : 'mk-dir--sold'}`}>
      <GameIcon name={bought ? 'arrowDown' : 'arrowUp'} size={13} />
      {bought ? t('market.bought') : t('market.sold')}
    </span>
  );
}

/**
 * The tab "History": the deals of the player, bought and sold, with the other side of each deal.
 *
 * @param deals The page of the deals, or `null` while it is read.
 * @param failed Whether the read failed.
 * @param itemView How an item looks.
 * @param onPage What to do when the player moves to another page; it gets the offset of the page.
 * @param onRetry What to do when the player asks to read again.
 */
export function HistoryView({
  deals: page,
  failed,
  itemView,
  onPage,
  onRetry,
}: {
  deals: MarketDeals | null;
  failed: boolean;
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  onPage: (offset: number) => void;
  onRetry: () => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const phone = useIsPhone();

  const deals = page?.items ?? null;

  if (deals === null) {
    return failed ? <ReadError onRetry={onRetry} /> : <ListSkeleton />;
  }

  if (deals.length === 0) {
    return <EmptyBox icon="history" title={t('market.noHist')} />;
  }

  const pill = (deal: MarketDeal) => (
    <SellerPill
      colorKey={deal.counterparty.raceColorKey}
      nickname={deal.counterparty.nickname}
      raceName={deal.counterparty.raceNameKey ? t('market.sellerRace', { race: text(deal.counterparty.raceNameKey) ?? deal.counterparty.raceNameKey }) : null}
    />
  );

  const pager = page && page.total > page.limit ? <Pager limit={page.limit} offset={page.offset} onGo={onPage} total={page.total} /> : null;

  if (phone) {
    return (
      <>
      <div className="mk-cards">
        {deals.map((deal) => {
          const view = itemView(deal.resourceTypeId, deal.partTypeId);

          return (
            <article className="mk-card" key={deal.id}>
              <div className="mk-card__top">
                <span className="mk-card__item">
                  <ItemCell size={26} view={{ ...view, name: `${view.name} × ${formatInteger(deal.quantity, language)}` }} />
                </span>
                <DirectionChip bought={deal.direction === 'bought'} />
              </div>
              <div className="mk-card__mid">
                <span className="mk-card__price">
                  {formatInteger(deal.pricePerUnit, language)} {t('market.perUnit')}
                </span>
                <span className="mk-card__total">
                  {t('market.cFee')}: <b>{formatInteger(deal.commission, language)}</b>
                </span>
              </div>
              <div className="mk-card__low">
                {pill(deal)}
                <span className="mk-card__meta">{formatStamp(deal.createdAt)}</span>
              </div>
            </article>
          );
        })}
      </div>
      {pager}
      </>
    );
  }

  return (
    <>
    <section className="mk-panel">
      <div aria-label={t('market.tHist')} className="mk-table mk-table--hist" role="table">
        <div role="rowgroup">
          <div className="mk-row mk-row--head" role="row">
            {(
              [
                ['market.cDate', 'l'],
                ['market.cItem', 'l'],
                ['market.cQty', 'r'],
                ['market.cUnit', 'r'],
                ['market.cFee', 'r'],
                ['market.cWho', 'l'],
                ['market.cDir', 'l'],
              ] as const
            ).map(([label, align]) => (
              <div className={`mk-th mk-th--${align}`} key={label} role="columnheader">
                {t(label)}
              </div>
            ))}
          </div>
        </div>
        <div role="rowgroup">
          {deals.map((deal) => (
            <div className="mk-row" key={deal.id} role="row">
              <div className="mk-td" role="cell">
                {formatStamp(deal.createdAt)}
              </div>
              <div className="mk-td mk-td--name" role="rowheader">
                <ItemCell view={itemView(deal.resourceTypeId, deal.partTypeId)} />
              </div>
              <div className="mk-td mk-td--r" role="cell">
                {formatInteger(deal.quantity, language)}
              </div>
              <div className="mk-td mk-td--r" role="cell">
                {formatInteger(deal.pricePerUnit, language)}
              </div>
              <div className="mk-td mk-td--r" role="cell">
                {formatInteger(deal.commission, language)}
              </div>
              <div className="mk-td" role="cell">
                {pill(deal)}
              </div>
              <div className="mk-td" role="cell">
                <DirectionChip bought={deal.direction === 'bought'} />
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
    {pager}
    </>
  );
}
