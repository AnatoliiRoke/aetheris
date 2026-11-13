# Задача 00073 — Колонізація планет колоніальним модулем

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (13.11.2026):** колоніальний модуль не встановлюється на корабель, а лежить у трюмі одного корабля; якщо в трюмі флоту є колоніальний модуль, у політному листі з'являється команда «Колонізація»; колонізувати можна лише планети, що підходять расі; корабель летить до планети й виконує команду; після команди модуль зникає; на планеті з'являються ті самі будівлі, що й під час реєстрації гравця; планета стає власністю гравця; паливо командою не витрачається. Додатково: у попапі трюму кнопка, яка завантажує ресурси й модулі зі складу планети одразу, не через політний лист.
> **Пов'язані документи:** [03 §4](../game-design/03-rozvytok-planet.md) (колонізація), [05 §3](../game-design/05-flot-ta-korabli.md), [05a §5.5](../game-design/05a-moduli-ta-komponenty.md) (колоніальний модуль), [09](../game-design/09-svit-ta-karta.md), трюм і вантаж [00056](00056-pokazaty-triuum-flotu.md), [00057](00057-zavantazhennia-ta-vyvantazhennia-v-politnomu-lysti.md), накази за модулями [00058](00058-nakazy-lystu-za-modulyamy.md), відрізки й паливо [00072](00072-giperperelit-dzhamp-ta-vytraty-palyva.md), реєстрація й стартовий пакет [00006](00006-reiestratsiia-ta-avtoryzatsiia-hravtsia.md), стартові будівлі [00030](00030-zbalansovana-ekonomika.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/07-planuvalnyk-podii.md`, `documentation/ui-art/05-yedynyi-styl-interfeisu.md`.
> **Питання й припущення:** [00073-pytannia.md](00073-pytannia.md).

## 1. Як є зараз (перевірено 13.11.2026)

1. **Модуль у даних є.** `part_m_70` «Колоніальний модуль» (`server/src/Aetheris.Production/Data/production-reference-seed.json`): група `pg_10`, атрибут `role = transport`, вага 717.951779 т, характеристики `char_colonists = 150`, `char_deploy_time = 129600` (с), `char_energy_use = 0`. Рецепт виробництва є (`recipe` з `part_type_id` модуля). Окремого атрибута «не встановлюється» немає: модуль зараз ставиться в слот як звичайний.
2. **Трюм і модулі в ньому.** Модулі в трюмі лежать на корабель: таблиця `ship_part_cargo (ship_id, part_type_id, mark_level, has_race_bonus, quantity)`, ресурси на корабель `ship_cargo`, підсумок флоту `fleet_cargo`. `FleetCargoService.LoadAsync` (`server/src/Aetheris.Fleet/Services/FleetCargoService.cs`) кладе модуль **цілим в один корабель** (`Place`, рядок ~321): модуль, що не влазить у вільне місце жодного корабля, відхиляє все завантаження (`fleet_cargo_overflow`). Брати можна лише на власній планеті, де стоїть флот (`StandsOnOwnBodyAsync`, `fleet_cargo_no_planet`). Ресурс важить 1 т (`world.resource.default_weight`), модуль — вагу свого виду.
3. **Завантаження зараз лише наказом листа** `load`/`unload` (`FlightOrderTypes`, `server/src/Aetheris.Fleet/Data/Entities/FlightPlanEntities.cs`). Окремої команди «завантажити зараз» немає.
4. **Попап трюму.** `FleetHoldButton` (`client/src/pages/player/fleets/FleetHold.tsx`) відкриває `FleetDialog` із переліком трюму (задача 00056); кнопок дій немає.
5. **Типи наказів:** `fly`, `hyperfly`, `jump`, `mine`, `scan`, `load`, `unload`, `wait`, `return`. Наказ, що з'являється за модулем флоту, уже є механізмом 00058 (атрибут `flight_order` модуля: `hyperfly`, `jump`, `scan`), але він рахує **встановлені** модулі, а не вантаж.
6. **Реєстрація гравця.** Планета: `StartPlanetAllocator.AllocateCoreAsync` (`server/src/Aetheris.World/Services/StartPlanetAllocator.cs`) ставить `planet.owner_player_id`. Будівлі: `PlanetStartPackage.PlaceStartBuildingsAsync(planetId, sectorId, raceBuildingTypeId, level)` (`server/src/Aetheris.Planets/Services/StartPackage.cs`): усі типи з прапором `building_type.is_start_building` плюс стартова будівля раси (`race.start_building_type_id`, `race.start_building_level`). Стан планети: `PlanetStartPackage.EnsureStateAsync`; колонія для населення: `IColonyRegistry.FoundAsync` (`server/src/Aetheris.Economy/Services/ColonyRegistry.cs`). Стартові ресурси раси: `race_start_resource`.
7. **Раса й типи планет.** У `race` одне поле `preferred_object_type_id` (бажаний тип; вирішує лише стартову планету). Колонізовні типи: `celestial_object_type.is_colonizable` (зараз 5 типів, id 5–9). Окремої таблиці «які типи підходять расі» немає.
8. **Ліміт планет.** Документ 03 §4: старт 1, «Колоніальне управління» (`tech_17`) +1 за рівень. Ефекту ліміту планет у даних і коді ще немає (у `status-zadach.md` §3.1, п. 8 — майбутня задача).
9. Вільні планети: `IWorldModule.GetFreePlanetsAsync` / `WorldQueryService.GetFreePlanetsAsync` (`server/src/Aetheris.World/Services/WorldQueryService.cs`).

