import type { RaceId } from '../../api/contentApi';
import type { MessageKey } from '../../i18n/uk';

/**
 * The order of the races on the page.
 */
export const raceOrder: RaceId[] = ['terran', 'kvarr', 'skolar', 'nexar'];

/**
 * The localization key of a race name.
 */
export const raceNameKey = (race: RaceId): MessageKey => `races.${race}.name` as MessageKey;
