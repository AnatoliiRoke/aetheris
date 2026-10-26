import { useEffect, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { useAdminText } from './i18n/useAdminText';

const focusable = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * A modal window of the admin panel (ui-art/05, section 7): it closes with Esc and its button,
 * keeps the focus inside and gives it back when it closes.
 */
export function AdminDialog({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  const { t } = useAdminText();
  const panel = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    panel.current?.querySelector<HTMLElement>(focusable)?.focus();

    return () => opener?.focus();
  }, []);

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
      return;
    }

    if (event.key !== 'Tab' || !panel.current) {
      return;
    }

    const items = [...panel.current.querySelectorAll<HTMLElement>(focusable)];

    if (items.length === 0) {
      return;
    }

    const first = items[0];
    const last = items[items.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  return (
    <div className="admin-dialog" onKeyDown={onKeyDown}>
      <div className="admin-dialog__backdrop" aria-hidden="true" onClick={onClose} />
      <div className="admin-dialog__panel" ref={panel} role="dialog" aria-modal="true" aria-label={title}>
        <header className="admin-dialog__head">
          <h2 className="admin-dialog__title">{title}</h2>
          <button className="btn btn--secondary" type="button" onClick={onClose}>
            {t('admin.card.close')}
          </button>
        </header>
        {children}
      </div>
    </div>
  );
}
