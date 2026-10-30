import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { GameIcon, type GameIconName } from '../../components/GameIcon';

/**
 * The sections of the menu of the game. The sections that no task has made yet stay in the
 * menu but are not active, with the reason in the hint (task 00010, section 2.7).
 *
 * The menu is the same on every screen of the game (task 00020, section 8.1): only the
 * active section changes.
 */
const menuItems: { id: string; icon: GameIconName; label: MessageKey; route?: string; soon?: boolean }[] = [
  { id: 'overview', icon: 'overview', label: 'civ.nav.overview', route: routes.game },
  { id: 'planet', icon: 'planet', label: 'civ.nav.planet', route: routes.planet },
  { id: 'storage', icon: 'box', label: 'civ.nav.storage', route: routes.planetStorage },
  { id: 'production', icon: 'production', label: 'civ.nav.production', route: routes.planetBuild },
  { id: 'shipyard', icon: 'shipyard', label: 'civ.nav.shipyard', soon: true },
  { id: 'fleets', icon: 'fleets', label: 'civ.nav.fleets', soon: true },
  { id: 'map', icon: 'map', label: 'civ.nav.map', soon: true },
  { id: 'research', icon: 'research', label: 'civ.nav.research', route: routes.planetResearch },
  { id: 'market', icon: 'market', label: 'civ.nav.market', soon: true },
  { id: 'ranking', icon: 'ranking', label: 'civ.nav.ranking', soon: true },
  { id: 'battle', icon: 'battle', label: 'civ.nav.battle', soon: true },
  { id: 'clan', icon: 'clan', label: 'civ.nav.clan', soon: true },
  { id: 'mail', icon: 'mail', label: 'civ.nav.mail', soon: true },
  { id: 'help', icon: 'help', label: 'civ.nav.help' },
  { id: 'settings', icon: 'settings', label: 'civ.nav.settings', route: routes.account },
];

/**
 * The menu of the game on the left: the sections that exist are links, the others are shown
 * with the reason in the hint.
 *
 * @param active The section the screen belongs to; it is marked as the current one.
 */
export function CivMenu({ active }: { active: string }) {
  const { t } = useI18n();
  const { session } = useSession();
  const confirmed = session.status === 'signed-in' && session.me.emailConfirmed;

  return (
    <nav aria-label={t('civ.gameMenu')} className="civ-side">
      {menuItems.map((item) =>
        item.route ? (
          <Link aria-current={item.id === active ? 'page' : undefined} className="civ-nav" key={item.id} to={item.route}>
            <GameIcon name={item.icon} />
            <span className="civ-nav__label">{t(item.label)}</span>
          </Link>
        ) : (
          <span className="civ-nav-wrap" key={item.id}>
            <button
              className="civ-nav is-off"
              disabled={item.soon === true || !confirmed}
              title={item.soon ? t('civ.soon') : t('player.game.locked')}
              type="button"
            >
              <GameIcon name={item.icon} />
              <span className="civ-nav__label">{t(item.label)}</span>
            </button>
            {item.soon ? <span className="civ-nav__tag">{t('civ.soon')}</span> : null}
          </span>
        ),
      )}
    </nav>
  );
}
