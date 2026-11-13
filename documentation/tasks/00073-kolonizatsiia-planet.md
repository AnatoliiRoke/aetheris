# Задача 00073 — Колонізація планет колоніальним модулем

> **Статус:** виконана (14.11.2026). Повний `dotnet test` має 12 падінь у файлах, яких задача не змінює; вони й відкриті пункти — у [00073-pytannia.md](00073-pytannia.md).
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
7. **Раса й типи планет.** У `race` одне поле `preferred_object_type_id`. Документація задає расі один тип планети: `references/perelik-5-rasy.md` §1 (колонка «Бажаний тип планети») і `game-design/08-rasy.md` §2: Террани — землеподібна, Літаріони — крижана, Драккури — вулканічна, Вейрани — пустельна (у базі розробки `preferred_object_type_id` = 5, 7, 6, 8 відповідно). Колонізовні типи: `celestial_object_type.is_colonizable` (5 типів, id 5–9; Океанічна не є бажаним типом жодної раси). Окремої таблиці «які типи підходять расі» немає.
8. **Ліміт планет.** «Колоніальне управління» — `tech_17`, `max_level = 25`. Ефекти технологій лежать у `technology_level_effect (technology_level_id, effect_type, value, …)`; ефект читається з рядка поточного рівня (`TechnologyInfo.Levels[level].Effects`, приклад — `EconomyTechnologyBonuses.Bonuses`, `server/src/Aetheris.Research/Services/EconomyTechnologyBonuses.cs`), тобто значення рівня абсолютне. Типу ефекту для ліміту планет немає (`TechnologyEffectTypes`, `server/src/Aetheris.Research/Services/TechnologyCodes.cs`). Технології вивчаються на планеті (`player_technology.planet_id`).
9. Вільні планети: `IWorldModule.GetFreePlanetsAsync` / `WorldQueryService.GetFreePlanetsAsync` (`server/src/Aetheris.World/Services/WorldQueryService.cs`).

## 2. Рішення (за документами й запитом автора; відкриті пункти — у питаннях)

### 2.1 Колоніальний модуль — вантаж, а не слот

1. Новий атрибут деталі `cargo_only` (за наявним механізмом атрибутів, як `engine_kind`): модуль з ним **не ставиться в слот** проєкту (`ShipDesignService` відхиляє `fleet_design_part_cargo_only`), але завантажується в трюм. Сід: `cargo_only` для `part_m_70`. Логіка за кодом атрибута, не за кодом деталі.
2. Новий вид дії модуля в трюмі — атрибут `cargo_order = colonize` (нейтральний код наказу). Наказ, що відкриває вантаж, береться з даних, як `flight_order` у 00058.
3. «Живе в трюмі ОДНОГО корабля»: модуль лежить у `ship_part_cargo` конкретного корабля; `FleetCargoService.Place` уже кладе модуль цілим в один корабель. Якщо вільне місце **жодного** корабля флоту менше за вагу модуля — модуль не завантажується (`fleet_cargo_overflow`, як зараз). Одиниця модуля ніколи не ділиться між кораблями.
4. Колоніальний модуль більше не прив'язаний до транспортного корпусу: правило «лише транспортні корпуси» (05 §3.1б) стосувалося слота, а слота тепер немає. Обмеження — лише місце в трюмі корабля (питання 4).

### 2.2 Команда «Колонізація»

