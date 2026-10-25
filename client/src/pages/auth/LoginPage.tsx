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
import { describeError, isEmail, type ErrorMessage } from './authErrors';
import { GoogleButton } from './GoogleButton';

/**
 * Sign-in with e-mail and password (task 00002, `POST /api/v1/auth/login`).
 */
export function LoginPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { refresh } = useSession();
  const { navigate } = useRouter();
  useDocumentTitle(t('meta.login.title'));

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: MessageKey; password?: MessageKey }>({});
  const [failure, setFailure] = useState<{ message: ErrorMessage; code: string } | null>(null);
  const [succeeded, setSucceeded] = useState(false);
  const [busy, setBusy] = useState(false);
  const emailRef = useRef<HTMLInputElement | null>(null);
  const passwordRef = useRef<HTMLInputElement | null>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    const errors = {
      email: isEmail(email.trim()) ? undefined : ('error.emailInvalid' as const),
      password: password ? undefined : ('error.passwordEmpty' as const),
    };
    setFieldErrors(errors);

    if (errors.email || errors.password) {
      (errors.email ? emailRef : passwordRef).current?.focus();
      return;
    }

    setBusy(true);

    try {
      await auth.login(email.trim(), password);
      setSucceeded(true);
      await refresh();
      navigate(routes.home);
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

        {failure ? (
          <Alert kind="error" title={t(failure.message.title)}>
            {t(failure.message.text)}
            {failure.code === 'email_not_confirmed' ? (
              <>
                {' '}
                <Link to={`${routes.checkEmail}?email=${encodeURIComponent(email.trim())}`}>{t('error.notConfirmed.action')}</Link>
              </>
            ) : null}
          </Alert>
        ) : null}
        {succeeded ? <Alert kind="success" title={t('auth.signin.successTitle')}>{t('auth.signin.successText')}</Alert> : null}

        <form className="form" noValidate onSubmit={onSubmit}>
          <TextField
            ref={emailRef}
            label={t('auth.email')}
            name="email"
            type="email"
            autoComplete="email"
            inputMode="email"
            placeholder={t('auth.emailPlaceholder')}
            required
            value={email}
            error={fieldErrors.email && t(fieldErrors.email)}
            onChange={(event) => {
              setEmail(event.target.value);
              setFieldErrors((current) => ({ ...current, email: undefined }));
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
