import { createContext, useContext } from 'react';
import type { OwnedPlanet } from '../../../api/worldApi';

/**
 * What the parts of the screen of the fleets share (task 00040): the bodies of the player with their
 * names, places and pictures, and the tone of every class of the hulls on the screen.
 */
export interface FleetsScene {
  /** The bodies of the player. */
  bodies: readonly OwnedPlanet[];
  /** The name of a body: the name the owner gave it, or its number. */
  nameOf: (planetId: number) => string;
  /** The place of a body as the mock-up writes it: the system and the orbit. */
  placeOf: (planetId: number) => string;
  /** The picture of the type of a body, or `undefined` for the placeholder. */
  imageOf: (planetId: number) => string | undefined;
  /** The tone of a class of the hulls, as a value of `var(--token)`. */
  toneOf: (classNameKey: string) => string;
}

export const SceneContext = createContext<FleetsScene | null>(null);

/**
 * Reads the bodies and the tones the screen of the fleets shares.
 */
export function useFleetsScene(): FleetsScene {
  const scene = useContext(SceneContext);

  if (!scene) {
    throw new Error('The screen of the fleets is missing its provider.');
  }

  return scene;
}
