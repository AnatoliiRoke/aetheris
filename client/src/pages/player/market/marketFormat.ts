import type { Language } from '../../../i18n/context';
import type { MessageKey } from '../../../i18n/uk';
import type { MessageParams } from '../../../i18n/context';

/**
 * Turns a key and its parameters into a text; the translate function of the i18n hook.
 */
export type Translate = (key: MessageKey, params?: MessageParams) => string;

const secondsInMinute = 60;
const minutesInHour = 60;
const hoursInDay = 24;
const millisecondsInSecond = 1000;

/**
 * Writes a whole number with the thousands apart by the separator of the language, as the
 * mock-up does ("12 400").
 *
 * @param value The number.
 * @param language The language of the player.
 */
export function formatInteger(value: number, language: Language): string {
  return new Intl.NumberFormat(language === 'uk' ? 'uk-UA' : 'en-GB', { maximumFractionDigits: 0 }).format(value);
}

/**
 * Writes a number with a fixed number of digits after the point, for a rate.
 *
 * @param value The number.
 * @param language The language of the player.
 * @param digits How many digits to keep after the point.
 */
export function formatDecimal(value: number, language: Language, digits: number): string {
  return new Intl.NumberFormat(language === 'uk' ? 'uk-UA' : 'en-GB', { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
}

/**
 * The time of a delivery the way the mock-up writes it: minutes under an hour, then hours and minutes.
 *
 * @param seconds The seconds.
 * @param t The translate function.
 */
export function formatDuration(seconds: number, t: Translate): string {
  const totalMinutes = Math.max(1, Math.ceil(seconds / secondsInMinute));

  if (totalMinutes < minutesInHour) {
    return t('market.timeMin', { minutes: totalMinutes });
  }

  return t('market.timeHour', {
    hours: Math.floor(totalMinutes / minutesInHour),
    minutes: String(totalMinutes % minutesInHour).padStart(2, '0'),
  });
}

/**
 * The time left on a timer as hours, minutes and seconds ("0:11:18").
 *
 * @param seconds The seconds left.
 */
export function formatClock(seconds: number): string {
  const whole = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(whole / (secondsInMinute * minutesInHour));
  const minutes = Math.floor((whole % (secondsInMinute * minutesInHour)) / secondsInMinute);
  const rest = whole % secondsInMinute;

  return `${hours}:${String(minutes).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}

/**
 * How long ago a moment was, in the words of the mock-up: now, minutes, hours, yesterday, days.
 *
 * @param isoMoment The moment of game time.
 * @param nowMs The game time now in milliseconds.
 * @param t The translate function.
 */
export function formatAgo(isoMoment: string, nowMs: number, t: Translate): string {
  const minutes = Math.max(0, Math.floor((nowMs - Date.parse(isoMoment)) / (millisecondsInSecond * secondsInMinute)));

  if (minutes < 1) {
    return t('market.justNow');
  }

  if (minutes < minutesInHour) {
    return t('market.minAgo', { minutes });
  }

  const hours = Math.floor(minutes / minutesInHour);

  if (hours < hoursInDay) {
    return t('market.hourAgo', { hours });
  }

  const days = Math.floor(hours / hoursInDay);

  return days === 1 ? t('market.yesterday') : t('market.daysAgo', { days });
}

/**
 * The date and the time of a deal the way the mock-up writes them ("26.10 14:32").
 *
 * @param isoMoment The moment of game time.
 */
export function formatStamp(isoMoment: string): string {
  const moment = new Date(isoMoment);
  const two = (value: number) => String(value).padStart(2, '0');

  return `${two(moment.getUTCDate())}.${two(moment.getUTCMonth() + 1)} ${two(moment.getUTCHours())}:${two(moment.getUTCMinutes())}`;
}

/**
 * Reads the digits of a field as a whole number, or `null` when there are none.
 *
 * @param text What the player typed.
 */
export function parseWhole(text: string): number | null {
  const digits = text.replace(/\D/g, '');

  return digits === '' ? null : Number.parseInt(digits, 10);
}
