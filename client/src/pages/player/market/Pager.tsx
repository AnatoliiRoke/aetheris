import { useI18n } from '../../../i18n/useI18n';
import { formatInteger } from './marketFormat';

/**
 * The pager under a long list: the range that is shown, the whole number and the two buttons that
 * move to the page before and the page after.
 *
 * @param offset How many rows were skipped.
 * @param limit How many rows a page holds.
 * @param total How many rows there are.
 * @param onGo What to do when the player moves to a page; it gets the offset of the page.
 */
export function Pager({ offset, limit, total, onGo }: { offset: number; limit: number; total: number; onGo: (offset: number) => void }) {
  const { t, language } = useI18n();
  const from = offset + 1;
  const to = Math.min(total, offset + limit);

  return (
    <nav aria-label={t('market.pagerAria')} className="mk-pager">
      <button className="mk-btn mk-btn--secondary" disabled={offset <= 0} onClick={() => onGo(offset - limit)} type="button">
        {t('market.pagePrev')}
      </button>
      <span className="mk-pager__range" role="status">
        {t('market.pageRange', { from: formatInteger(from, language), to: formatInteger(to, language), total: formatInteger(total, language) })}
      </span>
      <button className="mk-btn mk-btn--secondary" disabled={offset + limit >= total} onClick={() => onGo(offset + limit)} type="button">
        {t('market.pageNext')}
      </button>
    </nav>
  );
}
