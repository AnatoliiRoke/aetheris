-- Задача 00062. Запустити один раз одразу після перезапуску сервера,
-- до того як гравці змінять порядок вимкнення модулів.
-- Міграції мають уже бути застосовані. Скрипт нічого не перезаписує,
-- окрім порядку вимкнення вже збережених слотів.
BEGIN;
DO $$
BEGIN
  IF to_regclass('public.target_rule') IS NULL
     OR to_regclass('public.behavior') IS NULL
     OR to_regclass('public.part_accuracy') IS NULL
     OR NOT EXISTS (
       SELECT 1 FROM information_schema.columns
       WHERE table_schema = 'public' AND table_name = 'part_group' AND column_name = 'default_power_priority')
     OR NOT EXISTS (
       SELECT 1 FROM information_schema.columns
       WHERE table_schema = 'public' AND table_name = 'ship_design_slot' AND column_name = 'power_priority')
     OR NOT EXISTS (
       SELECT 1 FROM information_schema.columns
       WHERE table_schema = 'public' AND table_name = 'target_rule' AND column_name = 'image_key')
     OR NOT EXISTS (
       SELECT 1 FROM information_schema.columns
       WHERE table_schema = 'public' AND table_name = 'behavior' AND column_name = 'image_key')
  THEN
    RAISE EXCEPTION 'Спочатку перезапустіть сервер, щоб міграції задачі 00062 застосувались.';
  END IF;
END $$;

INSERT INTO characteristic (code, name_key, description_key, unit_key, kind, sort_order, is_archived, version)
VALUES
  ('char_accuracy', 'production.characteristic.char_accuracy.name', 'production.characteristic.char_accuracy.description', NULL, 'accuracy', 44, false, 1),
  ('char_signature', 'production.characteristic.char_signature.name', 'production.characteristic.char_signature.description', NULL, 'signature', 45, false, 1),
  ('char_signature_cut', 'production.characteristic.char_signature_cut.name', 'production.characteristic.char_signature_cut.description', NULL, 'signature_cut', 46, false, 1),
  ('char_leash_radius', 'production.characteristic.char_leash_radius.name', 'production.characteristic.char_leash_radius.description', 'production.characteristic.char_leash_radius.unit', 'leash_radius', 47, false, 1),
  ('char_drone_speed', 'production.characteristic.char_drone_speed.name', 'production.characteristic.char_drone_speed.description', 'production.characteristic.char_drone_speed.unit', 'drone_speed', 48, false, 1)
ON CONFLICT (code) DO NOTHING;

UPDATE characteristic AS stored
SET kind = incoming.kind
FROM (VALUES
  ('char_accuracy', 'accuracy'),
  ('char_signature', 'signature'),
  ('char_signature_cut', 'signature_cut'),
  ('char_leash_radius', 'leash_radius'),
  ('char_drone_speed', 'drone_speed')
) AS incoming(code, kind)
WHERE stored.code = incoming.code AND stored.kind IS NULL;

INSERT INTO part_group_characteristic (part_group_id, characteristic_id, sort_order, is_required, is_archived, version)
SELECT part_group.id, characteristic.id, incoming.sort_order, incoming.is_required, false, 1
FROM (VALUES
  ('pg_5', 'char_integrity', 1, true),
  ('pg_12', 'char_integrity', 3, true),
  ('pg_13', 'char_integrity', 3, true),
  ('pg_14', 'char_integrity', 3, true),
  ('pg_15', 'char_integrity', 3, true),
  ('pg_16', 'char_integrity', 3, true),
  ('pg_17', 'char_integrity', 3, true),
  ('pg_18', 'char_integrity', 3, true),
  ('pg_3', 'char_accuracy', 5, false),
  ('pg_4', 'char_accuracy', 6, false),
  ('pg_12', 'char_signature', 9, true),
  ('pg_13', 'char_signature', 9, true),
  ('pg_14', 'char_signature', 9, true),
  ('pg_15', 'char_signature', 9, true),
  ('pg_16', 'char_signature', 9, true),
  ('pg_17', 'char_signature', 9, true),
  ('pg_9', 'char_signature_cut', 6, false),
  ('pg_12', 'char_leash_radius', 10, true),
  ('pg_14', 'char_leash_radius', 10, true),
  ('pg_15', 'char_leash_radius', 10, true),
  ('pg_16', 'char_leash_radius', 10, true),
  ('pg_17', 'char_leash_radius', 10, true),
  ('pg_13', 'char_drone_speed', 10, true),
  ('pg_1', 'char_integrity', 5, true),
  ('pg_2', 'char_integrity', 2, true),
  ('pg_3', 'char_integrity', 6, true),
  ('pg_4', 'char_integrity', 7, true),
  ('pg_6', 'char_integrity', 6, true),
  ('pg_8', 'char_integrity', 5, true),
  ('pg_9', 'char_integrity', 7, true),
  ('pg_10', 'char_integrity', 15, false)
) AS incoming(group_code, characteristic_code, sort_order, is_required)
JOIN part_group ON part_group.code = incoming.group_code
JOIN characteristic ON characteristic.code = incoming.characteristic_code
ON CONFLICT (part_group_id, characteristic_id) DO NOTHING;

