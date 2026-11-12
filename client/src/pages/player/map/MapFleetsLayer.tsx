import { memo, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { FleetInFlight, FleetStationed } from '../../../api/fleetApi';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { formatDuration } from '../planetText';
import { fleetRoute, stationedPlace, type FleetMapLevel } from './mapFleets';
import { MapMarker } from './MapMarker';
import { worldStyle } from './mapStyle';

/** How often the mark of a fleet is drawn again, in milliseconds. */
const tickMilliseconds = 1000;

/**
 * The fleets of the player that are under way, as the map draws them (task 00059): the line of the
 * route from the place the fleet left to the place it flies to, the mark of the fleet on the point
 * of the route the time has reached, and the time that is left to its arrival.
 *
 * The mark moves without asking the server: the layer counts the place of the fleet from the time
 * of the game, as the server counts it, and only this layer is drawn again. When the time of a
 * flight runs out, the layer tells the screen once, and the screen reads the fleets again.
 *
 * @param fleets The fleets that are under way.
 * @param gameNow The game time of the answer.
 * @param timeMultiplier How many game seconds pass in a real second.
 * @param receivedAt The moment the answer came, on the clock of the browser, in milliseconds.
 * @param level The level of the map that is drawn.
 * @param sectorId The sector that is drawn, or `null` on the level of the galaxy.
 * @param sectorPlace The place of the sector that is drawn, or `null` on the level of the galaxy.
 * @param systemId The system that is drawn, or `null` outside a system.
 * @param orbitPlace The place of an orbit of the system that is drawn.
 * @param orbitCount How many orbits the system that is drawn has.
 * @param onSelect What to do when the player clicks the mark of a fleet.
 * @param onArrived What to do when the time of a flight runs out.
 */
export const MapFleetsLayer = memo(function MapFleetsLayer({
  fleets,
  stationed,
  gameNow,
  timeMultiplier,
  receivedAt,
  level,
  sectorId,
  sectorPlace,
  systemId,
  orbitPlace,
  orbitCount,
  onSelect,
  onArrived,
}: {
  fleets: FleetInFlight[];
  stationed: FleetStationed[];
  gameNow: string;
  timeMultiplier: number;
  receivedAt: number;
  level: FleetMapLevel;
  sectorId: number | null;
  sectorPlace: { x: number; y: number } | null;
  systemId: number | null;
  orbitPlace: (orbit: number, orbitCount: number) => { x: number; y: number };
  orbitCount: number;
  onSelect: (fleetId: number) => void;
  onArrived: () => void;
}) {
  const { t, language } = useI18n();
  const [now, setNow] = useState(() => Date.now());

  // The mark of a fleet moves with the time of the game: the layer is drawn again once a second
  // and nothing else of the map is.
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), tickMilliseconds);

    return () => window.clearInterval(timer);
  }, []);

  // The game time of the answer is the anchor: the real seconds that passed since it are the game
  // seconds that passed too.
  const gameNowMs = Date.parse(gameNow) + (now - receivedAt) * (timeMultiplier > 0 ? timeMultiplier : 1);

  const drawn = useMemo<ReactNode[]>(() => {
    const out: ReactNode[] = [];

    for (const fleet of fleets) {
      const route = fleetRoute(fleet, gameNowMs, level, sectorId, sectorPlace, systemId, orbitPlace, orbitCount);

      if (!route) {
        continue;
      }

      const secondsLeft = Math.round((Date.parse(fleet.flight.arrivesAt) - gameNowMs) / 1000);

      out.push(
        <svg aria-hidden="true" className="map-shapes" key={`r${fleet.fleetId}`}>
          <g className="map-shapes__world">
            <line className="map-route map-route--fleet" x1={route.from.x} x2={route.to.x} y1={route.from.y} y2={route.to.y} />
          </g>
        </svg>,
        <MapMarker
          key={`m${fleet.fleetId}`}
          label={fleet.name}
          name={`${fleet.name}. ${t('map.fleetInFlight')}`}
          onSelect={() => onSelect(fleet.fleetId)}
          selected={false}
          tone="fleet"
          x={route.mark.x}
          y={route.mark.y}
        >
          <GameIcon name="fleets" size={20} />
        </MapMarker>,
      );

      if (secondsLeft > 0) {
        out.push(
          <span
            aria-hidden="true"
            className="map-route-label map-fleet-label"
            key={`l${fleet.fleetId}`}
            style={worldStyle(route.mark.x, route.mark.y)}
          >
            <GameIcon name="mapClock" size={12} />
            {formatDuration(language, secondsLeft)}
          </span>,
        );
      }
    }

    // The fleets that stand at a place are drawn as marks without a route: a fleet that arrived
    // stands in the place it flew to (task 00059).
    for (const fleet of stationed) {
      const place = stationedPlace(fleet, level, sectorId, sectorPlace, systemId, orbitPlace, orbitCount);

      if (!place) {
        continue;
      }

      out.push(
        <MapMarker
          key={`s${fleet.fleetId}`}
          label={fleet.name}
          name={`${fleet.name}. ${t('map.fleetStationed')}`}
          onSelect={() => onSelect(fleet.fleetId)}
          selected={false}
          tone="fleet-stationed"
          x={place.x}
          y={place.y}
        >
          <GameIcon name="fleets" size={18} />
        </MapMarker>,
      );
    }

    return out;
  }, [fleets, gameNowMs, language, level, onSelect, orbitCount, orbitPlace, sectorId, sectorPlace, stationed, systemId, t]);

  // When the time of a flight runs out, the layer tells the screen once: the screen reads the
  // fleets again, and the fleet that arrived is no longer under way.
  const arrived = useMemo(() => fleets.some((fleet) => Date.parse(fleet.flight.arrivesAt) <= gameNowMs), [fleets, gameNowMs]);
  const told = useRef(false);

  useEffect(() => {
    if (arrived && !told.current) {
      told.current = true;
      onArrived();
    }

    if (!arrived) {
      told.current = false;
    }
  }, [arrived, onArrived]);

  return <>{drawn}</>;
});