1. Новий тип наказу `colonize` (`FlightOrderTypes.Colonize`). **Ціль не вказується** (✅ автор, 13.11.2026): колонізація відбувається на планеті, де флот стоїть, коли наказ починається; при збереженні ціль наказу записується як `none`.
2. Команда доступна, коли в трюмі флоту є хоча б один модуль з `cargo_order = colonize` (`FleetHoldService.ReadAsync` + каталог). Клієнт показує команду за списком доступних наказів флоту, як у 00058 (`availableOrders` з сервера), сервер перевіряє те саме при збереженні й при виконанні.
3. **Кнопка «Колонізувати» на карті** (✅ автор, 13.11.2026): у картці вільної планети, тип якої підходить расі гравця, кнопка відкриває вибір флоту, що стоїть на місці й має колоніальний модуль у трюмі (за `available_orders` трюму). Для обраного флоту сервер одним ендпоінтом складає й зберігає лист-чернетку: переліт до планети й «Колонізацію». Переліт залежить від двигунів: якщо кожен корабель має гіпердвигун (або, без нього, джамп-двигун) і звичайний двигун, а планета в іншій системі — гіперпереліт (або стрибок), який розкладається на справжні команди за 00072 (переліт до краю системи, гіперпереліт, переліт до планети); гіперпереліт має перевагу, якщо є обидва. Інакше — звичайний «Переліт» до планети. Чернетка відкривається у вікні листа, гравець переглядає час і паливо й відправляє сам.
4. Виконання (`FlightPlanRunner.BeginColonizeAsync`): флот стоїть на планеті (інакше `fleet_colonize_not_on_planet`); модуль у трюмі є; планета вільна й підходить расі; ліміт планет не перевищено. Тоді наказ триває стільки, скільки задає характеристика модуля `char_deploy_time` **з бази** (✅ автор, 13.11.2026; у коді числа немає; у сіді 600 с = 10 хв — значення для тестування, див. питання 6) — подія `fleet.colonize_complete`; модуль без часу розгортання колонізує одразу, у кроці листа. По події (`ColonizeCompleteHandler`, під блокуванням рядка гравця) у **одній транзакції**:
   1. повторна перевірка ліміту планет під блокуванням гравця (колонізації одного гравця йдуть по черзі) і планети кодами колонізації, потім захоплення планети з блокуванням рядка (`FOR UPDATE`); усі відмови — до будь-якого запису;
   2. `IWorldModule.ClaimPlanetAsync(planetId, playerId, raceId)` ставить `planet.owner_player_id`;
   3. знімається **одна** одиниця модуля з `ship_part_cargo` корабля, де він лежить (`FleetCargoService.TakePartAsync`), перерахунок `fleet_cargo`; якщо модуля раптом немає — уся транзакція відкочується (планета без колонії не лишається);
   4. `IPlanetsModule.FoundColonyAsync(playerId, planetId, sectorId, colonists)`: `PlanetStartPackage.EnsureStateAsync`, `PlaceStartBuildingsAsync` (ті самі будівлі, що в реєстрації: прапор `is_start_building` + стартова будівля раси, той самий метод, без копії), `IColonyRegistry.FoundAsync`, населення = `char_colonists` модуля (150 у сіді); стартових ресурсів раси (`race_start_resource`) колонія не отримує (✅ автор, 13.11.2026);
   5. звіт гравцю (`FleetReportService`, тип `fleet.colonized`).
5. **Паливо:** сам наказ `colonize` паливо не витрачає (у `FlightChainResolver` і `FuelPlanner` він не дає відрізків). Політ до планети витрачає паливо за 00072, як будь-який переліт.
6. **Решта флоту** лишається на орбіті колонізованої планети; кораблі не зникають (питання 3). Модуль зникає лише один, навіть якщо в трюмі кілька.
7. **Планету зайняли, поки флот летів або розгортався:** наказ блокується з кодом `fleet_colonize_taken`, лист на паузу, модуль лишається в трюмі.
8. **Придатність раси (дані):** нова таблиця `race_colonizable_type (race_id, object_type_id)`; планета підходить, якщо її тип колонізовний (`is_colonizable`) і є в таблиці для раси гравця. Сід — за документацією (`references/perelik-5-rasy.md` §1, `game-design/08-rasy.md` §2), по одному типу на расу:

   | Раса | Код | Тип планети |
   |---|---|---|
   | Террани | `race_1` | Землеподібна |
   | Літаріони | `race_2` | Крижана |
   | Драккури | `race_3` | Вулканічна |
   | Вейрани | `race_4` | Пустельна |

   Сід пише рядки за кодами раси й типу з їхніх довідників (не за id). Океанічна планета колонізовна, але жодній расі зараз не підходить (питання 1). Таблиця редагується в адмінці рас.
