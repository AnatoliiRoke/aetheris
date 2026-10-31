import { useState } from 'react';
import type { MarketLevel, MyLot } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatAgo, formatInteger } from './marketFormat';
import { EmptyBox, ItemCell } from './MarketBits';
import { levelLabelKeys } from './levelLabels';
import { ListSkeleton, ReadError } from './MarketStates';
import { useIsPhone } from './useIsPhone';
import type { ItemView } from './useMarketItems';

/**
 * The tab "My lots": the lots the player has on the market and the button that takes a lot off it.
 * The button asks once more, because the goods go back to the stock.
 *
 * @param lots The lots of the player, or `null` while they are read.
 * @param failed Whether the read failed.
 * @param levels The levels of the market, for the name of the level of a lot.
 * @param nowMs The game time now, for "2 hours ago".
 * @param itemView How an item looks.
 * @param busy Whether a command is being sent.
 * @param onRetry What to do when the player asks to read again.
 * @param onCancel What to do when the player confirms the cancel of a lot.
 */
export function MineView({
  lots,
  failed,
  levels,
  nowMs,
  itemView,
  busy,
  onRetry,
  onCancel,
}: {
  lots: MyLot[] | null;
  failed: boolean;
  levels: MarketLevel[];
  nowMs: number | null;
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  busy: boolean;
  onRetry: () => void;
  onCancel: (lot: MyLot) => void;
}) {
  const { t, language } = useI18n();
  const phone = useIsPhone();
  const [asking, setAsking] = useState<number | null>(null);

  if (lots === null) {
    return failed ? <ReadError onRetry={onRetry} /> : <ListSkeleton />;
  }

  if (lots.length === 0) {
    return (
      <EmptyBox icon="storage" title={t('market.noMine')}>
        <p className="mk-empty__text">{t('market.noMineHint')}</p>
      </EmptyBox>
    );
  }

  const levelName = (levelId: number) => {
    const level = levels.find((candidate) => candidate.id === levelId);

    return level ? t(levelLabelKeys[level.visibility] ?? 'market.lvSector') : '';
  };
  const created = (lot: MyLot) => (nowMs === null ? '' : formatAgo(lot.createdAt, nowMs, t));
  const controls = (lot: MyLot, full: boolean) =>
    asking === lot.id ? (
      <span className={`mk-confirm${full ? ' mk-confirm--full' : ''}`}>
        <span className="mk-confirm__ask">{t('market.cancelAsk')}</span>
        <button className="mk-btn mk-btn--danger mk-btn--small" disabled={busy} onClick={() => onCancel(lot)} type="button">
          {t('market.cancelYes')}
        </button>
        <button className="mk-btn mk-btn--secondary mk-btn--small" onClick={() => setAsking(null)} type="button">
          {t('market.cancelNo')}
        </button>
      </span>
    ) : (
      <button className={`mk-btn mk-btn--secondary mk-btn--small${full ? ' mk-btn--full' : ''}`} onClick={() => setAsking(lot.id)} type="button">
        {t('market.cancelLot')}
      </button>
    );

  if (phone) {
    return (
      <div className="mk-cards">
        {lots.map((lot) => {
          const view = itemView(lot.resourceTypeId, lot.partTypeId);

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
                  {formatInteger(lot.pricePerUnit, language)} {t('market.perUnit')}
                </span>
                <span className="mk-card__total">
                  {t('market.total')}: <b>{formatInteger(lot.totalPrice, language)}</b>
                </span>
              </div>
              <div className="mk-card__meta mk-card__meta--between">
                <span>{levelName(lot.marketLevelId)}</span>
                <span>{created(lot)}</span>
              </div>
              {controls(lot, true)}
            </article>
          );
        })}
      </div>
    );
  }

  return (
    <section className="mk-panel">
      <div aria-label={t('market.tMine')} className="mk-table mk-table--mine" role="table">
        <div role="rowgroup">
          <div className="mk-row mk-row--head" role="row">
            {(
              [
                ['market.cItem', 'l'],
                ['market.cQty', 'r'],
                ['market.cUnit', 'r'],
                ['market.cTotal', 'r'],
                ['market.cLevel', 'l'],
                ['market.cCreated', 'l'],
                [null, 'r'],
              ] as const
            ).map(([label, align], index) => (
              <div className={`mk-th mk-th--${align}`} key={index} role="columnheader">
                {label ? t(label) : null}
              </div>
            ))}
          </div>
        </div>
        <div role="rowgroup">
          {lots.map((lot) => (
            <div className="mk-row" key={lot.id} role="row">
              <div className="mk-td mk-td--name" role="rowheader">
                <ItemCell condition={lot.partConditionPercent !== null ? t('market.conditionChip', { percent: lot.partConditionPercent }) : undefined} view={itemView(lot.resourceTypeId, lot.partTypeId)} />
              </div>
              <div className="mk-td mk-td--r" role="cell">
                {formatInteger(lot.quantity, language)}
              </div>
              <div className="mk-td mk-td--r" role="cell">
                {formatInteger(lot.pricePerUnit, language)}
              </div>
              <div className="mk-td mk-td--r" role="cell">
                {formatInteger(lot.totalPrice, language)}
              </div>
              <div className="mk-td" role="cell">
                {levelName(lot.marketLevelId)}
              </div>
              <div className="mk-td" role="cell">
                {created(lot)}
              </div>
              <div className="mk-td mk-td--btn mk-td--end" role="cell">
                {controls(lot, false)}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
