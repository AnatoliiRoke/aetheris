import { lazy, Suspense, useEffect, useMemo, type ReactNode } from 'react';
import { isAdminPath } from './admin/adminRoutes';
import { createAuthApi } from './api/authApi';
import { ApiContext, type Api } from './api/context';
import { createEconomyApi } from './api/economyApi';
import { createGameApi } from './api/gameApi';
import { HttpClient } from './api/http';
import { mockContentApi } from './api/mockContentApi';
import { createPlanetApi } from './api/planetApi';
import { createProcessesApi } from './api/processesApi';
import { createProductionApi } from './api/productionApi';
import { createResearchApi } from './api/researchApi';
import { createTextsApi } from './api/textsApi';
import { createWorldApi } from './api/worldApi';
import { I18nProvider } from './i18n/I18nProvider';
import { useI18n } from './i18n/useI18n';
import { CheckEmailPage } from './pages/auth/CheckEmailPage';
import { ConfirmEmailPage } from './pages/auth/ConfirmEmailPage';
import { ForgotPasswordPage } from './pages/auth/ForgotPasswordPage';
import { LoginPage } from './pages/auth/LoginPage';
import { RegisterPage } from './pages/auth/RegisterPage';
import { ResetPasswordPage } from './pages/auth/ResetPasswordPage';
import { LandingPage } from './pages/landing/LandingPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ShellProvider } from './pages/player/ShellProvider';
import { routes } from './router/context';
import { RouterProvider } from './router/Router';
import { useRouter } from './router/useRouter';
import { SessionProvider } from './session/SessionProvider';
import { TextsProvider } from './texts/TextsProvider';
import { useSession } from './session/useSession';

/**
 * The admin panel (task 00007): a separate module, loaded only at `/admin`.
 */
const AdminApp = lazy(() => import('./admin/AdminApp'));

/**
 * The screens of a signed-in player are loaded when the player first opens them, so the first
 * load of the site carries only the public pages (task 00020, section 7.8).
 */
const CivShell = lazy(() => import('./pages/player/CivShell').then((module) => ({ default: module.CivShell })));
const AccountPage = lazy(() => import('./pages/player/AccountPage').then((module) => ({ default: module.AccountPage })));
const BuildPage = lazy(() => import('./pages/player/BuildPage').then((module) => ({ default: module.BuildPage })));
const CivilizationPage = lazy(() => import('./pages/player/CivilizationPage').then((module) => ({ default: module.CivilizationPage })));
const GamePage = lazy(() => import('./pages/player/GamePage').then((module) => ({ default: module.GamePage })));
const PlanetPage = lazy(() => import('./pages/player/PlanetPage').then((module) => ({ default: module.PlanetPage })));
const ResearchPage = lazy(() => import('./pages/player/ResearchPage').then((module) => ({ default: module.ResearchPage })));
const StoragePage = lazy(() => import('./pages/player/StoragePage').then((module) => ({ default: module.StoragePage })));

const pages: Record<string, () => ReactNode> = {
  [routes.home]: () => <LandingPage />,
  [routes.login]: () => <LoginPage />,
  [routes.register]: () => <RegisterPage />,
  [routes.forgotPassword]: () => <ForgotPasswordPage />,
  [routes.checkEmail]: () => <CheckEmailPage />,
  [routes.confirmEmail]: () => <ConfirmEmailPage />,
  [routes.resetPassword]: () => <ResetPasswordPage />,
};

/**
 * The screens of a signed-in player (tasks 00006 and 00011).
 */
const playerPages: Record<string, () => ReactNode> = {
  [routes.game]: () => <GamePage />,
  [routes.planet]: () => <PlanetPage />,
  [routes.planetBuild]: () => <BuildPage />,
  [routes.planetResearch]: () => <ResearchPage />,
  [routes.planetStorage]: () => <StoragePage />,
  [routes.civilization]: () => <CivilizationPage />,
  [routes.account]: () => <AccountPage />,
};

/**
 * The screens that stand in the frame of the game (the header, the menu and the bottom bar). The
 * frame is one element above them, so it stays mounted when the player goes from one of them to
 * another: its data is not read again and its blocks are not drawn again.
 */
const framedPaths: ReadonlySet<string> = new Set([routes.game, routes.planet, routes.planetBuild, routes.planetResearch, routes.planetStorage]);

/**
 * Shows the page of the current address.
 */
export function Pages() {
  const { path } = useRouter();
  const normalized = path.length > 1 ? path.replace(/\/+$/, '') : path;
  const page = pages[normalized];

  if (isAdminPath(normalized)) {
    return <AdminPage />;
  }

  if (playerPages[normalized]) {
    return <PlayerPage path={normalized} />;
  }

  return page ? page() : <NotFoundPage />;
}

function AdminPage() {
  const { t } = useI18n();

  return (
    <Suspense
      fallback={
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      }
    >
      <AdminApp />
    </Suspense>
  );
}

/**
 * The address a player belongs on: a visitor signs in first, and a player without a
 * civilization sees only its first step (task 00006, decision 9).
 */
function playerTarget(path: string, session: ReturnType<typeof useSession>['session']): string | null {
  if (session.status === 'anonymous') {
    return routes.login;
  }

  if (session.status !== 'signed-in') {
    return null;
  }

  const hasCivilization = session.me.civilization !== null;

  if (!hasCivilization && path !== routes.civilization) {
    return routes.civilization;
  }

  return hasCivilization && path === routes.civilization ? routes.game : null;
}

/**
 * A screen of a signed-in player, behind the gate of the session and the first step. The
 * server checks the same rules on its own.
 */
function PlayerPage({ path }: { path: string }) {
  const { t } = useI18n();
  const { session } = useSession();
  const { navigate } = useRouter();
  const target = playerTarget(path, session);

  useEffect(() => {
    if (target) {
      navigate(target, { replace: true });
    }
  }, [target, navigate]);

  if (session.status !== 'signed-in' || target) {
    return (
      <p className="lead" role="status">
        {t('common.loading')}
      </p>
    );
  }

  const page = playerPages[path]();

  return (
    <Suspense
      fallback={
        <p className="lead" role="status">
          {t('common.loading')}
        </p>
      }
    >
      {framedPaths.has(path) ? <CivShell>{page}</CivShell> : page}
    </Suspense>
  );
}

/**
 * The providers of the site: language, server interfaces, texts of the game data, session and
 * routing.
 *
 * @param api The server interfaces; tests pass their own.
 */
export function AppProviders({ api, children }: { api: Api; children: ReactNode }) {
  return (
    <I18nProvider>
      <ApiContext.Provider value={api}>
        <TextsProvider>
          <SessionProvider>
            <ShellProvider>
              <RouterProvider>{children}</RouterProvider>
            </ShellProvider>
          </SessionProvider>
        </TextsProvider>
      </ApiContext.Provider>
    </I18nProvider>
  );
}

/**
 * The public site (task 00003), the account screens and the first screens of a signed-in
 * player (task 00006). The game client (PixiJS, `src/game`) is added by a later task behind
 * the sign-in.
 */
export default function App() {
  const api = useMemo<Api>(() => {
    const http = new HttpClient();

    return {
      auth: createAuthApi(http),
      content: mockContentApi,
      economy: createEconomyApi(http),
      game: createGameApi(http),
      planets: createPlanetApi(http),
      processes: createProcessesApi(http),
      production: createProductionApi(http),
      research: createResearchApi(http),
      texts: createTextsApi(http),
      world: createWorldApi(http),
    };
  }, []);

  return (
    <AppProviders api={api}>
      <Pages />
    </AppProviders>
  );
}
