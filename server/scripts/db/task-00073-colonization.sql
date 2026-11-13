-- Apply to the existing development database after deploying task 00073.
-- The seed of the modules adds the missing rows by itself, but the seed runs only on a database
-- the server starts for the first time, so the rows that joined the starting data later are added
-- by this script.
BEGIN;

-- The table of the types of the bodies that suit a race (task 00073).
CREATE TABLE IF NOT EXISTS race_colonizable_type (
    race_id int NOT NULL,
    object_type_id int NOT NULL,
    CONSTRAINT pk_race_colonizable_type PRIMARY KEY (race_id, object_type_id),
    CONSTRAINT fk_race_colonizable_type_race_race_id FOREIGN KEY (race_id) REFERENCES race (id) ON DELETE CASCADE,
    CONSTRAINT fk_race_colonizable_type_celestial_object_type_object_type_id FOREIGN KEY (object_type_id) REFERENCES celestial_object_type (id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_race_colonizable_type_object_type_id ON race_colonizable_type (object_type_id);

DO $$
BEGIN
  IF (SELECT count(*) FROM race WHERE code IN ('race_1', 'race_2', 'race_3', 'race_4')) <> 4
     OR (SELECT count(*) FROM celestial_object_type WHERE is_colonizable) < 5 THEN
    RAISE EXCEPTION 'Colonization reference prerequisites are missing';
  END IF;
END $$;

-- The types of the bodies that suit every race (task 00073): one type a race, by the data of the
-- documentation. The oceanic type suits no race yet, and the administrator may give it to one.
INSERT INTO race_colonizable_type (race_id, object_type_id)
SELECT r.id, t.id
FROM (VALUES
  ('race_1', 'object_type_5'),
  ('race_2', 'object_type_7'),
  ('race_3', 'object_type_6'),
  ('race_4', 'object_type_8')
) AS v(race_code, type_code)
JOIN race r ON r.code = v.race_code
JOIN celestial_object_type t ON t.code = v.type_code
ON CONFLICT (race_id, object_type_id) DO NOTHING;

-- The colony module is a cargo (task 00073): it is never fitted to a hull and it opens the order of
-- the colonization.
INSERT INTO part_attribute (part_type_id, attribute_key, code_value)
SELECT p.id, 'cargo_only', 'true' FROM part_type p WHERE p.code = 'part_m_70'
ON CONFLICT (part_type_id, attribute_key) DO UPDATE SET code_value = EXCLUDED.code_value WHERE part_attribute.code_value IS DISTINCT FROM EXCLUDED.code_value;

INSERT INTO part_attribute (part_type_id, attribute_key, code_value)
SELECT p.id, 'cargo_order', 'colonize' FROM part_type p WHERE p.code = 'part_m_70'
ON CONFLICT (part_type_id, attribute_key) DO UPDATE SET code_value = EXCLUDED.code_value WHERE part_attribute.code_value IS DISTINCT FROM EXCLUDED.code_value;

-- The limit of the bodies of a player (task 00073): the effect of the colonial administration gives
-- the largest number of the bodies, the home body among them. The value of a level is absolute:
-- the levels 1 and 2 give one body, the level 3 gives two, and so on up to twelve at the level 25.
DELETE FROM technology_level_effect e
USING technology_level l, technology t
WHERE e.technology_level_id = l.id AND l.technology_id = t.id AND t.code = 'tech_17' AND e.effect_type = 'planet_limit';

INSERT INTO technology_level_effect (technology_level_id, effect_type, unlock_kind, unlock_target_id, value)
SELECT l.id, 'planet_limit', NULL, NULL, v.planets
FROM technology_level l
JOIN technology t ON t.id = l.technology_id
JOIN (VALUES
  (1, 1::numeric), (2, 1), (3, 2), (4, 2), (5, 2), (6, 3), (7, 3), (8, 4), (9, 4), (10, 5), (11, 5), (12, 6),
  (13, 6), (14, 7), (15, 7), (16, 7), (17, 8), (18, 8), (19, 9), (20, 9), (21, 10), (22, 10), (23, 11), (24, 11), (25, 12)
) AS v(level, planets) ON v.level = l.level
WHERE t.code = 'tech_17';

-- The texts of the new attributes, of the new order of a cargo and of the new effect (task 00073).
INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
VALUES
('admin.reference.attribute.cargo_only', 'uk', 'Лише вантаж', 1, now(), 'seed'),
('admin.reference.attribute.cargo_only', 'en', 'Cargo only', 1, now(), 'seed'),
('admin.reference.attribute.cargo_order', 'uk', 'Наказ вантажу', 1, now(), 'seed'),
('admin.reference.attribute.cargo_order', 'en', 'Cargo order', 1, now(), 'seed'),
('admin.reference.cargo_order.colonize', 'uk', 'Колонізація', 1, now(), 'seed'),
('admin.reference.cargo_order.colonize', 'en', 'Colonization', 1, now(), 'seed'),
('admin.reference.technology_effect.planet_limit', 'uk', 'Ліміт планет', 1, now(), 'seed'),
('admin.reference.technology_effect.planet_limit', 'en', 'Planet limit', 1, now(), 'seed')
ON CONFLICT (key, language) DO UPDATE SET text = EXCLUDED.text, version = localization_text.version + 1, updated_at = now()
WHERE localization_text.text IS DISTINCT FROM EXCLUDED.text;

COMMIT;
