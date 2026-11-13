# Задача 00062 — Бої: дані, характеристики й налаштування

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12.11.2026, 13.11.2026):** з узгодженої логіки боїв зробити докладні технічні задачі. Це перша: усе, що бою потрібно в даних, до будь-якого коду бою.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) v1.0, формули [14 §5](../game-design/14-balans-i-formuly.md), модулі [05a](../game-design/05a-moduli-ta-komponenty.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/03-model-danykh.md`.
> **Порядок задач боїв:** **00062** дані → [00071](00071-boiovyi-poriadok-flotu.md) бойовий порядок → [00063](00063-rushii-boiu-symuliator.md) рушій → [00064](00064-pochatok-boiu-atakuvaty-ta-perekhoplennia.md) початок бою → [00065](00065-tsykly-boiu-na-serveri.md) цикли → [00066](00066-naslidky-boiu-remont-ulamky-zvit.md) наслідки → [00067](00067-stsenarii-oborony-oflain.md) сценарій офлайн → [00068](00068-dyzain-ekrana-boiu.md) дизайн → [00069](00069-ekran-boiu-v-kliienti.md) екран бою → [00070](00070-ekran-stsenariiu-ta-zvitu-boiu.md) сценарій і звіт у клієнті.

## 1. Як є зараз (перевірено 13.11.2026)

1. Характеристики деталей — довідник `characteristics` у `server/src/Aetheris.Production/Data/production-reference-seed.json`; логічні види — `server/src/Aetheris.Production/Api/ProductionCharacteristicKinds.cs`.
2. Для бою вже є: `char_damage`, `char_rate`, `char_range`, `char_magazine`, `char_flight_speed`, `char_integrity` (броня й корпус), `char_shield_capacity`, `char_shield_regen`, `char_shield_restore`, `char_energy_block`, `char_kinetic_block`, `char_detection`, `char_speed`, `char_missiles`, `char_power`, `char_energy_use`, `char_hull_size`, `char_collect_speed`. Атрибут зброї `damage_kind` (`energy` / `kinetic`), атрибут корпусу `hull_role` (`combat`, `drone`, `transport`, `civil`).
3. **Класи корпусів уже є** — групи корпусів у `part_groups` сіду (`is_hull_group = true`, батько `pg_20`): `pg_12` Легкі, `pg_13` Дрони, `pg_14` Середні, `pg_15` Важкі, `pg_16` Надважкі, `pg_17` Цивільні; корпус належить групі через `parts[].group`. Нових класів не вводимо.
4. **Немає:** влучності зброї проти групи корпусу цілі, міцності кожного модуля в бою, сигнатури й маскування, радіуса повідка й швидкості дронів, пріоритету вимкнення модулів, налаштувань бою, довідників правил цілей і поведінок.
5. Справність модуля — `ShipModuleEntity.ConditionPercent` (int) і `ConditionCode` (`ShipModuleConditions`: `intact` / `damaged` / `destroyed`), `server/src/Aetheris.Fleet/Data/Entities/FleetEntities.cs`. Слот проєкту — `ShipDesignSlotEntity` (`DesignId`, `SlotIndex`, `PartTypeId`, `MarkLevel`, `HasRaceBonus`), таблиця `ship_design_slot` з унікальним `(design_id, slot_index)`.
6. Налаштування гри — `IGameConfig` (`GetInt64/GetDouble/GetBoolean`, значення в `config_parameter`); ключі модуля — константи в `*ConfigKeys.cs`, значення — клас `*Settings` (`FleetSettings.cs`), визначення з межами й описами додаються в сід хоста (так само, як `FleetConfigKeys`).
7. Міграції — `server/src/Aetheris.<Модуль>/Data/Migrations` (останні Fleet: `20261111083741_ShipPartCargo`); імена таблиць і колонок `snake_case` (`SnakeCaseNames.cs`); без зовнішніх ключів у таблиці інших модулів (tech/08 правило 5).

## 2. Рішення

### 2.1 Клас цілі = наявна група корпусу

Влучність зброї прив'язується до **групи корпусу цілі** (`pg_12…pg_17`). Нова група корпусів автоматично отримує `combat.accuracy_default`, доки адмін не задасть рядок.

### 2.2 Нова таблиця Production: `part_accuracy`

| Колонка | Тип | Обмеження |
|---|---|---|
| `part_type_id` | `integer` | частина PK; зброя |
| `target_part_group_id` | `integer` | частина PK; група з `is_hull_group = true` |
| `value` | `double precision` | `CHECK (value >= 0 AND value <= 1)` |

PK `(part_type_id, target_part_group_id)`, індекс `ix_part_accuracy_target (target_part_group_id)`. Сутність `PartAccuracyEntity` у `ProductionDbContext`. Стартові значення (початок для симулятора 00063, далі баланс): легкі лазери й скорострільні 0.9 / 0.8 / 0.6 / 0.35 / 0.2 проти `pg_13 / pg_12 / pg_14 / pg_15 / pg_16`; торпеди й важкі гармати 0.1 / 0.2 / 0.5 / 0.85 / 0.95; середні рівно 0.6; проти `pg_17` — як проти `pg_12`.

### 2.3 Нові характеристики

| Код | Назва | Одиниця | Де | Сенс |
|---|---|---|---|---|
| `char_accuracy` | Влучність | 0…1 | зброя, через `part_accuracy` | 06 §6.1 |
| `char_signature` | Сигнатура | множник | корпус | множник дальності виявлення цього корабля (1 = стандарт), 06 §5 |
| `char_signature_cut` | Маскування | 0…1 | модуль маскування | зменшує сигнатуру |
| `char_leash_radius` | Радіус повідка | од. поля | корпус носія | 06 §7.3 |
| `char_drone_speed` | Швидкість дрона | од./с | дрон | 06 §7.3 |
| `char_integrity` | Міцність | од. | **усі** корабельні модулі | модуль на нулі → `destroyed` |

`ProductionCharacteristicKinds` отримує константи `Accuracy`, `Signature`, `SignatureCut`, `LeashRadius`, `DroneSpeed`.

### 2.4 Пріоритет вимкнення модулів

1. `ship_design_slot.power_priority` — `smallint NOT NULL DEFAULT 100`, `CHECK (power_priority BETWEEN 0 AND 1000)`; менше — вимикається **пізніше** (06 §7.1).
2. `part_groups.default_power_priority` (`smallint`, сід): двигуни й реактори 0, щити 10, зброя 20, радар 30, ремонт 40, решта 50.
3. Конструктор підставляє типове групи; гравець змінює перетягуванням. Міграція Fleet заповнює наявні слоти **одним `UPDATE … FROM`** (без циклу в коді).

### 2.5 Налаштування бою (`config_parameter`)

Ключі — у `CombatConfigKeys`, значення — `CombatSettings.FromConfig(IGameConfig)`. Тип: `i` ціле, `d` дробове.

| Ключ | Тип | Старт | Межі | Опис (укр., у сід-визначенні) |
|---|---|---|---|---|
| `combat.deploy_seconds` | i | 300 | 30–3600 | Тривалість фази розстановки, ігрові секунди |
| `combat.pause_seconds` | i | 420 | 60–1800 | Тривалість паузи на накази |
| `combat.segment_seconds` | i | 60 | 10–300 | Тривалість відрізку, що рахує рушій |
| `combat.tick_ms` | i | 250 | 50–1000 | Крок розрахунку |
| `combat.track_step_seconds` | i | 10 | 1–60 | Крок точок сліду руху для клієнта |
| `combat.max_cycles` | i | 6 | 1–50 | Ліміт циклів |
| `combat.shield_restore_share` | d | 0.30 | 0–1 | Частка насичення щита, що повертається в кінці циклу |
| `combat.module_damage_share` | d | 0.25 | 0–1 | Частка шкоди, що після броні йде в модуль |
| `combat.field_width`, `combat.field_height` | i | 20000, 12000 | 2000–200000 | Розмір поля |
| `combat.deploy_zone_depth` | i | 3000 | 100–50000 | Глибина зони розстановки |
| `combat.asteroid_detection_mult`, `…_hit_mult`, `…_speed_mult` | d | 0.6, 0.7, 0.5 | 0.05–1 | Множники в астероїдному полі |
| `combat.accuracy_default` | d | 0.5 | 0–1 | Влучність по групі без рядка |
| `combat.distance_falloff` | d | 0.5 | 0–1 | Спад влучності на межі дальності |
| `combat.target_speed_falloff` | d | 0.5 | 0–1 | Спад влучності від швидкості цілі |
| `combat.squadron_max` | i | 12 | 1–50 | Ескадрилей у флоті |
| `combat.formation_max` | i | 5 | 1–20 | Формацій у флоті |
| `combat.debris_share` | d | 0.20 | 0–1 | Частка ресурсів знищеного корабля в уламках |
| `combat.commander_xp_per_battle` | i | 0 | 0–1000 | Досвід командира за бій |
| `combat.order_interval_seconds` | i | 2 | 0–60 | Мінімум між наказами одного гравця (захист від спаму) |
| `combat.scenario_rule_max` | i | 12 | 1–50 | Правил «якщо → то» у сценарії |
| `combat.force_damage_weight`, `combat.force_defense_weight` | d | 1.0, 1.0 | 0–100 | Ваги в «силі сторони» |

### 2.6 Довідники бою (сід, нейтральні коди, назви лише в `localization_text`)

Файл `server/src/Aetheris.Combat/Data/combat-reference-seed.json`, таблиці створює міграція 00063.

| Довідник | Коди | `logic_code` у програмі |
|---|---|---|
| `target_rule` | `target_rule_1…5` | `nearest`, `weakest`, `carriers`, `drones`, `strongest` |
| `behavior` | `behavior_1…4` | `approach`, `keep_distance`, `stand`, `escort` |

Колонки обох таблиць: `id int PK`, `code text UNIQUE`, `logic_code text NOT NULL UNIQUE`, `sort_order int NOT NULL`, `is_archived bool NOT NULL DEFAULT false`. Код не перевикористовується. Типи в `localization_text`: `target_rule`, `behavior`; реєструються в `admin-reference-texts.json`.

## 3. Що зробити

1. **Сід Production:** шість характеристик (2.3); `char_integrity` усім корабельним модулям; `default_power_priority` у групах; рядки `part_accuracy`; `char_signature` корпусам (стартово: дрони 0.2, легкі 0.6, середні 1.0, важкі 1.6, надважкі 2.2, цивільні 1.0); `char_leash_radius` носіям; `char_drone_speed` дронам. Сід дописує лише відсутнє.
2. **Міграція Production** `AddPartAccuracy`: таблиця 2.2 і колонка групи. Команда: `dotnet ef migrations add AddPartAccuracy --project server/src/Aetheris.Production --startup-project server/src/Aetheris.Host`.
3. **Міграція Fleet** `AddPowerPriority`: колонка 2.4; типовий порядок бере з файлу `power-priority-defaults.json` (карта `part_type_id → пріоритет`, генерується з сіду), щоб міграція не читала таблиць Production.
4. **Каталог:** у `ShipCatalogDtos.cs` — `ShipModuleDto.Accuracy: IReadOnlyDictionary<int,double>` (ключ `target_part_group_id`), `Integrity`, `SignatureCut`, `DefaultPowerPriority`; `ShipHullDto.PartGroupId`, `Signature`, `LeashRadius`. `ShipCatalogService` заповнює їх **двома пакетними запитами** (`part_accuracy WHERE part_type_id = ANY(@ids)`, характеристики) з `AsNoTracking`; результат у наявному кеші каталогу, інвалідація при зміні довідника.
5. **Конструктор (сервер):** `ShipDesignService` приймає `slots[].powerPriority`; поза 0…1000 — `GameException(FleetErrorCodes.PowerPriorityInvalid = "fleet_power_priority_invalid")`; без значення — типове групи.
6. **Конструктор (клієнт):** блок «Порядок вимкнення при нестачі енергії» у редакторі проєкту; готовий компонент списку з перетягуванням; тексти в `client/src/texts`; лише токени.
7. **Налаштування:** порожній проєкт `server/src/Aetheris.Combat` (+ у `Aetheris.slnx`) з `CombatConfigKeys.cs` і `CombatSettings.cs`; визначення ключів (тип, старт, межі, опис) додаються до сіду хоста поруч з `FleetConfigKeys`. Наповнення модуля — 00063.
8. **Адмінка:** характеристики, довідники й `part_accuracy` (вкладений список у картці зброї) — через універсальні описи полів (00023).
9. **База `aetheris_dev`:** сід дописує лише відсутнє, тож наявні деталі оновлюються разовим скриптом `server/tools/db/00062-update-dev.sql`; автор запускає сам.

## 4. Критерії приймання

1. Усі значення розділів 2.2–2.6 лежать у базі й адмінці; у коді немає жодного з цих чисел (тест архітектури «без магічних чисел» чистий).
2. `GET /fleet/ship-catalog` віддає для модуля `accuracy`, `integrity`, `signatureCut`; для корпусу `partGroupId`, `signature`, `leashRadius`; кількість запитів не залежить від кількості деталей (тест лічильника запитів).
3. Проєкт зберігає `power_priority`; старі проєкти після міграції мають типовий порядок; значення поза межами відхиляється.
4. Тести (`server/tests/Aetheris.Tests/Production`, `…/Fleet`): сід двічі без дублів; `part_accuracy` не приймає групу без `is_hull_group`; каталог з новими полями; збереження й перевірка `power_priority`; міграція заповнює порядок; `CombatSettings.FromConfig` читає всі ключі й падає на значення поза межами.
5. `dotnet build -c Release`, `dotnet test -c Release`, `dotnet format --verify-no-changes --no-restore`, у `client/` `npm run lint` і `npm run build` — зелені.

## 5. Результат виконання

_(заповнює виконавець)_
