-- Apply to the existing development database after deploying task 00072.
-- The seed of the production adds the missing rows by itself, but the seed runs only on a
-- database the server starts for the first time, so the rows that joined the starting data
-- later are added by this script.
BEGIN;
DO $$
BEGIN
  IF (SELECT count(*) FROM part_type WHERE code IN ('part_m_1', 'part_m_2', 'part_m_3', 'part_m_4', 'part_m_5', 'part_m_6')) <> 6
     OR (SELECT count(*) FROM characteristic WHERE code = 'char_jump_prep') <> 1
     OR (SELECT count(*) FROM resource_type WHERE code IN ('resource_12', 'resource_13', 'resource_14', 'resource_15')) <> 4 THEN
    RAISE EXCEPTION 'Engine reference prerequisites are missing';
  END IF;
END $$;

-- The class of the drives (task 00072): the ordinary drives, the gravitational drive, the hyperdrive
-- and the jump drive.
INSERT INTO part_attribute (part_type_id, attribute_key, code_value)
SELECT p.id, 'engine_kind', v.kind FROM part_type p
JOIN (VALUES ('part_m_1', 'normal'), ('part_m_2', 'normal'), ('part_m_3', 'normal'), ('part_m_4', 'gravity'), ('part_m_5', 'hyper'), ('part_m_6', 'jump')) AS v(code, kind) ON p.code = v.code
ON CONFLICT (part_type_id, attribute_key) DO UPDATE SET code_value = EXCLUDED.code_value WHERE part_attribute.code_value IS DISTINCT FROM EXCLUDED.code_value;

-- The time the jump drive prepares for a jump (task 00072): 900 seconds.
INSERT INTO part_characteristic_value (part_type_id, characteristic_id, number_value, is_archived, version)
SELECT p.id, c.id, 900::numeric, false, 1
FROM part_type p JOIN characteristic c ON c.code = 'char_jump_prep' WHERE p.code = 'part_m_6'
ON CONFLICT (part_type_id, characteristic_id) DO UPDATE SET number_value = EXCLUDED.number_value, version = part_characteristic_value.version + 1
WHERE part_characteristic_value.number_value IS DISTINCT FROM EXCLUDED.number_value;

-- The characteristic of the preparation of a jump belongs to the group of the drives (task 00072).
INSERT INTO part_group_characteristic (part_group_id, characteristic_id, sort_order, is_required, is_archived, version)
SELECT g.id, c.id, 4, false, false, 1 FROM part_group g CROSS JOIN characteristic c
WHERE g.code = 'pg_1' AND c.code = 'char_jump_prep'
ON CONFLICT (part_group_id, characteristic_id) DO NOTHING;

-- The fuel of the drives (task 00072): the deuterium and the helium-3 for the ordinary drives,
-- the dark matter and the ether for the hyperdrive and for the jump drive.
INSERT INTO part_fuel (part_type_id, resource_type_id, share)
SELECT p.id, r.id, v.share
FROM (VALUES
  ('part_m_1', 'resource_12', 1.0::double precision),
  ('part_m_2', 'resource_12', 1.0::double precision),
  ('part_m_3', 'resource_12', 1.0::double precision),
  ('part_m_4', 'resource_13', 1.0::double precision),
  ('part_m_5', 'resource_14', 0.3::double precision),
  ('part_m_5', 'resource_15', 0.7::double precision),
  ('part_m_6', 'resource_14', 0.7::double precision),
  ('part_m_6', 'resource_15', 0.3::double precision)
) AS v(part_code, resource_code, share)
JOIN part_type p ON p.code = v.part_code
JOIN resource_type r ON r.code = v.resource_code
ON CONFLICT (part_type_id, resource_type_id) DO UPDATE SET share = EXCLUDED.share WHERE part_fuel.share IS DISTINCT FROM EXCLUDED.share;

-- The texts of the new attribute and of the new classes (task 00072).
INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
VALUES
('admin.reference.attribute.engine_kind', 'uk', 'Клас двигуна', 1, now(), 'seed'),
('admin.reference.attribute.engine_kind', 'en', 'Engine class', 1, now(), 'seed'),
('admin.reference.engine_kind.normal', 'uk', 'Звичайний', 1, now(), 'seed'),
('admin.reference.engine_kind.normal', 'en', 'Normal', 1, now(), 'seed'),
('admin.reference.engine_kind.gravity', 'uk', 'Гравітаційний', 1, now(), 'seed'),
('admin.reference.engine_kind.gravity', 'en', 'Gravitational', 1, now(), 'seed'),
('admin.reference.engine_kind.hyper', 'uk', 'Гіпер', 1, now(), 'seed'),
('admin.reference.engine_kind.hyper', 'en', 'Hyper', 1, now(), 'seed'),
('admin.reference.engine_kind.jump', 'uk', 'Стрибок', 1, now(), 'seed'),
('admin.reference.engine_kind.jump', 'en', 'Jump', 1, now(), 'seed')
ON CONFLICT (key, language) DO NOTHING;

-- The consumption of a drive is counted in units of fuel per hour of its work (task 00072, decision
-- of the author of 13.11.2026): the characteristic gets the unit and the description says so.
UPDATE characteristic SET unit_key = 'production.characteristic.char_fuel_use.unit', version = version + 1
WHERE code = 'char_fuel_use' AND unit_key IS DISTINCT FROM 'production.characteristic.char_fuel_use.unit';

INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
VALUES
('production.characteristic.char_fuel_use.unit', 'uk', 'од./год', 1, now(), 'seed'),
('production.characteristic.char_fuel_use.unit', 'en', 'units/h', 1, now(), 'seed'),
('production.characteristic.char_fuel_use.description', 'uk', 'Одиниць палива за годину роботи двигуна.', 1, now(), 'seed'),
('production.characteristic.char_fuel_use.description', 'en', 'Units of fuel per hour of the engine work.', 1, now(), 'seed')
ON CONFLICT (key, language) DO UPDATE SET text = EXCLUDED.text, version = localization_text.version + 1, updated_at = now()
WHERE localization_text.text IS DISTINCT FROM EXCLUDED.text;

-- The parameters of the hyperflight, of the jump and of the check of the fuel (task 00072).
INSERT INTO config_parameter (key, value, version, updated_at, updated_by)
VALUES
('fleet.hyper.speed_multiplier', '10', 1, now(), 'seed'),
('fleet.jump.reference_distance', '1000', 1, now(), 'seed'),
('fleet.propellant.check_return_home', 'true', 1, now(), 'seed')
ON CONFLICT (key) DO NOTHING;
COMMIT;
