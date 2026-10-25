import { useEffect, useState } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { MailIcon } from '../../components/Icons';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { siteConfig } from '../../config/site';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { AuthLayout } from './AuthLayout';
import { describeError, type ErrorMessage } from './authErrors';

const SECONDS_PER_MINUTE = 60;

/**
 * "Check your email" after a registration (`?email=`) or a reset request (`&kind=reset`).
 * The confirmation e-mail can be requested again after a cooldown.
 */
export function CheckEmailPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { query } = useRouter();
  useDocumentTitle(t('meta.check.title'));

  const email = query.get('email') ?? '';
  const isReset = query.get('kind') === 'reset';
  const [secondsLeft, setSecondsLeft] = useState<number>(siteConfig.resendCooldownSeconds);
  const [resent, setResent] = useState(false);
  const [failure, setFailure] = useState<ErrorMessage | null>(null);

  useEffect(() => {
    if (secondsLeft <= 0) {
      return;
    }

    const timer = window.setTimeout(() => setSecondsLeft((value) => value - 1), 1000);
    return () => window.clearTimeout(timer);
  }, [secondsLeft]);

  const resend = async () => {
    setFailure(null);
    setResent(false);
    setSecondsLeft(siteConfig.resendCooldownSeconds);

    try {
      if (isReset) {
        await auth.forgotPassword(email);
      } else {
        await auth.resendConfirmation(email);
      }

      setResent(true);
    } catch (error) {
      setFailure(describeError(error));
    }
  };

  const minutes = Math.floor(secondsLeft / SECONDS_PER_MINUTE);
  const seconds = String(secondsLeft % SECONDS_PER_MINUTE).padStart(2, '0');
  const emailLabel = <span className="email-out">{email || t('auth.check.yourEmail')}</span>;
  const [before, after] = t(isReset ? 'auth.check.leadReset' : 'auth.check.lead').split('{email}');

  return (
    <AuthLayout>
      <section className="auth__view" aria-labelledby="check-title">
        <span className="mail-icon" aria-hidden="true">
          <MailIcon />
        </span>
        <div>
          <h1 className="auth__title" id="check-title">
            {t('auth.check.title')}
          </h1>
          <p className="lead">
            {before}
            {emailLabel}
            {after}
          </p>
        </div>
        <Alert kind="info">{t('auth.check.spam')}</Alert>
        {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}
        <button
          className="btn btn--primary btn--lg btn--block"
          type="button"
          disabled={secondsLeft > 0 || !email}
          onClick={() => void resend()}
        >
          <span>{t('auth.check.resend')}</span>
          {secondsLeft > 0 ? <span>{t('auth.check.resendTimer', { time: `${minutes}:${seconds}` })}</span> : null}
        </button>
        <p className="meta" role="status">
          {resent ? `✓ ${t('auth.check.resent')}` : ''}
        </p>
        <div className="row-between">
          <Link className="text-btn" to={isReset ? routes.forgotPassword : routes.register}>
            {t('auth.check.changeAddress')}
          </Link>
          <Link className="text-btn text-btn--muted" to={routes.login}>
            {t('auth.check.back')}
          </Link>
        </div>
      </section>
    </AuthLayout>
  );
}
