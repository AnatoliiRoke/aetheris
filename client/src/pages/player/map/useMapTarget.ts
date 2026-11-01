import { createContext, useContext } from 'react';
import type { MapTarget } from './mapLocation';

/**
 * What the map offers to the screens that will send a fleet (task 00034, the blank for the
 * fleets): the target the player pointed at, the mode of pointing and the confirmation. There are
 * no fleets yet, so nothing confirms a target; the screen of the fleets will subscribe with
 * `onTargetConfirmed`, open the map in the mode of pointing and take the target from here.
 */
export interface MapTargetValue {
  /** The target the player pointed at, or `null`. */
  target: MapTarget | null;
  /** Whether a click on the map points at a target instead of opening a card. */
  picking: boolean;
  setTarget: (target: MapTarget | null) => void;
  clearTarget: () => void;
  startPicking: () => void;
  stopPicking: () => void;
  /** Tells the subscribers that the player confirmed the target. */
  confirmTarget: () => void;
  /** Subscribes to the confirmation; returns the function that cancels the subscription. */
  onTargetConfirmed: (callback: (target: MapTarget) => void) => () => void;
}

export const MapTargetContext = createContext<MapTargetValue | null>(null);

/**
 * The target of a flight and the mode of pointing.
 */
export function useMapTarget(): MapTargetValue {
  const value = useContext(MapTargetContext);

  if (!value) {
    throw new Error('useMapTarget must be used inside MapTargetProvider.');
  }

  return value;
}
