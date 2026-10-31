import { useId, type KeyboardEvent } from 'react';

/**
 * A tab of the market: an identifier, the text and the count badge when there is something to count.
 */
export interface MarketTab {
  id: string;
  label: string;
  badge?: number;
}

/**
 * The tabs of the market. The active tab is told by the underline and by `aria-selected`, never by
 * colour alone; the arrow keys move between the tabs and the row scrolls on a phone.
 *
 * @param label The name of the row for a screen reader.
 * @param tabs The tabs in their order.
 * @param active The chosen tab.
 * @param onChange What to do when the player picks a tab.
 * @param panelId The identifier of the panel of the tabs, so the tabs name the panel they control.
 */
export function MarketTabs({
  label,
  tabs,
  active,
  onChange,
  panelId,
}: {
  label: string;
  tabs: MarketTab[];
  active: string;
  onChange: (id: string) => void;
  panelId: string;
}) {
  const prefix = useId();

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;

    if (step === 0) {
      return;
    }

    event.preventDefault();
    const target = tabs[(index + step + tabs.length) % tabs.length];

    onChange(target.id);
    document.getElementById(`${prefix}-${target.id}`)?.focus();
  };

  return (
    <div aria-label={label} className="mk-tabs" role="tablist">
      {tabs.map((tab, index) => (
        <button
          aria-controls={panelId}
          aria-selected={tab.id === active}
          className="mk-tab"
          id={`${prefix}-${tab.id}`}
          key={tab.id}
          onClick={() => onChange(tab.id)}
          onKeyDown={(event) => onKeyDown(event, index)}
          role="tab"
          tabIndex={tab.id === active ? 0 : -1}
          type="button"
        >
          {tab.label}
          {tab.badge && tab.badge > 0 ? <span className="mk-tab__badge">{tab.badge}</span> : null}
        </button>
      ))}
    </div>
  );
}
