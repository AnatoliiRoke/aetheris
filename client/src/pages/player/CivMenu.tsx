import { memo } from 'react';
import { GameIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { activeItemId, menuItems, type MenuItem } from './civMenuItems';
import { reportRender } from './renderProbe';

/**
 * An item of the menu: a link when the section has a screen, otherwise an inactive button with
 * the reason in the hint. The inactive item has no label of its own: it is only inactive.
 *
 * @param item The section.
 * @param current Whether the screen belongs to the section.
 */
export function CivMenuItem({ item, current }: { item: MenuItem; current: boolean }) {
  const { t } = useI18n();

  return (
    <>
      {item.separated ? <span aria-hidden="true" className="civ-nav-hr" /> : null}
      {item.route ? (
        <Link aria-current={current ? 'page' : undefined} className="civ-nav" to={item.route}>
          <GameIcon name={item.icon} />
          <span className="civ-nav__label">{t(item.label)}</span>
        </Link>
      ) : (
        <button className="civ-nav is-off" disabled title={t('civ.soonReason')} type="button">
          <GameIcon name={item.icon} />
          <span className="civ-nav__label">{t(item.label)}</span>
        </button>
      )}
    </>
  );
}

/**
 * The sections of the menu with the one the address belongs to marked as the current one.
 */
export function CivMenuList() {
  const { path } = useRouter();
  const active = activeItemId(path);

  return (
    <>
      {menuItems.map((item) => (
        <CivMenuItem current={item.id === active} item={item} key={item.id} />
      ))}
    </>
  );
}

/**
 * The menu of the game on the left. The menu is the same on every screen of the game
 * (task 00020, section 8.1): only the current section changes, and the menu knows it from the
 * address, so no page has to tell it. A block of its own: it is drawn again only when the
 * address or the language changes, never with the content of a page.
 */
export const CivMenu = memo(function CivMenu() {
  reportRender('CivMenu');

  const { t } = useI18n();

  return (
    <nav aria-label={t('civ.gameMenu')} className="civ-side">
      <CivMenuList />
    </nav>
  );
});
