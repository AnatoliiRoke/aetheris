import { useEffect, useState } from 'react';
import type { OwnedPlanet } from '../../api/worldApi';
import { useApi } from '../../api/useApi';
import { useI18n } from '../../i18n/useI18n';

/**
 * The bottom bar of the screens of the game: the switcher of the bodies and the place of the
 * chat, which no task has made yet (task 00020, section 8.1).
 *
 * The bar is the same on every screen of the game.
 */
export function CivFooter() {
  const { t } = useI18n();
  const { world } = useApi();
  const [bodies, setBodies] = useState<OwnedPlanet[]>([]);

  useEffect(() => {
    let cancelled = false;

    void world.planets().then((answer) => {
      if (!cancelled) {
        setBodies(answer);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [world]);

  return (
    <footer className="civ-bottombar">
      <div className="civ-bottombar__switch">
        {bodies.map((planet) => (
          <span className="civ-thumb" key={planet.planetId} title={planet.name ?? String(planet.planetId)}>
            <span className="civ-thumb__letter">{planet.name?.slice(0, 1) ?? '·'}</span>
          </span>
        ))}
        <span className="civ-current">
          <small>{t('civ.current')}</small>
          <strong>{t('civ.bodiesCount', { count: bodies.length })}</strong>
        </span>
      </div>
      <p className="meta civ-chat-soon">{t('civ.chatSoon')}</p>
    </footer>
  );
}
