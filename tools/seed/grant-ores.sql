-- Gives 250 of every ore except iron (resource_2 .. resource_7) to every planet of one player.
-- Usage: psql "<connection>" -v player_id="'<owner_player_id>'" -f tools/seed/grant-ores.sql
-- Find the id first: SELECT DISTINCT owner_player_id FROM planet_state WHERE owner_player_id IS NOT NULL;
BEGIN;

INSERT INTO planet_resource (sector_id, planet_id, resource_type_id, amount)
SELECT state.sector_id, state.planet_id, resource.id, 250
FROM planet_state AS state
CROSS JOIN resource_type AS resource
WHERE state.owner_player_id = :player_id
  AND resource.code IN ('resource_2', 'resource_3', 'resource_4', 'resource_5', 'resource_6', 'resource_7')
ON CONFLICT (planet_id, resource_type_id) DO UPDATE
SET amount = planet_resource.amount + EXCLUDED.amount;

COMMIT;