INSERT INTO part_characteristic_value (part_type_id, characteristic_id, number_value, is_archived, version)
SELECT part_type.id, characteristic.id, incoming.number_value, false, 1
FROM (VALUES
  ('part_m_1', 'char_integrity', 100::numeric),
  ('part_m_2', 'char_integrity', 100::numeric),
  ('part_m_3', 'char_integrity', 100::numeric),
  ('part_m_4', 'char_integrity', 100::numeric),
  ('part_m_5', 'char_integrity', 100::numeric),
  ('part_m_6', 'char_integrity', 100::numeric),
  ('part_m_7', 'char_integrity', 100::numeric),
  ('part_m_8', 'char_integrity', 100::numeric),
  ('part_m_9', 'char_integrity', 100::numeric),
  ('part_m_10', 'char_integrity', 100::numeric),
  ('part_m_11', 'char_integrity', 100::numeric),
  ('part_m_12', 'char_integrity', 100::numeric),
  ('part_m_13', 'char_integrity', 100::numeric),
  ('part_m_14', 'char_integrity', 100::numeric),
  ('part_m_15', 'char_integrity', 100::numeric),
  ('part_m_16', 'char_integrity', 100::numeric),
  ('part_m_17', 'char_integrity', 100::numeric),
  ('part_m_18', 'char_integrity', 100::numeric),
  ('part_m_19', 'char_integrity', 100::numeric),
  ('part_m_20', 'char_integrity', 100::numeric),
  ('part_m_21', 'char_integrity', 100::numeric),
  ('part_m_22', 'char_integrity', 100::numeric),
  ('part_m_23', 'char_integrity', 100::numeric),
  ('part_m_24', 'char_integrity', 100::numeric),
  ('part_m_25', 'char_integrity', 100::numeric),
  ('part_m_26', 'char_integrity', 100::numeric),
  ('part_m_27', 'char_integrity', 75::numeric),
  ('part_m_28', 'char_integrity', 75::numeric),
  ('part_m_29', 'char_integrity', 75::numeric),
  ('part_m_30', 'char_integrity', 225::numeric),
  ('part_m_31', 'char_integrity', 225::numeric),
  ('part_m_32', 'char_integrity', 225::numeric),
  ('part_m_33', 'char_integrity', 450::numeric),
  ('part_m_34', 'char_integrity', 450::numeric),
  ('part_m_35', 'char_integrity', 100::numeric),
  ('part_m_36', 'char_integrity', 100::numeric),
  ('part_m_37', 'char_integrity', 100::numeric),
  ('part_m_38', 'char_integrity', 100::numeric),
  ('part_m_39', 'char_integrity', 100::numeric),
  ('part_m_40', 'char_integrity', 100::numeric),
  ('part_m_41', 'char_integrity', 100::numeric),
  ('part_m_42', 'char_integrity', 100::numeric),
  ('part_m_53', 'char_integrity', 100::numeric),
  ('part_m_54', 'char_integrity', 100::numeric),
  ('part_m_55', 'char_integrity', 100::numeric),
  ('part_m_56', 'char_integrity', 100::numeric),
  ('part_m_57', 'char_integrity', 100::numeric),
  ('part_m_58', 'char_integrity', 100::numeric),
  ('part_m_59', 'char_integrity', 100::numeric),
  ('part_m_60', 'char_integrity', 100::numeric),
  ('part_m_61', 'char_integrity', 100::numeric),
  ('part_m_61', 'char_signature_cut', 0.35::numeric),
  ('part_m_62', 'char_integrity', 100::numeric),
  ('part_m_63', 'char_integrity', 100::numeric),
  ('part_m_64', 'char_integrity', 100::numeric),
  ('part_m_65', 'char_integrity', 100::numeric),
  ('part_m_66', 'char_integrity', 100::numeric),
  ('part_m_67', 'char_integrity', 100::numeric),
  ('part_m_68', 'char_integrity', 100::numeric),
  ('part_m_69', 'char_integrity', 100::numeric),
  ('part_h_1', 'char_integrity', 1000::numeric),
  ('part_h_1', 'char_signature', 0.6::numeric),
  ('part_h_1', 'char_leash_radius', 800::numeric),
  ('part_h_2', 'char_integrity', 1450::numeric),
  ('part_h_2', 'char_signature', 0.6::numeric),
  ('part_h_2', 'char_leash_radius', 800::numeric),
  ('part_h_3', 'char_integrity', 2200::numeric),
  ('part_h_3', 'char_signature', 0.6::numeric),
  ('part_h_3', 'char_leash_radius', 800::numeric),
  ('part_h_4', 'char_integrity', 3000::numeric),
  ('part_h_4', 'char_signature', 0.6::numeric),
  ('part_h_4', 'char_leash_radius', 800::numeric),
  ('part_h_5', 'char_integrity', 250::numeric),
  ('part_h_5', 'char_signature', 0.2::numeric),
  ('part_h_5', 'char_drone_speed', 200::numeric),
  ('part_h_6', 'char_integrity', 250::numeric),
  ('part_h_6', 'char_signature', 0.2::numeric),
  ('part_h_6', 'char_drone_speed', 200::numeric),
  ('part_h_7', 'char_integrity', 250::numeric),
  ('part_h_7', 'char_signature', 0.2::numeric),
  ('part_h_7', 'char_drone_speed', 200::numeric),
  ('part_h_8', 'char_integrity', 4000::numeric),
  ('part_h_8', 'char_signature', 1::numeric),
  ('part_h_8', 'char_leash_radius', 1200::numeric),
  ('part_h_9', 'char_integrity', 4450::numeric),
  ('part_h_9', 'char_signature', 1::numeric),
  ('part_h_9', 'char_leash_radius', 1200::numeric),
  ('part_h_10', 'char_integrity', 4900::numeric),
  ('part_h_10', 'char_signature', 1::numeric),
  ('part_h_10', 'char_leash_radius', 1200::numeric),
  ('part_h_11', 'char_integrity', 5400::numeric),
  ('part_h_11', 'char_signature', 1::numeric),
  ('part_h_11', 'char_leash_radius', 1200::numeric),
  ('part_h_12', 'char_integrity', 6000::numeric),
  ('part_h_12', 'char_signature', 1::numeric),
  ('part_h_12', 'char_leash_radius', 1200::numeric),
  ('part_h_13', 'char_integrity', 6700::numeric),
  ('part_h_13', 'char_signature', 1::numeric),
  ('part_h_13', 'char_leash_radius', 1200::numeric),
  ('part_h_14', 'char_integrity', 7300::numeric),
  ('part_h_14', 'char_signature', 1.6::numeric),
  ('part_h_14', 'char_leash_radius', 1600::numeric),
  ('part_h_15', 'char_integrity', 8000::numeric),
  ('part_h_15', 'char_signature', 1.6::numeric),
  ('part_h_15', 'char_leash_radius', 1600::numeric),
  ('part_h_16', 'char_integrity', 10000::numeric),
  ('part_h_16', 'char_signature', 1.6::numeric),
  ('part_h_16', 'char_leash_radius', 1600::numeric),
  ('part_h_17', 'char_integrity', 11500::numeric),
  ('part_h_17', 'char_signature', 1.6::numeric),
  ('part_h_17', 'char_leash_radius', 1600::numeric),
  ('part_h_18', 'char_integrity', 12750::numeric),
  ('part_h_18', 'char_signature', 2.2::numeric),
  ('part_h_18', 'char_leash_radius', 2000::numeric),
  ('part_h_19', 'char_integrity', 14000::numeric),
  ('part_h_19', 'char_signature', 2.2::numeric),
  ('part_h_19', 'char_leash_radius', 2000::numeric),
  ('part_h_20', 'char_integrity', 16000::numeric),
  ('part_h_20', 'char_signature', 2.2::numeric),
  ('part_h_20', 'char_leash_radius', 2000::numeric),
  ('part_h_21', 'char_integrity', 500::numeric),
  ('part_h_21', 'char_signature', 1::numeric),
  ('part_h_21', 'char_leash_radius', 1000::numeric),
  ('part_h_22', 'char_integrity', 2500::numeric),
  ('part_h_22', 'char_signature', 1::numeric),
  ('part_h_22', 'char_leash_radius', 1000::numeric),
  ('part_h_23', 'char_integrity', 5100::numeric),
  ('part_h_23', 'char_signature', 1::numeric),
  ('part_h_23', 'char_leash_radius', 1000::numeric),
  ('part_h_24', 'char_integrity', 3700::numeric),
  ('part_h_24', 'char_signature', 1::numeric),
  ('part_h_24', 'char_leash_radius', 1000::numeric),
  ('part_h_25', 'char_integrity', 100::numeric),
  ('part_h_26', 'char_integrity', 110::numeric),
  ('part_h_27', 'char_integrity', 121::numeric),
  ('part_m_84', 'char_integrity', 100::numeric)
) AS incoming(part_code, characteristic_code, number_value)
JOIN part_type ON part_type.code = incoming.part_code
JOIN characteristic ON characteristic.code = incoming.characteristic_code
ON CONFLICT (part_type_id, characteristic_id) DO NOTHING;

