import { useMemo } from 'react';
import { artImage } from '../../../assets/images';
import type { MarketOverview } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { useBodies } from '../useBodies';

/**
 * A body of the player as the market lists it: its name and picture, and whether it has the
 * trade centre and the portal the market asks for.
 */
export interface PlanetChoice {
  planetId: number;
  name: string;
  art: string | undefined;
  hasTradeCentre: boolean;
  hasPortal: boolean;
}

/**
 * The bodies of the player with the buildings of the market on them. The names and the pictures
 * come from the bodies the frame shares; the buildings come from the overview of the market.
 *
 * @param overview The overview of the market, or `null` while it is not read.
 */
export function useMarketPlanets(overview: MarketOverview | null): PlanetChoice[] {
  const { t } = useI18n();
  const { text } = useTexts();
  const { bodies, reference } = useBodies();

  return useMemo(() => {
    const types = new Map((reference?.objectTypes ?? []).map((type) => [type.id, type]));
    const flags = new Map((overview?.planets ?? []).map((planet) => [planet.planetId, planet]));

    return bodies.map((body) => ({
      planetId: body.planetId,
      name: body.name ?? text(types.get(body.objectTypeId)?.nameKey) ?? t('civ.untitled'),
      art: artImage(types.get(body.objectTypeId)?.imageKey),
      hasTradeCentre: flags.get(body.planetId)?.hasTradeCentre ?? false,
      hasPortal: flags.get(body.planetId)?.hasPortal ?? false,
    }));
  }, [bodies, reference, overview, text, t]);
}
