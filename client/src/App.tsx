import { lazy, Suspense, useEffect, useMemo, type ReactNode } from 'react';
import { isAdminPath } from './admin/adminRoutes';
import { createAuthApi } from './api/authApi';
import { ApiContext, type Api } from './api/context';
import { createGameApi } from './api/gameApi';
import { HttpClient } from './api/http';
import { mockContentApi } from './api/mockContentApi';
import { createPlanetApi } from './api/planetApi';
import { createProductionApi } from './api/productionApi';
import { createResearchApi } from './api/researchApi';
import { createTextsApi } from './api/textsApi';
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
import { AccountPage } from './pages/player/AccountPage';
import { CivilizationPage } from './pages/player/CivilizationPage';
import { GamePage } from './pages/player/GamePage';
import { PlanetPage } from './pages/player/PlanetPage';
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
  [routes.civilization]: () => <CivilizationPage />,
  [routes.account]: () => <AccountPage />,
};

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

  return playerPages[path]();
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
            <RouterProvider>{children}</RouterProvider>
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
      game: createGameApi(http),
      planets: createPlanetApi(http),
      production: createProductionApi(http),
      research: createResearchApi(http),
      texts: createTextsApi(http),
    };
  }, []);

  return (
    <AppProviders api={api}>
      <Pages />
    </AppProviders>
  );
}
