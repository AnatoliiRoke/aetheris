import type { ReactNode } from 'react';
import { useI18n } from '../../i18n/useI18n';

/**
 * The head of the content of a screen of the game: the heading, a line under it and what sits
 * beside it. The shell of the screen is the common one; this head belongs to the content.
 *
 * @param title The heading.
 * @param subtitle The line under the heading.
 * @param actions What sits beside the heading, for example the way back.
 */
export function ScreenHead({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="screen-head">
      <div className="screen-head__text">
        <h1 className="player__title">{title}</h1>
        {subtitle ? <p className="meta">{subtitle}</p> : null}
      </div>
      {actions ? (
        <div aria-label={t('screen.actions')} className="screen-head__actions" role="group">
          {actions}
        </div>
      ) : null}
    </div>
  );
}
