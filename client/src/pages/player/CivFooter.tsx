import { memo, useMemo } from 'react';
import { artImage } from '../../assets/images';
import { GameIcon } from '../../components/GameIcon';
import { useI18n } from '../../i18n/useI18n';
import { useTexts } from '../../texts/useTexts';
import { reportRender } from './renderProbe';
import { useBodies } from './useBodies';
import { useCurrentPlanetId, useShellActions } from './useShell';

/**
 * The bottom bar of the screens of the game: the switcher of the bodies with the thumbnails of
 * their types, the current body outlined, and the global chat, which no task has made yet, as an
 * inactive field with the reason in its hint (task 00020, section 8.1).
 *
 * A block of its own: it reads only the list of the bodies and the current body, so it is not
 * drawn again with the content of a page or with the tick of a timer.
 */
export const CivFooter = memo(function CivFooter() {
  reportRender('CivFooter');

  const { t } = useI18n();
  const { text } = useTexts();
  const { bodies, reference } = useBodies();
  const { selectPlanet } = useShellActions();
  const currentId = useCurrentPlanetId();
  const types = useMemo(() => new Map((reference?.objectTypes ?? []).map((type) => [type.id, type])), [reference]);
  const current = bodies.find((body) => body.planetId === currentId) ?? null;

  return (
    <footer className="civ-bottombar">
      <div className="civ-bottombar__switch">
        <div aria-label={t('civ.switcher')} className="civ-thumbs" role="group">
          {bodies.map((body) => {
            const name = body.name ?? t('civ.untitled');
            const source = artImage(types.get(body.objectTypeId)?.imageKey);

            return (
              <button
                aria-label={name}
                aria-pressed={body.planetId === currentId}
                className="civ-thumb"
                key={body.planetId}
                onClick={() => selectPlanet(body.planetId)}
                title={name}
                type="button"
              >
                {source ? <img alt="" height={44} loading="lazy" src={source} width={44} /> : <span className="civ-thumb__letter">{name.slice(0, 1)}</span>}
              </button>
            );
          })}
        </div>
        <span className="civ-current">
          <small>{t('civ.current')}</small>
          <strong>{current ? current.name ?? text(types.get(current.objectTypeId)?.nameKey) ?? t('civ.untitled') : t('civ.noData')}</strong>
        </span>
      </div>
      <form className="civ-chat" onSubmit={(event) => event.preventDefault()}>
        <label htmlFor="civ-chat-input">
          {t('civ.chat')}
          <span className="chip">{t('civ.soon')}</span>
        </label>
        <input autoComplete="off" disabled id="civ-chat-input" placeholder={t('civ.chatPlaceholder')} title={t('civ.chatSoon')} type="text" />
        <button aria-label={t('civ.chatSend')} className="civ-ibtn" disabled title={t('civ.chatSoon')} type="submit">
          <GameIcon name="send" size={20} />
        </button>
      </form>
    </footer>
  );
});
