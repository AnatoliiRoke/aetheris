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
import { describeError, fieldRuleErrors, isEmail, nicknameProblems, passwordProblems, type ErrorMessage } from './authErrors';
import { GoogleButton } from './GoogleButton';

interface FieldErrors {
  email?: MessageKey[];
  nickname?: MessageKey[];
  password?: MessageKey[];
  confirmation?: MessageKey[];
  terms?: MessageKey;
}

/**
 * Registration with e-mail, nickname, password and its repetition (tasks 00002 and 00006,
 * `POST /api/v1/auth/register`).
 *
 * The server answers "accepted" whether the address is new or not, so the page always
 * continues to "check your email". The client checks the fields first; the server checks
 * them again and also the restricted words, the taken nicknames and the blocked domains.
 */
export function RegisterPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { navigate } = useRouter();
  useDocumentTitle(t('meta.register.title'));

  const [email, setEmail] = useState('');
  const [nickname, setNickname] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [terms, setTerms] = useState(false);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [busy, setBusy] = useState(false);
  const emailRef = useRef<HTMLInputElement | null>(null);
  const nicknameRef = useRef<HTMLInputElement | null>(null);
  const passwordRef = useRef<HTMLInputElement | null>(null);
  const confirmationRef = useRef<HTMLInputElement | null>(null);
  const termsRef = useRef<HTMLInputElement | null>(null);

  const params = {
    min: siteConfig.passwordMinLength,
    nicknameMin: siteConfig.nicknameMinLength,
    nicknameMax: siteConfig.nicknameMaxLength,
  };
  const passwordOk = passwordProblems(password).length === 0;
  const describe = (keys?: MessageKey[]) => (keys && keys.length > 0 ? keys.map((key) => t(key, params)).join(' ') : undefined);

  const focusFirst = (next: FieldErrors) => {
    const first = next.email
      ? emailRef
      : next.nickname
        ? nicknameRef
        : next.password
          ? passwordRef
          : next.confirmation
            ? confirmationRef
            : next.terms
              ? termsRef
              : null;
    first?.current?.focus();
    return first !== null;
  };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    const nicknameErrors = nicknameProblems(nickname);
    const passwordErrors = passwordProblems(password);
    const next: FieldErrors = {
      email: isEmail(email.trim()) ? undefined : ['error.emailInvalid'],
      nickname: nicknameErrors.length > 0 ? nicknameErrors : undefined,
      password: passwordErrors.length > 0 ? passwordErrors : undefined,
      confirmation: confirmation === password ? undefined : ['error.password_mismatch'],
      terms: terms ? undefined : 'error.termsRequired',
    };
    setErrors(next);

    if (focusFirst(next)) {
      return;
    }

    setBusy(true);

    try {
      await auth.register({ email: email.trim(), nickname: nickname.trim(), password, passwordConfirmation: confirmation });
      navigate(`${routes.checkEmail}?email=${encodeURIComponent(email.trim())}`);
    } catch (error) {
      const server: FieldErrors = {
        email: nonEmpty(fieldRuleErrors(error, 'Email')),
        nickname: nonEmpty(fieldRuleErrors(error, 'Nickname')),
        password: nonEmpty(fieldRuleErrors(error, 'Password')),
        confirmation: nonEmpty(fieldRuleErrors(error, 'PasswordConfirmation')),
      };

      if (server.email || server.nickname || server.password || server.confirmation) {
        setErrors(server);
        focusFirst(server);
      } else {
        setFailure(describeError(error));
      }
    } finally {
      setBusy(false);
    }
  };

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
            error={describe(errors.email)}
            onChange={(event) => {
              setEmail(event.target.value);
              setErrors((current) => ({ ...current, email: undefined }));
            }}
          />
          <TextField
            ref={nicknameRef}
            label={t('auth.nickname')}
            name="nickname"
            autoComplete="username"
            placeholder={t('auth.nicknamePlaceholder')}
            required
            maxLength={siteConfig.nicknameMaxLength}
            value={nickname}
            hint={t('auth.nicknameHint', params)}
            error={describe(errors.nickname)}
            onChange={(event) => {
              setNickname(event.target.value);
              setErrors((current) => ({ ...current, nickname: undefined }));
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
            hint={t('auth.passwordHint', params)}
            hintOk={passwordOk}
            error={describe(errors.password)}
            onChange={(event) => {
              setPassword(event.target.value);
              setErrors((current) => ({ ...current, password: undefined }));
            }}
          />
          <PasswordField
            ref={confirmationRef}
            label={t('auth.passwordRepeat')}
            name="password-confirmation"
            autoComplete="new-password"
            placeholder={t('auth.passwordRepeatPlaceholder')}
            required
            value={confirmation}
            error={describe(errors.confirmation)}
            onChange={(event) => {
              setConfirmation(event.target.value);
              setErrors((current) => ({ ...current, confirmation: undefined }));
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

function nonEmpty(keys: MessageKey[]): MessageKey[] | undefined {
  return keys.length > 0 ? keys : undefined;
}
