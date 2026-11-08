import { memo, type CSSProperties } from 'react';
import { artImage } from '../../../assets/images';
import type { MapField, MapGalaxy, MapNebula, MapObject, MapRadar, MapSector, MapSectorView, MapSystem, MapSystemView } from '../../../api/mapApi';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { clusterPoints, orbitPlace, orbitRadius, systemWorldSize } from './mapGeometry';
import { objectClasses, type MapLayers } from './mapLayers';
import { MapMarker } from './MapMarker';
import { asteroidMark, fieldRocks, nebulaMark, starPicture } from './mapPictures';
import { sectorClipPath, sectorPath } from './mapSectorShape';
import { worldStyle } from './mapStyle';
import { useMapZoom } from './mapZoom';

/** The size of a cell of the grouping of stars, in pixels of the screen: stars nearer than this merge into one bubble. */
const clusterCellPixels = 46;

/** The scale of the map, relative to the scale that fits it, from which the names of the stars are drawn. */
const labelRelativeScale = 2.2;

/** The share of the sector the sector of the galaxy covers, so a gap stays between neighbours. */
const sectorCover = 0.9;

/** What the layers of the map need from the screen. */
export interface LayerContext {
  /** The name of the type of an object by its identifier. */
  typeName: (typeId: number | null) => string;
  /** The key of the picture of the type of an object. */
  typeImage: (typeId: number | null) => string | null;
  selectedKey: string | null;
  onSelect: (key: string) => void;
  onOpen: (key: string) => void;
}

/**
 * The sectors of the galaxy as areas of the world with a name, the badge of the sector of the
 * newcomers and the number of the bodies of the player.
 */
/**
 * The sectors of the galaxy as areas of the world with a name, the badge of the sector of the
 * newcomers and the number of the bodies of the player.
 *
 * The new design draws the outline of a sector as a shape of the world (an irregular curve, as if
 * a cloud of dust had settled there) and puts the name over it. The shape is drawn once, as an
 * SVG, and the button over it is transparent and only takes the click.
 */
export const GalaxyLayer = memo(function GalaxyLayer({ galaxy, context }: { galaxy: MapGalaxy; context: LayerContext }) {
  const { t } = useI18n();
  const size = galaxy.sectorSize * sectorCover;
  const radius = size / 2;

  return (
    <>
      <svg aria-hidden="true" className="map-shapes">
        <g className="map-shapes__world">
          <ellipse className="map-galaxy__core" cx={0} cy={0} rx={radius * 7} ry={radius * 7} />
          {galaxy.sectors.map((sector: MapSector) => (
            <path
              className={`map-sector__edge${sector.ownPlanetCount > 0 ? ' is-mine' : ''}${context.selectedKey === `sector:${sector.id}` ? ' is-selected' : ''}`}
              d={sectorPath(sector.id, radius)}
              key={`e${sector.id}`}
              transform={`translate(${sector.x} ${sector.y})`}
            />
          ))}
        </g>
      </svg>
      {galaxy.sectors.map((sector: MapSector) => {
        const key = `sector:${sector.id}`;
        const name = [
          t('map.sector', { name: sector.name }),
          t('map.systemCount', { count: sector.systemCount }),
          sector.isNewbie ? t('map.newbies') : '',
          sector.ownPlanetCount > 0 ? t('map.ownPlanets', { count: sector.ownPlanetCount }) : '',
        ]
          .filter(Boolean)
          .join(', ');

        return (
          <button
            aria-label={name}
            aria-pressed={context.selectedKey === key}
            className={`map-sector${context.selectedKey === key ? ' is-selected' : ''}${sector.ownPlanetCount > 0 ? ' is-mine' : ''}`}
            data-map-hit=""
            key={sector.id}
            style={{ ...worldStyle(sector.x, sector.y, size, size), clipPath: sectorClipPath(sector.id) }}
            type="button"
            onClick={() => context.onSelect(key)}
            onDoubleClick={() => context.onOpen(key)}
          >
            <span aria-hidden="true" className="map-sector__label">
              <span className="map-sector__name">{sector.name}</span>
              <span className="map-sector__badges">
                {sector.isNewbie ? (
                  <span className="map-badge map-badge--newbie">
                    <GameIcon name="star" size={12} />
                    {t('map.newbies')}
                  </span>
                ) : null}
                {sector.ownPlanetCount > 0 ? (
                  <span className="map-badge map-badge--mine">
                    <GameIcon name="house" size={12} />
                    {sector.ownPlanetCount}
                  </span>
                ) : null}
              </span>
              <span className="map-sector__count">{t('map.systemCount', { count: sector.systemCount })}</span>
            </span>
          </button>
        );
      })}
    </>
  );
});

/** The picture of a nebula, sized to its area, and the dashed edge of that area. */
function NebulaMark({ nebula }: { nebula: MapNebula }) {
  const diameter = nebula.radius * 2;

  return (
    <span aria-hidden="true" className="map-nebula-art" style={worldStyle(nebula.x, nebula.y, diameter, diameter)}>
      <img alt="" draggable={false} src={nebulaMark} />
    </span>
  );
}

