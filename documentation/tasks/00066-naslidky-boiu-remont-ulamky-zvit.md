# Задача 00066 — Наслідки бою: втрати, ремонт на верфі, уламки, звіт

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12–13.11.2026):** що лишається після бою.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) §11, верф [05 §3.5, §3.10](../game-design/05-flot-ta-korabli.md), модулі [05a §5.6](../game-design/05a-moduli-ta-komponenty.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/07-planuvalnyk-podii.md`, `documentation/ui-art/05-yedynyi-styl-interfeisu.md`.
> **Залежить від:** [00065](00065-tsykly-boiu-na-serveri.md); пошта [00060](00060-vnutrishnia-poshta.md); верф [00039](00039-skladannia-korabliv.md).

## 1. Як є зараз (перевірено 13.11.2026)

1. `ShipEntity` (`FleetEntities.cs`): `Id, PlayerId, PlanetId, ShipDesignId, Crew, UpkeepPerHour, StateCode, CreatedAt, ModuleIds, CostIds`. **Поточних значень корпусу й броні немає.** `ShipModuleEntity`: `ShipId, PartTypeId, MarkLevel, HasRaceBonus, ConditionPercent, ConditionCode, Quantity`.
2. Верф: `ShipyardOrderEntity` (`PlayerId, PlanetId, PlanetBuildingId, ShipDesignId, Position, Quantity, ProducedQuantity, BaseDurationSeconds, Crew, UpkeepPerHour, StartedAt, FinishesAt`, вартість `ShipyardOrderCostEntity`, деталі `ShipyardOrderPartEntity`); сервіси `ShipyardQueueService`, `ShipyardAssembly`, подія `ShipyardCompleteHandler`. Ремонту немає.
3. «Збирач уламків» у сіді (`char_collect_speed`) нічого не робить.
4. Пошта: `IMailModule.SendSystemAsync` (00060).
5. Склад деталей планети й справність деталей — 00013 п. 13 (деталь має `ConditionPercent`).

## 2. Рішення

1. **Втрати:** знищений корабель видаляється назавжди разом з екіпажем (екіпаж в населення не повертається); флот без кораблів — флот і командир зникають (05 §4); утримання (`UpkeepPerHour`) припиняється.
2. **Пошкодження:** `ship.hull_points` і `ship.armor_points` (`double precision`, `NULL` = повний), модулі — `ConditionPercent` з бою (`ConditionCode`: >0 → `damaged`/`intact`, 0 → `destroyed`). Пошкоджений корабель літає й б'ється тим, що лишилось.
3. **Ремонт на верфі:** новий тип рядка черги верфі `kind = 'repair'`. Відновлює корпус і броню до повних, знищені й пошкоджені модулі **замінює цілими зі складу деталей** планети (той самий тип і Mk); зняті пошкоджені йдуть на склад окремим записом справності (00013 п. 13). Корабель має стояти на планеті з верф'ю.
   - **Вартість** (`RepairCostCalculator`): `cost(r) = assemblyCost(r) × (repairBase + repairHullWeight × lostHullShare + repairModuleWeight × lostModuleShare)`, де `lostHullShare = 1 − (hull+armor)/(hullMax+armorMax)`, `lostModuleShare = Σ(замінені модулі masa)/Σ(усі модулі masa)`; ресурси — з `ship_cost`.
   - **Час:** `repairSeconds = assemblySeconds × (repairTimeBase + repairTimeWeight × lostShare)`.
   - Коефіцієнти — налаштування (нижче). Брак модуля на складі → `GameException("fleet_repair_part_missing")` з переліком відсутніх.
4. **Трюм після бою (06 §7.4):** вміст трюмів уцілілих кораблів (`combat_ship_hold`) знову зводиться в спільний трюм флоту (`fleet_cargo`, деталі трюму) **одним пакетним `UPDATE … FROM`/upsert**; частка знищених кораблів зникає; витрачені боєприпаси й втрачені дрони не повертаються.
5. **Уламки:** зі знищеного корабля — `combat.debris_share` ресурсів корпусу й модулів (`ship_cost`), створюються як `DebrisState` у рушії; збирає лише «Збирач уламків» учасника за `char_collect_speed` протягом відрізку, у вільну місткість трюму свого корабля; після кінця бою незібране зникає. Зібране додається до трюму флоту разом із п. 4.
6. **Звіт:** системний лист `combat_report` кожному учаснику (див. 4).
7. **Командир:** досвід — `combat.commander_xp_per_battle` (0 до Академії; питання автору 4 у `00062-pytannia.md`).

## 3. Налаштування (додаються цією задачею)

| Ключ | Тип | Старт | Межі | Опис |
|---|---|---|---|---|
| `shipyard.repair_base` | d | 0.05 | 0–1 | Базова частка вартості складання за вихід на ремонт |
| `shipyard.repair_hull_weight` | d | 0.35 | 0–2 | Вага втраченого корпусу й броні у вартості ремонту |
| `shipyard.repair_module_weight` | d | 0.60 | 0–2 | Вага замінених модулів у вартості ремонту |
| `shipyard.repair_time_base` | d | 0.05 | 0–1 | Базова частка часу складання |
| `shipyard.repair_time_weight` | d | 0.40 | 0–2 | Вага втрат у часі ремонту |

## 4. Схема й код

1. **Міграція Fleet `AddShipCombatState`:** `ship.hull_points double precision NULL`, `ship.armor_points double precision NULL`, `CHECK (hull_points IS NULL OR hull_points >= 0)`; `shipyard_order.kind text NOT NULL DEFAULT 'build'` (`CHECK (kind IN ('build','repair'))`), `shipyard_order.repair_ship_id bigint NULL`; індекс `(repair_ship_id)`.
2. **Інтерфейс `ICombatResultSink`** (`Aetheris.Fleet.Abstractions`): `Task ApplyAsync(BattleResult result, CancellationToken ct)`, де `BattleResult { IReadOnlyList<ShipResult> Ships (ShipId, Destroyed, Hull, Armor, Modules[{PartTypeId, ConditionPercent}]), IReadOnlyList<HoldResult> Holds, IReadOnlyList<long> FleetsLeft }`. Реалізація `CombatResultSink` у Fleet: **один** `ExecuteUpdate`/`UPDATE … FROM (VALUES …)` на пакет кораблів, `DELETE` знищених (разом із `ship_module`, `ship_cost`), перерахунок флоту, видалення порожніх флотів і командирів. Викликається `CombatCycleService` у транзакції кінця бою (00065 §2.8).
3. **Ремонт:** `ShipyardRepairService.QueueAsync(playerId, shipId, planetBuildingId)`; ендпоїнт `POST /fleet/shipyards/{planetBuildingId}/repair { shipId }` → `201 { orderId, cost[], seconds, missingParts[] }`; `GET /fleet/ships` додає `hullPoints`, `armorPoints`, `damaged: bool`, `repairEstimate`. Подія завершення — `ShipyardRepairCompleteHandler` за зразком `ShipyardCompleteHandler`, `Type = "fleet.shipyard_repair_complete"`.
4. **Уламки:** розширення рушія 00063 (`DebrisState`, збір у `SegmentSimulator`, `debris_collected` у журналі); перенесення в трюм — у `CombatResultSink`.
5. **Лист `combat_report`:** тип у сіді пошти (`mail_type_N`, код нейтральний, назва в `localization_text`), `SendSystemAsync(playerId, typeCode, payloadJson)`; payload: `{ battleId, winnerSideId, mySide, cycles, losses:[{designId, count, crew}], damaged:[{shipId, hullPct}], debris:[{resourceTypeId, amount}], enemyLosses:[{designId, count}] }` (ворожі дані лише з того, що видно; без детальних проєктів невидимих); тексти — шаблон у `client/src/texts`.
6. **Клієнт верфі:** кнопка «Ремонт» у картці пошкодженого корабля (стан, вартість, час, що бракує на складі); тексти й токени за ui-art/05.

## 5. Тести

`server/tests/Aetheris.Tests/Fleet/CombatResultTests.cs`: знищений корабель і екіпаж (населення не змінюється); знищений флот і командир; пошкоджений корабель зберігає `hull_points`/модулі; трюм після бою (знищена частка зникає, витрачене не повертається, кількість запитів фіксована); `RepairCostCalculatorTests` (формули); ремонт: модулі зі складу, брак модуля → відмова, зняті на склад, подія завершення відновлює 100 %; уламки збирає лише збирач і лише в бою; лист приходить обом сторонам і не містить невидимого.

## 6. Критерії приймання

1. Після бою стан кораблів у базі відповідає кінцевому стану рушія.
2. Ремонт працює через чергу верфі й події планувальника; числа — у налаштуваннях.
3. Звіт бою приходить листом кожному учаснику.
4. `dotnet build/test/format` і `client` lint/build — зелені.

## 7. Результат виконання

_(заповнює виконавець)_
