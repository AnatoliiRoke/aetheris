import { useMemo, useState } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import type { LotGroup } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatInteger } from './marketFormat';
import { EmptyBox, ItemCell, TradeIcon } from './MarketBits';
import { ListSkeleton, ReadError } from './MarketStates';
import type { ItemView } from './useMarketItems';

type GroupSort = 'name' | 'count' | 'price';

/**
 * The lots by item: a row for every item that has a lot, with how many lots it has, the lowest price
 * of a unit and the quantity of all of them. A player who cannot find a lot in a long list finds the
 * item here first and opens its lots with one button.
 *
 * @param groups The groups, or `null` while they are read.
 * @param failed Whether the read failed.
 * @param itemView How an item looks.
 * @param onShow What to do when the player opens the lots of an item.
 * @param onReset What to do when the player clears the filters.
 * @param onRetry What to do when the player asks to read again.
 */
export function LotGroupsView({
  groups,
  failed,
  itemView,
  onShow,
  onReset,
  onRetry,
}: {
  groups: LotGroup[] | null;
  failed: boolean;
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  onShow: (resourceTypeId: number | null, partTypeId: number | null) => void;
  onReset: () => void;
  onRetry: () => void;
}) {
  const { t, language } = useI18n();
  const [sort, setSort] = useState<GroupSort>('name');

  const rows = useMemo(() => {
    const decorated = (groups ?? []).map((group) => ({ group, view: itemView(group.resourceTypeId, group.partTypeId) }));

    return decorated.sort((a, b) =>
      sort === 'count'
        ? b.group.lotCount - a.group.lotCount || a.view.name.localeCompare(b.view.name, language)
        : sort === 'price'
          ? a.group.minPricePerUnit - b.group.minPricePerUnit || a.view.name.localeCompare(b.view.name, language)
          : a.view.name.localeCompare(b.view.name, language),
    );
  }, [groups, itemView, sort, language]);

  if (groups === null) {
    return failed ? <ReadError onRetry={onRetry} /> : <ListSkeleton />;
  }

  if (rows.length === 0) {
    return (
      <EmptyBox icon="market" title={t('market.noLots')}>
        <button className="mk-btn mk-btn--secondary" onClick={onReset} type="button">
          {t('market.reset')}
        </button>
      </EmptyBox>
    );
  }

  return (
    <section aria-label={t('market.groupsAria')} className="mk-panel">
      <div className="mk-groupsort" role="group" aria-label={t('market.sortAria')}>
        {(
          [
            ['name', 'market.cItem'],
            ['count', 'market.cLots'],
            ['price', 'market.cUnit'],
          ] as const
        ).map(([id, label]) => (
          <button aria-pressed={sort === id} className="mk-chip mk-chip--sort" key={id} onClick={() => setSort(id)} type="button">
            {t(label)}
          </button>
        ))}
      </div>
      <ul className="mk-groups">
        {rows.map(({ group, view }) => (
          <li className="mk-group" key={`${group.resourceTypeId ?? ''}-${group.partTypeId ?? ''}`}>
            <span className="mk-group__item">
              <ItemCell view={view} />
            </span>
            <span className="mk-group__facts">
              <span>{t('market.groupLots', { count: formatInteger(group.lotCount, language) })}</span>
              <span className="mk-money">
                {group.minPricePerUnit === 0 ? (
                  <span className="mk-free">{t('market.free')}</span>
                ) : (
                  <>
                    {t('market.groupFrom')} <TradeIcon />
                    {formatInteger(group.minPricePerUnit, language)}
                  </>
                )}
              </span>
              <span className="mk-group__qty">
                {t('market.cQty')}: {formatInteger(group.totalQuantity, language)}
              </span>
            </span>
            <button aria-label={`${t('market.groupShow')}: ${view.name}`} className="mk-btn mk-btn--secondary mk-btn--small mk-group__open" onClick={() => onShow(group.resourceTypeId, group.partTypeId)} type="button">
              {t('market.groupShow')}
              <GameIcon name="chevronDown" size={14} />
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
