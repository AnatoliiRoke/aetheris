import { marketVisibilities } from '../../../api/marketApi';
import type { MessageKey } from '../../../i18n/uk';

/**
 * The name of a level by its scope. The scope is the structural property of a level, so the client
 * names a level by it and never by the number of its row.
 */
export const levelLabelKeys: Record<string, MessageKey> = {
  [marketVisibilities.clan]: 'market.lvClan',
  [marketVisibilities.sector]: 'market.lvSector',
  [marketVisibilities.galactic]: 'market.lvGalaxy',
};
