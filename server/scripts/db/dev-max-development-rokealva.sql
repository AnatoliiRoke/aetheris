-- Maximum development of the player «rokealva» on the development database (request of the author
-- of 13.11.2026): every building type at its last level and every technology at its last level, on
-- every planet the player owns.
--
-- The levels come from the reference data, not from numbers of the script:
--   * the last level of a building type is the greatest row of building_level of that type;
--   * the last level of a technology is technology.max_level.
-- Archived building types and technologies, and types without levels, are left out.
--
-- The construction queue and the research queue of the player are emptied (without a refund) and
-- their pending events are cancelled, so nothing finishes on top of the new levels.
--
-- After the run: no server restart is needed, the levels are read from the database on every
-- request; reload the page in the browser. The stock and the income of a planet are brought to the
-- current moment on the next opening of the planet, with the new levels, so the planet may gain the
-- production of the time since its last update at the new rates.
--
-- The script can be run again: it changes nothing when everything is at its last level already.
--
-- psql -h localhost -p 5434 -d aetheris_dev -v ON_ERROR_STOP=1 -f server/scripts/db/dev-max-development-rokealva.sql
BEGIN;

DO $$
DECLARE
  v_player_id varchar;
  v_planets int;
  v_buildings_before int;
  v_buildings_after int;
  v_technologies_before int;
  v_technologies_after int;
  v_construction_items int;
  v_research_items int;
  v_events int;
BEGIN
  SELECT id INTO v_player_id FROM asp_net_users WHERE user_name = 'rokealva';

  IF v_player_id IS NULL THEN
    RAISE EXCEPTION 'The player rokealva does not exist';
  END IF;

  SELECT count(*) INTO v_planets FROM planet WHERE owner_player_id = v_player_id;

  IF v_planets = 0 THEN
    RAISE EXCEPTION 'The player rokealva owns no planet';
  END IF;

  SELECT count(*) INTO v_buildings_before
  FROM planet_building pb JOIN planet p ON p.id = pb.planet_id
  WHERE p.owner_player_id = v_player_id;

  SELECT count(*) INTO v_technologies_before FROM player_technology WHERE player_id = v_player_id;

  -- The pending events of the queues of the player are cancelled, so they never complete.
  UPDATE scheduled_event e SET status = 'cancelled', version = e.version + 1
  WHERE e.status = 'pending'
    AND ((e.type = 'planets.construction_complete'
          AND (e.payload::jsonb ->> 'planet_id')::bigint IN (SELECT id FROM planet WHERE owner_player_id = v_player_id))
      OR (e.type = 'research.complete' AND e.payload::jsonb ->> 'PlayerId' = v_player_id));
  GET DIAGNOSTICS v_events = ROW_COUNT;

  -- The queues are emptied: everything they would build is at its last level after the script.
  DELETE FROM construction_queue_cost c USING construction_queue_item i, planet p
  WHERE c.construction_queue_item_id = i.id AND i.planet_id = p.id AND p.owner_player_id = v_player_id;

  DELETE FROM construction_queue_item i USING planet p
  WHERE i.planet_id = p.id AND p.owner_player_id = v_player_id;
  GET DIAGNOSTICS v_construction_items = ROW_COUNT;

  DELETE FROM research_queue_cost c USING research_queue_item i
  WHERE c.research_queue_item_id = i.id AND i.player_id = v_player_id;

  DELETE FROM research_queue_item WHERE player_id = v_player_id;
  GET DIAGNOSTICS v_research_items = ROW_COUNT;

  -- Every building type at its last level on every planet of the player.
  INSERT INTO planet_building (sector_id, planet_id, building_type_id, level)
  SELECT p.sector_id, p.id, t.id, l.max_level
  FROM planet p
  CROSS JOIN building_type t
  JOIN (SELECT building_type_id, max(level) AS max_level FROM building_level GROUP BY building_type_id) l
    ON l.building_type_id = t.id
  WHERE p.owner_player_id = v_player_id AND NOT t.is_archived AND l.max_level > 0
  ON CONFLICT (planet_id, building_type_id) DO UPDATE SET level = EXCLUDED.level
  WHERE planet_building.level IS DISTINCT FROM EXCLUDED.level;

  -- Every technology at its last level on every planet of the player.
  INSERT INTO player_technology (player_id, planet_id, technology_id, level)
  SELECT v_player_id, p.id, t.id, t.max_level
  FROM planet p
  CROSS JOIN technology t
  WHERE p.owner_player_id = v_player_id AND NOT t.is_archived AND t.max_level > 0
  ON CONFLICT (player_id, planet_id, technology_id) DO UPDATE SET level = EXCLUDED.level
  WHERE player_technology.level IS DISTINCT FROM EXCLUDED.level;

  SELECT count(*) INTO v_buildings_after
  FROM planet_building pb JOIN planet p ON p.id = pb.planet_id
  WHERE p.owner_player_id = v_player_id;

  SELECT count(*) INTO v_technologies_after FROM player_technology WHERE player_id = v_player_id;

  RAISE NOTICE 'rokealva: planets %, buildings % -> %, technologies % -> %, construction queue removed %, research queue removed %, events cancelled %',
    v_planets, v_buildings_before, v_buildings_after, v_technologies_before, v_technologies_after,
    v_construction_items, v_research_items, v_events;
END $$;

COMMIT;
