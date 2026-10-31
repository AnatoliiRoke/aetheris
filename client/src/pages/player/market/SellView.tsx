import { useCallback, useEffect, useMemo, useState } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import type { MarketLevel, MarketOverview, PriceLimit } from '../../../api/marketApi';
import { useApi } from '../../../api/useApi';
import { ApiError } from '../../../api/http';
import { useI18n } from '../../../i18n/useI18n';
import { routes } from '../../../router/context';
import { Link } from '../../../router/Router';
import { useStock } from '../useStock';
import { useShell, useShellSelector } from '../useShell';
import { formatInteger, parseWhole } from './marketFormat';
import { TradeIcon } from './MarketBits';
import { MarketSelect, type SelectOption } from './MarketSelect';
import type { PlanetChoice } from './marketPlanets';
import type { ItemView } from './useMarketItems';
import { useRead } from './useRead';

/**
 * One kind of goods the player may put up from a body: a resource, or a part of one condition.
 */
export interface SellItem {
  id: string;
  resourceTypeId: number | null;
  partTypeId: number | null;
  conditionPercent: number | null;
  available: number;
}

/**
 * The commission a level takes out of a sum, rounded down as the server rounds it.
 *
 * @param sum The sum.
 * @param percent The commission in per cent.
 */
function commissionOf(sum: number, percent: number): number {
  return Math.min(sum, Math.floor((sum * percent) / 100));
}

/**
 * The tab "Sell": the form of a lot, the counter of the lots and the note on the delivery. The price
 * limit comes from the server; the form shows it and does not guess it. When no body has a trade
 * centre and a portal, the form is closed and a banner leads to the construction.
 *
 * @param level The chosen level of the market.
 * @param overview The overview of the market.
 * @param planets The bodies of the player.
 * @param initialPlanetId The body to choose first.
 * @param itemView How an item looks.
 * @param busy Whether a command is being sent.
 * @param error The text of the refusal of the last try, or `null`.
 * @param onList What to do when the player lists the lot.
 */