9. **Ліміт планет — ефект технології «Колоніальне управління»** (✅ автор, 13.11.2026): максимум 12 планет на 25-му рівні, перша додаткова планета на 3-му рівні, проміжні рівні розподілені рівномірно. Новий тип ефекту `planet_limit` (`TechnologyEffectTypes.PlanetLimit`); значення рівня абсолютне — скільки планет гравець може мати всього, **разом із домашньою** (питання 2). Без вивченої технології ліміт — налаштування `research.colony.base_limit` (1). Значення в сіді `technology_level_effect` для `tech_17` (`planets(L) = 2 + ⌊(L − 3) × 10 / 22⌋` для L ≥ 3, далі як дані, формули в коді немає):

   | Рівень | 1–2 | 3–5 | 6–7 | 8–9 | 10–11 | 12–13 | 14–16 | 17–18 | 19–20 | 21–22 | 23–24 | 25 |
   |---|---|---|---|---|---|---|---|---|---|---|---|---|
   | Планет усього | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |

   Технології вивчаються на планеті, тож ліміт гравця береться з найвищого рівня `tech_17` серед його планет (`IResearchModule.GetPlanetLimitAsync(playerId)` через `TechnologyCatalog`, без запиту на планету). Перевірка — при валідації листа, на початку наказу й ще раз під блокуванням гравця під час завершення; відмова `fleet_colonize_limit` з `details: { owned, allowed }`. Клієнт показує біля команди «Колонізація» «Планет: N з M».

### 2.3 Завантаження зі складу одразу (попап трюму)

1. У попапі трюму флоту (`FleetHoldButton`, ✅ автор, 13.11.2026: саме там, а не в картці планети) кнопки «Завантажити зі складу» та «Вивантажити на склад», активні, коли флот стоїть на власній планеті й не летить.
2. Команди `fleet.cargo_load` і `fleet.cargo_unload` (`FleetCargoCommands`) викликають ті самі `FleetCargoService.LoadAsync`/`UnloadAsync`, що й накази `load`/`unload` листа, в одній транзакції з блокуванням гравця (`FleetLocks.LockPlayerAsync`); повертають новий стан трюму (`FleetHoldDto`). Правила місця, ваги й наявності — ті самі (жодної копії логіки).
3. Вибір вмісту — та сама форма, що й у наказі листа (`FlightCargoFields.tsx`): ресурси й модулі складу з кількістю; вага й вільне місце показані до підтвердження.

## 3. Схема й налаштування

**Production** (міграція `AddCargoOnlyParts`): нових таблиць немає, атрибути `cargo_only` (bool-код) і `cargo_order` (код) — через `part_attribute`; довідник значень `cargo_order`: `colonize`. Сід: обидва атрибути для `part_m_70`.

**World** (міграція `AddRaceColonizableTypes`):

| Таблиця | Колонки | Обмеження |
|---|---|---|
| `race_colonizable_type` | `race_id int`, `object_type_id int` | PK `(race_id, object_type_id)`; FK на `race`, `celestial_object_type`; індекс `(object_type_id)` |

**Fleet:** нових таблиць немає; `flight_plan_order.type_code` приймає `colonize` (CHECK, якщо є, розширюється).

**Research** (сід, без міграції схеми): тип ефекту `planet_limit` у довіднику типів ефектів (`TechnologyEffectTypes`, адмінка й підписи `admin.reference.effect_type.planet_limit`); 25 рядків `technology_level_effect` для `tech_17` за таблицею §2.2.9; SQL-скрипт `server/scripts/db/task-00073-colonization.sql` для наявної бази (ефекти, `race_colonizable_type`, атрибути модуля).

**Налаштування** (сід визначень з описами українською):

| Ключ | Тип | Старт | Межі | Опис |
|---|---|---|---|---|
| `research.colony.base_limit` | i | 1 | 1–1000 | Скільки планет гравець може мати без вивченого «Колоніального управління» (домашня планета входить). Налаштування модуля Research: модуль не читає внутрішні налаштування Planets |

## 4. Серверна логіка

