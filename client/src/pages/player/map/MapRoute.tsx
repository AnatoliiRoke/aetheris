import { GameIcon } from '../../../components/GameIcon';
import { worldStyle } from './mapStyle';

/**
 * The route of a future flight and its target on the map (task 00034): a dashed line from the
 * place the flight starts to the target, with the mark of the target at its end and the distance
 * and the time on the line. No fleet flies yet; this is the picture of the blank.
 *
 * @param from The place the flight starts from, or `null` when it is not in this world.
 * @param to The target.
 * @param label The text on the line.
 * @param name What readers hear about the target.
 */
export function MapRoute({
  from,
  to,
  label,
  name,
}: {
  from: { x: number; y: number } | null;
  to: { x: number; y: number };
  label: string | null;
  name: string;
}) {
  return (
    <>
      {from ? (
        <svg aria-hidden="true" className="map-shapes">
      <g className="map-shapes__world">
          <line className="map-route" x1={from.x} x2={to.x} y1={from.y} y2={to.y} />
        </g>
        </svg>
      ) : null}
      <span aria-label={name} className="map-target" role="img" style={worldStyle(to.x, to.y)}>
        <GameIcon name="mapTarget" size={26} />
      </span>
      {from && label ? (
        <span aria-hidden="true" className="map-route-label" style={worldStyle((from.x + to.x) / 2, (from.y + to.y) / 2)}>
          <GameIcon name="mapClock" size={12} />
          {label}
        </span>
      ) : null}
    </>
  );
}
