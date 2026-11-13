-- The time of the deployment of the colony module on the development database (task 00073, request
-- of the author of 13.11.2026): 600 seconds (10 minutes) for the tests of the colonization. It is
-- the value of the testing, to be raised before the release; the seed holds the same value.
--
-- Only the characteristic of the deployment time (kind deploy_time) of the colony module (the module
-- whose attribute cargo_order is colonize) is changed. The script can be run again.
--
-- After the run: the server reads the catalog of the modules again when its cache expires
-- (production.catalog.cache_seconds, 30 s by default); a restart is not needed.
--
-- psql -h localhost -p 5434 -d aetheris_dev -v ON_ERROR_STOP=1 -f server/scripts/db/dev-colony-deploy-time-10min.sql
BEGIN;

DO $$
DECLARE
  v_target numeric := 600;
  v_changed int;
BEGIN
  UPDATE part_characteristic_value v
  SET number_value = v_target, version = v.version + 1
  FROM characteristic c, part_attribute a
  WHERE c.id = v.characteristic_id
    AND c.kind = 'deploy_time'
    AND a.part_type_id = v.part_type_id
    AND a.attribute_key = 'cargo_order'
    AND a.code_value = 'colonize'
    AND v.number_value IS DISTINCT FROM v_target;
  GET DIAGNOSTICS v_changed = ROW_COUNT;

  RAISE NOTICE 'colony module: deploy time rows changed %', v_changed;
END $$;

COMMIT;
