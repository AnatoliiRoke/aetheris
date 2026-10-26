import type { BuildingEffect, Planet, ResourceAmount, UnmetRequirement } from '../../api/planetApi';
import { worldNamesEn, worldNamesUk } from '../../admin/i18n/worldNames';
import { dictionaries, format, type Language, type MessageParams } from '../../i18n/context';
import type { MessageKey } from '../../i18n/uk';

const worldNames: Record<Language, Record<string, string>> = { uk: worldNamesUk, en: worldNamesEn };

/**
 * Finds the text of a localisation key that comes from the server (a building, a resource, a
 * reason). A key without a text shows the fallback, never an empty place.
 */
export function lookup(language: Language, key: string | null | undefined, fallback: string, params?: MessageParams): string {
  if (!key) {
    return fallback;
  }

  const messages: Record<string, string> = dictionaries[language];
  const text = messages[key] ?? worldNames[language][key];

  return text === undefined ? fallback : format(text, params);
}

/**
 * Whether the dictionary has a key; the components use it before passing a server key to `t`.
 */
export function hasKey(language: Language, key: string): key is MessageKey {
  return Object.hasOwn(dictionaries[language], key);
}

/**
 * Names the resources and groups of a body by their localisation keys.
 */
export function namer(planet: Planet, language: Language) {
  const resources = new Map(planet.resourceTypes.map((resource) => [resource.id, resource]));
  const groups = new Map(planet.resourceGroups.map((group) => [group.id, group]));
  const buildings = new Map(planet.buildings.map((building) => [building.buildingTypeId, building]));

  return {
    resource: (id: number | null) => {
      const resource = id === null ? undefined : resources.get(id);
      return lookup(language, resource?.nameKey, resource?.code ?? String(id));
    },
    group: (id: number | null) => {
      const group = id === null ? undefined : groups.get(id);
      return lookup(language, group?.nameKey, group?.code ?? String(id));
    },
    building: (id: number) => {
      const building = buildings.get(id);
      return lookup(language, building?.nameKey, building?.code ?? String(id));
    },
    amounts: (amounts: ResourceAmount[]) =>
      amounts.map((amount) => `${lookup(language, resources.get(amount.resourceTypeId)?.nameKey, String(amount.resourceTypeId))} ${formatNumber(amount.amount, language)}`).join(', '),
  };
}

/**
 * The text of an effect of a building level.
 */
export function effectText(language: Language, effect: BuildingEffect, names: ReturnType<typeof namer>): string {
  const name = effect.resourceTypeId !== null ? names.resource(effect.resourceTypeId) : names.group(effect.resourceGroupId);

  return lookup(language, `planet.effect.${effect.effectType}`, effect.effectType, { name, value: formatNumber(effect.value, language) });
}

/**
 * The text of an unmet requirement.
 */
export function requirementText(language: Language, requirement: UnmetRequirement): string {
  const name = lookup(language, requirement.targetNameKey, String(requirement.targetId));

  return lookup(language, `reason.${requirement.reasonCode}`, requirement.reasonCode, { name, level: requirement.targetLevel });
}

/**
 * The text of a reason a building cannot be queued.
 */
export function reasonText(language: Language, code: string): string {
  return lookup(language, `reason.${code}`, code);
}

/**
 * A whole number in the format of the language.
 */
export function formatNumber(value: number, language: Language): string {
  return new Intl.NumberFormat(language === 'uk' ? 'uk-UA' : 'en-GB', { maximumFractionDigits: 1 }).format(value);
}

const millisecondsPerSecond = 1000;
const secondsPerMinute = 60;
const secondsPerHour = 3600;
const secondsPerDay = 86400;

/**
 * A span of seconds as `h:mm:ss`, with days in front when it is longer than a day.
 */
export function formatDuration(language: Language, totalSeconds: number): string {
  const seconds = Math.max(0, Math.ceil(totalSeconds));
  const days = Math.floor(seconds / secondsPerDay);
  const hours = Math.floor((seconds % secondsPerDay) / secondsPerHour);
  const minutes = Math.floor((seconds % secondsPerHour) / secondsPerMinute);
  const rest = seconds % secondsPerMinute;
  const pad = (value: number) => String(value).padStart(2, '0');
  const time = `${hours}:${pad(minutes)}:${pad(rest)}`;

  return days > 0 ? format(dictionaries[language]['planet.durationDays'], { days, time }) : time;
}

/**
 * The real seconds left until a moment of game time, from the answer of the server and the
 * moment it was received.
 */
export function secondsLeft(planet: Planet, finishesAt: string, receivedAt: number, now: number): number {
  const gameSeconds = (Date.parse(finishesAt) - Date.parse(planet.gameNow)) / millisecondsPerSecond;
  const multiplier = planet.timeMultiplier > 0 ? planet.timeMultiplier : 1;

  return gameSeconds / multiplier - (now - receivedAt) / millisecondsPerSecond;
}
