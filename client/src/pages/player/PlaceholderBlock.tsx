import { useI18n } from '../../i18n/useI18n';

const skeletonBarWidths = ['skel--wide', 'skel--mid', 'skel--short'] as const;

/**
 * A block whose data no task has made yet (task 00020, section 2.3): the muted shape of the real
 * content, the word "Soon" and an inactive button with the reason in its hint. The block shows
 * no made-up number, name or timer, so nobody takes it for data.
 *
 * @param title The heading of the block.
 * @param reason Why the block is not available yet.
 * @param rows How many bars of the skeleton to draw.
 */
export function PlaceholderBlock({ title, reason, rows = 3 }: { title: string; reason: string; rows?: number }) {
  const { t } = useI18n();

  return (
    <section aria-label={title} className="panel placeholder-block">
      <div className="panel__head">
        <h2 className="panel__title">{title}</h2>
        <span className="chip">{t('civ.soon')}</span>
      </div>
      <div aria-hidden="true" className="placeholder-block__skeleton">
        {Array.from({ length: rows }, (_, index) => (
          <span className={`skel ${skeletonBarWidths[index % skeletonBarWidths.length]}`} key={index} />
        ))}
      </div>
      <p className="meta">{reason}</p>
      <button className="btn btn--secondary" disabled title={reason} type="button">
        {t('placeholder.action')}
      </button>
    </section>
  );
}
