import { useEffect, useState } from 'react';
import { useApi } from '../../api/useApi';
import { useI18n } from '../../i18n/useI18n';

/**
 * Continue with Google. The button only works when the server has the Google credentials
 * (`GET /api/v1/auth/providers`); otherwise it stays disabled with an explanation.
 */
export function GoogleButton() {
  const { t } = useI18n();
  const { auth } = useApi();
  const [enabled, setEnabled] = useState(false);

  useEffect(() => {
    let current = true;

    auth.providers().then(
      (providers) => current && setEnabled(providers.google),
      () => current && setEnabled(false),
    );

    return () => {
      current = false;
    };
  }, [auth]);

  return (
    <>
      <div className="divider">{t('auth.or')}</div>
      <button
        className="btn btn--secondary btn--lg btn--block"
        type="button"
        disabled={!enabled}
        aria-describedby={enabled ? undefined : 'google-unavailable'}
        onClick={() => window.location.assign(auth.googleSignInUrl(`${window.location.origin}/`))}
      >
        <span className="provider-mark" aria-hidden="true">
          G
        </span>
        <span>{t('auth.google')}</span>
      </button>
      {enabled ? null : (
        <p className="meta auth__note" id="google-unavailable">
          {t('auth.googleUnavailable')}
        </p>
      )}
    </>
  );
}
