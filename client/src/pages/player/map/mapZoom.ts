import { createContext, useContext } from 'react';

/** How the drawn layers learn the scale: the scale in steps (so a layer is drawn again only when the step changes) and the scale that fits the whole world. */
export interface MapZoom {
  /** The scale in steps, in pixels per unit of the world. */
  scale: number;
  /** The scale that fits the whole world into the area. */
  fit: number;
}

export const MapZoomContext = createContext<MapZoom>({ scale: 1, fit: 1 });

/** The scale of the map, in steps. */
export function useMapZoom(): MapZoom {
  return useContext(MapZoomContext);
}
