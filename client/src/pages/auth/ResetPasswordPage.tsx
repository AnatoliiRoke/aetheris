import { useState, type FormEvent } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { PasswordField } from '../../components/FormField';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { siteConfig } from '../../config/site';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { AuthLayout } from './AuthLayout';
import { describeError, passwordProblems, passwordRuleErrors, type ErrorMessage } from './authErrors';

/**
 * The target of the reset link: `/reset-password?userId=…&token=…`.
 */
export function ResetPasswordPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { query } = useRouter();
  useDocumentTitle(t('meta.reset.title'));

  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<MessageKey[]>([]);
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    const problems = passwordProblems(password);

    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    setBusy(true);

    try {
      await auth.resetPassword(query.get('userId') ?? '', query.get('token') ?? '', password);
      setDone(true);
    } catch (error) {
      const rules = passwordRuleErrors(error, 'NewPassword');

      if (rules.length > 0) {
        setErrors(rules);
      } else {
        setFailure(describeError(error));
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <section className="auth__view" aria-labelledby="reset-title">
        <div>
          <h1 className="auth__title" id="reset-title">
            {t('auth.reset.title')}
          </h1>
          <p className="lead">{t('auth.reset.lead')}</p>
        </div>
        {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}
        {done ? (
          <>
            <Alert kind="success" title={t('auth.reset.successTitle')}>
              {t('auth.reset.successText')}
            </Alert>
            <Link className="btn btn--primary btn--lg btn--block" to={routes.login}>
              {t('common.signIn')}
            </Link>
          </>
        ) : (
          <form className="form" noValidate onSubmit={onSubmit}>
            <PasswordField
              label={t('auth.password')}
              name="newPassword"
              autoComplete="new-password"
              placeholder={t('auth.newPasswordPlaceholder')}
              required
              value={password}
              hint={t('auth.passwordHint', { min: siteConfig.passwordMinLength })}
              hintOk={passwordProblems(password).length === 0}
              error={errors.length > 0 ? errors.map((key) => t(key, { min: siteConfig.passwordMinLength })).join(' ') : undefined}
              onChange={(event) => {
                setPassword(event.target.value);
                setErrors([]);
              }}
            />
            <button className="btn btn--primary btn--lg btn--block" type="submit" disabled={busy} aria-busy={busy}>
              {t('auth.reset.submit')}
            </button>
          </form>
        )}
      </section>
    </AuthLayout>
  );
}
