import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';

const skeletonRows = [0, 1, 2, 3, 4, 5] as const;

/**
 * The skeleton of a list that is loading: rows of muted bars, so the screen is never empty.
 */
export function ListSkeleton() {
  const { t } = useI18n();

  return (
    <section aria-busy="true" aria-label={t('market.loading')} className="mk-panel">
      {skeletonRows.map((index) => (
        <div className="mk-skel-row" key={index}>
          <span className="mk-skel mk-skel--icon" />
          <span className={`mk-skel mk-skel--name-${index % 3}`} />
          <span className={`mk-skel mk-skel--tail-${index % 2}`} />
        </div>
      ))}
    </section>
  );
}

/**
 * The error of a read: what happened, what to do, and the button that reads again.
 *
 * @param onRetry What to do when the player asks to read again.
 */
export function ReadError({ onRetry }: { onRetry: () => void }) {
  const { t } = useI18n();

  return (
    <section className="mk-empty" role="alert">
      <GameIcon className="mk-empty__icon mk-empty__icon--error" name="warn" size={40} />
      <h2 className="mk-empty__title">{t('market.errTitle')}</h2>
      <p className="mk-empty__text">{t('market.errText')}</p>
      <button className="mk-btn mk-btn--primary" onClick={onRetry} type="button">
        {t('market.retry')}
      </button>
    </section>
  );
}
