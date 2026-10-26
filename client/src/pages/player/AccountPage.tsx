import { useState, type FormEvent } from 'react';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { PasswordField, TextField } from '../../components/FormField';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { siteConfig } from '../../config/site';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { useRouter } from '../../router/useRouter';
import { useSession } from '../../session/useSession';
import { describeError, fieldRuleErrors, nicknameProblems, passwordProblems, type ErrorMessage } from '../auth/authErrors';
import { PlayerLayout } from './PlayerLayout';

type Outcome = { kind: 'success'; text: string } | { kind: 'error'; message: ErrorMessage } | null;

/**
 * The account page (task 00006, decision 10): the nickname, the password, the sign-out of
 * every device and the deletion of the account. The e-mail address is changed only by the
 * support.
 */
export function AccountPage() {
  const { t } = useI18n();
  const { session } = useSession();
  const { query } = useRouter();
  useDocumentTitle(t('meta.account.title'));

  if (session.status !== 'signed-in') {
    return null;
  }

  const { me } = session;

  return (
    <PlayerLayout>
      <h1 className="player__title">{t('player.account.title')}</h1>
      {query.get('deletion') === 'cancelled' ? (
        <Alert kind="success" title={t('player.account.deletionCancelledTitle')}>
          {t('player.account.deletionCancelledText')}
        </Alert>
      ) : null}
      <section className="panel" aria-labelledby="profile-title">
        <h2 className="panel__title" id="profile-title">
          {t('player.account.profile')}
        </h2>
        <dl className="facts">
          <dt>{t('auth.nickname')}</dt>
          <dd>{me.nickname}</dd>
          <dt>{t('auth.email')}</dt>
          <dd>{me.email}</dd>
        </dl>
        <p className="hint">{t('player.account.emailBySupport', { support: siteConfig.supportEmail })}</p>
      </section>
      <NicknameForm current={me.nickname} />
      <PasswordForm />
      <SignOutEverywhere />
      <DeleteAccount />
    </PlayerLayout>
  );
}

function OutcomeAlert({ outcome }: { outcome: Outcome }) {
  const { t } = useI18n();

  if (!outcome) {
    return null;
  }

  return outcome.kind === 'success' ? (
    <Alert kind="success">{outcome.text}</Alert>
  ) : (
    <Alert kind="error" title={t(outcome.message.title)}>
      {t(outcome.message.text)}
    </Alert>
  );
}

