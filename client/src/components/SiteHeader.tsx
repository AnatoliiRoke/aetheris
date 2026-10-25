import { useEffect, useRef, useState } from 'react';
import type { MessageKey } from '../i18n/uk';
import { useI18n } from '../i18n/useI18n';
import { routes } from '../router/context';
import { Link } from '../router/Router';
import { useSession } from '../session/useSession';
import { CloseIcon, MenuIcon } from './Icons';
import { LangSwitch } from './LangSwitch';
import { LogoContent } from './Logo';

/**
 * The width from which the full menu is shown; below it the menu folds into a button.
 */
const DESKTOP_QUERY = '(min-width: 1024px)';

const sections: { id: string; key: MessageKey }[] = [
  { id: 'about', key: 'nav.about' },
  { id: 'races', key: 'nav.races' },
  { id: 'ranking', key: 'nav.ranking' },
  { id: 'news', key: 'nav.news' },
  { id: 'help', key: 'nav.help' },
];

/**
 * The sticky site header: logo, sections, language, sign-in and play.
 *
 * @param onLanding Whether the sections are on the current page; elsewhere the links lead
 * back to the landing page.
 */
export function SiteHeader({ onLanding }: { onLanding: boolean }) {
  const { t } = useI18n();
  const { session, signOut } = useSession();
  const [menuOpen, setMenuOpen] = useState(false);
  const toggleRef = useRef<HTMLButtonElement | null>(null);

  const sectionHref = (id: string) => (onLanding ? `#${id}` : `/#${id}`);
  const signedIn = session.status === 'signed-in';

  useEffect(() => {
    if (!menuOpen) {
      return;
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false);
        toggleRef.current?.focus();
      }
    };

    const desktop = window.matchMedia?.(DESKTOP_QUERY);
    const onDesktop = (event: MediaQueryListEvent) => {
      if (event.matches) {
        setMenuOpen(false);
      }
    };

    document.addEventListener('keydown', onKeyDown);
    desktop?.addEventListener('change', onDesktop);

    return () => {
      document.removeEventListener('keydown', onKeyDown);
      desktop?.removeEventListener('change', onDesktop);
    };
  }, [menuOpen]);

  const navLinks = sections.map(({ id, key }) => (
    <a key={id} href={sectionHref(id)}>
      {t(key)}
    </a>
  ));

  const accountAction = signedIn ? (
    <button className="btn btn--secondary" type="button" onClick={() => void signOut()}>
      {t('common.signOut')}
    </button>
  ) : (
    <Link className="btn btn--secondary" to={routes.login}>
      {t('common.signIn')}
    </Link>
  );

  return (
    <header className="site-header">
      <div className="container site-header__bar">
        <Link className="logo" to={routes.home} aria-label={t('common.logoHome')}>
          <LogoContent />
        </Link>

        <nav className="site-nav" aria-label={t('common.mainNav')}>
          {navLinks}
        </nav>

        <div className="site-header__actions">
          <LangSwitch />
          {accountAction}
          {signedIn ? null : (
            <Link className="btn btn--primary" to={routes.register}>
              {t('common.play')}
            </Link>
          )}
          <button
            ref={toggleRef}
            className="icon-btn menu-toggle"
            type="button"
            aria-expanded={menuOpen}
            aria-controls="mobile-menu"
            aria-label={t(menuOpen ? 'common.menuClose' : 'common.menuOpen')}
            onClick={() => setMenuOpen((open) => !open)}
          >
            <MenuIcon className="icon-open" />
            <CloseIcon className="icon-close" />
          </button>
        </div>
      </div>

      <div className="mobile-menu" id="mobile-menu" hidden={!menuOpen}>
        <nav
          className="mobile-menu__nav"
          aria-label={t('common.mainNav')}
          onClick={(event) => {
            if ((event.target as HTMLElement).closest('a')) {
              setMenuOpen(false);
            }
          }}
        >
          {navLinks}
        </nav>
        <div className="mobile-menu__row">
          <span className="mobile-menu__label">{t('common.language')}</span>
          <LangSwitch />
        </div>
        {signedIn ? (
          <button className="btn btn--secondary btn--lg btn--block" type="button" onClick={() => void signOut()}>
            {t('common.signOut')}
          </button>
        ) : (
          <Link className="btn btn--secondary btn--lg btn--block" to={routes.login} onClick={() => setMenuOpen(false)}>
            {t('common.signIn')}
          </Link>
        )}
      </div>
    </header>
  );
}
