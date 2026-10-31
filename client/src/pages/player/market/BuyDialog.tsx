import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { createPortal } from 'react-dom';
import { GameIcon } from '../../../components/GameIcon';
import type { MarketLot, MarketQuote } from '../../../api/marketApi';
import { useApi } from '../../../api/useApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatDuration, formatInteger } from './marketFormat';
import type { PlanetChoice } from './marketPlanets';
import type { ItemView } from './useMarketItems';
import { useRead } from './useRead';

/**
 * The dialog that confirms a purchase: the body the goods arrive at and the sum the server counted
 * for it (task 00026, section 3.1). On a phone it is a sheet from the bottom of the screen.
 *
 * @param lot The lot to buy.
 * @param view How the item of the lot looks.
 * @param planets The bodies of the player; a body without a portal cannot be chosen.
 * @param initialPlanetId The body to choose first.
 * @param tradeBalance The trade currency the player has now.
 * @param busy Whether the purchase is being sent.
 * @param error The text of the refusal of the last try, or `null`.
 * @param onConfirm What to do when the player confirms the purchase on a body; it gets the time of the delivery the server counted.
 * @param onClose What to do when the player closes the dialog.
 */
export function BuyDialog({
  lot,
  view,
  planets,
  initialPlanetId,
  tradeBalance,
  busy,
  error,
  onConfirm,
  onClose,
}: {
  lot: MarketLot;
  view: ItemView;
  planets: PlanetChoice[];
  initialPlanetId: number | null;
  tradeBalance: number;
  busy: boolean;
  error: string | null;
  onConfirm: (planetId: number, deliverySeconds: number) => void;
  onClose: () => void;
}) {
  const { t, language } = useI18n();
  const { market } = useApi();
  const boxRef = useRef<HTMLDivElement>(null);
  const firstChoice = planets.find((planet) => planet.planetId === initialPlanetId && planet.hasPortal) ?? planets.find((planet) => planet.hasPortal) ?? null;
  const [planetId, setPlanetId] = useState<number | null>(firstChoice?.planetId ?? null);

  const load = useCallback((): Promise<MarketQuote> => market.quote(lot.id, planetId ?? 0), [market, lot.id, planetId]);
  const quote = useRead(load, planetId !== null);

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

    const focusable = [...(boxRef.current?.querySelectorAll<HTMLElement>('button:not([disabled])') ?? [])];

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

  const answer = quote.data;
  const free = lot.pricePerUnit === 0;
  const unit = t('market.tradeShort');
  const rows: { key: string; label: string; value: string; muted?: boolean; total?: boolean }[] = answer
    ? [
        { key: 'price', label: t('market.sPrice'), value: free ? t('market.free') : `${formatInteger(answer.sum, language)} ${unit}` },
        { key: 'recv', label: t('market.sRecv'), value: `${formatInteger(answer.sum - answer.commission, language)} ${unit}`, muted: true },
        { key: 'rods', label: t('market.sRods'), value: formatInteger(answer.energyRodAmount, language) },
        { key: 'time', label: t('market.sTime'), value: formatDuration(answer.deliverySeconds, t) },
        { key: 'balance', label: t('market.sBal'), value: `${formatInteger(tradeBalance - answer.sum, language)} ${unit}`, total: true },
      ]
    : [];

  // The dialog lives on the body: the content of a page sits under the frame of the screen, which the sheet must cover.
  return createPortal(
    <div className="mk-overlay" onClick={onClose} role="presentation">
      <div aria-labelledby="mk-dlg-title" aria-modal="true" className="mk-dialog" onClick={(event) => event.stopPropagation()} onKeyDown={onKeyDown} ref={boxRef} role="dialog">
        <div className="mk-dialog__head">
          <h2 className="mk-dialog__title" id="mk-dlg-title">
            {t('market.dlgBuy')}: {view.name} × {formatInteger(lot.quantity, language)}
          </h2>
          <button aria-label={t('market.close')} className="mk-ibtn" onClick={onClose} type="button">
            <GameIcon name="close" size={20} />
          </button>
        </div>
        <div className="mk-dialog__block">
          <span className="mk-dialog__label" id="mk-dlg-pl">
            {t('market.deliverTo')}
          </span>
          <div aria-labelledby="mk-dlg-pl" className="mk-planets" role="radiogroup">
            {planets.length === 0 ? <p className="mk-dialog__none">{t('market.noDestination')}</p> : null}
            {planets.map((planet) =>
              planet.hasPortal ? (
                <button
                  aria-checked={planet.planetId === planetId}
                  className="mk-planet"
                  key={planet.planetId}
                  onClick={() => setPlanetId(planet.planetId)}
                  role="radio"
                  type="button"
                >
                  <span className="mk-planet__who">
                    {planet.art ? <img alt="" className="mk-planet__art" height={28} src={planet.art} width={28} /> : null}
                    <span className="mk-planet__name">{planet.name}</span>
                  </span>
                  <GameIcon className={`mk-planet__tick${planet.planetId === planetId ? '' : ' is-hidden'}`} name="check" size={16} />
                </button>
              ) : (
                <button aria-checked="false" aria-disabled="true" className="mk-planet mk-planet--off" disabled key={planet.planetId} role="radio" title={t('market.noPortalPl')} type="button">
                  <span className="mk-planet__who">
                    {planet.art ? <img alt="" className="mk-planet__art" height={28} src={planet.art} width={28} /> : null}
                    <span className="mk-planet__text">
                      <span className="mk-planet__name">{planet.name}</span>
                      <span className="mk-planet__reason">{t('market.noPortalPl')}</span>
                    </span>
                  </span>
                </button>
              ),
            )}
          </div>
        </div>
        <dl aria-busy={quote.loading} className="mk-sum">
          {rows.length === 0 && quote.failed ? <p className="mk-dialog__none">{t('market.errText')}</p> : null}
          {rows.map((row) => (
            <div className={`mk-sum__row${row.total ? ' is-total' : ''}`} key={row.key}>
              <dt className={row.muted ? 'is-muted' : undefined}>{row.label}</dt>
              <dd className={row.muted ? 'is-muted' : row.total ? 'is-accent' : undefined}>{row.value}</dd>
            </div>
          ))}
        </dl>
        {error ? (
          <p className="mk-error" role="alert">
            <GameIcon name="warn" size={14} />
            {error}
          </p>
        ) : null}
        <div className="mk-dialog__buttons">
          <button className="mk-btn mk-btn--secondary" onClick={onClose} type="button">
            {t('market.cancel')}
          </button>
          <button className="mk-btn mk-btn--primary" disabled={busy || answer === null || planetId === null} onClick={() => planetId !== null && answer !== null && onConfirm(planetId, answer.deliverySeconds)} type="button">
            {t('market.confirm')}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  );
}