## 2. Рішення (за документами й запитом автора; відкриті пункти — у питаннях)

### 2.1 Колоніальний модуль — вантаж, а не слот

1. Новий атрибут деталі `cargo_only` (за наявним механізмом атрибутів, як `engine_kind`): модуль з ним **не ставиться в слот** проєкту (`ShipDesignService` відхиляє `fleet_design_part_cargo_only`), але завантажується в трюм. Сід: `cargo_only` для `part_m_70`. Логіка за кодом атрибута, не за кодом деталі.
2. Новий вид дії модуля в трюмі — атрибут `cargo_order = colonize` (нейтральний код наказу). Наказ, що відкриває вантаж, береться з даних, як `flight_order` у 00058.
3. «Живе в трюмі ОДНОГО корабля»: модуль лежить у `ship_part_cargo` конкретного корабля; `FleetCargoService.Place` уже кладе модуль цілим в один корабель. Якщо вільне місце **жодного** корабля флоту менше за вагу модуля — модуль не завантажується (`fleet_cargo_overflow`, як зараз). Одиниця модуля ніколи не ділиться між кораблями.
4. Колоніальний модуль більше не прив'язаний до транспортного корпусу: правило «лише транспортні корпуси» (05 §3.1б) стосувалося слота, а слота тепер немає. Обмеження — лише місце в трюмі корабля (питання 2).

### 2.2 Команда «Колонізація»

1. Новий тип наказу `colonize` (`FlightOrderTypes.Colonize`). Ціль — об'єкт-планета (`target_kind = object`/`planet`).
2. Команда доступна, коли в трюмі флоту є хоча б один модуль з `cargo_order = colonize` (`FleetHoldService.ReadAsync` + каталог). Клієнт показує команду за списком доступних наказів флоту, як у 00058 (`availableOrders` з сервера), сервер перевіряє те саме при збереженні й при виконанні.
3. Команда складається з перельоту до планети й самої колонізації: при збереженні листа `colonize` розкладається на політ (`fly`, або 2–3 відрізки гіпер-/джамп-перельоту, якщо в листі обрано режим, за 00072) і наказ `colonize` на місці. Простіше й за правилами 00072: гравець ставить `fly`/`hyperfly`/`jump` до планети, а `colonize` — окремим наказом на місці; клієнт пропонує ці два кроки разом (питання 3, за замовчуванням — один рядок групи, як у 00072, `group_key` + `command = colonize`).
4. Виконання (`FlightPlanRunner.BeginColonizeAsync`): флот стоїть на орбіті цільової планети; модуль у трюмі є; планета вільна, колонізовна й підходить расі; ліміт планет не перевищено. Тоді наказ триває `char_deploy_time` модуля (дані, 129600 с = 36 год) — подія `fleet.colonize_complete`; по ній (`ColonizeCompleteHandler`) у **одній транзакції**:
   1. повторна перевірка планети з блокуванням рядка (`FOR UPDATE`): вільна, колонізовна, підходить расі;
   2. знімається **одна** одиниця модуля з `ship_part_cargo` корабля, де він лежить (`FleetCargoService.TakePartAsync`), перерахунок `fleet_cargo`;
   3. `IWorldModule.ClaimPlanetAsync(planetId, playerId)` ставить `planet.owner_player_id`;
   4. `IPlanetsModule.FoundColonyAsync(playerId, planetId, sectorId, colonists)`: `PlanetStartPackage.EnsureStateAsync`, `PlaceStartBuildingsAsync` (ті самі будівлі, що в реєстрації: прапор `is_start_building` + стартова будівля раси, той самий метод, без копії), `IColonyRegistry.FoundAsync`, населення = `char_colonists` модуля (питання 4);
   5. звіт гравцю (`FleetReportService`, тип `fleet.colonized`).