INSERT INTO part_accuracy (part_type_id, target_part_group_id, value)
SELECT part_type.id, part_group.id, incoming.value
FROM (VALUES
  ('part_m_13', 'pg_13', 0.9::double precision),
  ('part_m_13', 'pg_12', 0.8::double precision),
  ('part_m_13', 'pg_14', 0.6::double precision),
  ('part_m_13', 'pg_15', 0.35::double precision),
  ('part_m_13', 'pg_16', 0.2::double precision),
  ('part_m_13', 'pg_17', 0.8::double precision),
  ('part_m_15', 'pg_13', 0.9::double precision),
  ('part_m_15', 'pg_12', 0.8::double precision),
  ('part_m_15', 'pg_14', 0.6::double precision),
  ('part_m_15', 'pg_15', 0.35::double precision),
  ('part_m_15', 'pg_16', 0.2::double precision),
  ('part_m_15', 'pg_17', 0.8::double precision),
  ('part_m_22', 'pg_13', 0.9::double precision),
  ('part_m_22', 'pg_12', 0.8::double precision),
  ('part_m_22', 'pg_14', 0.6::double precision),
  ('part_m_22', 'pg_15', 0.35::double precision),
  ('part_m_22', 'pg_16', 0.2::double precision),
  ('part_m_22', 'pg_17', 0.8::double precision),
  ('part_m_14', 'pg_13', 0.1::double precision),
  ('part_m_14', 'pg_12', 0.2::double precision),
  ('part_m_14', 'pg_14', 0.5::double precision),
  ('part_m_14', 'pg_15', 0.85::double precision),
  ('part_m_14', 'pg_16', 0.95::double precision),
  ('part_m_14', 'pg_17', 0.2::double precision),
  ('part_m_19', 'pg_13', 0.1::double precision),
  ('part_m_19', 'pg_12', 0.2::double precision),
  ('part_m_19', 'pg_14', 0.5::double precision),
  ('part_m_19', 'pg_15', 0.85::double precision),
  ('part_m_19', 'pg_16', 0.95::double precision),
  ('part_m_19', 'pg_17', 0.2::double precision),
  ('part_m_20', 'pg_13', 0.1::double precision),
  ('part_m_20', 'pg_12', 0.2::double precision),
  ('part_m_20', 'pg_14', 0.5::double precision),
  ('part_m_20', 'pg_15', 0.85::double precision),
  ('part_m_20', 'pg_16', 0.95::double precision),
  ('part_m_20', 'pg_17', 0.2::double precision),
  ('part_m_23', 'pg_13', 0.1::double precision),
  ('part_m_23', 'pg_12', 0.2::double precision),
  ('part_m_23', 'pg_14', 0.5::double precision),
  ('part_m_23', 'pg_15', 0.85::double precision),
  ('part_m_23', 'pg_16', 0.95::double precision),
  ('part_m_23', 'pg_17', 0.2::double precision),
  ('part_m_24', 'pg_13', 0.1::double precision),
  ('part_m_24', 'pg_12', 0.2::double precision),
  ('part_m_24', 'pg_14', 0.5::double precision),
  ('part_m_24', 'pg_15', 0.85::double precision),
  ('part_m_24', 'pg_16', 0.95::double precision),
  ('part_m_24', 'pg_17', 0.2::double precision),
  ('part_m_25', 'pg_13', 0.1::double precision),
  ('part_m_25', 'pg_12', 0.2::double precision),
  ('part_m_25', 'pg_14', 0.5::double precision),
  ('part_m_25', 'pg_15', 0.85::double precision),
  ('part_m_25', 'pg_16', 0.95::double precision),
  ('part_m_25', 'pg_17', 0.2::double precision),
  ('part_m_26', 'pg_13', 0.1::double precision),
  ('part_m_26', 'pg_12', 0.2::double precision),
  ('part_m_26', 'pg_14', 0.5::double precision),
  ('part_m_26', 'pg_15', 0.85::double precision),
  ('part_m_26', 'pg_16', 0.95::double precision),
  ('part_m_26', 'pg_17', 0.2::double precision),
  ('part_m_84', 'pg_13', 0.1::double precision),
  ('part_m_84', 'pg_12', 0.2::double precision),
  ('part_m_84', 'pg_14', 0.5::double precision),
  ('part_m_84', 'pg_15', 0.85::double precision),
  ('part_m_84', 'pg_16', 0.95::double precision),
  ('part_m_84', 'pg_17', 0.2::double precision),
  ('part_m_16', 'pg_13', 0.6::double precision),
  ('part_m_16', 'pg_12', 0.6::double precision),
  ('part_m_16', 'pg_14', 0.6::double precision),
  ('part_m_16', 'pg_15', 0.6::double precision),
  ('part_m_16', 'pg_16', 0.6::double precision),
  ('part_m_16', 'pg_17', 0.6::double precision),
  ('part_m_17', 'pg_13', 0.6::double precision),
  ('part_m_17', 'pg_12', 0.6::double precision),
  ('part_m_17', 'pg_14', 0.6::double precision),
  ('part_m_17', 'pg_15', 0.6::double precision),
  ('part_m_17', 'pg_16', 0.6::double precision),
  ('part_m_17', 'pg_17', 0.6::double precision),
  ('part_m_18', 'pg_13', 0.6::double precision),
  ('part_m_18', 'pg_12', 0.6::double precision),
  ('part_m_18', 'pg_14', 0.6::double precision),
  ('part_m_18', 'pg_15', 0.6::double precision),
  ('part_m_18', 'pg_16', 0.6::double precision),
  ('part_m_18', 'pg_17', 0.6::double precision),
  ('part_m_21', 'pg_13', 0.6::double precision),
  ('part_m_21', 'pg_12', 0.6::double precision),
  ('part_m_21', 'pg_14', 0.6::double precision),
  ('part_m_21', 'pg_15', 0.6::double precision),
  ('part_m_21', 'pg_16', 0.6::double precision),
  ('part_m_21', 'pg_17', 0.6::double precision)
) AS incoming(part_code, group_code, value)
JOIN part_type ON part_type.code = incoming.part_code
JOIN part_group ON part_group.code = incoming.group_code
ON CONFLICT (part_type_id, target_part_group_id) DO NOTHING;

