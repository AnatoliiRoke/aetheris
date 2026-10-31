import { memo, Suspense, useEffect, type ReactNode } from 'react';
import { useI18n } from '../../i18n/useI18n';
import { CivFooter } from './CivFooter';
import { CivHeader } from './CivHeader';
import { CivMenu } from './CivMenu';
import { CivTabbar } from './CivTabbar';
import { EmailBanner } from './EmailBanner';
import { reportRender } from './renderProbe';
import { useCurrentPlanetId, useShellActions } from './useShell';

const Banner = memo(EmailBanner);

/**
 * Reads what the frame shows once: the economy, the bodies with the reference tables, the races
 * and the current body. The same reads that a page asks for are one request, because the shared
 * state does not send a read that is under way again.
 */
const ShellLoader = memo(function ShellLoader() {
  const actions = useShellActions();
  const planetId = useCurrentPlanetId();

  useEffect(() => {
    void actions.loadEconomy();
    void actions.loadBodies();
    void actions.loadRaces();
  }, [actions]);

  useEffect(() => {
    if (planetId !== null) {
      void actions.loadPlanet(planetId);
    }
  }, [actions, planetId]);

  return null;
});

/**
 * The frame of the screens of the game (task 00020, section 8.1): the header, the menu on the left,
 * the bottom bar and the tabs of the phone around the content of a page.
 *
 * The frame is the same on every screen and stands above the pages in the route, so it stays
 * mounted when the player goes from one screen to another: its data is not read again and its
 * blocks are not drawn again. A page gives it only its own content; the current section of the menu
 * comes from the address.
 *
 * @param children The content of the screen.
 */
export function CivShell({ children }: { children: ReactNode }) {
  reportRender('CivShell');

  const { t } = useI18n();

  return (
    <div className="civ">
      <ShellLoader />
      <CivHeader />
      <Banner />
      <div className="civ__body">
        <CivMenu />
        <main className="civ__main">
          <Suspense
            fallback={
              <p className="lead" role="status">
                {t('common.loading')}
              </p>
            }
          >
            {children}
          </Suspense>
        </main>
      </div>
      <CivFooter />
      <CivTabbar />
    </div>
  );
}