function NicknameForm({ current }: { current: string }) {
  const { t, language } = useI18n();
  const { auth } = useApi();
  const { refresh } = useSession();
  const [nickname, setNickname] = useState(current);
  const [errors, setErrors] = useState<MessageKey[]>([]);
  const [outcome, setOutcome] = useState<Outcome>(null);
  const [busy, setBusy] = useState(false);
  const params = { nicknameMin: siteConfig.nicknameMinLength, nicknameMax: siteConfig.nicknameMaxLength };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setOutcome(null);

    const problems = nicknameProblems(nickname);
    setErrors(problems);

    if (problems.length > 0) {
      return;
    }

    setBusy(true);

    try {
      const answer = await auth.changeNickname(nickname.trim());
      await refresh();
      setOutcome({ kind: 'success', text: t('player.account.nicknameChanged', { date: formatDate(answer.nextChangeAt, language) }) });
    } catch (error) {
      const fields = fieldRuleErrors(error, 'nickname');

      if (fields.length > 0) {
        setErrors(fields);
      } else {
        setOutcome({ kind: 'error', message: describeError(error) });
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="form panel" noValidate onSubmit={onSubmit} aria-labelledby="nickname-title">
      <h2 className="panel__title" id="nickname-title">
        {t('player.account.nicknameTitle')}
      </h2>
      <OutcomeAlert outcome={outcome} />
      <TextField
        label={t('auth.nickname')}
        name="nickname"
        autoComplete="username"
        maxLength={siteConfig.nicknameMaxLength}
        value={nickname}
        hint={t('player.account.nicknameHint')}
        error={errors.length > 0 ? errors.map((key) => t(key, params)).join(' ') : undefined}
        onChange={(event) => {
          setNickname(event.target.value);
          setErrors([]);
        }}
      />
      <button className="btn btn--primary" type="submit" disabled={busy || nickname.trim() === current} aria-busy={busy}>
        {t('player.account.nicknameSubmit')}
      </button>
    </form>
  );
}

function PasswordForm() {
  const { t } = useI18n();
  const { auth } = useApi();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [errors, setErrors] = useState<{ current?: MessageKey[]; next?: MessageKey[]; confirmation?: MessageKey[] }>({});
  const [outcome, setOutcome] = useState<Outcome>(null);
  const [busy, setBusy] = useState(false);
  const params = { min: siteConfig.passwordMinLength };
  const describe = (keys?: MessageKey[]) => (keys && keys.length > 0 ? keys.map((key) => t(key, params)).join(' ') : undefined);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setOutcome(null);

    const nextProblems = passwordProblems(newPassword);
    const next = {
      current: currentPassword ? undefined : (['error.passwordEmpty'] as MessageKey[]),
      next: nextProblems.length > 0 ? nextProblems : undefined,
      confirmation: confirmation === newPassword ? undefined : (['error.password_mismatch'] as MessageKey[]),
    };
    setErrors(next);

    if (next.current || next.next || next.confirmation) {
      return;
    }

    setBusy(true);

    try {
      await auth.changePassword(currentPassword, newPassword, confirmation);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmation('');
      setOutcome({ kind: 'success', text: t('player.account.passwordChanged') });
    } catch (error) {
      const server = {
        current: fieldRuleErrors(error, 'current_password'),
        next: fieldRuleErrors(error, 'new_password'),
        confirmation: fieldRuleErrors(error, 'new_password_confirmation'),
      };

      if (server.current.length + server.next.length + server.confirmation.length > 0) {
        setErrors(server);
      } else {
        setOutcome({ kind: 'error', message: describeError(error) });
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="form panel" noValidate onSubmit={onSubmit} aria-labelledby="password-title">
      <h2 className="panel__title" id="password-title">
        {t('player.account.passwordTitle')}
      </h2>
      <OutcomeAlert outcome={outcome} />
      <PasswordField
        label={t('player.account.currentPassword')}
        name="current-password"
        autoComplete="current-password"
        value={currentPassword}
        error={describe(errors.current)}
        onChange={(event) => setCurrentPassword(event.target.value)}
      />
      <PasswordField
        label={t('player.account.newPassword')}
        name="new-password"
        autoComplete="new-password"
        value={newPassword}
        hint={t('auth.passwordHint', params)}
        hintOk={passwordProblems(newPassword).length === 0}
        error={describe(errors.next)}
        onChange={(event) => setNewPassword(event.target.value)}
      />
      <PasswordField
        label={t('auth.passwordRepeat')}
        name="new-password-confirmation"
        autoComplete="new-password"
        value={confirmation}
        error={describe(errors.confirmation)}
        onChange={(event) => setConfirmation(event.target.value)}
      />
      <button className="btn btn--primary" type="submit" disabled={busy} aria-busy={busy}>
        {t('player.account.passwordSubmit')}
      </button>
    </form>
  );
}

function SignOutEverywhere() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { forget } = useSession();
  const { navigate } = useRouter();
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [busy, setBusy] = useState(false);

  const onClick = async () => {
    setBusy(true);
    setFailure(null);

    try {
      await auth.signOutEverywhere();
      forget();
      navigate(routes.login);
    } catch (error) {
      setFailure(describeError(error));
      setBusy(false);
    }
  };

  return (
    <section className="panel" aria-labelledby="everywhere-title">
      <h2 className="panel__title" id="everywhere-title">
        {t('player.account.everywhereTitle')}
      </h2>
      <p className="lead">{t('player.account.everywhereText')}</p>
      {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}
      <button className="btn btn--secondary" type="button" disabled={busy} aria-busy={busy} onClick={() => void onClick()}>
        {t('player.account.everywhereSubmit')}
      </button>
    </section>
  );
}

function DeleteAccount() {
  const { t } = useI18n();
  const { auth } = useApi();
  const { forget } = useSession();
  const { navigate } = useRouter();
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<MessageKey[]>([]);
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    if (!password) {
      setErrors(['error.passwordEmpty']);
      return;
    }

    setBusy(true);

    try {
      const { deletionDueAt } = await auth.deleteAccount(password);
      forget();
      navigate(`${routes.login}?deleted=${encodeURIComponent(deletionDueAt)}`);
    } catch (error) {
      const fields = fieldRuleErrors(error, 'password');

      if (fields.length > 0) {
        setErrors(fields);
      } else {
        setFailure(describeError(error));
      }

      setBusy(false);
    }
  };

  return (
    <form className="form panel panel--danger" noValidate onSubmit={onSubmit} aria-labelledby="delete-title">
      <h2 className="panel__title" id="delete-title">
        {t('player.account.deleteTitle')}
      </h2>
      <p className="lead">{t('player.account.deleteText')}</p>
      {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}
      <PasswordField
        label={t('auth.password')}
        name="delete-password"
        autoComplete="current-password"
        value={password}
        error={errors.length > 0 ? errors.map((key) => t(key)).join(' ') : undefined}
        onChange={(event) => {
          setPassword(event.target.value);
          setErrors([]);
        }}
      />
      <button className="btn btn--secondary" type="submit" disabled={busy} aria-busy={busy}>
        {t('player.account.deleteSubmit')}
      </button>
    </form>
  );
}

/**
 * Formats a moment of the server for the language of the page.
 */
function formatDate(value: string, language: string): string {
  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? value : date.toLocaleString(language === 'uk' ? 'uk-UA' : 'en-GB', { dateStyle: 'long', timeStyle: 'short' });
}
