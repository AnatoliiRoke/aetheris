import { useState } from 'react';
import { ApiError } from '../../api/http';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { useSession } from '../../session/useSession';

/**
 * "Confirm your email" with a button that sends the e-mail again, shown while the address
 * of the account is not confirmed (task 00006, section 3.2).
 */
export function EmailBanner() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { session } = useSession();
  const [state, setState] = useState<'idle' | 'busy' | 'sent' | 'too-soon' | 'failed'>('idle');

  if (session.status !== 'signed-in' || session.me.emailConfirmed) {
    return null;
  }

  const resend = async () => {
    setState('busy');

    try {
      await auth.resendMyConfirmation();
      setState('sent');
    } catch (error) {
      setState(error instanceof ApiError && error.code === 'confirmation_too_soon' ? 'too-soon' : 'failed');
    }
  };

  const note = {
    idle: null,
    busy: null,
    sent: t('player.banner.sent'),
    'too-soon': t('player.banner.tooSoon'),
    failed: t('error.generic.text'),
  }[state];

  return (
    <Alert kind="info" title={t('player.banner.title')}>
      <span className="banner">
        <span>
          {t('player.banner.text', { email: session.me.email })}
          {note ? ` ${note}` : null}
        </span>
        <button className="btn btn--secondary" type="button" disabled={state === 'busy'} aria-busy={state === 'busy'} onClick={() => void resend()}>
          {t('player.banner.resend')}
        </button>
      </span>
    </Alert>
  );
}
