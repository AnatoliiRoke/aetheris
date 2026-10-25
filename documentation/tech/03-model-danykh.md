# AETHERIS — Модель даних

> **Статус документа:** чернетка v0.2 (24.10.2026), оновлено під обраний стек (документ 02)
> **Позначення:** ✅ — підтверджено автором (зокрема в попередніх документах); 💡 — пропозиція; ❓ — відкрите питання.
> Пов'язані документи: [01 — Архітектура](01-arkhitektura.md), [04 — API](04-api.md), документи `game-design/`.
> ✅ База даних — PostgreSQL (документ 02); доступ через EF Core, гарячі запити через Dapper (💡). Сама модель — 💡 пропозиція. Назви таблиць англійською, як прийнято в коді.

---

## 1. Принципи (💡)

- **Довідники** (типи кораблів, модулів, будівель, технологій, ресурсів, рецепти) — у конфігу балансу, а не в коді. Таблиці гравців посилаються на них за ідентифікатором (`hull_type = 'light_fighter'`).
- ✅ Тип корабля має **технічний ідентифікатор**, назва залежить від раси (документ 05).
- Ресурси зберігаються «ліниво»: кількість + швидкість + час останнього оновлення (документ 01).
- ✅ **Усі налаштування, множники й коефіцієнти гри живуть у базі даних, а не в коді** (автор, 25.10.2026): параметри формул (документ 14), вартість і час, характеристики кораблів і модулів, бонуси рас, коефіцієнти щитів та урону, ринкові комісії, ліміти, пороги, ймовірності. У коді лише **структура формул**; числа в таблицях конфігурації. Це дає змогу підтягувати баланс прогонами без релізу.
- ✅ **Жодного хардкоду** (автор, 25.10.2026): у коді правил гри не буває «магічних» чисел, порогів, відсотків, лімітів, цін, часів і списків значень балансу. 💡 Як це гарантуємо: правила читають значення лише через єдиний модуль конфігурації; перевірка в CI (аналізатор «магічних чисел» у модулях правил) і автотест, що значення беруться з конфігурації (tech/06); правило для агентів: нова механіка не приймається, поки її параметри не додані в конфігурацію й довідники з початковими значеннями.
- ✅ Конфігурація **тримається в пам'яті сервера** для швидкості: читання з бази лише при старті й після зміни. 💡 Механізм: версія конфігурації; зміна в адмінці проходить перевірку (межі значень, зв'язність) → записується в базу з номером версії й журналом (хто, що, старе/нове) → сервер перезавантажує кеш і підміняє його атомарно (без зупинки); події, що виконуються під час підміни, беруть одну версію. 💡 Є відкат до попередньої версії, і конфігурація кожного бою/угоди запам'ятовує версію, за якою рахувалась (відтворюваність, tech/01 §3.3).
- Усі гроші й ресурси — **цілі числа** (без дробів), щоб не було помилок округлення.
- 💡 Гарячі дані (активні планети, флоти, бої) сервер тримає в пам'яті, а в базу пише пакетами раз на 1–2 секунди; критичні дії (оплата, грабунок, торгівля) — відразу в транзакції (документ 01, розділ 3.4).

---

## 2. Основні сутності

```
Player ─┬─ Planet ─┬─ Building
        │          ├─ PlanetStorage (ресурси)
        │          └─ ProductionQueue
        ├─ Research
        ├─ ShipDesign (проєкт) ── DesignSlot (модуль у слоті)
        ├─ Fleet ─┬─ Ship (кожен корабель окремо: проєкт, стан, стан модулів)
        │         └─ Commander
        ├─ PartsInventory (деталі на складі)
        ├─ Artifact
        ├─ SpaceStructure ── StructureModule
        ├─ DefenseScenario
        └─ ClanMember ── Clan ─┬─ ClanWarehouse
                               └─ DiplomacyRelation
Galaxy ── Sector ── StarSystem ─┬─ Planet
                                ├─ AsteroidField / Nebula
                                └─ SpaceStructure
MarketOrder, Convoy, Battle ── BattleCycle ── BattleReport
ScheduledEvent (усі таймери)
```

---

## 3. Таблиці (💡 основні поля)

### 3.1. Гравець і клан
| Таблиця | Поля |
|---|---|
| `player` | id, name, race (✅ незмінна), created_at, newbie_protection_until, rating_total, rating_parts, credits, trade_currency, premium_currency, guild_id, language |
| `clan` | id, name, tag, level (✅ рівень впливає на ліміт учасників), member_limit |
| `clan_member` | clan_id, player_id, role, joined_at |
| `clan_warehouse` | clan_id, resource/part, amount ✅ |
| `diplomacy_relation` | clan_a, clan_b, type (✅ 7 видів), started_at, min_until, last_battle_at (✅ війна закінчується через 7 днів без боїв) |

### 3.2. Світ
| Таблиця | Поля |
|---|---|
| `sector` | id, galaxy_id, name, x, y, is_newbie |
| `star_system` | id, sector_id, x, y, star_type, in_nebula |
| `planet` | id, system_id, `sector_id` (денормалізовано), orbit, type, size, owner_id, deposits (метали, кристал), population_max, shield_until |
| `asteroid_field` | id, system_id, kind, reserves, expires_at |
| `nebula` | id, sector_id, shape, resources |

