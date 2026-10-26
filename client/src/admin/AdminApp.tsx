import { useCallback, useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react';
import { ApiError, HttpClient } from '../api/http';
import { Alert } from '../components/Alert';
import { LangSwitch } from '../components/LangSwitch';
import { useDocumentTitle } from '../components/useDocumentTitle';
import { Link } from '../router/Router';
import { useRouter } from '../router/useRouter';
import { createAdminApi, type AdminApi, type AdminMe } from './adminApi';
import { AdminContext, errorKey, type AdminContextValue } from './adminContext';
import { adminRoutes } from './adminRoutes';
import type { AdminMessageKey } from './i18n/uk';
import { useAdminText } from './i18n/useAdminText';
import { ConfigScreen } from './screens/ConfigScreen';
import { GenerationScreen } from './screens/GenerationScreen';
import { AuditScreen, RunsScreen } from './screens/RunsAndAudit';
import { FieldsScreen, ObjectsScreen, SectorsScreen, SystemsScreen } from './screens/WorldTables';
import './admin.css';

type AdminSession = { status: 'loading' } | { status: 'anonymous'; notice: AdminMessageKey | null } | { status: 'signed-in'; me: AdminMe };

/** A screen of the admin panel and the permission that opens it. */
interface Screen {
  path: string;
  label: AdminMessageKey;
  permission: string;
  render: () => ReactNode;
}

const screens: Screen[] = [
  { path: adminRoutes.generation, label: 'admin.nav.generation', permission: 'admin.world_generation_estimated', render: () => <GenerationScreen /> },
  { path: adminRoutes.runs, label: 'admin.nav.runs', permission: 'admin.world_generation_runs_read', render: () => <RunsScreen /> },
  { path: adminRoutes.sectors, label: 'admin.nav.sectors', permission: 'admin.world_sectors_read', render: () => <SectorsScreen /> },
  { path: adminRoutes.systems, label: 'admin.nav.systems', permission: 'admin.world_systems_read', render: () => <SystemsScreen /> },
  { path: adminRoutes.objects, label: 'admin.nav.objects', permission: 'admin.world_objects_read', render: () => <ObjectsScreen /> },
  { path: adminRoutes.fields, label: 'admin.nav.fields', permission: 'admin.world_fields_read', render: () => <FieldsScreen /> },
  { path: adminRoutes.config, label: 'admin.nav.config', permission: 'admin.config_read', render: () => <ConfigScreen /> },
  { path: adminRoutes.audit, label: 'admin.nav.audit', permission: 'admin.audit_read', render: () => <AuditScreen /> },
];

/**
 * The admin panel (task 00007): its own sign-in, the navigation by permissions and the
 * screens. It lives at `/admin` in the same application, as a separate module loaded only
 * there; the menu of a player has no link to it. The server checks every right itself; the
 * client only hides what is not allowed.
 *
 * @param api The admin endpoints; tests pass their own.
 */
export default function AdminApp({ api: givenApi }: { api?: AdminApi }) {
  const { t } = useAdminText();
  const api = useMemo(() => givenApi ?? createAdminApi(new HttpClient()), [givenApi]);
  const [session, setSession] = useState<AdminSession>({ status: 'loading' });
  useDocumentTitle(t('admin.meta.title'));

  useEffect(() => {
    let active = true;

    api.me().then(
      (me) => {
        if (active) {
          setSession({ status: 'signed-in', me });
        }
      },
      () => {
        if (active) {
          setSession({ status: 'anonymous', notice: null });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [api]);

  const onError = useCallback((error: unknown) => {
    if (error instanceof ApiError && error.status === 401) {
      setSession({ status: 'anonymous', notice: error.code === 'second_factor_required' ? 'admin.error.secondFactorRequired' : 'admin.login.sessionEnded' });
    }
  }, []);

  const signOut = useCallback(async () => {
    try {
      await api.logout();
    } finally {
      setSession({ status: 'anonymous', notice: null });
    }
  }, [api]);

  if (session.status === 'loading') {
    return (
      <p className="lead admin-loading" role="status">
        {t('admin.loading')}
      </p>
    );
  }

  if (session.status === 'anonymous') {
    return <AdminLogin api={api} notice={session.notice} onSignedIn={(me) => setSession({ status: 'signed-in', me })} />;
  }

  return <AdminShell api={api} me={session.me} onError={onError} onSignOut={() => void signOut()} />;
}

function AdminShell({ api, me, onError, onSignOut }: { api: AdminApi; me: AdminMe; onError: (error: unknown) => void; onSignOut: () => void }) {
  const { t } = useAdminText();
  const { path, navigate } = useRouter();
  const can = useCallback((code: string) => me.permission_codes.includes(code), [me]);
  const allowed = screens.filter((screen) => can(screen.permission));
  const normalized = path.length > 1 ? path.replace(/\/+$/, '') : path;
  const current = allowed.find((screen) => screen.path === normalized);
  const value = useMemo<AdminContextValue>(() => ({ api, me, can, onError }), [api, me, can, onError]);

  useEffect(() => {
    if (!current && allowed.length > 0) {
      navigate(allowed[0].path, { replace: true });
    }
  }, [current, allowed, navigate]);

  return (
    <AdminContext.Provider value={value}>
      <div className="admin">
        <header className="admin__top">
          <span className="admin__brand">{t('admin.title')}</span>
          <nav className="admin__nav" aria-label={t('admin.nav.label')}>
            {allowed.map((screen) => (
              <Link
                key={screen.path}
                className={screen === current ? 'admin__link admin__link--active' : 'admin__link'}
                aria-current={screen === current ? 'page' : undefined}
                to={screen.path}
              >
                {t(screen.label)}
              </Link>
            ))}
          </nav>
          <div className="admin__account">
            <span className="hint">{t('admin.signedInAs', { name: me.display_name, minutes: me.idle_minutes })}</span>
            {me.require_totp ? null : <span className="admin__badge">{t('admin.totpOff')}</span>}
            <LangSwitch />
            <button className="btn btn--secondary" type="button" onClick={onSignOut}>
              {t('admin.signOut')}
            </button>
          </div>
        </header>
        <main className="admin__main">{current ? current.render() : <p className="hint">{t('admin.noScreens')}</p>}</main>
      </div>
    </AdminContext.Provider>
  );
}

/**
 * The sign-in of an administrator: e-mail, password and, when the server asks for it, the
 * one-time code of an authenticator app. No "remember me".
 */
function AdminLogin({ api, notice, onSignedIn }: { api: AdminApi; notice: AdminMessageKey | null; onSignedIn: (me: AdminMe) => void }) {
  const { t } = useAdminText();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<AdminMessageKey | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      await api.login(email.trim(), password, code.trim());
      onSignedIn(await api.me());
    } catch (failure) {
      setError(errorKey(failure));
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="admin-login">
      <form className="panel admin-login__form" onSubmit={(event) => void onSubmit(event)} aria-labelledby="admin-login-title">
        <h1 className="admin-screen__title" id="admin-login-title">
          {t('admin.login.title')}
        </h1>
        {notice ? <Alert kind="info">{t(notice)}</Alert> : null}
        {error ? <Alert kind="error">{t(error)}</Alert> : null}
        <label className="admin-field">
          <span>{t('admin.login.email')}</span>
          <input className="input" type="email" autoComplete="username" required value={email} onChange={(event) => setEmail(event.target.value)} />
        </label>
        <label className="admin-field">
          <span>{t('admin.login.password')}</span>
          <input
            className="input"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <label className="admin-field">
          <span>{t('admin.login.code')}</span>
          <input
            className="input"
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={8}
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
          <span className="hint">{t('admin.login.codeHint')}</span>
        </label>
        <button className="btn btn--primary" type="submit" disabled={busy}>
          {t('admin.login.submit')}
        </button>
        <LangSwitch />
      </form>
    </main>
  );
}
