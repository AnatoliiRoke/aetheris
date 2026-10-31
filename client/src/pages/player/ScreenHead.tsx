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
 * The planet mock-up shows the name alone, the type in a chip, and the place on the next line.
 * The construction and storage mock-ups show the screen word with the name of the body beside it
 * in a softer weight. The accessible name keeps the older full title.
 *
 * @param title The heading.
 * @param titlePrefix A word before the title that stays in the accessible name and is not shown.
 * @param titleNote The name of the body, shown softer after the title.
 * @param badge The chip beside the title, for example the type of the body.
 * @param subtitle The line under the heading.
 * @param actions What sits beside the heading, for example the way back.
 * @param breadcrumbs The steps from the planet to the page, for example "Planet / Nova".
 */
export function ScreenHead({
  title,
  titlePrefix,
  titleNote,
  badge,
  subtitle,
  actions,
  breadcrumbs,
}: {
  title: string;
  titlePrefix?: string;
  titleNote?: string;
  badge?: string;
  subtitle?: string;
  actions?: ReactNode;
  breadcrumbs?: Crumb[];
}) {
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
        <div className="screen-head__title">
          <h1
            aria-label={titleNote ? `${title}: ${titleNote}` : titlePrefix ? `${titlePrefix} ${title}` : undefined}
            className="player__title"
          >
            {title}
            {titleNote ? <span className="screen-head__note"> {titleNote}</span> : null}
          </h1>
          {badge ? <span className="screen-head__badge">{badge}</span> : null}
        </div>
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