1. `ShipDesignService.SaveAsync`: деталь з `cargo_only` у слоті → `GameException("fleet_design_part_cargo_only")`; `BuildWarnings` для старих проєктів з таким модулем у слоті — попередження з тим самим кодом.
2. `FleetOrdersService.AvailableAsync(fleetId)` (розширення механізму 00058): накази від встановлених модулів + накази від вантажу (`cargo_order` модулів у `ship_part_cargo` кораблів флоту, один запит).
3. `FlightPlanService.BuildLegsAsync`: `colonize` дозволений лише, якщо доступний флоту; ціль записується як `none`.
3а. `FlightPlanService.CreateColonizePlanAsync(playerId, fleetId, planetId)` (кнопка карти): перевіряє модуль, планету (вільна, раса), ліміт, вибирає переліт (`ColonizeFlightAsync`: гіпер, стрибок або звичайний; без двигуна — `fleet_colonize_no_drive`) і зберігає чернетку тим самим `BuildLegsAsync`; лист, що виконується, не замінює (`fleet_flight_plan_under_way`).
4. `FlightPlanRunner`: `ValidateAsync` (модуль, ліміт) і `BeginAsync` отримують гілку `Colonize`; `BeginColonizeAsync` бере планету з місця флоту, планує подію `fleet.colonize_complete` (час = `char_deploy_time`) або колонізує одразу при нулі; `ColonizeCompleteHandler` виконує 2.2.4 в одній транзакції.
5. `IWorldModule.ClaimPlanetAsync(long planetId, string playerId, int raceId)`: блокує рядок планети, перевіряє `owner_player_id IS NULL`, `is_colonizable`, тип у `race_colonizable_type`; ставить власника. Fleet перевіряє планету своїми кодами до захоплення, а відмову захоплення (планету взяли між перевіркою й блокуванням) повідомляє як `fleet_colonize_taken`.
6. `IPlanetsModule.FoundColonyAsync(...)`: обгортка над `PlanetStartPackage` (`EnsureStateAsync`, `PlaceStartBuildingsAsync`, `IColonyRegistry.FoundAsync`, населення).
7. `FleetCargoCommands`: `fleet.cargo_load`, `fleet.cargo_unload` (2.3).
8. `IResearchModule.GetPlanetLimitAsync(playerId)`: найвищий рівень технологій з ефектом `planet_limit` серед планет гравця → значення ефекту рівня; без технології — `research.colony.base_limit`. Кількість планет — `IWorldModule.CountOwnedPlanetsAsync`.
9. Коди помилок: `fleet_colonize_no_module`, `fleet_colonize_not_on_planet`, `fleet_colonize_taken`, `fleet_colonize_unsuitable`, `fleet_colonize_limit`, `fleet_colonize_no_drive`, `fleet_design_part_cargo_only`; тексти uk/en для кожного.

## 5. API

- Політний лист: наказ `{ "type_code": "colonize" }` без цілі; відмови з кодами вище.
- `POST /api/v1/fleet/flight-plans/colonize` (команда `fleet.flight_plan_colonize`): `{ "fleet_id": 1, "planet_id": 11 }` → `FlightPlanDto` чернетки (команди перельоту з цілями й часом, далі `colonize`); паливо й нестачу клієнт читає з `plan-estimate`.
- `GET /api/v1/fleet/fleets/{id}/hold` додає `available_orders` (`["colonize", ...]`).
- `POST` команди `fleet.cargo_load` / `fleet.cargo_unload`: `{ "fleet_id": 1, "items": [{ "kind": "resource", "id": 12, "amount": 500 }, { "kind": "part", "id": 70, "mark_level": 1, "has_race_bonus": false, "amount": 1 }] }` → `FleetHoldDto`; відмови `fleet_cargo_no_planet`, `fleet_cargo_overflow`, `fleet_cargo_short`, `fleet_cargo_items_invalid`.
- `GET /api/v1/world/free-planets` додає `suitable_for_race: bool`.

## 6. Клієнт

- Політний лист (`FlightPlanDialog.tsx`): команда «Колонізація» в меню, коли сервер її повертає, без полів цілі; біля неї «Планет: N з M».
- Карта (`MapCard.tsx`, `MapPage.tsx`, `ColonizeFleetDialog.tsx`): кнопка «Колонізувати» лише для вільної планети, придатної для раси; вибір флоту з модулем; чернетка відкривається у вікні листа.
- Попап трюму (`FleetHold.tsx`): кнопки «Завантажити зі складу» / «Вивантажити на склад», форма `FlightCargoFields`.
- Конструктор: модуль з `cargo_only` не пропонується для слота; підказка «Лише для трюму».
- Тексти `client/src/i18n/uk.ts`, `en.ts`; лише токени й компоненти ui-art/05.

## 7. Тести

