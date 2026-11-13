-- Fuel on the stock of the player «rokealva» on the development database (request of the author
-- of 13.11.2026): every resource of the group of the fuel (resource_group_3: deuterium, helium-3,
-- dark matter, ether, enriched uranium) holds at least the target amount on every planet the player
-- owns, so the fuel can be loaded into the holds of the fleets for the tests of task 00072.
--
-- The resources are found by the neutral code of their group, not by identifiers. An amount that is
-- already above the target is not lowered. The target is the amount the other resources of the stock
-- of the player hold (2 000 000). The stock writes no capacity of its own: the server caps only the
-- production by the capacity, so the script does not count it.
--
-- After the run: no server restart is needed; reload the page in the browser. The moment the stock is
-- full (full_at) of a changed row is cleared, the server counts it again on the next opening of the
-- planet.
--
-- The script can be run again: it changes nothing when every fuel holds the target amount already.
--
-- psql -h localhost -p 5434 -d aetheris_dev -v ON_ERROR_STOP=1 -f server/scripts/db/dev-add-fuel-rokealva.sql
BEGIN;

DO $$
DECLARE
  v_target bigint := 2000000;
  v_player_id varchar;
  v_planets int;
  v_fuels int;
  v_before int;
  v_after int;
  v_changed int;
BEGIN
  SELECT id INTO v_player_id FROM asp_net_users WHERE user_name = 'rokealva';

  IF v_player_id IS NULL THEN
    RAISE EXCEPTION 'The player rokealva does not exist';
  END IF;

  SELECT count(*) INTO v_planets FROM planet WHERE owner_player_id = v_player_id;

  SELECT count(*) INTO v_fuels
  FROM resource_type r JOIN resource_group g ON g.id = r.resource_group_id
  WHERE g.code = 'resource_group_3' AND NOT r.is_archived;

  IF v_planets = 0 OR v_fuels = 0 THEN
    RAISE EXCEPTION 'The player rokealva owns no planet, or the group of the fuel has no resource';
  END IF;

  SELECT count(*) INTO v_before
  FROM planet_resource pr
  JOIN planet p ON p.id = pr.planet_id
  JOIN resource_type r ON r.id = pr.resource_type_id
  JOIN resource_group g ON g.id = r.resource_group_id
  WHERE p.owner_player_id = v_player_id AND g.code = 'resource_group_3' AND pr.amount >= v_target;

  INSERT INTO planet_resource (sector_id, planet_id, resource_type_id, amount, full_at)
  SELECT p.sector_id, p.id, r.id, v_target, NULL
  FROM planet p
  CROSS JOIN resource_type r
  JOIN resource_group g ON g.id = r.resource_group_id
  WHERE p.owner_player_id = v_player_id AND g.code = 'resource_group_3' AND NOT r.is_archived
  ON CONFLICT (planet_id, resource_type_id) DO UPDATE SET amount = EXCLUDED.amount, full_at = NULL
  WHERE planet_resource.amount < EXCLUDED.amount;
  GET DIAGNOSTICS v_changed = ROW_COUNT;

  SELECT count(*) INTO v_after
  FROM planet_resource pr
  JOIN planet p ON p.id = pr.planet_id
  JOIN resource_type r ON r.id = pr.resource_type_id
  JOIN resource_group g ON g.id = r.resource_group_id
  WHERE p.owner_player_id = v_player_id AND g.code = 'resource_group_3' AND pr.amount >= v_target;

  RAISE NOTICE 'rokealva: planets %, fuel resources %, rows at the target % -> %, rows written %',
    v_planets, v_fuels, v_before, v_after, v_changed;
END $$;

COMMIT;
