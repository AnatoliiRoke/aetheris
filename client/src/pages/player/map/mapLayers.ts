/**
 * The layers of the map the player can switch (task 00034). The first six work; the other four
 * wait for fleets, clans and the PvE and are shown switched off, as the design draws them.
 */
export const workingLayers = ['mine', 'others', 'free', 'fields', 'nebulae', 'giants'] as const;

/** The layers that are not ready yet. */
export const comingLayers = ['fleets', 'clan', 'enemies', 'pirates'] as const;

/** The name of a layer that works. */
export type MapLayer = (typeof workingLayers)[number];

/** Which layers are on. */
export type MapLayers = Record<MapLayer, boolean>;

/** All the layers on, the way the screen starts. */
export const allLayersOn: MapLayers = { mine: true, others: true, free: true, fields: true, nebulae: true, giants: true };

/** The class of the objects of a system that the layers name; the server gives the same words. */
export const objectClasses = {
  star: 'star',
  planet: 'planet',
  gasGiant: 'gas_giant',
  field: 'asteroid_field',
  unknown: 'unknown',
} as const;
