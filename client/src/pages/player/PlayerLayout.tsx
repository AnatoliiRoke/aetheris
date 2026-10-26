import type { ReactNode } from 'react';
import { LangSwitch } from '../../components/LangSwitch';
import { LogoContent } from '../../components/Logo';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { useSession } from '../../session/useSession';
import { EmailBanner } from './EmailBanner';

/**
 * The frame of the screens of a signed-in player: the logo, the navigation, the sign-out and
 * the banner of an unconfirmed address.
 *
 * @param showNavigation Whether the start screen and the account page are offered; the first
 *   step of the game offers only the sign-out (task 00006, decision 9).
 */
export function PlayerLayout({ children, showNavigation = true }: { children: ReactNode; showNavigation?: boolean }) {
  const { t } = useI18n();
  const { signOut } = useSession();
  const { navigate } = useRouter();

  const onSignOut = async () => {
    await signOut();
    navigate(routes.home);
  };

  return (
    <div className="player">
      <header className="player__top">
        <Link className="logo" to={showNavigation ? routes.game : routes.civilization} aria-label={t('common.logoHome')}>
          <LogoContent />
        </Link>
        <nav className="player__nav" aria-label={t('player.nav')}>
          {showNavigation ? (
            <>
              <Link className="text-btn" to={routes.game}>
                {t('player.navGame')}
              </Link>
              <Link className="text-btn" to={routes.planet}>
                {t('player.navPlanet')}
              </Link>
              <Link className="text-btn" to={routes.account}>
                {t('player.navAccount')}
              </Link>
            </>
          ) : null}
          <LangSwitch />
          <button className="btn btn--secondary" type="button" onClick={() => void onSignOut()}>
            {t('common.signOut')}
          </button>
        </nav>
      </header>
      <main className="player__main">
        <EmailBanner />
        {children}
      </main>
    </div>
  );
}
