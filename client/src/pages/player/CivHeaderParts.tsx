import { GameIcon, type GameIconName } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { useSession } from '../../session/useSession';

/**
 * A counter of the top bar: the icon, the name for a screen reader and the value. A counter whose
 * data the server does not have yet shows a dash, not a number, so nobody takes it for data.
 *
 * @param icon The icon.
 * @param tone The colour class of the icon.
 * @param label The name of the counter.
 * @param value The text of the value, or `null` for the dash of a placeholder.
 * @param title The hint.
 */
export function Counter({
  icon,
  tone,
  label,
  value,
  title,
}: {
  icon: GameIconName;
  tone: 'credits' | 'trade' | 'energy';
  label: string;
  value: string | null;
  title: string;
}) {
  return (
    <li className={value === null ? `civ-counter civ-counter--${tone} is-off` : `civ-counter civ-counter--${tone}`} title={title}>
      <GameIcon name={icon} size={18} />
      <span className="visually-hidden">{label}:</span>
      <span className="num">{value ?? '—'}</span>
    </li>
  );
}

/**
 * An alert of the top bar. An alert that has no data yet is inactive and says why in its hint;
 * an alert that fires leads where the player can act.
 *
 * @param icon The icon.
 * @param label The name of the alert.
 * @param tone The colour of a fired alert.
 * @param title The hint.
 * @param to Where a fired alert leads; without it the alert is inactive.
 */
export function AlertPill({
  icon,
  label,
  tone,
  title,
  to,
}: {
  icon: GameIconName;
  label: string;
  tone: 'danger' | 'warning';
  title: string;
  to?: string;
}) {
  return to ? (
    <Link className={`civ-alert civ-alert--${tone}`} title={title} to={to}>
      <GameIcon name={icon} size={16} />
      {label}
    </Link>
  ) : (
    <button aria-disabled="true" className="civ-alert is-off" disabled title={title} type="button">
      <GameIcon name={icon} size={16} />
      {label}
    </button>
  );
}

/**
 * The buttons of the top bar: the mail (its counter of the unread letters comes with the mail
 * itself), the settings of the account and the sign-out. The sign-out is the one of the other
 * screens of the player: it ends the session and leads to the home page.
 */
export function HeaderTools() {
  const { t } = useI18n();
  const { signOut } = useSession();
  const { navigate } = useRouter();

  const onSignOut = async () => {
    await signOut();
    navigate(routes.home);
  };

  return (
    <>
      <span aria-hidden="true" className="civ-sep" />
      <button aria-disabled="true" aria-label={t('civ.nav.mail')} className="civ-ibtn" disabled title={t('civ.mailSoon')} type="button">
        <GameIcon name="mail" size={22} />
      </button>
      <Link aria-label={t('civ.nav.settings')} className="civ-ibtn" title={t('civ.nav.settings')} to={routes.account}>
        <GameIcon name="settings" size={22} />
      </Link>
      <button aria-label={t('common.signOut')} className="civ-ibtn" onClick={() => void onSignOut()} title={t('common.signOut')} type="button">
        <GameIcon name="signout" size={22} />
      </button>
    </>
  );
}