Сервер: модуль `cargo_only` не ставиться в слот; модуль кладеться в трюм одного корабля, а не влазить — `fleet_cargo_overflow`; команда `colonize` недоступна без модуля; наскрізна колонізація (політ, розгортання за `char_deploy_time`, власник, стартові будівлі як у реєстрації, модуль зник рівно один, паливо командою не витрачено); непридатна для раси планета — відмова; зайнята в дорозі — `fleet_colonize_taken`, модуль лишився; ліміт планет (рівень 2 → 1 планета, рівень 3 → 2, рівень 25 → 12; відмова `fleet_colonize_limit` з `owned`/`allowed`; повторна перевірка під час завершення); сід ефекту `planet_limit` для `tech_17` збігається з таблицею §2.2.9 і росте не спадаючи; сід `race_colonizable_type` за таблицею §2.2.8; `fleet.cargo_load`/`unload` з тими самими правилами, що накази листа; кількість запитів `available_orders` не залежить від кількості кораблів. Кнопка карти (ендпоінт): флот без гіпердвигуна → «Переліт» + «Колонізація»; флот із гіпердвигуном → команди гіперперельоту з часом + «Колонізація»; без модуля, непридатна планета, ліміт — відмови; дві колонізації одного гравця одночасно — друга зупиняється з `fleet_colonize_limit`; модуль без часу розгортання колонізує одразу. Клієнт: кнопка «Колонізувати» лише для вільної придатної планети, вибір флоту, відкриття чернетки; кнопки попапу трюму.

## 8. Критерії приймання

1. Колоніальний модуль не встановлюється в слот, лежить у трюмі одного корабля; якщо місця в жодному кораблі немає — не завантажується.
2. Команда «Колонізація» з'являється лише з модулем у трюмі, не має цілі й колонізує планету, де стоїть флот; колонізувати можна лише вільну, колонізовну й придатну для раси планету, і лише в межах ліміту планет від «Колоніального управління» (12 на 25-му рівні, перша додаткова на 3-му).
2а. Кнопка «Колонізувати» на карті складає для обраного флоту чернетку листа: гіперпереліт (стрибок) за 00072, якщо всі кораблі його мають і планета в іншій системі, інакше звичайний переліт; наприкінці — «Колонізація».
3. Розгортання триває `char_deploy_time` модуля з бази; після нього модуль зникає, планета належить гравцю, на ній ті самі будівлі, що при реєстрації, і 150 колоністів (`char_colonists`) без стартових ресурсів раси; паливо команда не витрачає.
4. У попапі трюму можна одразу завантажити й вивантажити ресурси й модулі зі складу планети, де стоїть флот.
5. У коді немає чисел балансу й кодів сутностей; `dotnet build/test/format` і `client` lint/build — зелені.

## 9. Результат виконання

### 9.1 Зроблено

- **Production:** атрибути `cargo_only` і `cargo_order = colonize` для `part_m_70` (сід, адмінка); `char_deploy_time` модуля в сіді 600 с (значення для тестування, питання 6); каталог віддає `CargoOnly`/`CargoOrder`.
- **World:** таблиця `race_colonizable_type` (міграція `AddRaceColonizableTypes`, сід за документацією), `IWorldModule.ClaimPlanetAsync` (захоплення з блокуванням рядка), `PlanetSuitsRaceAsync`, `suitable_for_race` у даних карти й вільних планет.
- **Planets:** `IPlanetsModule.FoundColonyAsync` — той самий `PlanetStartPackage.PlaceStartBuildingsAsync`, що й реєстрація, `IColonyRegistry.FoundAsync` з населенням `char_colonists`.
- **Research:** тип ефекту `planet_limit`, 25 значень для `tech_17` (таблиця §2.2.9), `PlanetLimitService` / `IResearchModule.GetPlanetLimitAsync`; базовий ліміт — налаштування модуля Research `research.colony.base_limit` (модуль більше не читає внутрішні налаштування Planets).
- **Fleet:**
  - конструктор відхиляє модуль `cargo_only` кодом `fleet_design_part_cargo_only` до перевірок корпусу;
  - `FleetOrdersService` — накази від вантажу в `available_orders` трюму;
  - наказ `colonize` без цілі: колонізує планету, де стоїть флот (`fleet_colonize_not_on_planet`);
  - `FleetColonizeService`: перевірка планети кодами колонізації, повторна перевірка ліміту під блокуванням гравця під час завершення, захоплення, зняття одного модуля (його відсутність відкочує всю транзакцію), заснування колонії, звіт;
  - модуль без часу розгортання колонізує одразу, у кроці листа;
  - `POST /api/v1/fleet/flight-plans/colonize` (`fleet.flight_plan_colonize`, `FlightPlanService.CreateColonizePlanAsync`): чернетка «переліт + колонізація», переліт за двигунами (гіпер, стрибок або звичайний) тим самим розкладанням, що й вікно листа;
  - `fleet.cargo_load` / `fleet.cargo_unload` для попапу трюму тими самими `FleetCargoService.LoadAsync`/`UnloadAsync`.
