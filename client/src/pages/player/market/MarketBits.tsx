import { GameIcon } from '../../../components/GameIcon';
import { Glyph } from '../../../components/Glyph';
import type { ItemView } from './useMarketItems';
import type { ReactNode } from 'react';

/**
 * The icon of the energy rods: a yellow bar, as in the mock-up and in the header.
 *
 * @param size The size of the icon in pixels.
 */
export function RodIcon({ size = 13 }: { size?: number }) {
  return (
    <svg aria-hidden="true" className="mk-rod" focusable="false" height={size} viewBox="0 0 24 24" width={size}>
      <path d="M10 2h4v20h-4z" fill="currentColor" />
    </svg>
  );
}

/**
 * The icon of the trade currency: the diamond of the header, in the colour of the trade currency.
 *
 * @param size The size of the icon in pixels.
 */
export function TradeIcon({ size = 14 }: { size?: number }) {
  return <GameIcon className="mk-trade" name="shapeTrade" size={size} />;
}

/**
 * A line of the sheet of the item: its icon and its name, and for a part its condition chip.
 *
 * @param view How the item looks.
 * @param size The size of the icon in pixels.
 * @param condition The text of the condition chip, or `undefined` for a resource.
 */
export function ItemCell({ view, size = 24, condition }: { view: ItemView; size?: number; condition?: string }) {
  return (
    <>
      <GameIcon className={view.iconClass} name={view.iconName} size={size} />
      <span className="mk-item">
        <span className="mk-item__name">{view.name}</span>
        {condition ? <span className="mk-cond">{condition}</span> : null}
      </span>
    </>
  );
}

/**
 * The nickname of a player in a pill with the glyph of the race, and the race for a screen reader.
 *
 * @param nickname The nickname.
 * @param colorKey The colour key of the race, or `null` when the race is unknown.
 * @param raceName The name of the race for the screen reader.
 */
export function SellerPill({ nickname, colorKey, raceName }: { nickname: string; colorKey: string | null; raceName: string | null }) {
  return (
    <span className="mk-pill">
      <Glyph colorKey={colorKey} small />
      {nickname}
      {raceName ? <span className="visually-hidden">, {raceName}</span> : null}
    </span>
  );
}

/**
 * A box that tells the player there is nothing here, with a picture and a line of text.
 *
 * @param icon The icon.
 * @param title The line of text.
 * @param children The button or the hint under the line.
 */
export function EmptyBox({ icon, title, children }: { icon: Parameters<typeof GameIcon>[0]['name']; title: string; children?: ReactNode }) {
  return (
    <section className="mk-empty">
      <GameIcon className="mk-empty__icon" name={icon} size={40} />
      <h2 className="mk-empty__title">{title}</h2>
      {children}
    </section>
  );
}
