# Задача 00064 — Початок бою: наказ «Атакувати» і ставка «Перехоплювати»

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12–13.11.2026):** задачі боїв з узгодженої логіки. Ця — як бій починається.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) §9, §9.1.
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/07-planuvalnyk-podii.md`, `documentation/tech/04-api.md`.
> **Залежить від:** [00063](00063-rushii-boiu-symuliator.md), [00071](00071-boiovyi-poriadok-flotu.md), польотний лист [00050](00050-politnyi-lyst-flotu.md), карта [00059](00059-fleet-on-map.md).

## 1. Як є зараз (перевірено 13.11.2026)

1. Типи наказів — `FlightOrderTypes` (`server/src/Aetheris.Fleet/Data/Entities/FlightPlanEntities.cs`): `All = [Fly, Hyperfly, Jump, Mine, Scan, Load, Unload, Wait, Return]`. Наказу атаки немає.
2. Виконання листа — `FlightPlanRunner` (`Services/FlightPlanRunner.cs`): `ArrivalEventType`, `ArrivalPayload`, `HandleArrivalAsync`; події `FleetArrivalHandler`, `FleetWaitHandler`, `FleetScanHandler`, `FleetExtractionCycleHandler` реалізують `IScheduledEventHandler` (`Aetheris.Core.Scheduling`).
3. Планувальник: `IEventScheduler.ScheduleAsync(ScheduledEventRequest(SectorId, Type, RunAt, Payload, DedupeKey?))`, подія пишеться в транзакції запиту; обробник виконується в транзакції, що ставить подію виконаною (tech/07). Прив'язка до сектора (`SectorId`).
4. Станів флоту два (`FleetStates`: `stationed`, `flying`); стану «у бою» немає.
5. Положення флоту в польоті — `FleetFlightEntity` (00059); радар — `RadarVisibility` (World, `IWorldModule`).
6. Помилки — `GameException(код, повідомлення)`, коди в `FleetErrorCodes`.

## 2. Рішення

### 2.1 Наказ `attack`

1. `FlightOrderTypes.Attack = "attack"`, додається в `All`. Параметри (JSON в `FlightOrderEntity.ParametersJson`): `{ "targetFleetId": long, "targetPlayerId": string }`.
2. Складання листа з `attack` дозволене, якщо ціль **видима гравцю на карті в момент складання** (`RadarVisibility` повертає флот у множині видимих); невидимо → `GameException("fleet_attack_target_not_visible")`.
3. Точка зустрічі: `InterceptCalculator.Solve(Vec3 shooterPos, double shooterSpeed, FlightLeg targetLeg, DateTimeOffset now)` — розв'язує `|P_t(t) − P_s| = v_s × t` для ціли, що летить рівномірно (`P_t(t) = P0 + V × t`, квадратне рівняння `a t² + b t + c = 0`); якщо дискримінант < 0 або `t` поза часом польоту цілі → `null`. Ціль стоїть → точка = її місце. Всі координати — `Vec3` (z = 0).
4. Подія зміни плану цілі: Fleet публікує внутрішню подію `fleet.plan_changed` (`{fleetId}`), на яку атакуючі перераховують точку (підписка — таблиця `fleet_attack_watch(attacker_fleet_id, target_fleet_id, flight_plan_id)`, індекс за `target_fleet_id`); опитування нема. Якщо ціль зникла з видимості чи тікає швидше — наказ завершується з причиною `lost_target` / `unreachable`.

### 2.2 Ставка `intercept`

1. Булевий параметр польотного листа `flight_plan.intercept` (`boolean NOT NULL DEFAULT false`), редагується разом із листом.
2. Спрацьовує, коли ворожий флот опиняється в `detectionRange` флоту зі ставкою: (а) прибуття/стоянка флоту — запит до `IWorldModule.QueryFleetsInRadius(sectorId, pos, range)` (один просторовий запит на подію); (б) для флоту в польоті — подія `fleet.intercept_check` на момент найближчого зближення двох маршрутів, що **рахується при старті польоту** обох (мінімум функції відстані між двома рівномірними рухами в замкненому вигляді); без таймера.
3. Дальність виявлення — `max(char_detection)` радарів флоту × сигнатура цілі.

### 2.3 Хто «ворожий»

Інтерфейс `IAttackRules` (`Aetheris.Fleet.Abstractions`): `Task<AttackVerdict> CanAttackAsync(string attackerPlayerId, string defenderPlayerId, long sectorId, CancellationToken ct)`; `AttackVerdict(bool Allowed, string? ReasonCode)`. Реалізація `DefaultAttackRules`: заборонено, якщо захисник — під захистом новачка (`player_protection`, строк у налаштуванні `combat.newbie_protection_hours`, ключ додає ця задача: i, 72, 0–720, «Година захисту новачка від нападів») або це той самий гравець. Коди: `fleet_attack_protected_newbie`, `fleet_attack_self`. Рейтинг і клани — пізніше, через іншу реалізацію інтерфейсу.

### 2.4 Мирна зустріч

Без `attack` і без `intercept` бою не буває; флоти просто бачать один одного.

### 2.5 Створення бою

Подія планувальника `fleet.attack_contact` (`RunAt` = момент зустрічі, `DedupeKey` = `attack:{attackerFleetId}:{targetFleetId}`) → `AttackContactHandler` (Fleet) викликає `ICombatModule.CreateBattleAsync(CreateBattleRequest)`.

`BattleFactory.CreateAsync` в одній транзакції:
1. Перевіряє, що обидва флоти **не в бою** (`FleetStates.InCombat`); якщо ціль уже в бою → підкріплення (2.6).
2. Поле: розмір з налаштувань; об'єкти в радіусі `max(field_width, field_height)` навколо точки — **один запит** `IWorldModule.GetBodiesAndFieldsAsync(sectorId, centerVec3, radius)`; кожен об'єкт відображається в поле (позиція зміщується відносно центру поля, масштаб `combat.world_to_field_scale`, ключ i/d: d, 1.0, 0.01–100, «Масштаб світових координат у одиниці поля»). Планета → `FieldObject(Kind=Planet, radius=з даних тіла)`, астероїдне поле → `FieldObject(Kind=AsteroidField, radius)`.
3. Сторони: `Attacker`, `Defender`; зони розстановки — смуги глибиною `deploy_zone_depth` з протилежних країв; нападник — з боку напрямку підльоту (кут із вектора руху нападника), захисник навпроти.
4. `seed` = `SplitMix64(battleId × 0x9E3779B97F4A7C15 ^ unixSeconds(gameClock))`; зберігається.
5. Обидва флоти → `FleetStates.InCombat`, їхні польотні листи ставляться на паузу (`flight_plan.status = paused_by_combat`), подія наступного ордера скасовується (переривання через `IEventScheduler` — видалення за `DedupeKey` або позначка `cancelled`).
6. Викликає `ICombatCycleService.StartAsync(battleId)` (00065), що ставить подію `combat.phase_end` на кінець `deploy`.

### 2.6 Один бій на місце

Флот, що з наказом `attack`/ставкою долетів у точку, де вже йде бій (`combat_battle` у тому ж секторі в межах `combat.join_radius` — i, 2000, 100–100000, «Радіус пошуку бою для підкріплення»), стає **підкріпленням** (`combat_fleet.entered_cycle = поточний + 1`); окремий бій не створюється. Підкріплення входить за правилами 00065 §2.7.

### 2.7 Флот у бою

Не виконує інших наказів, не видобуває, не може бути ціллю нового окремого бою; атаки на нього перенаправляються в підкріплення.

## 3. Що зробити

1. **Fleet:** константа `Attack` + `All`; `FlightPlanRunner.ValidateAsync` (гілка `attack`: видимість, `IAttackRules`, ціль існує); `StartAsync` (гілка `attack`: `InterceptCalculator`, `ScheduleAsync("fleet.attack_contact")`); `FleetStates.InCombat = "in_combat"`; колонка `flight_plan.intercept`; таблиця `fleet_attack_watch`; міграція `AddCombatFlightFields`.
2. **Події:** `fleet.attack_contact` (payload `AttackContactPayload(long AttackerFleetId, long TargetFleetId, Vec3 Point, long SectorId)`), `fleet.intercept_check` (`InterceptCheckPayload(long FleetAId, long FleetBId)`), `fleet.plan_changed`; обробники в `Aetheris.Fleet/Events` за зразком `FleetArrivalHandler`; реєстрація в `FleetServiceCollectionExtensions`.
3. **Combat:** таблиці (міграція `Aetheris.Combat` `InitialBattle`), назви `snake_case`, без зовнішніх ключів в інші модулі:
   - `combat_battle(id bigint identity PK, sector_id bigint, x/y/z double precision, seed bigint, state text, cycle int, phase text, phase_ends_at timestamptz, version int, created_at timestamptz, finished_at timestamptz null)`, `CHECK (state IN ('active','finished'))`, `CHECK (phase IN ('deploy','orders','resolve','finished'))`, індекси `(state)`, `(sector_id, state)`;
   - `combat_side(id, battle_id, role text 'attacker'|'defender', zone_json, winner bool null)` індекс `(battle_id)`;
   - `combat_fleet(id, battle_id, side_id, fleet_id, player_id text, entered_cycle int, left_cycle int null, ready bool, last_order_at timestamptz null)` унікальний `(battle_id, fleet_id)`, індекси `(fleet_id)`, `(player_id)`;
   - `combat_field_object(id, battle_id, kind text, x, y, radius, body_ref bigint null)` індекс `(battle_id)`.
4. **ICombatModule:** `Task<long> CreateBattleAsync(CreateBattleRequest, ct)`, `Task<long?> FindBattleOfFleetAsync(long fleetId, ct)`, `Task<bool> JoinBattleAsync(long battleId, long fleetId, ct)`; Fleet викликає **лише його**.
5. **Клієнт:** `client/src/api/fleetApi.ts` — тип наказу `attack` з параметрами, `intercept`; вікно польотного листа — наказ «Атакувати» (вибір цілі: флоти з карти, `mapApi`), перемикач «Перехоплювати ворожих»; значок бою на флоті на карті з посиланням `/combat/:id` (екран — 00069). Тексти в `client/src/texts`, лише готові компоненти й токени.
6. **Тести:** `InterceptCalculatorTests` (стоїть, летить, недосяжна ціль, нульова швидкість), атака невидимого флоту відхиляється (`fleet_attack_target_not_visible`), захист новачка, мирна зустріч без бою, ставка `intercept` (стоянка й політ), створення бою з об'єктами поля (кількість запитів до World = 1), листи на паузі, друга атака на флот у бою стає підкріпленням, подія `fleet.plan_changed` перераховує точку, `DedupeKey` не дає двох боїв.

## 4. Критерії приймання

1. Флот з наказом «Атакувати» долітає й створює бій; обидва флоти `in_combat`, листи на паузі.
2. Ставка «Перехоплювати» запускає бій без участі гравця; мирна зустріч — ні.
3. Жодного опитування за таймером; усі моменти — події планувальника.
4. Кількість запитів до БД при створенні бою не залежить від кількості кораблів (тест лічильника).
5. `dotnet build/test/format` і `client` lint/build — зелені.

## 5. Результат виконання

_(заповнює виконавець)_