UPDATE part_group AS stored
SET default_power_priority = incoming.priority
FROM (VALUES
  ('pg_1', 0::smallint),
  ('pg_2', 0::smallint),
  ('pg_3', 20::smallint),
  ('pg_4', 20::smallint),
  ('pg_5', 50::smallint),
  ('pg_6', 10::smallint),
  ('pg_7', 50::smallint),
  ('pg_8', 50::smallint),
  ('pg_9', 30::smallint),
  ('pg_10', 50::smallint),
  ('pg_11', 50::smallint),
  ('pg_12', 50::smallint),
  ('pg_13', 50::smallint),
  ('pg_14', 50::smallint),
  ('pg_15', 50::smallint),
  ('pg_16', 50::smallint),
  ('pg_17', 50::smallint),
  ('pg_18', 50::smallint),
  ('pg_19', 50::smallint),
  ('pg_20', 50::smallint),
  ('pg_21', 50::smallint),
  ('pg_22', 20::smallint)
) AS incoming(code, priority)
WHERE stored.code = incoming.code AND stored.default_power_priority IS NULL;

UPDATE ship_design_slot AS slot
SET power_priority = part_group.default_power_priority
FROM part_type
JOIN part_group ON part_group.id = part_type.part_group_id
WHERE slot.part_type_id = part_type.id
  AND part_group.default_power_priority IS NOT NULL;

