import type { ReactNode } from 'react';
import { GameIcon, type GameIconName } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { CivFooter } from './CivFooter';
import { CivHeader } from './CivHeader';
import { CivMenu } from './CivMenu';
import { EmailBanner } from './EmailBanner';

/**
 * The tabs of the phone: the sections of the menu that the player reaches most.
 */
const tabItems: { id: string; icon: GameIconName; label: MessageKey; route?: string }[] = [
  { id: 'overview', icon: 'overview', label: 'civ.nav.overview', route: routes.game },
  { id: 'planet', icon: 'planet', label: 'civ.nav.planet', route: routes.planet },
  { id: 'storage', icon: 'box', label: 'civ.nav.storage', route: routes.planetStorage },
  { id: 'production', icon: 'production', label: 'civ.nav.production', route: routes.planetBuild },
];

/**
 * The frame of the screens of the game (task 00020, section 8.1): the header with the
 * resources and the balances, the menu on the left, the bottom bar and the tabs of the
 * phone.
 *
 * The frame is the same on every screen of the game: a screen gives it its own content and
 * says which section of the menu is the current one, тож the player always finds the
 * resources, the money and the menu in the same place.
 *
 * @param active The section of the menu the screen belongs to.
 * @param children The content of the screen.
 */
export function CivShell({ active, children }: { active: string; children: ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="civ">
      <CivHeader />
      <EmailBanner />
      <div className="civ__body">
        <CivMenu active={active} />
        <main className="civ__main">{children}</main>
      </div>
      <CivFooter />
      <nav aria-label={t('civ.gameMenu')} className="civ-tabbar">
        {tabItems.map((item) =>
          item.route ? (
            <Link aria-current={item.id === active ? 'page' : undefined} key={item.id} to={item.route}>
              <GameIcon name={item.icon} size={22} />
              <span>{t(item.label)}</span>
            </Link>
          ) : (
            <span aria-disabled="true" className="is-off" key={item.id}>
              <GameIcon name={item.icon} size={22} />
              <span>{t(item.label)}</span>
            </span>
          ),
        )}
      </nav>
    </div>
  );
}
