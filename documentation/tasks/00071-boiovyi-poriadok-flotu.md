# Задача 00071 — Формації флоту: ескадрильї, розстановка й розподіл трюму до бою

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12–13.11.2026):** до бою, зокрема на планеті, передвстановити кількість і склад ескадрилей, щоб на старті бою не розподіляти; і мати **кілька формацій** для швидкої зміни на старті бою.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) §4.0, §7.4, §10.
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/ui-art/05-yedynyi-styl-interfeisu.md`.
> **Залежить від:** [00062](00062-dani-boiu-kharakterystyky-ta-nalashtuvannia.md); флоти [00040](00040-formuvannia-flotiv.md), трюм [00050](00050-politnyi-lyst-flotu.md), [00056](00056-pokazaty-triuum-flotu.md). **Від неї залежать:** 00063, 00065, 00067, 00070.

## 1. Як є зараз (перевірено 13.11.2026)

1. Слово «формація» в коді вже зайнято: `FleetFormationService` (`server/src/Aetheris.Fleet/Services/FleetFormationService.cs`) і `FleetFormationDtos.cs`/`FleetFormationCommands.cs` — це **складання самого флоту** з кораблів (створити, перейменувати, розпустити). Щоб не плутати, бойові налаштування в коді й таблицях називаємо **`BattleFormation`** (таблиці `fleet_battle_*`), а в інтерфейсі — «Формація» (так сказав автор).
2. Флот — `FleetEntity` (`Id`, `PlayerId`, `Name`, `HomePlanetId`, `PlanetId`, …), склад — сутність кораблів флоту в `FleetFormationEntities.cs`; ескадрильй немає.
3. Трюм флоту спільний: `FleetCargoEntity`, `FleetHoldService`, `FleetCargoService`; дрони й боєприпаси лежать як вантаж/деталі трюму (міграція `ShipPartCargo`).
4. Ліміт на дії флоту — блокування рядка гравця `FleetLocks`; єдина одиниця роботи — `FleetUnitOfWork`; ендпоїнти — `FleetEndpoints.cs` (група `/fleet`, `RequireAuthorization(SecurityPolicies.Player)`, `RequireRateLimiting(RateLimitGroups.Command)`, відповіді `Results.Json(…, FleetJson.Options)`).
5. Стан кораблів у світі: `ShipEntity.PlanetId`, `StateCode` (`ShipStates.Ready`…).

## 2. Рішення

1. **Формація** = іменований набір: ескадрильї + розстановка + бажаний розподіл трюму. Флот має 1…`combat.formation_max` (стартово 5) формацій; рівно одна **типова** (`is_default`). Типова застосовується на старті бою автоматично; інша обирається одним кліком у фазі `deploy` (00065). Редагувати можна, коли флот не `in_combat`; нічого фізично не переміщується.
2. **Ескадрильї:** 1…`combat.squadron_max` на формацію; кожна має назву, типову **поведінку** (`behavior`, 00062 §2.6) і **правило цілей** (`target_rule`); корабель — рівно в одній ескадрильї кожної формації.
3. **Новий флот:** одна формація «Основна» (типова) з однією ескадрильєю «Ескадрилья 1», поведінка `behavior_1` (`approach`), правило `target_rule_1` (`nearest`).
4. **Зміна складу флоту** (додано/знищено/розібрано корабель) — автоматично в **усіх** формаціях: додається в ескадрилью з кораблем того ж проєкту, інакше в першу; прибрано зі складу; порожня ескадрилья видаляється (остання лишається).
5. **Розстановка:** `deploy_x`, `deploy_y` ∈ [0, 1] — частки зони розстановки (0 — ближче до власного краю). Застосовується при створенні бою (множення на розмір зони).
6. **Розподіл трюму:** частка спільного запасу (`share`, 0…1) на ескадрилью за видом (дрон `part_type_id` або боєприпас `part_type_id`); сума часток одного виду по формації ≤ 1; решту розкладає автоматично `HoldSplitter` (00063). Усередині ескадрильї — пропорційно місткості трюмів; більше місткості корабля не кладеться.
7. **Копіювання** з іншої формації цього чи іншого флоту гравця: ескадрильї зіставляються за проєктами кораблів (`ship_design_id`), ліш­ні корабелі йдуть у першу ескадрилью.
8. **Типову формацію** не видалити, доки вона єдина; видалення типової при кількох — спершу призначити іншу.
9. Флот без бойових кораблів теж має формації (ескадрильї без зброї).

## 3. Схема даних (Fleet, міграція `AddBattleFormations`)

| Таблиця | Колонки | Обмеження й індекси |
|---|---|---|
| `fleet_battle_formation` | `id bigint identity PK`, `fleet_id bigint NOT NULL`, `name varchar(40) NOT NULL`, `position smallint NOT NULL`, `is_default bool NOT NULL`, `created_at timestamptz`, `updated_at timestamptz`, `version int NOT NULL DEFAULT 1` | унікальний `(fleet_id, position)`; **частковий унікальний** `(fleet_id) WHERE is_default` (рівно одна типова); `CHECK (position >= 1)` |
| `fleet_battle_squadron` | `id bigint identity PK`, `formation_id bigint NOT NULL`, `name varchar(40) NOT NULL`, `position smallint NOT NULL`, `behavior_code text NOT NULL`, `target_rule_code text NOT NULL`, `deploy_x real NOT NULL DEFAULT 0.5`, `deploy_y real NOT NULL DEFAULT 0.5` | FK `formation_id` → `fleet_battle_formation` ON DELETE CASCADE; унікальний `(formation_id, position)`; `CHECK (deploy_x BETWEEN 0 AND 1 AND deploy_y BETWEEN 0 AND 1)` |
| `fleet_battle_squadron_ship` | `squadron_id bigint NOT NULL`, `formation_id bigint NOT NULL`, `ship_id bigint NOT NULL` | PK `(squadron_id, ship_id)`; **унікальний `(formation_id, ship_id)`** (корабель у двох ескадрильях однієї формації неможливий); індекс `(ship_id)` |
| `fleet_battle_hold_share` | `squadron_id bigint NOT NULL`, `cargo_kind text NOT NULL` (`'drone'`/`'ammo'`), `part_type_id int NOT NULL`, `share real NOT NULL` | PK `(squadron_id, cargo_kind, part_type_id)`; `CHECK (share BETWEEN 0 AND 1)` |

Сутності: `FleetBattleFormationEntity`, `FleetBattleSquadronEntity`, `FleetBattleSquadronShipEntity`, `FleetBattleHoldShareEntity` у `Aetheris.Fleet/Data/Entities/BattleFormationEntities.cs`; конфігурація в `FleetDbContext.OnModelCreating`. `behavior_code`/`target_rule_code` — **нейтральні коди** довідників Combat (текстові посилання без FK між модулями; валідуються через `ICombatModule.GetReferenceCodesAsync()`, кеш).

Міграція також створює формацію «Основна» для кожного наявного флоту **одним `INSERT … SELECT`** (ескадрилья з усіма кораблями флоту, `behavior_1`/`target_rule_1`).

## 4. Серверна логіка

`BattleFormationService` (`Aetheris.Fleet/Services/BattleFormationService.cs`), блокує рядок гравця (`FleetLocks`) перед записом:

- `GetAsync(playerId, fleetId)` — усі формації одним запитом (3 запити: формації, ескадрильї + кораблі + частки через `Include`/split-query; `AsNoTracking`).
- `SaveAsync(playerId, fleetId, formationId, SaveFormationRequest, expectedVersion)` — оптимістичне блокування за `version`; повна заміна вмісту формації одним записом.
- `CreateAsync(…, copyFromFormationId?)`, `DeleteAsync`, `SetDefaultAsync`.
- `OnFleetCompositionChanged(IReadOnlyCollection<ShipChange>)` — пакетний обробник події складу (додано/прибрано) для **усіх** формацій флоту без циклу «запит на корабель».

Валідація (`BattleFormationValidator`, чисте, тестується окремо): коди ескадрилей і правил з довідників; кожен корабель флоту рівно в одній ескадрильї; кількість формацій ≤ `combat.formation_max`; кількість ескадрилей ≤ `combat.squadron_max`; назва 1…40 символів; `0 ≤ x,y ≤ 1`; сума часток одного виду ≤ 1; рівно одна типова.

Публічний метод для Combat: `IFleetModule.GetBattleFormationsAsync(long fleetId, ct) → IReadOnlyList<BattleFormationDto>`.

Коди помилок (`FleetErrorCodes`): `fleet_formation_unknown`, `fleet_formation_limit`, `fleet_formation_invalid`, `fleet_formation_ship_missing`, `fleet_formation_ship_duplicate`, `fleet_formation_share_over`, `fleet_formation_default_last`, `fleet_in_combat`, `fleet_formation_conflict` (версія застаріла).

## 5. API (за tech/04; префікс `/fleet`, лише власник флоту, `Idempotency-Key` на записі)

| Метод і шлях | Запит | Відповідь |
|---|---|---|
| `GET /fleet/fleets/{fleetId}/formations` | — | `200 { "formations":[{ "id":1,"name":"Основна","isDefault":true,"version":3,"squadrons":[{ "id":10,"name":"Ескадрилья 1","behaviorCode":"behavior_1","targetRuleCode":"target_rule_1","deployX":0.5,"deployY":0.3,"shipIds":[101,102],"holdShares":[{"kind":"drone","partTypeId":55,"share":0.5}] }] }] }` |
| `PUT /fleet/fleets/{fleetId}/formations/{id}` | те саме тіло без `id` + `"expectedVersion":3` | `200` оновлена формація; `409 fleet_formation_conflict`; `400 fleet_formation_invalid`/`…_ship_duplicate`/`…_share_over`; `423 fleet_in_combat` |
| `POST /fleet/fleets/{fleetId}/formations` | `{ "name":"Оборона","copyFromFormationId":1? }` | `201` нова формація; `409 fleet_formation_limit` |
| `DELETE /fleet/fleets/{fleetId}/formations/{id}` | — | `204`; `409 fleet_formation_default_last` |
| `POST /fleet/fleets/{fleetId}/formations/{id}/default` | — | `200` |
| `POST /fleet/fleets/{fleetId}/formations/copy-from/{sourceFleetId}` | `{ "sourceFormationId":5, "name":"…" }` | `201`; чужий флот → `403 not_your_object` |

Не-власник — стандартна відповідь `NotYourObjectException`; читання без ключа ідемпотентності.

## 6. Клієнт

Файли: `client/src/api/fleetApi.ts` (методи й типи формацій), `client/src/pages/fleets/FormationsTab.tsx` (ліниво), `client/src/pages/fleets/formations/*` (список ескадрилей, зона розстановки, таблиця трюму). Поведінка:
- верх: вкладки-назви формацій, «+ Нова», «Копіювати…», «Зробити типовою», «Видалити»;
- список ескадрилей з кораблями (перетягування кораблів між ескадрильями; «Розділити», «Об'єднати»), випадаючі списки поведінки й правила з довідників (через `contentApi`);
- міні-карта зони розстановки з перетягуванням ескадрилей (значення x/y у 0…1);
- таблиця розподілу дронів і боєприпасів: повзунки, підсумок «зайнято / місткість»;
- «Зберегти» — один запит `PUT` з `expectedVersion`; при `409` показати «формацію змінено, оновити»;
- стан у самій вкладці, оболонка не перемальовується; тексти в `client/src/texts`; лише токени й компоненти ui-art/05.

## 7. Тести

`server/tests/Aetheris.Tests/Fleet/BattleFormationTests.cs`: формація за замовчуванням і міграція наявних флотів; рівно одна типова (спроба двох → помилка БД і сервісу); кілька формацій; додавання/знищення корабля оновлює всі формації одним запитом (лічильник); валідація кожного випадку з розділу 4; копіювання (зіставлення за проєктом); `PUT` з застарілою версією → 409; чужий флот → 403; флот у бою → 423; `GetBattleFormationsAsync` повертає все одним викликом; застосування при створенні бою (інтеграційно з 00065). Клієнт: `FormationsTab.test.tsx` (перемикач, збереження, конфлікт).

## 8. Критерії приймання

1. Гравець налаштовує формації на сторінці флоту поза боєм; на старті бою застосована типова без дій; перемкнутися можна одним натисканням (00065).
2. Зміна складу флоту сама оновлює всі формації: корабель завжди рівно в одній ескадрильї кожної формації.
3. Жодних чисел у коді (ліміти в налаштуваннях); кількість запитів не залежить від кількості кораблів.
4. `dotnet build/test/format` і `client` lint/build — зелені.

## 9. Результат виконання

_(заповнює виконавець)_
