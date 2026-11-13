# Задача 00065 — Цикли бою на сервері: розстановка, накази, відрізки, кінець

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12–13.11.2026):** задачі боїв з узгодженої логіки. Ця — життєвий цикл бою на сервері та API.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) §3, §4, §5, §7.4, §8, §9.1.
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/07-planuvalnyk-podii.md`, `documentation/tech/04-api.md`, `documentation/tech/05-bezpeka-ta-antychyt.md`.
> **Залежить від:** [00063](00063-rushii-boiu-symuliator.md), [00064](00064-pochatok-boiu-atakuvaty-ta-perekhoplennia.md), [00071](00071-boiovyi-poriadok-flotu.md).

## 1. Як є зараз (перевірено 13.11.2026)

1. Після 00064 бій створено (`combat_battle`, `combat_side`, `combat_fleet`, `combat_field_object`), флоти `in_combat`, але далі нічого не відбувається.
2. Планувальник: `IEventScheduler.ScheduleAsync(ScheduledEventRequest(SectorId, Type, RunAt, Payload, DedupeKey))`, `IScheduledEventHandler.HandleAsync(ScheduledEvent, ct)` у транзакції події; хост: `server/src/Aetheris.Host/Scheduling/*` (`EfEventScheduler`, `ScheduledEventRunner`).
3. Канал реального часу: `GameEventPublisher.Enqueue(type, payload)` → `GameHub` (метод клієнта `gameEvent`), `GameEventEnvelope(Seq, Type, Payload, CreatedAt)`.
4. Годинник гри — `IGameClock`; `DateTime.UtcNow` заборонено (tech/09).

## 2. Рішення

### 2.1 Фази бою (машина станів `combat_battle.phase`)

```
deploy ──(end)──► orders ──(end)──► resolve ──► orders … ──► finished
```

- `deploy` (тривалість `combat.deploy_seconds`) — розстановка, перемикання формації, розподіл трюму, накази.
- `orders` (`combat.pause_seconds`) — накази.
- `resolve` — миттєвий (синхронний) розрахунок відрізку, без таймера.
- Події планувальника: тип `combat.phase_end`, payload `PhaseEndPayload(long BattleId, string Phase, int Cycle)`, `DedupeKey = "combat:{battleId}:{phase}:{cycle}"`, `SectorId` — сектор бою. Обробник `PhaseEndHandler` ігнорує подію, якщо `phase`/`cycle` у payload не збігаються зі станом (захист від застарілих подій).
- «Готово»: якщо всі сторони «готові», фаза закінчується **одразу** — сервіс скасовує подію й виконує перехід у тій самій транзакції (скасування — позначка `cancelled` рядка події за `DedupeKey`).

### 2.2 Стан і зберігання

| Таблиця | Колонки | Індекси |
|---|---|---|
| `combat_squadron` | `id bigint PK`, `battle_id`, `side_id`, `fleet_id`, `name`, `behavior_code`, `target_rule_code`, `deploy_x real`, `deploy_y real`, `ship_ids bigint[]` | `(battle_id)`, `(fleet_id)` |
| `combat_order` | `id bigint PK`, `battle_id`, `cycle int`, `squadron_id`, `order_json jsonb`, `created_at`, `player_id` | унікальний `(battle_id, cycle, squadron_id)` |
| `combat_state` | `battle_id bigint`, `cycle int`, `state_zstd bytea` (JSON, стиснутий) | PK `(battle_id, cycle)` — зберігаємо стан **кожного** циклу (перегляд і відновлення) |
| `combat_segment` | `battle_id`, `cycle`, `tracks_json jsonb`, `log_json jsonb`, `outcome text` | PK `(battle_id, cycle)` |
| `combat_ship_hold` | `battle_id`, `ship_id`, `kind`, `part_type_id`, `quantity bigint` | PK `(battle_id, ship_id, kind, part_type_id)` |

Накази ескадрильї за цикл — `SquadronOrder` з 00063 (маршрут точок, `behaviorCode`, `targetRuleCode`, `focusShipId`, `droneCommand`, `retreat`). Накази **таємні**: читає лише своя сторона.

### 2.3 Старт бою (`ICombatCycleService.StartAsync`)

1. Для кожного флоту: `IFleetModule.GetBattleFormationsAsync(fleetId)` → типова формація; будує `combat_squadron` (позиції = частки зони × розмір зони сторони).
2. Розподіл трюму: спершу `hold_share` формації, решта `HoldSplitter.Split(...)`; запис у `combat_ship_hold`. Одне читання трюму флоту на флот.
3. Стартовий стан → `combat_state(cycle=0)`. Подія `combat.phase_end(phase=deploy, cycle=0)` на `deploy_seconds`.
4. Реалтайм: `combat.started`.

### 2.4 Перемикання формації (лише `deploy`)

`POST /combat/{id}/formation { "fleetId":…, "formationId":… }`: перераховує ескадрильї, позиції й розподіл трюму з обраної формації флоту (тим самим кодом, що старт); зміни наказів і розстановки, зроблені вручну, скидаються. Поза `deploy` → `combat_phase_invalid`. «Зберегти як порядок флоту»: `POST /combat/{id}/formation/save` — записує поточні ескадрильї й розстановку у формацію через `IFleetModule` (публічний метод `SaveBattleFormationAsync`).

### 2.5 Накази

Правила перевірки (`OrderValidator`): ескадрилья твоя; фаза — `deploy` (також розстановка) або `orders`; точки маршруту в межах поля й не на планеті; розстановка — лише в `deploy` і **лише в своїй зоні**; ціль фокусу **видима** своїй стороні в стані цього циклу; `droneCommand` ∈ `launch`/`recall`; до 32 точок маршруту (`combat.route_points_max`, i, 32, 2–256, «Найбільше точок маршруту ескадрильї»); ліміт частоти `combat.order_interval_seconds`. Поділ/об'єднання ескадрилей — `POST /combat/{id}/squadrons` (у `deploy`/`orders`): корабель переходить між ескадрильями тієї самої сторони; порожня видаляється.

### 2.6 Розрахунок відрізку (`PhaseEndHandler`, фаза `orders` → `resolve`)

В одній транзакції:
1. Блокування `SELECT … FOR UPDATE` рядка `combat_battle`; перевірка версії.
2. Накази: взяти `combat_order` цього циклу; ескадрильї без наказу — повторюють попередній; офлайн-сторона — сценарій (00067) або «стояти й стріляти».
3. `SegmentSimulator.Run(state, input, settings)`.
4. Записати `combat_state(cycle+1)`, `combat_segment(cycle)`, оновити `combat_battle.cycle`, `phase`, `phase_ends_at`, `version`.
5. Якщо `Outcome != Continue` або `cycle ≥ max_cycles` → кінець бою (2.8); інакше → `orders`, нова подія `combat.phase_end`.
6. Після коміту: `GameEventPublisher.Enqueue("combat.segment_ready", {"battleId","cycle"})`.

### 2.7 Підкріплення (06 §9.1)

Флот, що долетів до бою (00064 §2.6), записується в `combat_fleet.entered_cycle = cycle + 1`; на початку наступної `orders` його ескадрильї (за типовою формацією) з'являються біля краю поля, з якого він підлетів (напрямок із `combat_fleet.approach_bearing`); розподіл трюму — лише автоматичний.

### 2.8 Кінець бою

Умови: на полі лишилась одна сторона; `cycle ≥ combat.max_cycles` (нападник відходить: `winner = defender`); усі ескадрильї сторони вийшли. Дії в транзакції: `ICombatResultSink.ApplyAsync` (00066), флоти виходять з `in_combat` і повертаються у світ у точці бою, листи знімаються з паузи, подія `combat.finished`, `combat_battle.state = 'finished'`.

### 2.9 Видимість у відповідях (античит)

`BattleViewBuilder.BuildFor(sideId, state)`: віддає лише власні кораблі й те, що бачить сторона (за `visible[side]` рушія на кінець циклу); ворожі невидимі позиції, склад і сліди не віддаються; для ворожої ескадрильї слід — лише точки, у яких її було видно; у журналі — лише записи, що стосуються видимого. Окремий тест «чужі невидимі дані не потрапляють у відповідь».

## 3. API (префікс `/combat`, `Player`, `Command` rate limit; запис — з `Idempotency-Key`)

| Метод і шлях | Запит | Відповідь / помилки |
|---|---|---|
| `GET /combat/active` | — | `200 [{ "battleId", "phase", "cycle", "phaseEndsAt", "sectorId", "myFleetIds":[…] }]` |
| `GET /combat/{id}` | — | `200 BattleViewDto { battleId, phase, cycle, maxCycles, phaseEndsAt, field{width,height,objects[]}, myZone, mySquadrons[{id,name,fleetId,shipIds,behaviorCode,targetRuleCode,order,deployX,deployY,ships[{id,hull,hullMax,armor,shield,ammo,drones,modules[]}]}], visibleEnemies[{squadronId?,shipId,position,hullGroupId,lastSeenCycle}], formations[{fleetId,items[{id,name,isDefault}]}], hold[{shipId,items[…]}], ready, myRole }`; `403 not_your_object` |
| `POST /combat/{id}/formation` | `{ fleetId, formationId }` | `200 BattleViewDto`; `409 combat_phase_invalid` |
| `POST /combat/{id}/formation/save` | `{ fleetId, formationId }` | `200` |
| `POST /combat/{id}/hold` | `{ fleetId, ships:[{shipId, items:[{kind,partTypeId,quantity}]}] }` | `200`; `400 combat_hold_over_capacity`; `409 combat_phase_invalid` |
| `POST /combat/{id}/squadrons` | `{ action:"split"|"merge", squadronId, shipIds?, targetSquadronId? }` | `200`; `400 combat_squadron_invalid` |
| `POST /combat/{id}/orders` | `{ orders:[{ squadronId, route:[{x,y}], behaviorCode, targetRuleCode, focusShipId?, droneCommand?, retreat?, deployX?, deployY? }] }` | `200`; `400 combat_order_invalid` / `combat_target_not_visible` / `combat_outside_zone`; `409 combat_phase_invalid`; `429` |
| `POST /combat/{id}/ready` | `{ ready:true|false }` | `200 { phaseEndsAt }` |
| `GET /combat/{id}/segments/{cycle}` | — | `200 { cycle, positionsBefore[], positionsAfter[], myTracks[], enemyTracks[], log[], outcome }` (відфільтровано, 2.9); `404 combat_segment_unknown` |

Реалтайм-події (`GameEventPublisher`): `combat.started`, `combat.phase_changed {battleId, phase, cycle, phaseEndsAt}`, `combat.segment_ready {battleId, cycle}`, `combat.finished {battleId, winnerSideId}`; payload не містить даних про невидиме, адресується лише гравцям-учасникам.

Коди помилок (`CombatErrorCodes`): `combat_unknown`, `combat_phase_invalid`, `combat_order_invalid`, `combat_target_not_visible`, `combat_outside_zone`, `combat_hold_over_capacity`, `combat_squadron_invalid`, `combat_segment_unknown`, `combat_not_participant`.

## 4. Що зробити

1. Міграція `Aetheris.Combat` `AddBattleCycles` (таблиці 2.2, `combat_fleet.approach_bearing`).
2. Сервіси: `CombatCycleService`, `OrderValidator`, `BattleViewBuilder`, `PhaseEndHandler` (реєстрація в `CombatServiceCollectionExtensions`), `CombatEndpoints` (мапування 3).
3. Підкріплення; реалтайм-події; відновлення стану з `combat_state` (для повторного розрахунку).
4. Тести (`server/tests/Aetheris.Tests/Combat/`): повний бій від створення до кінця; «Готово» скорочує фазу й скасовує подію; накази поза зоною; ціль невидима; перемикання формації лише в `deploy`; розподіл трюму понад місткість; фільтр видимості (відповідь і журнал); підкріплення з правильного краю; `max_cycles` → відхід; застаріла подія `phase_end` ігнорується; повторний розрахунок зі збереженого стану дає той самий результат; кількість запитів на `GET /combat/{id}` не залежить від числа кораблів.

## 5. Критерії приймання

1. Бій проходить усі фази без адміністратора; таймери — події планувальника, опитувань немає.
2. Клієнт не отримує даних про невидимих ворогів (тест API).
3. Розрахунок відрізку детермінований і відтворюється зі збереженого стану.
4. `dotnet build/test/format` зелені.

## 6. Результат виконання

_(заповнює виконавець)_