### 3.3. Економіка
| Таблиця | Поля |
|---|---|
| `planet_resource` | planet_id, resource, amount_at_t, rate_per_hour, cap, t |
| `building` | planet_id, slot, type, level (✅ до 20) |
| `queue_item` | id, owner, queue_type (будівля / завод / верф / дослідження), target, finishes_at |
| `research` | player_id, tech, level |
| `parts_inventory` | planet_id, part_type, mk (✅ I–III), amount |

### 3.4. Флот
| Таблиця | Поля |
|---|---|
| `ship_design` | id, player_id, name, hull_type, race_extra_slot |
| `design_slot` | design_id, slot_index, part_type, mk |
| `fleet` | id, player_id, `sector_id` (денормалізовано), commander_id, location, order, target, arrives_at, cargo, fuel |
| `ship` | id, fleet_id, design_id, hp, shield, ammo, `module_state` (✅ кожен корабель окремо: у бою модулі можуть бути пошкоджені чи знищені; 💡 зберігаємо лише відхилення від проєкту: бітова маска або короткий JSON зі списком пошкоджених/знищених модулів) |
| `commander` | id, player_id, level, xp, skills |
| `defense_scenario` | player_id, rules (JSON), template |

### 3.5. Ринок і бій
| Таблиця | Поля |
|---|---|
| `market_order` | id, seller_id, market_level, item, amount, price, currency, created_at |
| `convoy` | id, order_id, from, to, arrives_at, escort_fleet_id |
| `currency_rate` | time, rate (✅ плаваючий), commission |
| `battle` | id, `sector_id` (денормалізовано), location, sides, cycle, next_cycle_at, status, rng_seed |
| `battle_order` | battle_id, player_id, cycle, orders (JSON) |
| `battle_report` | battle_id, cycle, log (JSON), losses, loot |

### 3.6. Події
| Таблиця | Поля |
|---|---|
| `scheduled_event` | id, run_at, type, payload (JSON), status |
| `player_event` | player_id, seq, type, payload (JSON), created_at — 💡 події для гравця з номерами; зберігаємо короткий час для докачування після обриву зв'язку (документ 04, розділ 5) |

### 3.7. Адмінка
| Таблиця | Поля |
|---|---|
| `admin_account` | id, login, roles, two_factor_enabled, created_at (💡 окремо від `player`) |
| `admin_audit_log` | id, admin_id, action, target, old_value, new_value, at (💡 журнал не редагується) |

💡 Індекс за `(sector_id, status, run_at)` (✅ узгоджено з tech/07; `sector_id` є і в подій; у пам'яті сервера додатково тримаємо мапу «об'єкт → сектор»): планувальник бере найближчі невиконані події.

---

## 4. Довідники й конфігурація балансу (✅ у базі й в пам'яті; структура таблиць 💡)

💡 Таблиці: `config_version` (номер, автор, час, статус), `config_parameter` (ключ, значення, межі, версія), а також довідники нижче. Усе це кешується в пам'яті сервера (розділ 1).

| Довідник | Приклад |
|---|---|
| `resources` | iron, titanium, copper, iridium, quartzite, rubium, sapphirite, obsidian, deuterium, plasma, antimatter, ether |
| `produced` | building_materials, optical_fiber, microprocessors, composites, energy_rods ✅ |
| `hull_types` | 14 типів ✅ + цивільні; слоти, HP, екіпаж |
| `ship_names` | (race, hull_type) → ключ перекладу |
| `parts` | модулі й компоненти: слот, енергія, екіпаж, параметри за Mk |
| `buildings`, `techs`, `recipes` | вартість, час, ефекти |

---

## 5. Відкриті питання

1. ✅ Ліниве зберігання ресурсів (автор, 24.10.2026): `кількість_T + швидкість + ліміт + час T`; поточне значення рахується за формулою `min(ліміт, кількість_T + швидкість × (зараз − T))` лише коли воно потрібне (планету відкрили, атакують, щось будується), без нарахування кроками і без обходу всіх планет. Запис у базу тільки при зміні швидкості чи ліміту (нова шахта, грабунок, витрата). 💡 Окремо: страховий знімок гарячого стану в базу кожні кілька хвилин для відновлення після збою (автор згадував інтервал 10–15 хв, уточнюється).
2. ✅ Кожен корабель окремо (автор, 24.10.2026: у бою пошкоджуються й знищуються модулі). ~~Кораблі групами~~ у флоті (тип + проєкт + кількість) замість кожного корабля окремо: так? Це значно легше для бази даних.
3. ✅ Довідники (час, ціна, характеристики, стан, опис, зображення) у базі даних, а не в коді (автор, 24.10.2026). 💡 Опис і назва зберігаються як ключі локалізації (гра багатомовна, ✅), зображення як шлях до файлу на CDN; сервер завантажує довідники в пам'ять при старті й оновлює після змін в адмінці.