5. **Паливо:** сам наказ `colonize` паливо не витрачає (у `FlightChainResolver` і `FuelPlanner` він не дає відрізків). Політ до планети витрачає паливо за 00072, як будь-який переліт.
6. **Решта флоту** лишається на орбіті колонізованої планети; кораблі не зникають (питання 5). Модуль зникає лише один, навіть якщо в трюмі кілька.
7. **Планету зайняли, поки флот летів або розгортався:** наказ блокується з кодом `fleet_colonize_taken`, лист на паузу, модуль лишається в трюмі.
8. **Придатність раси (дані):** нова таблиця `race_colonizable_type (race_id, object_type_id)`; планета підходить, якщо її тип колонізовний (`is_colonizable`) і є в таблиці для раси гравця. Сід: бажаний тип раси (`preferred_object_type_id`) для кожної раси (питання 1). Таблиця редагується в адмінці рас.
9. **Ліміт планет:** налаштування `planets.colony.planet_limit` (0 = без ліміту, стартово 0), бо ефекту «Колоніальне управління» ще немає (питання 6). Коли ефект з'явиться, ліміт = налаштування + ефект.

### 2.3 Завантаження зі складу одразу (попап трюму)

1. У попапі трюму (`FleetHoldButton`) кнопки «Завантажити зі складу» та «Вивантажити на склад», активні, коли флот стоїть на власній планеті й не летить.
2. Команди `fleet.cargo_load` і `fleet.cargo_unload` (`FleetCargoCommands`) викликають ті самі `FleetCargoService.LoadAsync`/`UnloadAsync`, що й накази `load`/`unload` листа, в одній транзакції з блокуванням гравця (`FleetLocks.LockPlayerAsync`); повертають новий стан трюму (`FleetHoldDto`). Правила місця, ваги й наявності — ті самі (жодної копії логіки).
3. Вибір вмісту — та сама форма, що й у наказі листа (`FlightCargoFields.tsx`): ресурси й модулі складу з кількістю; вага й вільне місце показані до підтвердження.

## 3. Схема й налаштування

**Production** (міграція `AddCargoOnlyParts`): нових таблиць немає, атрибути `cargo_only` (bool-код) і `cargo_order` (код) — через `part_attribute`; довідник значень `cargo_order`: `colonize`. Сід: обидва атрибути для `part_m_70`.

**World** (міграція `AddRaceColonizableTypes`):

| Таблиця | Колонки | Обмеження |
|---|---|---|
| `race_colonizable_type` | `race_id int`, `object_type_id int` | PK `(race_id, object_type_id)`; FK на `race`, `celestial_object_type`; індекс `(object_type_id)` |

**Fleet:** нових таблиць немає; `flight_plan_order.type_code` приймає `colonize` (CHECK, якщо є, розширюється).

**Налаштування** (сід визначень з описами українською):

| Ключ | Тип | Старт | Межі | Опис |
|---|---|---|---|---|
| `planets.colony.planet_limit` | i | 0 | 0–1000 | Найбільша кількість планет гравця; 0 — без ліміту, доки немає ефекту технології |

## 4. Серверна логіка

