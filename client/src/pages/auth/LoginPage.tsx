import { useRef, useState, type FormEvent } from 'react';
import { ApiError } from '../../api/http';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { PasswordField, TextField } from '../../components/FormField';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { useSession } from '../../session/useSession';
import { AuthLayout } from './AuthLayout';
import { describeError, type ErrorMessage } from './authErrors';
import { GoogleButton } from './GoogleButton';

/**
 * Sign-in with the e-mail address or the nickname and the password (tasks 00002 and 00006,
 * `POST /api/v1/auth/login`). "Remember me" keeps the session for days; without it the
 * session ends with the browser.
 */
export function LoginPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { refresh } = useSession();
  const { navigate, query } = useRouter();
  useDocumentTitle(t('meta.login.title'));
  const deletionDueAt = query.get('deleted');

  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<{ login?: MessageKey; password?: MessageKey }>({});
  const [failure, setFailure] = useState<{ message: ErrorMessage; code: string } | null>(null);
  const [succeeded, setSucceeded] = useState(false);
  const [busy, setBusy] = useState(false);
  const loginRef = useRef<HTMLInputElement | null>(null);
  const passwordRef = useRef<HTMLInputElement | null>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    const errors = {
      login: login.trim() ? undefined : ('error.loginEmpty' as const),
      password: password ? undefined : ('error.passwordEmpty' as const),
    };
    setFieldErrors(errors);

    if (errors.login || errors.password) {
      (errors.login ? loginRef : passwordRef).current?.focus();
      return;
    }

    setBusy(true);

    try {
      const { deletionCancelled } = await auth.login(login.trim(), password, rememberMe);
      setSucceeded(true);
      await refresh();
      navigate(deletionCancelled ? `${routes.account}?deletion=cancelled` : routes.game);
    } catch (error) {
      const code = error instanceof ApiError ? error.code : '';
      setFailure({ message: describeError(error), code });

      if (code === 'invalid_credentials') {
        setFieldErrors({ password: 'error.passwordWrong' });
        passwordRef.current?.focus();
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <section className="auth__view" aria-labelledby="signin-title">
        <div>
          <h1 className="auth__title" id="signin-title">
            {t('auth.signin.title')}
          </h1>
          <p className="lead">{t('auth.signin.lead')}</p>
        </div>

        {deletionDueAt && !failure && !succeeded ? (
          <Alert kind="info" title={t('auth.signin.deletionTitle')}>
            {t('auth.signin.deletionText')}
          </Alert>
        ) : null}
        {failure ? (
          <Alert kind="error" title={t(failure.message.title)}>
            {t(failure.message.text)}
          </Alert>
        ) : null}
        {succeeded ? <Alert kind="success" title={t('auth.signin.successTitle')}>{t('auth.signin.successText')}</Alert> : null}

        <form className="form" noValidate onSubmit={onSubmit}>
          <TextField
            ref={loginRef}
            label={t('auth.login')}
            name="login"
            autoComplete="username"
            placeholder={t('auth.loginPlaceholder')}
            required
            value={login}
            error={fieldErrors.login && t(fieldErrors.login)}
            onChange={(event) => {
              setLogin(event.target.value);
              setFieldErrors((current) => ({ ...current, login: undefined }));
            }}
          />
          <PasswordField
            ref={passwordRef}
            label={t('auth.password')}
            name="password"
            autoComplete="current-password"
            placeholder={t('auth.passwordPlaceholder')}
            required
            value={password}
            error={fieldErrors.password && t(fieldErrors.password)}
            labelAside={
              <Link className="field__link" to={routes.forgotPassword}>
                {t('auth.forgot')}
              </Link>
            }
            onChange={(event) => {
              setPassword(event.target.value);
              setFieldErrors((current) => ({ ...current, password: undefined }));
            }}
          />
          <div className="field">
            <label className="check">
              <input type="checkbox" name="remember" checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} />
              <span>{t('auth.rememberMe')}</span>
            </label>
          </div>
          <button className="btn btn--primary btn--lg btn--block" type="submit" disabled={busy} aria-busy={busy}>
            {t('common.signIn')}
          </button>
        </form>

        <GoogleButton />
        <p className="auth__switch">
          {t('auth.signin.noAccount')}{' '}
          <Link className="text-btn" to={routes.register}>
            {t('auth.signin.toSignup')}
          </Link>
        </p>
      </section>
    </AuthLayout>
  );
}
