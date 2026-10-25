import type { ReactNode } from 'react';

type AlertKind = 'error' | 'success' | 'info';

const icons: Record<AlertKind, string> = { error: '!', success: '✓', info: 'i' };

interface AlertProps {
  kind: AlertKind;
  title?: string;
  children?: ReactNode;
}

/**
 * A message box. Errors are announced at once, other messages politely.
 */
export function Alert({ kind, title, children }: AlertProps) {
  return (
    <div className={`alert alert--${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      <span className="alert__icon" aria-hidden="true">
        {icons[kind]}
      </span>
      {title ? (
        <div className="alert__body">
          <strong>{title}</strong>
          {children ? <span>{children}</span> : null}
        </div>
      ) : (
        <span>{children}</span>
      )}
    </div>
  );
}