1. `ShipDesignService.SaveAsync`: деталь з `cargo_only` у слоті → `GameException("fleet_design_part_cargo_only")`; `BuildWarnings` для старих проєктів з таким модулем у слоті — попередження з тим самим кодом.
2. `FleetOrdersService.AvailableAsync(fleetId)` (розширення механізму 00058): накази від встановлених модулів + накази від вантажу (`cargo_order` модулів у `ship_part_cargo` кораблів флоту, один запит).
3. `FlightPlanService.BuildLegsAsync`: `colonize` дозволений лише, якщо доступний флоту; ціль — планета; група з перельотом (2.2.3).
4. `FlightPlanRunner`: `ValidateAsync` і `BeginAsync` отримують гілку `Colonize`; `BeginColonizeAsync` планує подію `fleet.colonize_complete` (час = `char_deploy_time`); `ColonizeCompleteHandler` виконує 2.2.4 в одній транзакції.
5. `IWorldModule.ClaimPlanetAsync(long planetId, string playerId, int raceId)`: блокує рядок планети, перевіряє `owner_player_id IS NULL`, `is_colonizable`, тип у `race_colonizable_type`; ставить власника. Коди: `world_planet_taken`, `world_planet_not_colonizable`, `world_planet_race_unsuitable`.
6. `IPlanetsModule.FoundColonyAsync(...)`: обгортка над `PlanetStartPackage` (`EnsureStateAsync`, `PlaceStartBuildingsAsync`, `IColonyRegistry.FoundAsync`, населення).
7. `FleetCargoCommands`: `fleet.cargo_load`, `fleet.cargo_unload` (2.3).
8. Коди помилок: `fleet_colonize_no_module`, `fleet_colonize_not_at_target`, `fleet_colonize_taken`, `fleet_colonize_unsuitable`, `fleet_colonize_limit`, `fleet_design_part_cargo_only`.

## 5. API

- Політний лист: наказ `{ "type_code": "colonize", "target_kind": "object", "target_object_id": 123 }`; відмови з кодами вище.
- `GET /api/v1/fleet/fleets/{id}/hold` додає `available_orders` (`["colonize", ...]`).
- `POST` команди `fleet.cargo_load` / `fleet.cargo_unload`: `{ "fleet_id": 1, "items": [{ "kind": "resource", "id": 12, "amount": 500 }, { "kind": "part", "id": 70, "mark_level": 1, "has_race_bonus": false, "amount": 1 }] }` → `FleetHoldDto`; відмови `fleet_cargo_no_planet`, `fleet_cargo_overflow`, `fleet_cargo_short`, `fleet_cargo_items_invalid`.
- `GET /api/v1/world/free-planets` додає `suitable_for_race: bool`.

## 6. Клієнт

- Політний лист (`FleetPanel.tsx`, `flightPlanParts.ts`): команда «Колонізація» в меню, коли сервер її повертає; ціль — планета (лише придатні для раси, інші неактивні з поясненням); рядок групи «переліт + колонізація».
- Попап трюму (`FleetHold.tsx`): кнопки «Завантажити зі складу» / «Вивантажити на склад», форма `FlightCargoFields`.
- Конструктор: модуль з `cargo_only` не пропонується для слота; підказка «Лише для трюму».
- Карта: планета, придатна для колонізації, має позначку в картці об'єкта.
- Тексти `client/src/i18n/uk.ts`, `en.ts`; лише токени й компоненти ui-art/05.

## 7. Тести

Сервер: модуль `cargo_only` не ставиться в слот; модуль кладеться в трюм одного корабля, а не влазить — `fleet_cargo_overflow`; команда `colonize` недоступна без модуля; наскрізна колонізація (політ, розгортання за `char_deploy_time`, власник, стартові будівлі як у реєстрації, модуль зник рівно один, паливо командою не витрачено); непридатна для раси планета — відмова; зайнята в дорозі — `fleet_colonize_taken`, модуль лишився; ліміт планет; `fleet.cargo_load`/`unload` з тими самими правилами, що накази листа; кількість запитів `available_orders` не залежить від кількості кораблів. Клієнт: команда з'являється за `available_orders`; кнопки попапу трюму; модуль не в списку слотів конструктора.

## 8. Критерії приймання

1. Колоніальний модуль не встановлюється в слот, лежить у трюмі одного корабля; якщо місця в жодному кораблі немає — не завантажується.
2. Команда «Колонізація» з'являється лише з модулем у трюмі; колонізувати можна лише вільну, колонізовну й придатну для раси планету.
3. Після команди модуль зникає, планета належить гравцю, на ній ті самі будівлі, що при реєстрації; паливо команда не витрачає.
4. У попапі трюму можна одразу завантажити й вивантажити ресурси й модулі зі складу планети, де стоїть флот.
5. У коді немає чисел балансу й кодів сутностей; `dotnet build/test/format` і `client` lint/build — зелені.

## 9. Результат виконання

_Порожньо до виконання._