- **Клієнт:** кнопка «Колонізувати» в картці вільної планети, придатної для раси (`MapCard.tsx`), вибір флоту з модулем (`ColonizeFleetDialog.tsx`), чернетка у вікні листа (`MapPage.tsx`); наказ «Колонізація» без полів цілі з «Планет: N з M»; кнопки «Завантажити зі складу» / «Вивантажити на склад» у попапі трюму; тексти причин `fleet_colonize_*` uk/en; тестові заглушки `renderPage.tsx` доповнено.
- **Скрипти бази розробки:** `server/scripts/db/task-00073-colonization.sql`, `server/scripts/db/dev-colony-deploy-time-10min.sql` (запущено на `aetheris_dev` 13.11.2026: `deploy time rows changed 1`, перевірка SELECT — `part_m_70 | 600`).

### 9.2 Тести

- Сервер, `server/tests/Aetheris.Tests/Fleet/ColonizationTests.cs` (10) і `ColonizationFlowTests.cs` (10): модуль не ставиться в слот; каталог позначає модуль; команда з'являється лише з модулем; відмова без модуля; ліміт від технології й сід ефекту; сід типів рас; команди трюму; план із кнопки карти для флоту без гіпердвигуна («Переліт» + «Колонізація») і з гіпердвигуном (команди гіперперельоту з часом + «Колонізація»); наскрізна колонізація (переліт, власник, стартові будівлі реєстрації, модуль зник, паливо командою не витрачено); планету зайняли в дорозі — `fleet_colonize_taken`, модуль лишився; непридатна планета — `fleet_colonize_unsuitable`; без модуля і на ліміті — відмови; дві колонізації одночасно — друга зупиняється з `fleet_colonize_limit`; модуль важчий за трюм — `fleet_cargo_overflow`; модуль без часу розгортання колонізує одразу; кількість запитів трюму однакова для 1 і 3 кораблів.
- `ShipDesignTests.A_module_goes_only_on_the_hull_of_its_role`: колоніальний модуль на бойовому корпусі тепер відхиляється як вантаж (`fleet_design_part_cargo_only`).
- Клієнт: `MapColonize.test.tsx` (кнопка лише на вільній придатній планеті, вибір флоту, відкриття чернетки; без придатності кнопки немає), `MapScreen.test.tsx` (у картці зірки кнопки «Колонізувати» немає), `FleetHold.test.tsx`.

### 9.3 Результати команд (14.11.2026)

- `dotnet build -c Release`: 0 помилок, 0 попереджень.
- `dotnet format --verify-no-changes --no-restore`: код виходу 0.
- `dotnet test -c Release --filter` (конструктор, колонізація, межі модулів, описи налаштувань, нейтральні коди): 41 з 41.
- `dotnet test -c Release` (повний прогін на `aetheris_test`, без паралельних прогонів): 774 з 787; 13 падінь, з них `ShipDesignTests.A_module_goes_only_on_the_hull_of_its_role` після цього виправлено й перевірено вибірковим прогоном (41 з 41), повний прогін наново не запускався; решта 12 — у файлах, яких задача не змінює (`BalancedEconomyTests` ×3, `NoGameTermsTests`, `CharacteristicNameScanTests`, `RequestCostTests`, `ChosenSeedTests`, `ProcessesTests`, `TechnologyReferenceTests`, `CurrencyDebtTests`, `UpgradedLauncherTests`, тест сканера `FleetFlightTests.A_system_scanner…`).
- `client`: `npm run lint` код виходу 0, `npm run build` код виходу 0, `vitest run` 548 з 555; ті самі 7 падінь (`Characteristics`, `OrderQueuesPage`, `ProductionPanel`, `ShipyardPage`) є й на чистому `HEAD`.
