import { useEffect, useState } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { AuthLayout } from './AuthLayout';
import { describeError, type ErrorMessage } from './authErrors';

/**
 * The target of the confirmation link: `/confirm-email?userId=…&token=…`.
 */
export function ConfirmEmailPage() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { query } = useRouter();
  useDocumentTitle(t('meta.confirm.title'));

  const userId = query.get('userId') ?? '';
  const token = query.get('token') ?? '';
  const linkIsComplete = userId.length > 0 && token.length > 0;
  const [answer, setState] = useState<{ status: 'pending' } | { status: 'done' } | { status: 'failed'; message: ErrorMessage }>({
    status: 'pending',
  });

  // A link without its parts is invalid without asking the server.
  const state: typeof answer = linkIsComplete
    ? answer
    : { status: 'failed', message: { title: 'error.invalidToken.title', text: 'error.invalidToken.text' } };

  useEffect(() => {
    let current = true;

    if (!linkIsComplete) {
      return;
    }

    auth.confirmEmail(userId, token).then(
      () => current && setState({ status: 'done' }),
      (error: unknown) => current && setState({ status: 'failed', message: describeError(error) }),
    );

    return () => {
      current = false;
    };
  }, [auth, userId, token, linkIsComplete]);

  return (
    <AuthLayout>
      <section className="auth__view" aria-labelledby="confirm-title">
        <h1 className="auth__title" id="confirm-title">
          {t('auth.confirm.title')}
        </h1>
        {state.status === 'pending' ? <p className="lead" role="status">{t('auth.confirm.pending')}</p> : null}
        {state.status === 'done' ? (
          <Alert kind="success" title={t('auth.confirm.successTitle')}>
            {t('auth.confirm.successText')}
          </Alert>
        ) : null}
        {state.status === 'failed' ? (
          <Alert kind="error" title={t(state.message.title)}>
            {t(state.message.text)}
          </Alert>
        ) : null}
        <Link className="btn btn--primary btn--lg btn--block" to={routes.login}>
          {t('common.signIn')}
        </Link>
      </section>
    </AuthLayout>
  );
}
