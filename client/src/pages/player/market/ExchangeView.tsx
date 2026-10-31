import { useState } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import type { MarketOverview, MarketRate, RateHistory } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatDecimal, formatInteger, parseWhole } from './marketFormat';
import { RateChart } from './RateChart';

const percentOfWhole = 100;
const rateDigits = 2;

/**
 * The tab "Exchange": the direction, the amount, the result, the rate with its fee and its change in
 * a day, and the chart of the rate. The result is counted with the same rule as the server
 * (the amount at the rate of the moment, less the fee, rounded down); the server counts the real one.
 *
 * @param overview The overview of the market, for the two currencies.
 * @param rate The rate of the exchange now, or `null` while it is read.
 * @param history The history of the rate, or `null` while it is read.
 * @param credits The credits the player has.
 * @param trade The trade currency the player has.
 * @param busy Whether a command is being sent.
 * @param error The text of the refusal of the last try, or `null`.
 * @param onExchange What to do when the player exchanges an amount of a currency.
 */
export function ExchangeView({
  overview,
  rate,
  history,
  credits,
  trade,
  busy,
  error,
  onExchange,
}: {
  overview: MarketOverview;
  rate: MarketRate | null;
  history: RateHistory | null;
  credits: number;
  trade: number;
  busy: boolean;
  error: string | null;
  onExchange: (giveResourceTypeId: number, amount: number) => void;
}) {
  const { t, language } = useI18n();
  const [direction, setDirection] = useState<'toTrade' | 'toCredits'>('toTrade');
  const [amountText, setAmountText] = useState('1000');
  const toTrade = direction === 'toTrade';
  const amount = parseWhole(amountText);
  const balance = toTrade ? credits : trade;
  const keep = rate ? (percentOfWhole - rate.commissionPercent) / percentOfWhole : 1;
  const out = rate && amount ? Math.floor(toTrade ? (amount / rate.rate) * keep : amount * rate.rate * keep) : 0;
  const problem = amount === null || amount <= 0 ? t('market.exBad') : amount > balance ? t('market.exNoFunds') : '';
  const change = history?.dayChangePercent ?? null;
  // The server counts credits for a unit of the trade currency; the chart and the line show the trade currency for a credit.
  const shownChange = change === null ? null : toTrade ? (1 / (1 + change / percentOfWhole) - 1) * percentOfWhole : change;
  const rounded = shownChange === null ? null : Math.round(shownChange * 10) / 10;
  const giveId = toTrade ? overview.creditsResourceTypeId : overview.tradeResourceTypeId;
  const canExchange = rate !== null && problem === '' && !busy;
  const fromName = toTrade ? t('market.credits') : t('market.trade');
  const toName = toTrade ? t('market.trade') : t('market.credits');
  const rateLine = rate
    ? toTrade
      ? t('market.rateLine1', { rate: formatDecimal(1 / rate.rate, language, rateDigits) })
      : t('market.rateLine2', { rate: formatDecimal(rate.rate, language, rateDigits) })
    : '';

  return (
    <div className="mk-exchange">
      <section aria-label={t('market.tEx')} className="mk-form">
        <div aria-label={t('market.exDirAria')} className="mk-seg" role="group">
          {(
            [
              ['toTrade', 'market.exCT'],
              ['toCredits', 'market.exTC'],
            ] as const
          ).map(([id, label]) => (
            <button aria-pressed={direction === id} className="mk-seg__item" key={id} onClick={() => setDirection(id)} type="button">
              {t(label)}
            </button>
          ))}
        </div>
        <div className="mk-field">
          <label className="mk-field__label" htmlFor="mk-exin">
            {t('market.exAmount')} · {fromName}
          </label>
          <input autoComplete="off" className="mk-input mk-input--fill" id="mk-exin" inputMode="numeric" onChange={(event) => setAmountText(event.target.value)} type="text" value={amountText} />
          {problem ? (
            <span className="mk-error" role="alert">
              <GameIcon name="warn" size={14} />
              {problem}
            </span>
          ) : null}
        </div>
        <div className="mk-field">
          <label className="mk-field__label" htmlFor="mk-exout">
            {t('market.exGet')} · {toName}
          </label>
          <input className="mk-input mk-input--fill mk-input--read" id="mk-exout" readOnly type="text" value={formatInteger(out, language)} />
        </div>
        <div className="mk-exchange__lines">
          <span>{rateLine}</span>
          <span className="mk-exchange__fee">{rate ? t('market.feeLine', { percent: rate.commissionPercent }) : ''}</span>
          <span className="mk-exchange__change">
            {rounded === null ? null : <GameIcon name={rounded < 0 ? 'arrowDown' : 'arrowUp'} size={14} />}
            {rounded === null ? t('market.dayChangeNone') : t('market.dayChange', { percent: `${rounded > 0 ? '+' : ''}${formatDecimal(rounded, language, 1)}` })}
          </span>
        </div>
        <button className="mk-btn mk-btn--primary mk-btn--full" disabled={!canExchange} onClick={() => amount !== null && onExchange(giveId, amount)} title={canExchange ? undefined : problem} type="button">
          {t('market.exBtn')}
        </button>
        {error ? (
          <span className="mk-error" role="alert">
            <GameIcon name="warn" size={14} />
            {error}
          </span>
        ) : null}
      </section>
      <section aria-label={t('market.chartTitle')} className="mk-form">
        <h2 className="mk-h2">{t('market.chartTitle')}</h2>
        {history === null ? <p className="mk-chart__note">{t('market.loading')}</p> : <RateChart points={history.points} />}
        <p className="mk-chart__note">{t('market.chartNote')}</p>
      </section>
    </div>
  );
}
