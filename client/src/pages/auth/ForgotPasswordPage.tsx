import { useState, type FormEvent } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { TextField } from '../../components/FormField';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { AuthLayout } from './AuthLayout';
import { describeError, isEmail, type ErrorMessage } from './authErrors';

/**
 * The request of a password reset link (`POST /api/v1/auth/forgot-password`).
 */
export function ForgotPasswordPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { navigate } = useRouter();
  useDocumentTitle(t('meta.forgot.title'));

  const [email, setEmail] = useState('');
  const [invalid, setInvalid] = useState(false);
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    if (!isEmail(email.trim())) {
      setInvalid(true);
      return;
    }

    setBusy(true);

    try {
      await auth.forgotPassword(email.trim());
      navigate(`${routes.checkEmail}?email=${encodeURIComponent(email.trim())}&kind=reset`);
    } catch (error) {
      setFailure(describeError(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <section className="auth__view" aria-labelledby="forgot-title">
        <div>
          <h1 className="auth__title" id="forgot-title">
            {t('auth.forgot.title')}
          </h1>
          <p className="lead">{t('auth.forgot.lead')}</p>
        </div>
        {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}
        <form className="form" noValidate onSubmit={onSubmit}>
          <TextField
            label={t('auth.email')}
            name="email"
            type="email"
            autoComplete="email"
            inputMode="email"
            placeholder={t('auth.emailPlaceholder')}
            required
            value={email}
            error={invalid ? t('error.emailInvalid') : undefined}
            onChange={(event) => {
              setEmail(event.target.value);
              setInvalid(false);
            }}
          />
          <button className="btn btn--primary btn--lg btn--block" type="submit" disabled={busy} aria-busy={busy}>
            {t('auth.forgot.submit')}
          </button>
        </form>
        <p className="auth__switch">
          <Link className="text-btn text-btn--muted" to={routes.login}>
            {t('auth.check.back')}
          </Link>
        </p>
      </section>
    </AuthLayout>
  );
}