export function SellView({
  level,
  overview,
  planets,
  initialPlanetId,
  itemView,
  busy,
  error,
  onList,
}: {
  level: MarketLevel | null;
  overview: MarketOverview;
  planets: PlanetChoice[];
  initialPlanetId: number | null;
  itemView: (resourceTypeId: number | null, partTypeId: number | null) => ItemView;
  busy: boolean;
  error: string | null;
  onList: (request: { planetId: number; item: SellItem; quantity: number; pricePerUnit: number }) => void;
}) {
  const { t, language } = useI18n();
  const { market } = useApi();
  const { actions } = useShell();
  const eligible = planets.filter((planet) => planet.hasTradeCentre && planet.hasPortal);
  const formOn = eligible.length > 0 && level !== null;
  const [chosenPlanet, setChosenPlanet] = useState<number | null>(initialPlanetId);
  const planetId = eligible.find((planet) => planet.planetId === chosenPlanet)?.planetId ?? eligible.find((planet) => planet.planetId === initialPlanetId)?.planetId ?? eligible[0]?.planetId ?? null;
  const [chosenItem, setChosenItem] = useState<string | null>(null);

  useEffect(() => {
    if (planetId !== null) {
      void actions.loadPlanet(planetId);
    }
  }, [actions, planetId]);

  const planet = useShellSelector((state) => (planetId === null ? undefined : state.planets.get(planetId)?.planet));
  const stock = useStock(planetId);

  const items = useMemo<SellItem[]>(() => {
    const resources = (planet?.resources ?? [])
      .filter((resource) => Math.floor(resource.amount) > 0)
      .map((resource) => ({ id: `r${resource.resourceTypeId}`, resourceTypeId: resource.resourceTypeId, partTypeId: null, conditionPercent: null, available: Math.floor(resource.amount) }));
    const grouped = new Map<string, SellItem>();

    for (const stack of stock.stock?.stacks ?? []) {
      const id = `p${stack.partTypeId}-${stack.conditionPercent}`;
      const known = grouped.get(id);

      grouped.set(id, known ? { ...known, available: known.available + stack.quantity } : { id, resourceTypeId: null, partTypeId: stack.partTypeId, conditionPercent: stack.conditionPercent, available: stack.quantity });
    }

    return [...resources, ...grouped.values()];
  }, [planet, stock.stock]);

  const item = items.find((candidate) => candidate.id === chosenItem) ?? items[0] ?? null;
  const loadLimit = useCallback(
    (): Promise<PriceLimit | null> =>
      item === null
        ? Promise.resolve(null)
        : market.priceLimit(item.resourceTypeId, item.partTypeId).catch((cause: unknown) => {
            if (cause instanceof ApiError && cause.code === 'market_no_reference_price') {
              return null;
            }

            throw cause;
          }),
    [market, item],
  );
  const limit = useRead(loadLimit, item !== null && formOn);

  // A new item starts with its whole stock and the middle of its allowed range, as the mock-up does;
  // what the player typed belongs to the item it was typed for.
  const itemKey = item?.id ?? null;
  const available = item?.available ?? 0;
  const [edits, setEdits] = useState<{ key: string | null; quantity?: string; price?: string }>({ key: null });
  const own = edits.key === itemKey ? edits : null;
  const middle = limit.data ? String(Math.round((limit.data.min + limit.data.max) / 2)) : '0';
  const quantity = own?.quantity ?? String(available);
  const price = own?.price ?? middle;
  const edit = (change: { quantity?: string; price?: string }) => setEdits((previous) => ({ ...(previous.key === itemKey ? previous : { key: itemKey }), ...change }));

  const qty = parseWhole(quantity);
  const unitPrice = parseWhole(price);
  const qtyBad = qty === null || qty < 1 || qty > available;
  const range = limit.data;
  const priceBad = unitPrice === null || (unitPrice !== 0 && (range === null || range === undefined || unitPrice < range.min || unitPrice > range.max));
  const percent = level?.commissionPercent ?? 0;
  const total = (qty ?? 0) * (unitPrice ?? 0);
  const fee = commissionOf(total, percent);
  const used = overview.lotLimit.used;
  const max = overview.lotLimit.limit;
  const limitHit = used >= max;
  const canList = formOn && item !== null && !qtyBad && !priceBad && !limitHit && !busy && planetId !== null;
  const reason = !formOn ? t('market.npTitle') : limitHit ? t('market.rLimit') : qtyBad || priceBad ? t('market.rBad') : '';
  const view = item ? itemView(item.resourceTypeId, item.partTypeId) : null;
  const percentText = `${percent}%`;

  const planetOptions: SelectOption[] = planets.map((candidate) => ({
    id: String(candidate.planetId),
    label: candidate.name,
    disabledReason: candidate.hasTradeCentre && candidate.hasPortal ? undefined : t('market.noTcPl'),
  }));
  const itemOptions: SelectOption[] = items.map((candidate) => ({
    id: candidate.id,
    label: `${itemView(candidate.resourceTypeId, candidate.partTypeId).name} · ${formatInteger(candidate.available, language)}`,
  }));
  const chosenPlanetLabel = planets.find((candidate) => candidate.planetId === planetId)?.name ?? (planets[0]?.name ?? '');

  return (
    <>
      {!formOn ? (
        <section className="mk-warnpanel" role="alert">
          <GameIcon className="mk-warnpanel__icon" name="warn" size={24} />
          <div className="mk-warnpanel__text">
            <span className="mk-warnpanel__title">{t('market.npTitle')}</span>
            <span className="mk-warnpanel__hint">{t('market.npText')}</span>
          </div>
          <Link className="mk-btn mk-btn--secondary" to={routes.planetBuild}>
            {t('market.toBuild')}
          </Link>
        </section>
      ) : null}
      <div className="mk-sell">
        <section aria-label={t('market.sellTitle')} className="mk-form">
          <h2 className="mk-h2">{t('market.sellTitle')}</h2>
          <MarketSelect
            disabled={!formOn}
            label={t('market.fPlanet')}
            onSelect={(id) => {
              setChosenPlanet(Number(id));
              setChosenItem(null);
            }}
            options={planetOptions}
            selectedId={planetId === null ? null : String(planetId)}
            valueLabel={formOn ? chosenPlanetLabel : `${chosenPlanetLabel} · ${t('market.noTcPl')}`}
          />
          <MarketSelect
            disabled={!formOn || items.length === 0}
            label={t('market.fItem')}
            onSelect={setChosenItem}
            options={itemOptions}
            selectedId={item?.id ?? null}
            valueLabel={item && view ? `${view.name} · ${formatInteger(item.available, language)}` : formOn ? t('market.sellNoStock') : ''}
          />
          <div className="mk-field">
            <label className="mk-field__label" htmlFor="mk-qty">
              {t('market.fQty')} <span className="mk-field__hint">· {t('market.avail')} {formatInteger(available, language)}</span>
            </label>
            <div className="mk-field__row">
              <input autoComplete="off" className="mk-input mk-input--fill" disabled={!formOn} id="mk-qty" inputMode="numeric" onChange={(event) => edit({ quantity: event.target.value })} type="text" value={quantity} />
              <button className="mk-btn mk-btn--secondary" disabled={!formOn || item === null} onClick={() => edit({ quantity: String(available) })} type="button">
                {t('market.max')}
              </button>
            </div>
            {formOn && item !== null && qtyBad ? (
              <span className="mk-error" role="alert">
                <GameIcon name="warn" size={14} />
                {t('market.eQty', { max: formatInteger(available, language) })}
              </span>
            ) : null}
          </div>
          <div className="mk-field">
            <label className="mk-field__label" htmlFor="mk-price">
              {t('market.fPrice')}
            </label>
            <div className="mk-pricebox">
              <span className="mk-pricebox__icon">
                <TradeIcon size={16} />
              </span>
              <input autoComplete="off" className="mk-input mk-input--price" disabled={!formOn} id="mk-price" inputMode="numeric" onChange={(event) => edit({ price: event.target.value })} type="text" value={price} />
            </div>
            <span className="mk-field__hint">
              {range ? t('market.rangeLine', { min: formatInteger(range.min, language), max: formatInteger(range.max, language) }) : formOn && item !== null && !limit.loading ? t('market.rangeNone') : ''}
            </span>
            {formOn ? (
              <button className="mk-link" onClick={() => edit({ price: '0' })} type="button">
                {t('market.freeHint')}
              </button>
            ) : (
              <span className="mk-link mk-link--off">{t('market.freeHint')}</span>
            )}
            {formOn && item !== null && priceBad ? (
              <span className="mk-error" role="alert">
                <GameIcon name="warn" size={14} />
                {t('market.ePrice')}
              </span>
            ) : null}
          </div>
          <dl className="mk-sum">
            <div className="mk-sum__row">
              <dt className="is-muted">{t('market.sTotal')}</dt>
              <dd>{formatInteger(total, language)} {t('market.tradeShort')}</dd>
            </div>
            <div className="mk-sum__row">
              <dt className="is-muted">
                {t('market.sFee')} {percentText}
              </dt>
              <dd className="is-muted">−{formatInteger(fee, language)} {t('market.tradeShort')}</dd>
            </div>
            <div className="mk-sum__row is-total">
              <dt>{t('market.sGet')}</dt>
              <dd className="is-accent is-big">{formatInteger(total - fee, language)} {t('market.tradeShort')}</dd>
            </div>
          </dl>
          <div className="mk-form__foot">
            <div className="mk-counter">
              <span className="mk-counter__text">
                {t('market.lotsUsed')}: <b>{used} / {max}</b>
              </span>
              <span aria-label={t('market.lotsUsed')} aria-valuemax={max} aria-valuemin={0} aria-valuenow={used} className="mk-meter" role="progressbar">
                <span className="mk-meter__bar" style={{ width: `${max === 0 ? 0 : Math.min(100, Math.round((used / max) * 100))}%` }} />
              </span>
            </div>
            <button
              className="mk-btn mk-btn--primary"
              disabled={!canList}
              onClick={() => {
                if (canList && item !== null && planetId !== null && qty !== null && unitPrice !== null) {
                  onList({ planetId, item, quantity: qty, pricePerUnit: unitPrice });
                }
              }}
              title={canList ? undefined : reason}
              type="button"
            >
              {t('market.list')}
            </button>
          </div>
          {!canList && reason ? <span className="mk-error mk-error--plain">{reason}</span> : null}
          {error ? (
            <span className="mk-error" role="alert">
              <GameIcon name="warn" size={14} />
              {error}
            </span>
          ) : null}
        </section>
        <aside aria-label={t('market.infoTitle')} className="mk-info">
          <GameIcon className="mk-info__icon" name="info" size={24} />
          <h3 className="mk-info__title">{t('market.infoTitle')}</h3>
          <p className="mk-info__text">{t('market.infoText')}</p>
        </aside>
      </div>
    </>
  );
}
