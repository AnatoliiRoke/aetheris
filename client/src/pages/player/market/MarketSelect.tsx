import { useEffect, useId, useRef, useState } from 'react';
import { GameIcon } from '../../../components/GameIcon';

/**
 * An option of the select: the text, whether it is chosen, and when it cannot be chosen, the reason.
 */
export interface SelectOption {
  id: string;
  label: string;
  disabledReason?: string;
}

/**
 * A select of the market: a button that opens a list of options. The chosen option is told by
 * `aria-selected` and by the colour; an option that cannot be chosen shows its reason in text.
 *
 * @param label The text of the label above the select.
 * @param valueLabel The text of the chosen option, shown on the button.
 * @param options The options.
 * @param selectedId The identifier of the chosen option.
 * @param onSelect What to do when the player picks an option.
 * @param disabled Whether the select is closed to the player.
 */
export function MarketSelect({
  label,
  valueLabel,
  options,
  selectedId,
  onSelect,
  disabled = false,
}: {
  label: string;
  valueLabel: string;
  options: SelectOption[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  disabled?: boolean;
}) {
  const labelId = useId();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const onPointer = (event: PointerEvent) => {
      if (box.current && event.target instanceof Node && !box.current.contains(event.target)) {
        setOpen(false);
      }
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };

    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);

    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div className="mk-field">
      <span className="mk-field__label" id={labelId}>
        {label}
      </span>
      <div className="mk-select" ref={box}>
        <button aria-expanded={open} aria-haspopup="listbox" aria-labelledby={labelId} className="mk-select__button" disabled={disabled} onClick={() => setOpen((value) => !value)} type="button">
          <span className="mk-select__value">{valueLabel}</span>
          <GameIcon name="chevronDown" size={16} />
        </button>
        {open ? (
          <ul aria-labelledby={labelId} className="mk-select__list" role="listbox">
            {options.map((option) => (
              <li className="mk-select__item" key={option.id} role="presentation">
                {option.disabledReason ? (
                  <button aria-disabled="true" aria-selected="false" className="mk-opt mk-opt--off" disabled role="option" title={option.disabledReason} type="button">
                    <span>{option.label}</span>
                    <span className="mk-opt__reason">{option.disabledReason}</span>
                  </button>
                ) : (
                  <button
                    aria-selected={option.id === selectedId}
                    className="mk-opt"
                    onClick={() => {
                      onSelect(option.id);
                      setOpen(false);
                    }}
                    role="option"
                    type="button"
                  >
                    {option.label}
                  </button>
                )}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </div>
  );
}
