import { useId, type KeyboardEvent } from 'react';

/**
 * A tab of the tab bar: an identifier and the visible text.
 */
export interface TabItem {
  id: string;
  label: string;
}

/**
 * The tab bar of the game screens: one look for the storage tabs and the branches of the
 * technologies. The active tab is marked by the underline and by `aria-selected`, never by
 * colour alone; the arrow keys move between the tabs.
 *
 * @param label The name of the tab bar for a screen reader.
 * @param tabs The tabs in their order.
 * @param active The identifier of the active tab.
 * @param onChange What to do when the player picks a tab.
 */
export function Tabs({ label, tabs, active, onChange }: { label: string; tabs: TabItem[]; active: string; onChange: (id: string) => void }) {
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
    <div aria-label={label} className="tabs" role="tablist">
      {tabs.map((tab, index) => (
        <button
          aria-selected={tab.id === active}
          className="tabs__tab"
          id={`${prefix}-${tab.id}`}
          key={tab.id}
          onClick={() => onChange(tab.id)}
          onKeyDown={(event) => onKeyDown(event, index)}
          role="tab"
          tabIndex={tab.id === active ? 0 : -1}
          type="button"
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
