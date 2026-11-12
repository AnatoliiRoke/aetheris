-- Apply to the existing development database after deploying task 00061.
-- Seed files add missing rows but deliberately do not overwrite edited values.
BEGIN;
DO $$
BEGIN
  IF (SELECT count(*) FROM part_type WHERE code IN ('part_m_58', 'part_m_59')) <> 2
     OR (SELECT count(*) FROM characteristic WHERE code = 'char_scan_radius') <> 1
     OR (SELECT count(*) FROM part_group WHERE code = 'pg_9') <> 1 THEN
    RAISE EXCEPTION 'Scan reference prerequisites are missing';
  END IF;
END $$;

INSERT INTO characteristic (code, name_key, description_key, unit_key, kind, image_key, sort_order, is_archived, version)
VALUES ('char_scan_duration', 'production.characteristic.char_scan_duration.name', 'production.characteristic.char_scan_duration.description', NULL, 'scan_duration', NULL, 18, false, 1)
ON CONFLICT (code) DO NOTHING;

INSERT INTO part_group_characteristic (part_group_id, characteristic_id, sort_order, is_required, is_archived, version)
SELECT g.id, c.id, 5, false, false, 1 FROM part_group g CROSS JOIN characteristic c
WHERE g.code = 'pg_9' AND c.code = 'char_scan_duration'
ON CONFLICT (part_group_id, characteristic_id) DO NOTHING;

INSERT INTO part_characteristic_value (part_type_id, characteristic_id, number_value, is_archived, version)
SELECT p.id, c.id, v.amount, false, 1
FROM part_type p JOIN (VALUES ('char_scan_radius', 50::numeric), ('char_scan_duration', 600::numeric)) AS v(code, amount) ON true
JOIN characteristic c ON c.code = v.code WHERE p.code = 'part_m_58'
ON CONFLICT (part_type_id, characteristic_id) DO UPDATE SET number_value = EXCLUDED.number_value, version = part_characteristic_value.version + 1
WHERE part_characteristic_value.number_value IS DISTINCT FROM EXCLUDED.number_value;

INSERT INTO part_attribute (part_type_id, attribute_key, code_value)
SELECT p.id, 'scan_target', v.target FROM part_type p JOIN (VALUES ('part_m_58', 'systems'), ('part_m_59', 'fleets')) AS v(code, target) ON p.code = v.code
ON CONFLICT (part_type_id, attribute_key) DO UPDATE SET code_value = EXCLUDED.code_value WHERE part_attribute.code_value IS DISTINCT FROM EXCLUDED.code_value;

INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
VALUES
('production.characteristic.char_scan_duration.name', 'uk', 'Тривалість сканування', 1, now(), 'seed'),
('production.characteristic.char_scan_duration.name', 'en', 'Scan duration', 1, now(), 'seed'),
('production.characteristic.char_scan_duration.description', 'uk', 'Тривалість сканування в секундах ігрового часу.', 1, now(), 'seed'),
('production.characteristic.char_scan_duration.description', 'en', 'The scan duration in game seconds.', 1, now(), 'seed'),
('admin.reference.kind.scan_duration', 'uk', 'Тривалість сканування', 1, now(), 'seed'),
('admin.reference.kind.scan_duration', 'en', 'Scan duration', 1, now(), 'seed'),
('admin.reference.attribute.scan_target', 'uk', 'Ціль сканування', 1, now(), 'seed'),
('admin.reference.attribute.scan_target', 'en', 'Scan target', 1, now(), 'seed'),
('admin.reference.scan_target.systems', 'uk', 'Системи', 1, now(), 'seed'),
('admin.reference.scan_target.systems', 'en', 'Systems', 1, now(), 'seed'),
('admin.reference.scan_target.fleets', 'uk', 'Флоти', 1, now(), 'seed'),
('admin.reference.scan_target.fleets', 'en', 'Fleets', 1, now(), 'seed'),
('mail.scan_report', 'uk', 'Звіт сканування', 1, now(), 'seed'),
('mail.scan_report', 'en', 'Scan report', 1, now(), 'seed')
ON CONFLICT (key, language) DO NOTHING;

INSERT INTO config_parameter (key, value, version, updated_at, updated_by)
VALUES ('fleet.scan.snapshot_lifetime_seconds', '172800', 1, now(), 'seed') ON CONFLICT (key) DO NOTHING;
COMMIT;
