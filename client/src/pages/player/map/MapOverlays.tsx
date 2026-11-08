import { useEffect, useRef, useState, type MutableRefObject } from 'react';
import { GameIcon, type GameIconName } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import type { Bounds, MapView, Viewport } from './mapGeometry';
import { comingLayers, workingLayers, type MapLayer, type MapLayers } from './mapLayers';

const layerIcons: Record<MapLayer, GameIconName> = {
  mine: 'house',
  others: 'mapFlag',
  free: 'mapPlus',
  fields: 'mapField',
  nebulae: 'mapCloud',
  giants: 'mapGiant',
};

const comingIcons: Record<(typeof comingLayers)[number], GameIconName> = {
  fleets: 'fleets',
  clan: 'clan',
  enemies: 'mapAttack',
  pirates: 'danger',
};

/**
 * The panel of the layers of the map: the six that work are checkboxes, the four that wait for
 * fleets, clans and the PvE are switched off with a clock and the reason.
 *
 * @param layers Which layers are on.
 * @param onChange What to do when the player switches a layer.
 */
export function LayersPanel({ layers, onChange }: { layers: MapLayers; onChange: (layer: MapLayer, on: boolean) => void }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const count = workingLayers.filter((layer) => layers[layer]).length;

  return (
    <section aria-label={t('map.layers')} className="map-panel map-layers">
      <button aria-expanded={open} className="map-panel__head" type="button" onClick={() => setOpen((value) => !value)}>
        <GameIcon name="mapLayers" size={18} />
        <span>{t('map.layers')}</span>
        <span className="map-panel__count num">{`${count} / ${workingLayers.length}`}</span>
        <GameIcon className={open ? 'map-panel__chevron is-open' : 'map-panel__chevron'} name="mapChevron" size={16} />
      </button>
      {open ? (
        <ul className="map-layers__list">
          {workingLayers.map((layer) => (
            <li key={layer}>
              <label className="map-check">
                <input checked={layers[layer]} type="checkbox" onChange={(event) => onChange(layer, event.target.checked)} />
                <GameIcon name={layerIcons[layer]} size={16} />
                {t(`map.layer.${layer}` as never)}
              </label>
            </li>
          ))}
          {comingLayers.map((layer) => (
            <li key={layer}>
              <label className="map-check is-off" title={t('civ.soonReason')}>
                <input aria-disabled="true" checked={false} disabled type="checkbox" />
                <GameIcon name={comingIcons[layer]} size={16} />
                {t(`map.layer.${layer}` as never)}
                <GameIcon className="map-check__soon" name="mapClock" size={12} />
              </label>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

const legendItems: { icon: GameIconName; key: string }[] = [
  { icon: 'house', key: 'mine' },
  { icon: 'mapFlag', key: 'colony' },
  { icon: 'mapPlus', key: 'free' },
  { icon: 'mapLeaf', key: 'terraformed' },
  { icon: 'mapUnknown', key: 'unknown' },
  { icon: 'mapField', key: 'field' },
  { icon: 'mapGiant', key: 'giant' },
  { icon: 'mapStation', key: 'station' },
  { icon: 'mapCloud', key: 'nebula' },
  { icon: 'mapCrosshair', key: 'radar' },
  { icon: 'mapRoute', key: 'route' },
];

/** The legend of the signs of the map: a plate that opens above the button of the legend. */
export function MapLegend() {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);

  return (
    <div className="map-legend">
      {open ? (
        <ul aria-label={t('map.legend')} className="map-legend__list">
          {legendItems.map((item) => (
            <li key={item.key}>
              <GameIcon name={item.icon} size={18} />
              <span>{t(`map.legendItem.${item.key}` as never)}</span>
            </li>
          ))}
        </ul>
      ) : null}
      <button aria-expanded={open} className="map-legend__button" type="button" onClick={() => setOpen((value) => !value)}>
        <GameIcon name="mapInfo" size={18} />
        <span>{t('map.legend')}</span>
      </button>
    </div>
  );
}

/** A point of the mini-map. */
export interface MiniPoint {
  x: number;
  y: number;
  mine: boolean;
}

/**
 * The mini-map: the whole level with the rectangle of what the area shows now. The rectangle is
 * moved by the area through `updateRef`, without drawing the component again.
 *
 * @param bounds The whole level.
 * @param points The places to mark.
 * @param label The name of the level.
 * @param updateRef Where the mini-map puts the function that moves its rectangle.
 */
export function MiniMap({
  bounds,
  points,
  label,
  updateRef,
}: {
  bounds: Bounds;
  points: MiniPoint[];
  label: string;
  updateRef: MutableRefObject<((view: MapView, viewport: Viewport) => void) | null>;
}) {
  const rect = useRef<SVGRectElement>(null);
  const width = bounds.maxX - bounds.minX;
  const height = bounds.maxY - bounds.minY;

  useEffect(() => {
    updateRef.current = (view, viewport) => {
      const node = rect.current;

      if (node) {
        node.setAttribute('x', String(-view.x / view.k));
        node.setAttribute('y', String(-view.y / view.k));
        node.setAttribute('width', String(viewport.width / view.k));
        node.setAttribute('height', String(viewport.height / view.k));
      }
    };

    return () => {
      updateRef.current = null;
    };
  }, [updateRef]);

  return (
    <figure aria-label={label} className="map-mini">
      <svg aria-hidden="true" preserveAspectRatio="xMidYMid meet" viewBox={`${bounds.minX} ${bounds.minY} ${width} ${height}`}>
        {points.map((point, index) => (
          <circle className={point.mine ? 'map-mini__dot is-mine' : 'map-mini__dot'} cx={point.x} cy={point.y} key={index} r={Math.max(width, height) / 90} />
        ))}
        <rect className="map-mini__view" ref={rect} />
      </svg>
      <figcaption>{label}</figcaption>
    </figure>
  );
}
