import { useMemo, type ReactNode } from 'react';
import { createAuthApi } from './api/authApi';
import { ApiContext, type Api } from './api/context';
import { HttpClient } from './api/http';
import { mockContentApi } from './api/mockContentApi';
import { I18nProvider } from './i18n/I18nProvider';
import { CheckEmailPage } from './pages/auth/CheckEmailPage';
import { ConfirmEmailPage } from './pages/auth/ConfirmEmailPage';
import { ForgotPasswordPage } from './pages/auth/ForgotPasswordPage';
import { LoginPage } from './pages/auth/LoginPage';
import { RegisterPage } from './pages/auth/RegisterPage';
import { ResetPasswordPage } from './pages/auth/ResetPasswordPage';
import { LandingPage } from './pages/landing/LandingPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { routes } from './router/context';
import { RouterProvider } from './router/Router';
import { useRouter } from './router/useRouter';
import { SessionProvider } from './session/SessionProvider';

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
 * Shows the page of the current address.
 */
export function Pages() {
  const { path } = useRouter();
  const normalized = path.length > 1 ? path.replace(/\/+$/, '') : path;
  const page = pages[normalized];

  return page ? page() : <NotFoundPage />;
}

/**
 * The providers of the site: language, server interfaces, session and routing.
 *
 * @param api The server interfaces; tests pass their own.
 */
export function AppProviders({ api, children }: { api: Api; children: ReactNode }) {
  return (
    <I18nProvider>
      <ApiContext.Provider value={api}>
        <SessionProvider>
          <RouterProvider>{children}</RouterProvider>
        </SessionProvider>
      </ApiContext.Provider>
    </I18nProvider>
  );
}

/**
 * The public site (task 00003): the landing page and the account screens. The game client
 * (PixiJS, `src/game`) is added by a later task behind the sign-in.
 */
export default function App() {
  const api = useMemo<Api>(() => ({ auth: createAuthApi(new HttpClient()), content: mockContentApi }), []);

  return (
    <AppProviders api={api}>
      <Pages />
    </AppProviders>
  );
}
