import { memo, useState } from 'react';
import { GameIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { Link } from '../../router/Router';
import { useRouter } from '../../router/useRouter';
import { activeItemId, menuItems, tabItemIds } from './civMenuItems';
import { CivMenuList } from './CivMenu';
import { reportRender } from './renderProbe';

/**
 * The tabs of the phone: the sections of the design of the phone and the button "More" that opens
 * the rest of the menu, so every section of the menu is reachable on the phone.
 */
export const CivTabbar = memo(function CivTabbar() {
  reportRender('CivTabbar');

  const { t } = useI18n();
  const { path } = useRouter();
  const [moreOpen, setMoreOpen] = useState(false);
  const active = activeItemId(path);
  const tabs = menuItems.filter((item) => tabItemIds.includes(item.id));

  return (
    <>
      {moreOpen ? (
        <nav aria-label={t('civ.moreMenu')} className="civ-more" onClick={() => setMoreOpen(false)}>
          <CivMenuList />
        </nav>
      ) : null}
      <nav aria-label={t('civ.gameMenu')} className="civ-tabbar">
        {tabs.map((item) =>
          item.route ? (
            <Link aria-current={item.id === active ? 'page' : undefined} key={item.id} to={item.route}>
              <GameIcon name={item.icon} size={22} />
              <span>{t(item.label)}</span>
            </Link>
          ) : (
            <button className="is-off" disabled key={item.id} title={t('civ.soonReason')} type="button">
              <GameIcon name={item.icon} size={22} />
              <span>{t(item.label)}</span>
            </button>
          ),
        )}
        <button aria-expanded={moreOpen} onClick={() => setMoreOpen((open) => !open)} type="button">
          <GameIcon name="more" size={22} />
          <span>{t('civ.more')}</span>
        </button>
      </nav>
    </>
  );
});
