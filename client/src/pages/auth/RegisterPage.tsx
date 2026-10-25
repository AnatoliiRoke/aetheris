import { useRef, useState, type FormEvent } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { PasswordField, TextField } from '../../components/FormField';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { siteConfig } from '../../config/site';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { AuthLayout } from './AuthLayout';
import { describeError, isEmail, passwordRuleErrors, type ErrorMessage } from './authErrors';
import { GoogleButton } from './GoogleButton';

interface FieldErrors {
  email?: MessageKey;
  password?: MessageKey[];
  terms?: MessageKey;
}

/**
 * Registration with e-mail and password (task 00002, `POST /api/v1/auth/register`).
 *
 * The server always answers "accepted", whether the address is new or not, so the page
 * always continues to "check your email".
 */
export function RegisterPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { navigate } = useRouter();
  useDocumentTitle(t('meta.register.title'));

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [terms, setTerms] = useState(false);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [busy, setBusy] = useState(false);
  const emailRef = useRef<HTMLInputElement | null>(null);
  const passwordRef = useRef<HTMLInputElement | null>(null);
  const termsRef = useRef<HTMLInputElement | null>(null);

  const passwordLongEnough = password.length >= siteConfig.passwordMinLength;

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    const next: FieldErrors = {
      email: isEmail(email.trim()) ? undefined : 'error.emailInvalid',
      password: passwordLongEnough ? undefined : ['error.passwordShort'],
      terms: terms ? undefined : 'error.termsRequired',
    };
    setErrors(next);

    const firstInvalid = next.email ? emailRef : next.password ? passwordRef : next.terms ? termsRef : null;

    if (firstInvalid) {
      firstInvalid.current?.focus();
      return;
    }

    setBusy(true);

    try {
      await auth.register(email.trim(), password);
      navigate(`${routes.checkEmail}?email=${encodeURIComponent(email.trim())}`);
    } catch (error) {
      const rules = passwordRuleErrors(error, 'Password');

      if (rules.length > 0) {
        setErrors({ password: rules });
        passwordRef.current?.focus();
      } else {
        setFailure(describeError(error));
      }
    } finally {
      setBusy(false);
    }
  };

  const passwordError = errors.password
    ?.map((key) => t(key, { min: siteConfig.passwordMinLength }))
    .join(' ');

  return (
    <AuthLayout>
      <section className="auth__view" aria-labelledby="signup-title">
        <div>
          <h1 className="auth__title" id="signup-title">
            {t('auth.signup.title')}
          </h1>
          <p className="lead">{t('auth.signup.lead')}</p>
        </div>

        {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}

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
            error={errors.email && t(errors.email)}
            onChange={(event) => {
              setEmail(event.target.value);
              setErrors((current) => ({ ...current, email: undefined }));
            }}
          />
          <PasswordField
            ref={passwordRef}
            label={t('auth.password')}
            name="password"
            autoComplete="new-password"
            placeholder={t('auth.newPasswordPlaceholder')}
            required
            minLength={siteConfig.passwordMinLength}
            value={password}
            hint={t('auth.passwordHint', { min: siteConfig.passwordMinLength })}
            hintOk={passwordLongEnough}
            error={passwordError}
            onChange={(event) => {
              setPassword(event.target.value);
              setErrors((current) => ({ ...current, password: undefined }));
            }}
          />
          <div className="field">
            <label className="check">
              <input
                ref={termsRef}
                type="checkbox"
                name="terms"
                required
                checked={terms}
                aria-invalid={errors.terms ? true : undefined}
                aria-describedby={errors.terms ? 'signup-terms-error' : undefined}
                onChange={(event) => {
                  setTerms(event.target.checked);
                  setErrors((current) => ({ ...current, terms: undefined }));
                }}
              />
              <span>
                {t('auth.signup.termsPrefix')} <a href="/#terms">{t('footer.terms')}</a> {t('auth.signup.termsAnd')}{' '}
                <a href="/#privacy">{t('footer.privacy')}</a>
              </span>
            </label>
            {errors.terms ? (
              <p className="error-text" id="signup-terms-error">
                {t(errors.terms)}
              </p>
            ) : null}
          </div>
          <button className="btn btn--primary btn--lg btn--block" type="submit" disabled={busy} aria-busy={busy}>
            {t('auth.signup.submit')}
          </button>
        </form>

        <GoogleButton />
        <p className="auth__switch">
          {t('auth.signup.haveAccount')}{' '}
          <Link className="text-btn" to={routes.login}>
            {t('common.signIn')}
          </Link>
        </p>
      </section>
    </AuthLayout>
  );
}
