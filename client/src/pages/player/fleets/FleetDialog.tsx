import { useEffect, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';

/**
 * The window of the screen of the fleets (mock-up `AETHERIS_fleets`): a narrow window with the heading,
 * a button that closes it and the buttons at the bottom. It closes on Escape and keeps the focus inside.
 *
 * The window is drawn inside the frame of the game, so it has the sizes of the text of the game and not
 * only those of the root.
 *
 * @param title The name of the window.
 * @param lead The line under the name, or `null`.
 * @param onClose What to do when the player closes the window.
 * @param children The content.
 * @param actions The buttons at the bottom.
 */
export function FleetDialog({
  title,
  lead,
  onClose,
  children,
  actions,
}: {
  title: string;
  lead?: string | null;
  onClose: () => void;
  children: ReactNode;
  actions?: ReactNode;
}) {
  const { t } = useI18n();
  const boxRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    // The field of the name comes first, then the first button at the bottom, then any button.
    (
      boxRef.current?.querySelector<HTMLElement>('.fl-input') ??
      boxRef.current?.querySelector<HTMLElement>('.fl-dialog__actions button:not([disabled])') ??
      boxRef.current?.querySelector<HTMLElement>('button:not([disabled]), input:not([disabled])')
    )?.focus();

    return () => previous?.focus();
  }, []);

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();

      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const focusable = [...(boxRef.current?.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled])') ?? [])];

    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return createPortal(
    <div className="fl-dialog" onKeyDown={onKeyDown} role="presentation">
      <div aria-label={title} aria-modal="true" className="fl-dialog__box" ref={boxRef} role="dialog">
        <div className="fl-dialog__head">
          <div>
            <h2 className="fl-dialog__title">{title}</h2>
            {lead ? <p className="fl-dialog__lead">{lead}</p> : null}
          </div>
          <button aria-label={t('fleets.closeDialog')} className="fl-iconbtn" onClick={onClose} type="button">
            <GameIcon name="close" />
          </button>
        </div>
        {children}
        {actions ? <div className="fl-dialog__actions">{actions}</div> : null}
      </div>
    </div>,
    document.querySelector('.civ') ?? document.body,
  );
}
