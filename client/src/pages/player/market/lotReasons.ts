import type { MessageKey } from '../../../i18n/uk';

/**
 * The text of the reason a lot cannot be bought, by the code the server sends. The client never
 * guesses a reason: it only words the one the server named.
 */
export const lotReasonKeys: Record<string, MessageKey> = {
  market_own_lot: 'market.rOwn',
  market_portal_missing: 'market.rNoPortal',
  market_trade_currency_short: 'market.rNoTC',
  market_energy_short: 'market.rNoRods',
};