/** The dashed edge of a nebula. The line keeps its width at any zoom. */
function NebulaEdges({ nebulas }: { nebulas: MapNebula[] }) {
  return (
    <svg aria-hidden="true" className="map-shapes">
      <g className="map-shapes__world">
        {nebulas.map((nebula) => (
          <circle className="map-nebula" cx={nebula.x} cy={nebula.y} key={nebula.id} r={nebula.radius} />
        ))}
      </g>
    </svg>
  );
}

/**
 * The range of a radar, as in the design: a dashed disc, an inner ring and a wedge that turns.
 * The disc grows with the map; the line stays one width.
 */
function RadarRange({ radar }: { radar: MapRadar }) {
  const diameter = radar.radius * 2;

  return (
    <span aria-hidden="true" className="map-radar" style={worldStyle(radar.x, radar.y, diameter, diameter)}>
      <span className="map-radar__ring" />
      <span className="map-radar__sweep" />
    </span>
  );
}

/** One of the four star pictures, fixed for this system. A system of the player also carries the house. */
function StarPicture({ systemId, own }: { systemId: number; own: boolean }) {
  return (
    <>
      <img alt="" className="map-star-picture" draggable={false} height={25} src={starPicture(systemId)} width={25} />
      {own ? <GameIcon className="map-marker__sign" name="house" size={11} /> : null}
    </>
  );
}

/** An asteroid field: the same clump several times, each a different size. */
function AsteroidCluster({ seed }: { seed: number }) {
  return (
    <span className="map-field">
      {fieldRocks(seed).map((rock, index) => (
        <img
          alt=""
          className="map-field__rock"
          draggable={false}
          height={rock.size}
          key={index}
          src={asteroidMark}
          style={{ '--rock-x': `${rock.offsetX}px`, '--rock-y': `${rock.offsetY}px`, width: rock.size, height: rock.size } as CSSProperties}
          width={rock.size}
        />
      ))}
    </span>
  );
}

/**
 * The stars of a sector, the nebulas, the free fields and the ranges of the radars. Near stars merge
 * into bubbles while the map is far and split while it comes near; a star of the player or an open one
 * is never hidden in a bubble.
 */
export const SectorLayer = memo(function SectorLayer({
  sector,
  layers,
  context,
  onZoomTo,
}: {
  sector: MapSectorView;
  layers: MapLayers;
  context: LayerContext;
  onZoomTo: (point: { x: number; y: number }) => void;
}) {
  const { t } = useI18n();
  const { scale, fit } = useMapZoom();
  const stars = sector.systems.filter((system) => (system.state === 'open' && system.ownPlanetCount === 0 ? layers.others : system.ownPlanetCount > 0 ? layers.mine : true));
  const clusters = clusterPoints(stars, clusterCellPixels / scale, (system: MapSystem) => system.state === 'open' || system.ownPlanetCount > 0);
  const named = scale >= fit * labelRelativeScale;

  return (
    <>
      {layers.nebulae ? sector.nebulas.map((nebula) => <NebulaMark key={`art${nebula.id}`} nebula={nebula} />) : null}
      {layers.nebulae && sector.nebulas.length > 0 ? <NebulaEdges nebulas={sector.nebulas} /> : null}
      {sector.radars.map((radar) => (
        <RadarRange key={`r${radar.planetId}`} radar={radar} />
      ))}
      {layers.nebulae
        ? sector.nebulas.map((nebula) => (
            <span aria-hidden="true" className="map-nebula-label" key={`l${nebula.id}`} style={worldStyle(nebula.x, nebula.y + nebula.radius)}>
              <GameIcon name="mapCloud" size={14} />
              {t('map.nebulaLabel', { name: nebula.name })}
            </span>
          ))
        : null}
      {clusters.map((cluster) => {
        const [first] = cluster.members;

        if (cluster.members.length > 1) {
          return (
            <MapMarker
              key={`c${first!.id}-${cluster.members.length}`}
              label={null}
              name={t('map.cluster', { count: cluster.members.length })}
              onSelect={() => onZoomTo(cluster)}
              selected={false}
              tone="cluster"
              x={cluster.x}
              y={cluster.y}
            >
              <span className="map-cluster">{cluster.members.length}</span>
            </MapMarker>
          );
        }

        const system = first!;
        const key = `system:${system.id}`;
        const own = system.ownPlanetCount > 0;
        const open = system.state === 'open';
        const name = [system.name ?? t('map.unknownStar'), t('map.starKind'), own ? t('map.mine') : '', open ? '' : t('map.needRadar')].filter(Boolean).join('. ');

        return (
          <MapMarker
            key={key}
            label={open && (own || named) ? system.name : null}
            name={name}
            onOpen={() => context.onOpen(key)}
            onSelect={() => context.onSelect(key)}
            selected={context.selectedKey === key}
            tone={own ? 'mine' : open ? 'open' : 'known'}
            x={system.x}
            y={system.y}
          >
            <StarPicture own={own} systemId={system.id} />
            {system.inNebula ? <GameIcon className="map-marker__cloud" name="mapCloud" size={10} /> : null}
          </MapMarker>
        );
      })}
      {layers.fields
        ? sector.fields.map((field: MapField) => {
            const key = `object:${field.objectId}`;

            return (
              <MapMarker
                key={key}
                label={named ? field.name : null}
                name={`${field.name}. ${context.typeName(field.objectTypeId)}`}
                onSelect={() => context.onSelect(key)}
                selected={context.selectedKey === key}
                tone="field"
                x={field.x}
                y={field.y}
              >
                <AsteroidCluster seed={field.objectId} />
              </MapMarker>
            );
          })
        : null}
    </>
  );
});

