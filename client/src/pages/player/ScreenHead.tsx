import type { ReactNode } from 'react';
import { useI18n } from '../../i18n/useI18n';
import { Link } from '../../router/Router';

/**
 * A step of the breadcrumbs above the heading: the label and, for a step that leads somewhere,
 * the route. The last step is the page itself and has no route.
 */
export interface Crumb {
  label: string;
  to?: string;
}

/**
 * The head of the content of a screen of the game: the breadcrumbs, the heading, a line under it
 * and what sits beside it. The shell of the screen is the common one; this head belongs to the
 * content.
 *
 * @param title The heading.
 * @param subtitle The line under the heading.
 * @param actions What sits beside the heading, for example the way back.
 * @param breadcrumbs The steps from the planet to the page, for example "Planet / Nova".
 */
export function ScreenHead({ title, subtitle, actions, breadcrumbs }: { title: string; subtitle?: string; actions?: ReactNode; breadcrumbs?: Crumb[] }) {
  const { t } = useI18n();

  return (
    <div className="screen-head">
      <div className="screen-head__text">
        {breadcrumbs && breadcrumbs.length > 0 ? (
          <nav aria-label={t('screen.breadcrumbs')}>
            <ol className="crumbs">
              {breadcrumbs.map((crumb, index) => (
                <li aria-current={crumb.to ? undefined : 'page'} className="crumbs__step" key={`${index}-${crumb.label}`}>
                  {crumb.to ? <Link to={crumb.to}>{crumb.label}</Link> : <span>{crumb.label}</span>}
                </li>
              ))}
            </ol>
          </nav>
        ) : null}
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
