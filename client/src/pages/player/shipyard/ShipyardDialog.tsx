import { useEffect, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';

/**
 * The window of the shipyard (task 00038): a big window on a desktop screen and a sheet from the
 * bottom on a phone. It closes on Escape and keeps the focus inside.
 *
 * @param title The name of the window.
 * @param lead The line under the name, or `null`.
 * @param onClose What to do when the player closes the window.
 * @param children The content of the window.
 * @param actions The buttons at the bottom of the window.
 */
export function ShipyardDialog({
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

    boxRef.current?.querySelector<HTMLElement>('button:not([disabled])')?.focus();

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

      return;
    }

    if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return createPortal(
    <div className="sh-dialog" onKeyDown={onKeyDown} role="presentation">
      <div aria-label={title} aria-modal="true" className="sh-dialog__box" ref={boxRef} role="dialog">
        <div className="sh-dialog__head">
          <div>
            <h2 className="sh-dialog__title">{title}</h2>
            {lead ? <p className="sh-dialog__lead">{lead}</p> : null}
          </div>
          <button aria-label={t('shipyard.close')} className="btn btn--secondary" onClick={onClose} type="button">
            <GameIcon name="close" />
          </button>
        </div>
        {children}
        {actions ? <div className="sh-dialog__actions">{actions}</div> : null}
      </div>
    </div>,
    document.body,
  );
}