/** A planet or a gas giant of a star system: the picture of its type in a ring that shows who has it. */
function BodySymbol({ object, context }: { object: MapObject; context: LayerContext }) {
  const picture = artImage(context.typeImage(object.objectTypeId));
  const isGiant = object.objectClass === objectClasses.gasGiant;

  return (
    <>
      {picture ? <img alt="" className={isGiant ? 'map-body map-body--giant' : 'map-body'} draggable={false} height={32} src={picture} width={32} /> : <GameIcon name={isGiant ? 'mapGiant' : 'mapPlanet'} size={22} />}
      {object.isOwn ? <GameIcon className="map-marker__sign" name="house" size={11} /> : null}
      {object.owner && !object.isOwn ? <GameIcon className="map-marker__sign" name="mapFlag" size={11} /> : null}
      {object.isFree ? <GameIcon className="map-marker__sign" name="mapPlus" size={11} /> : null}
      {object.isFree && object.isTerraformed ? <GameIcon className="map-marker__sign map-marker__sign--second" name="mapLeaf" size={11} /> : null}
      {isGiant ? <GameIcon className="map-marker__sign" name="mapStation" size={11} /> : null}
    </>
  );
}

/**
 * A star system: the star in the middle, the orbits, and the objects on them at fixed places. The
 * layers decide which objects are drawn; a place the player only knows of is always drawn as "?".
 */
export const SystemLayer = memo(function SystemLayer({
  view,
  layers,
  context,
}: {
  view: MapSystemView;
  layers: MapLayers;
  context: LayerContext;
}) {
  const { t } = useI18n();
  const middle = systemWorldSize / 2;
  const orbits = Array.from({ length: view.orbitCount }, (_, index) => index + 1);

  const shown = (object: MapObject): boolean => {
    if (object.state === 'known' || object.objectClass === objectClasses.star) {
      return true;
    }

    if (object.objectClass === objectClasses.field) {
      return layers.fields;
    }

    if (object.objectClass === objectClasses.gasGiant) {
      return layers.giants;
    }

    return object.isOwn ? layers.mine : object.owner ? layers.others : object.isFree ? layers.free : true;
  };

  return (
    <>
      <svg aria-hidden="true" className="map-shapes">
      <g className="map-shapes__world">
        {orbits.map((orbit) => (
          <circle className="map-orbit" cx={middle} cy={middle} key={orbit} r={orbitRadius(orbit, view.orbitCount)} />
        ))}
        </g>
      </svg>
      <img alt="" className="map-star" draggable={false} height={50} src={starPicture(view.system.id)} style={worldStyle(middle, middle)} width={50} />
      {orbits.map((orbit) => (
        <span aria-hidden="true" className="map-orbit-number" key={`o${orbit}`} style={worldStyle(middle, middle - orbitRadius(orbit, view.orbitCount))}>
          {orbit}
        </span>
      ))}
      {view.objects
        .filter((object) => object.orbit !== null && object.objectClass !== objectClasses.star && shown(object))
        .map((object) => {
          const key = `object:${object.id}`;
          const place = orbitPlace(object.id, object.orbit!, view.orbitCount);
          const known = object.state === 'known';
          const isField = object.objectClass === objectClasses.field;
          const owner = object.owner ? `${t('map.ownerOf', { name: object.owner.nickname })}` : object.isFree ? t('map.free') : object.isOwn ? t('map.mine') : '';
          const name = known
            ? t('map.unknownObject', { orbit: object.orbit! })
            : [object.name ?? '', context.typeName(object.objectTypeId), owner, t('map.orbitNumber', { orbit: object.orbit! })].filter(Boolean).join('. ');
          const tone = known ? 'known' : object.isOwn ? 'mine' : object.owner ? 'other' : object.isFree ? 'free' : isField ? 'field' : 'open';

          return (
            <MapMarker
              key={key}
              label={known ? '?' : (object.name ?? null)}
              name={name}
              onSelect={() => context.onSelect(key)}
              selected={context.selectedKey === key}
              tone={tone}
              x={place.x}
              y={place.y}
            >
              {known ? <GameIcon name="mapUnknown" size={16} /> : isField ? <AsteroidCluster seed={object.id} /> : <BodySymbol context={context} object={object} />}
            </MapMarker>
          );
        })}
    </>
  );
});
