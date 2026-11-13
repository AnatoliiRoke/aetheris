# Задача 00062 — Бої: дані, характеристики й налаштування

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12.11.2026):** з узгодженої логіки боїв зробити кілька докладних задач. Це перша: усе, що бою потрібно в даних, до будь-якого коду бою.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) v1.0, формули [14 §5](../game-design/14-balans-i-formuly.md), модулі [05a](../game-design/05a-moduli-ta-komponenty.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/03-model-danykh.md`.
> **Порядок задач боїв:** **00062** дані → [00071](00071-boiovyi-poriadok-flotu.md) бойовий порядок флоту → [00063](00063-rushii-boiu-symuliator.md) рушій і симулятор → [00064](00064-pochatok-boiu-atakuvaty-ta-perekhoplennia.md) початок бою → [00065](00065-tsykly-boiu-na-serveri.md) цикли на сервері → [00066](00066-naslidky-boiu-remont-ulamky-zvit.md) наслідки → [00067](00067-stsenarii-oborony-oflain.md) сценарій офлайн → [00068](00068-dyzain-ekrana-boiu.md) дизайн → [00069](00069-ekran-boiu-v-kliienti.md) екран бою → [00070](00070-ekran-stsenariiu-ta-zvitu-boiu.md) сценарій і звіт у клієнті.

## 1. Як є зараз (перевірено 12.11.2026)

1. Характеристики деталей — довідник `characteristics` у `server/src/Aetheris.Production/Data/production-reference-seed.json`; логічні види в `server/src/Aetheris.Production/Api/ProductionCharacteristicKinds.cs`.
2. Для бою вже є: `char_damage`, `char_rate`, `char_range`, `char_magazine`, `char_flight_speed`, `char_integrity`, `char_shield_capacity`, `char_shield_regen`, `char_shield_restore`, `char_energy_block`, `char_kinetic_block`, `char_detection`, `char_speed`, `char_missiles`, `char_power`, `char_energy_use`, `char_hull_size`. Атрибут зброї `damage_kind` (`energy` / `kinetic`), атрибут корпусу `hull_role` (`combat`, `drone`, `transport`, `civil`).
3. **Класи корпусів уже є** — групи корпусів у `part_groups` сіду (`is_hull_group = true`, батько `pg_20` «Корпуси кораблів»): `pg_12` Легкі, `pg_13` Дрони, `pg_14` Середні, `pg_15` Важкі, `pg_16` Надважкі, `pg_17` Цивільні; кожен корпус належить своїй групі (`parts[].group`).
4. **Немає:** влучності зброї проти класу корпусу цілі, міцності модуля в бою, сигнатури й рівня маскування, радіуса повідка дронів, пріоритету вимкнення модулів у проєкті, налаштувань бою (тривалості, ліміти, частки).
5. Модуль корабля вже має справність: `ShipModuleEntity.ConditionPercent` і `ConditionCode` (`intact` / `damaged` / `destroyed`) у `server/src/Aetheris.Fleet/Data/Entities/FleetEntities.cs`.
6. Налаштування гри читаються через `IGameConfig`; ключі флоту — `server/src/Aetheris.Fleet/Configuration/FleetConfigKeys.cs` і `FleetSettings.cs`.

## 2. Рішення

1. **Клас цілі = наявний клас корпусу** (група корпусу, розділ 1.3). Нового довідника класів **не створюємо**: влучність зброї прив'язується до групи корпусу цілі. Нова група корпусів у майбутньому автоматично отримує свій рядок влучності (типове значення — налаштування `combat.accuracy_default`).
2. **Влучність проти класу корпусу** — нова характеристика-таблиця зброї `char_accuracy` з одним значенням на кожну групу корпусів (частка 0…1). Стартові значення: легкі лазери й скорострільні — високі проти дронів і легких, низькі проти важких і надважких; торпеди й важкі гармати — навпаки; середні — рівні; цивільні — як легкі. Числа підбирає симулятор (00063).
3. **Міцність модуля в бою** — кожен встановлений модуль має `char_integrity` (уже є в броні й корпусі; додати всім модулям, що ставляться на корабель). Модуль з нульовою міцністю стає `destroyed`.
4. **Сигнатура й маскування** — характеристика корпусу `char_signature` і модуля маскування `char_signature_cut` (частка зменшення); детектор — `char_detection` (уже є). Видимість у бою: дальність виявлення × сигнатура цілі.
5. **Дрони:** `char_leash_radius` у носія (береться з корпусу носія, сід) і `char_drone_speed` у дрона.
6. **Пріоритет вимкнення модулів** (06 §7.1) — нове поле `power_priority` у слоті проєкту (`ShipDesignSlotEntity`), ціле, менше = вимикається пізніше. Конструктор пропонує типовий порядок із довідника груп модулів (поле `default_power_priority` у групі), гравець може змінити.
7. **Налаштування бою** (`IGameConfig`, новий клас ключів `CombatConfigKeys` у новому модулі `Aetheris.Combat`, див. 00063):

| Ключ | Старт | Звідки |
|---|---|---|
| `combat.deploy_seconds` | 300 | 06 §3 |
| `combat.pause_seconds` | 420 | 06 §3 (5–10 хв) |
| `combat.segment_seconds` | 60 | 06 §3 |
| `combat.tick_ms` | 250 | крок розрахунку відрізку |
| `combat.max_cycles` | 6 | 06 §8 |
| `combat.shield_restore_share` | 0.30 | 06 §7.2 |
| `combat.module_damage_share` | 0.25 | 06 §7 (частка шкоди в модуль після броні) |
| `combat.field_width`, `combat.field_height` | 20000, 12000 | 06 §2 (одиниці поля) |
| `combat.deploy_zone_depth` | 3000 | 06 §2 |
| `combat.asteroid_detection_mult`, `combat.asteroid_hit_mult`, `combat.asteroid_speed_mult` | 0.6, 0.7, 0.5 | 06 §2.1 |
| `combat.accuracy_default` | 0.5 | влучність по новій групі корпусів |
| `combat.distance_falloff`, `combat.target_speed_falloff` | 0.5, 0.5 | 14 §5.1 |

8. **Довідники бою** (сід + адмінка за універсальними описами полів 00023): правила пріоритету цілей (`target_rule_N`: найближчі, найслабші, носії, дрони, найсильніші), поведінки ескадрильї (`behavior_N`: зближатися, тримати дистанцію, стояти, супроводжувати). Логіку кожного коду визначає програма (логічний код), назви — тексти.

## 3. Що зробити

1. Сід Production: характеристика `char_accuracy` (з розбиттям за групою корпусу цілі — окрема таблиця `part_accuracy(part_type_id, target_part_group_id, value)`, лише групи з `is_hull_group`), `char_signature`, `char_signature_cut`, `char_leash_radius`, `char_drone_speed`; `char_integrity` для всіх корабельних модулів; `default_power_priority` у групах модулів. Тексти — `admin-reference-texts.json` і i18n клієнта.
2. Міграція Production: таблиця `part_accuracy`; міграція Fleet: колонка `power_priority` у `ship_design_slot` з типовим значенням із групи.
3. `ShipModuleDto` / каталог кораблів (`ShipCatalogDtos.cs`, `ShipCatalogService.cs`) повертають нові характеристики й групу корпусу одним запитом (кеш каталогу, без N+1).
4. Конструктор (сервер, `ShipDesignService.cs`): збереження й перевірка `power_priority`; клієнт конструктора — поле порядку вимкнення (готовий компонент списку з перетягуванням, токени `tokens.css`).
5. Адмінка: нові характеристики й довідники видно й редагуються (універсальні описи полів).
6. Налаштування бою — записи в базі конфігурації з таблиці розділу 2.7.
7. База `aetheris_dev`: ті самі зміни скриптом, бо сід дописує лише відсутні рядки.

## 4. Критерії приймання

1. Усі значення з розділу 2 лежать у базі й адмінці; у коді немає жодного з цих чисел (аналізатор «магічних чисел» чистий).
2. Каталог кораблів віддає для кожного модуля влучність за класами, міцність, сигнатуру; для корпусу — його група (клас).
3. Проєкт корабля зберігає порядок вимкнення модулів; старі проєкти отримують типовий порядок міграцією.
4. Тести: сід застосовується двічі без дублів; каталог з новими полями; перевірка `power_priority`.
5. `dotnet build -c Release`, `dotnet test -c Release`, `dotnet format --verify-no-changes --no-restore`, у `client/` lint і build — зелені.

## 5. Результат виконання

_(заповнює виконавець)_