INSERT INTO target_rule (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('target_rule_1', 'nearest', 'combat.target_rule.target_rule_1.name', 'combat.target_rule.target_rule_1.description', 10, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO target_rule (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('target_rule_2', 'weakest', 'combat.target_rule.target_rule_2.name', 'combat.target_rule.target_rule_2.description', 20, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO target_rule (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('target_rule_3', 'carriers', 'combat.target_rule.target_rule_3.name', 'combat.target_rule.target_rule_3.description', 30, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO target_rule (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('target_rule_4', 'drones', 'combat.target_rule.target_rule_4.name', 'combat.target_rule.target_rule_4.description', 40, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO target_rule (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('target_rule_5', 'strongest', 'combat.target_rule.target_rule_5.name', 'combat.target_rule.target_rule_5.description', 50, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO behavior (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('behavior_1', 'approach', 'combat.behavior.behavior_1.name', 'combat.behavior.behavior_1.description', 10, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO behavior (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('behavior_2', 'keep_distance', 'combat.behavior.behavior_2.name', 'combat.behavior.behavior_2.description', 20, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO behavior (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('behavior_3', 'stand', 'combat.behavior.behavior_3.name', 'combat.behavior.behavior_3.description', 30, false, 1)
ON CONFLICT (code) DO NOTHING;
INSERT INTO behavior (code, logic_code, name_key, description_key, sort_order, is_archived, version)
VALUES ('behavior_4', 'escort', 'combat.behavior.behavior_4.name', 'combat.behavior.behavior_4.description', 40, false, 1)
ON CONFLICT (code) DO NOTHING;

INSERT INTO code_sequence (code_group, last_number)
VALUES ('target_rule', 5), ('behavior', 4)
ON CONFLICT (code_group) DO UPDATE
SET last_number = GREATEST(code_sequence.last_number, EXCLUDED.last_number);

INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
VALUES
  ('production.characteristic.char_accuracy.name', 'uk', 'Влучність', 1, now(), 'production_seed'),
  ('production.characteristic.char_accuracy.name', 'en', 'Accuracy', 1, now(), 'production_seed'),
  ('production.characteristic.char_accuracy.description', 'uk', 'Влучність зброї по класу цілі, від 0 до 1. Значення лежить у таблиці влучності, а не одним числом деталі.', 1, now(), 'production_seed'),
  ('production.characteristic.char_accuracy.description', 'en', 'Accuracy of a weapon against a target class, from 0 to 1. The value lives in the accuracy table, not as one number of the part.', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature.name', 'uk', 'Сигнатура', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature.name', 'en', 'Signature', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature.description', 'uk', 'Наскільки помітний корпус. Менша сигнатура важче виявляється.', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature.description', 'en', 'How visible a hull is. A smaller signature is harder to detect.', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature_cut.name', 'uk', 'Зниження сигнатури', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature_cut.name', 'en', 'Signature cut', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature_cut.description', 'uk', 'Частка сигнатури корпусу, яку знімає модуль маскування, від 0 до 1.', 1, now(), 'production_seed'),
  ('production.characteristic.char_signature_cut.description', 'en', 'The share of the hull signature a camouflage module removes, from 0 to 1.', 1, now(), 'production_seed'),
  ('production.characteristic.char_leash_radius.name', 'uk', 'Радіус повідця', 1, now(), 'production_seed'),
  ('production.characteristic.char_leash_radius.name', 'en', 'Leash radius', 1, now(), 'production_seed'),
  ('production.characteristic.char_leash_radius.description', 'uk', 'Як далеко від носія може відійти дрон, в одиницях поля бою.', 1, now(), 'production_seed'),
  ('production.characteristic.char_leash_radius.description', 'en', 'How far a drone may fly from its carrier, in field units.', 1, now(), 'production_seed'),
  ('production.characteristic.char_leash_radius.unit', 'uk', 'од. поля', 1, now(), 'production_seed'),
  ('production.characteristic.char_leash_radius.unit', 'en', 'field units', 1, now(), 'production_seed'),
  ('production.characteristic.char_drone_speed.name', 'uk', 'Швидкість дрона', 1, now(), 'production_seed'),
  ('production.characteristic.char_drone_speed.name', 'en', 'Drone speed', 1, now(), 'production_seed'),
  ('production.characteristic.char_drone_speed.description', 'uk', 'Швидкість корпусу дрона на полі бою, в одиницях поля за секунду.', 1, now(), 'production_seed'),
  ('production.characteristic.char_drone_speed.description', 'en', 'The speed of a drone hull on the field, in field units per second.', 1, now(), 'production_seed'),
  ('production.characteristic.char_drone_speed.unit', 'uk', 'од. поля/с', 1, now(), 'production_seed'),
  ('production.characteristic.char_drone_speed.unit', 'en', 'field units/s', 1, now(), 'production_seed'),
  ('combat.target_rule.target_rule_1.name', 'uk', 'Найближча', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_1.name', 'en', 'Nearest', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_1.description', 'uk', 'Обирає найближчого супротивника.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_1.description', 'en', 'Chooses the closest opponent.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_2.name', 'uk', 'Найслабша', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_2.name', 'en', 'Weakest', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_2.description', 'uk', 'Обирає супротивника з найменшою міцністю.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_2.description', 'en', 'Chooses the opponent with the least integrity.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_3.name', 'uk', 'Носії', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_3.name', 'en', 'Carriers', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_3.description', 'uk', 'Обирає корпуси, що випускають дрони.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_3.description', 'en', 'Chooses hulls that launch drones.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_4.name', 'uk', 'Дрони', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_4.name', 'en', 'Drones', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_4.description', 'uk', 'Обирає дрони.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_4.description', 'en', 'Chooses drones.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_5.name', 'uk', 'Найсильніша', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_5.name', 'en', 'Strongest', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_5.description', 'uk', 'Обирає супротивника з найбільшою міцністю.', 1, now(), 'combat_seed'),
  ('combat.target_rule.target_rule_5.description', 'en', 'Chooses the opponent with the most integrity.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_1.name', 'uk', 'Зближення', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_1.name', 'en', 'Approach', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_1.description', 'uk', 'Зближується з ціллю.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_1.description', 'en', 'Moves closer to the chosen opponent.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_2.name', 'uk', 'Тримати дистанцію', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_2.name', 'en', 'Keep distance', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_2.description', 'uk', 'Тримає дистанцію до цілі.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_2.description', 'en', 'Stays at a distance from the chosen opponent.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_3.name', 'uk', 'Стояти', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_3.name', 'en', 'Stand', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_3.description', 'uk', 'Лишається на місці.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_3.description', 'en', 'Stays in place.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_4.name', 'uk', 'Супровід', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_4.name', 'en', 'Escort', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_4.description', 'uk', 'Супроводжує союзний корпус.', 1, now(), 'combat_seed'),
  ('combat.behavior.behavior_4.description', 'en', 'Follows an allied hull.', 1, now(), 'combat_seed')
ON CONFLICT (key, language) DO NOTHING;

COMMIT;
