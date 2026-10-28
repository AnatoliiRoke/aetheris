import type { BuildingEffect, Planet, ResourceAmount, UnmetRequirement } from '../../api/planetApi';
import { dictionaries, format, type Language, type MessageParams } from '../../i18n/context';
import type { MessageKey } from '../../i18n/uk';
import type { EntityText } from '../../texts/context';

/**
 * Finds the text of a localisation key that comes from the server: a reason of the interface
 * from the dictionary of the client, or a building or a resource from the texts of the game
 * data in the database (task 00022). A key without a text shows the fallback, never an empty
 * place.
 */
export function lookup(
  language: Language,
  key: string | null | undefined,
  fallback: string,
  params?: MessageParams,
  entities?: EntityText,
): string {
  if (!key) {
    return fallback;
  }

  const messages: Record<string, string> = dictionaries[language];
  const text = messages[key] ?? entities?.(key);

  return text === undefined ? fallback : format(text, params);
}

/**
 * Whether the dictionary has a key; the components use it before passing a server key to `t`.
 */
export function hasKey(language: Language, key: string): key is MessageKey {
  return Object.hasOwn(dictionaries[language], key);
}

/**
 * Names the resources, groups and buildings of a body by their localisation keys and the texts
 * of the game data. A row without a text shows its key, never its code.
 */
export function namer(planet: Planet, language: Language, entities: EntityText) {
  const resources = new Map(planet.resourceTypes.map((resource) => [resource.id, resource]));
  const groups = new Map(planet.resourceGroups.map((group) => [group.id, group]));
  const buildings = new Map(planet.buildings.map((building) => [building.buildingTypeId, building]));
  const name = (key: string | undefined, id: number | null) => lookup(language, key, key ?? String(id), undefined, entities);

  return {
    entities,
    resource: (id: number | null) => name(id === null ? undefined : resources.get(id)?.nameKey, id),
    group: (id: number | null) => name(id === null ? undefined : groups.get(id)?.nameKey, id),
    building: (id: number) => name(buildings.get(id)?.nameKey, id),
    amounts: (amounts: ResourceAmount[]) =>
      amounts.map((amount) => `${name(resources.get(amount.resourceTypeId)?.nameKey, amount.resourceTypeId)} ${formatNumber(amount.amount, language)}`).join(', '),
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
export function requirementText(language: Language, requirement: UnmetRequirement, entities: EntityText): string {
  const name = lookup(language, requirement.targetNameKey, requirement.targetNameKey ?? String(requirement.targetId), undefined, entities);

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
