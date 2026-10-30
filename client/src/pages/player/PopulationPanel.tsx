import { Alert } from '../../components/Alert';
import type { Language } from '../../i18n/context';
import { useI18n } from '../../i18n/useI18n';
import { formatDuration, formatNumber } from './planetText';
import { useEconomy } from './useEconomy';

const secondsPerMinute = 60;
const secondsPerHour = 3600;
const secondsPerDay = 86400;

/**
 * The population of a body (task 00015): how many inhabitants it has now, the largest number
 * it may have, how fast it grows and how long it needs to reach the largest one.
 *
 * The panel is the same for every body: the server sends the facts of the population, so the
 * code of the client knows no rule of the game.
 */
export function PopulationPanel({ planetId }: { planetId: number }) {
  const { t, language } = useI18n();
  const { population, loaded, failed } = useEconomy(planetId);

  return (
    <section className="panel" aria-labelledby="population-title">
      <div className="panel__head">
        <h2 className="panel__title" id="population-title">
          {t('population.title')}
        </h2>
      </div>
      {failed ? <Alert kind="error">{t('population.loadFailed')}</Alert> : null}
      {population?.overLimit ? <Alert kind="info">{t('population.overLimit')}</Alert> : null}
      {!loaded && !failed ? (
        <p className="lead" role="status">
          {t('population.loading')}
        </p>
      ) : (
        <dl className="facts">
          <dt>{t('population.current')}</dt>
          <dd className="num">{showValue(population?.amount, language)}</dd>
          <dt>{t('population.maximum')}</dt>
          <dd className="num">{showValue(population?.maximum, language)}</dd>
          <dt>{t('population.growth')}</dt>
          <dd className="num">{growthText(population?.growthPerHour, language)}</dd>
          <dt>{t('population.timeToMaximum')}</dt>
          <dd className="num">{timeText(population?.timeToMaximum, language)}</dd>
        </dl>
      )}
    </section>
  );
}

/**
 * A fact of the server as a number; a value without data is a placeholder, never a made-up
 * number (task 00010). A real zero is a number too: a body without inhabitants has none.
 */
function showValue(value: number | null | undefined, language: Language): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '—';
  }

  return formatNumber(value, language);
}

/**
 * The speed of the population: it grows or it dies down, and the sign says which one.
 */
function growthText(growth: number | null | undefined, language: Language): string {
  if (growth === null || growth === undefined || !Number.isFinite(growth)) {
    return '—';
  }

  const value = formatNumber(growth, language);

  return growth > 0 ? `+${value}` : value;
}

/**
 * The time to the largest population as the server sends it: `[-][d.]hh:mm:ss[.fffffff]`. No time
 * means the population never reaches the largest one; a time of zero means it is there already.
 */
function timeText(value: string | null | undefined, language: Language): string {
  const seconds = timeToSeconds(value);

  if (seconds === null) {
    return '—';
  }

  return formatDuration(language, seconds);
}

/**
 * The seconds of the time span of the server, or `null` when the text is not a time span. The
 * server counts the time in ticks, so the seconds may carry a fraction of up to seven digits.
 */
function timeToSeconds(value: string | null | undefined): number | null {
  if (!value) {
    return null;
  }

  const parts = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(value);

  if (!parts) {
    return null;
  }

  const [, sign, days, hours, minutes, seconds, fraction] = parts;
  const total =
    Number(days ?? 0) * secondsPerDay +
    Number(hours) * secondsPerHour +
    Number(minutes) * secondsPerMinute +
    Number(seconds) +
    Number(`0.${fraction ?? '0'}`);

  return sign === '-' ? -total : total;
}
