import { useMemo, type ReactNode } from 'react';
import type { FleetsView } from '../../../api/fleetApi';
import { artImage } from '../../../assets/images';
import { useBodies } from '../useBodies';
import { SceneContext, type FleetsScene } from './useFleetsScene';

/**
 * The tones the classes of the hulls are drawn in, as the variables of the tokens. A class has no tone of
 * its own in the data, so the classes of the screen take the tones in turn.
 */
const toneVariables = ['--accent-hover', '--group-gem', '--trade', '--group-flame', '--success', '--research'] as const;

/**
 * Gives the parts of the screen the bodies and the tones of the classes.
 *
 * @param view The fleets of the player, so the classes of all the ships get their tones.
 * @param children The screen.
 */
export function FleetsSceneProvider({ view, children }: { view: FleetsView; children: ReactNode }) {
  const { bodies, reference } = useBodies();

  const scene = useMemo<FleetsScene>(() => {
    const types = new Map((reference?.objectTypes ?? []).map((type) => [type.id, type]));
    const classes = [...new Set([...view.fleets.flatMap((fleet) => fleet.ships), ...view.freeShips].map((ship) => ship.classNameKey))].sort();
    const tones = new Map(classes.map((key, index) => [key, `var(${toneVariables[index % toneVariables.length]})`]));
    const bodyOf = (planetId: number) => bodies.find((body) => body.planetId === planetId);

    return {
      bodies,
      nameOf: (planetId) => bodyOf(planetId)?.name?.trim() || String(planetId),
      placeOf: (planetId) => {
        const body = bodyOf(planetId);

        if (!body) {
          return String(planetId);
        }

        const system = body.systemName?.trim() ? body.systemName.trim() : String(body.systemId);

        return body.orbit === null ? system : `${system} ${body.orbit}`;
      },
      imageOf: (planetId) => {
        const body = bodyOf(planetId);

        return body ? artImage(types.get(body.objectTypeId)?.imageKey) : undefined;
      },
      toneOf: (classNameKey) => tones.get(classNameKey) ?? 'var(--text-2)',
    };
  }, [bodies, reference, view]);

  return <SceneContext.Provider value={scene}>{children}</SceneContext.Provider>;
}
