import { useI18n } from '../../i18n/useI18n';
import { formatNumber, formatSigned } from './planetText';
import { useEconomy } from './useEconomy';
import { useWorldReference } from './useWorldReference';

/**
 * The finances of the civilization on the screen of a body: the credits with their balance, their
 * spending and their income per hour.
 *
 * The server sends the balance and the income of every currency of the civilization; the credits
 * are the currency the reference table of the world marks as the income of the population, so the
 * client knows no currency by its code. The server sends no spending while there are no fleets to
 * spend the credits, so the client shows a zero, never a made-up number.
 */
export function FinancePanel() {
  const { t, language } = useI18n();
  const { balances } = useEconomy();
  const reference = useWorldReference();
  const creditsId = reference?.resources.find((resource) => resource.earnsPopulationIncome)?.id ?? null;
  const credits = balances.find((balance) => balance.resourceTypeId === creditsId) ?? null;

  return (
    <section className="panel" aria-labelledby="finance-title">
      <div className="panel__head">
        <h2 className="panel__title" id="finance-title">
          {t('finance.title')}
        </h2>
      </div>
      <dl className="facts">
        <dt>{t('finance.balance')}</dt>
        <dd className="num">{credits ? formatNumber(credits.amount, language) : '—'}</dd>
        <dt>{t('finance.consumption')}</dt>
        <dd className="num">{credits ? formatNumber(0, language) : '—'}</dd>
        <dt>{t('finance.income')}</dt>
        <dd className="num">{credits ? formatSigned(credits.incomePerHour, language) : '—'}</dd>
      </dl>
      <p className="meta">{t('finance.note')}</p>
    </section>
  );
}
