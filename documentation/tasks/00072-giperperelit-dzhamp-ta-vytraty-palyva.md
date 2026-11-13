# Задача 00072 — Гіперперельот, стрибок і витрати палива

> **Статус:** виконана (13.11.2026). Повний `dotnet test` має 12 падінь у файлах, яких задача не змінює; вони винесені в [00072-pytannia.md](00072-pytannia.md).
> **Запит автора (13.11.2026):** правильний розрахунок гіперперельоту, стрибка й витрат палива; розкладання команди польотного листа на відрізки; жорстка перевірка палива перед відправкою.
> **Пов'язані документи:** [05a §4.1](../game-design/05a-moduli-ta-komponenty.md) (двигуни), [14 §2.6](../game-design/14-balans-i-formuly.md), польотний лист [00050](00050-politnyi-lyst-flotu.md), швидкість і переліт [00053](00053-vykonannia-perelotu-povernennia-ta-pravky-vydobuvannia.md), карта [00059](00059-flot-u-polyoti-na-karti.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`, `documentation/tech/07-planuvalnyk-podii.md`, `documentation/ui-art/05-yedynyi-styl-interfeisu.md`.
> **Питання й припущення:** [00072-pytannia.md](00072-pytannia.md).

## 1. Як є зараз (перевірено 13.11.2026)

1. Типи наказів — `FlightOrderTypes` (`server/src/Aetheris.Fleet/Data/Entities/FlightPlanEntities.cs`): `Fly`, `Hyperfly`, `Jump`, `Mine`, `Scan`, `Load`, `Unload`, `Wait`, `Return`. Гілок виконання для `Hyperfly` і `Jump` у `FlightPlanRunner` немає: `ValidateAsync` і `BeginAsync` обробляють лише `Fly`/`Return` (`case` на рядках ~193 і ~547).
2. Один переліт — `FlightPlanRunner.BeginFlightAsync`: відстань `IWorldModule.GetTravelDistance(origin, destination)` (в одній системі — кількість орбіт × крок орбіти, інакше 3D-відстань між координатами), швидкість `FleetSpeeds.Current`, час `FleetSpeedService.FlightSeconds(distance, speed)`, запис `FleetFlightEntity` і подія `ArrivalEventType` (`fleet.arrival`).
3. Швидкість — `FleetSpeedService.ShipSpeed(push, mass, settings)`: `v = K·√(F/m)`; повертає `FleetSpeeds { Empty, Current, Full }` за найповільнішим кораблем; тяга береться з усіх двигунів проєкту разом (`DrivePush`). Класу двигуна немає.
4. Характеристики в сіді вже є: `char_thrust` (`drive_push`), `char_fuel_use` (`fuel_use`), `char_jump_prep` (`jump_prep`, секунди). Паливо двигуна в даних не задано; витрата палива **ніде не рахується й не списується**; відправка без палива не блокується.
5. Двигуни в 05a §4.1: реактивний (дейтерій), плазмовий (гелій-3), іонний (темна матерія), гравітаційний (без палива, лише біля зірки), гіпердвигун (квартан, стобій), джамп-двигун (ефір); сказано «двох швидкостей двигуна не вводимо» — це рішення цією задачею змінюється (6).
6. Паливо — ресурси групи `resource_group_3` («Паливо»): Дейтерій, Гелій-3, Темна матерія, Ефір, Збагачений Уран; лежить у спільному трюмі флоту (`FleetCargoEntity`, `FleetHoldService`).

## 2. Рішення (✅ автор, 13.11.2026)

### 2.1 Класи двигунів і склад корабля

1. Атрибут двигуна `engine_kind` (за наявним механізмом атрибутів деталі, як `damage_kind`): `normal` (реактивний, плазмовий, іонний, гравітаційний), `hyper`, `jump`. Нейтральні коди значень — у довіднику атрибута; логіка — за `logic_code`.
2. Корабель з гіпер- або джамп-двигуном **повинен** мати ≥ 1 двигун класу `normal`. **Конструктор проєкт без нього зберігає, але показує проблему** (✅ автор, 13.11.2026): відповідь збереження й читання проєкту містить `warnings: [{ code: "fleet_ftl_needs_normal_drive", slotIndexes: [...] }]` (слоти гіпер-/джамп-двигунів), клієнт показує попередження біля проєкту й у вікні конструктора («Потрібен ще звичайний двигун: гіпер- і джамп-двигуни не працюють у системі»). Збереження не блокується; такий корабель можна будувати, але польотний лист з гіпер-/джамп-командою для флоту з ним відхиляється (`GameException("fleet_ftl_needs_normal_drive")` з переліком кораблів), а верф при постановці в чергу показує те саме попередження. Те саме діє для проєктів, зібраних раніше. Попередження обчислює `ShipDesignService.BuildWarnings(design)` (чиста функція над каталогом, без запитів на слот).
3. Корабель може мати кілька двигунів різних класів і видів; тяга й витрата рахуються **окремо за класом**.

### 2.2 Три відрізки перельоту (команди `hyperfly` і `jump`)

У системі гіпер і джамп не діють (гравітація зірки), тож переліт між системами складається з відрізків (`leg`):

| Відрізок | Куди | Двигуни | Час |
|---|---|---|---|
| `to_edge` | з поточного місця на **орбіту 12** (останню орбіту системи) поточної системи | `normal` | за швидкістю `normal` і відстанню в орбітах |
| `ftl` | з орбіти 12 поточної системи на орбіту 12 системи призначення | `hyper` або `jump` | гіпер: за швидкістю `hyper` × `fleet.hyper.speed_multiplier`; джамп: **миттєво після підготовки** |
| `from_edge` | з орбіти 12 системи призначення до цілі (орбіта, планета, об'єкт системи) | `normal` | як `to_edge` |

1. Якщо флот стартує **не в системі** (відкритий космос), `to_edge` немає. Якщо ціль **не в системі** (точка космосу, поле, туманність), `from_edge` немає. Орбіта 12 — це **остання орбіта системи** з даних світу (`IWorldModule.GetSystemEdgeOrbit(systemId)`), а не число в коді.
2. Якщо ціль у **тій самій системі**, що й старт, команди `hyperfly`/`jump` відхиляються (`fleet_ftl_same_system`): там летять лише `normal`-двигуни командою `fly`.
3. **Гіпер:** під час `ftl` летять лише гіпердвигуни, звичайні вимкнені; швидкість залежить тільки від них: `v_hyper = fleet.hyper.speed_multiplier × K·√(F_hyper / m)`; `fleet.hyper.speed_multiplier` стартово **10** (тлумачення автора «прискорення ×10», див. питання 1).
4. **Джамп:** перед стрибком **час підготовки** `prep = max_по_кораблях(char_jump_prep) × max(1, distance / fleet.jump.reference_distance)`; `char_jump_prep` у сіді 900 с (15 хв), `fleet.jump.reference_distance` стартово 1000 (од. карти), тому поки відстань ≤ 1000, підготовка рівно 15 хв. Під час підготовки флот **стоїть на орбіті 12**, потім **миттєво** з'являється на орбіті 12 системи призначення.
5. **Флот летить зі швидкістю найповільнішого корабля** кожного відрізка; корабель без потрібного класу двигуна блокує відрізок: гіпер-/джамп-команда для флоту, де не всі кораблі мають відповідний двигун, відхиляється (`fleet_ftl_ship_missing`, у відповіді — список кораблів).

### 2.3 Перетворення команди при збереженні листа

Коли гравець зберігає лист з командою `hyperfly` або `jump`, сервер **замінює** її на 2–3 записи:

1. `fly` (`leg = to_edge`) — «просто переліт до 12 орбіти з часом»; пропускається, якщо флот стартує не в системі;
2. `hyperfly` або `jump` (`leg = ftl`) — «гіперперельот між точками»;
3. `fly` (`leg = from_edge`) — «переліт від 12 орбіти з часом»; пропускається, якщо ціль не в системі.

Усі записи мають спільний `group_key` і `command` (`hyperfly`/`jump`), за яким клієнт показує їх згорнуто одним рядком із розгортанням. Початок `to_edge` і кінець `from_edge` обчислюються **під час виконання** за реальним місцем флоту (попередні накази лист міг змінити), а при збереженні — за ланцюжком листа (`FlightChainResolver`) для оцінки й палива. Редагування групи = нова команда, сервер розкладає знову (лист зберігається повною заміною).

### 2.4 Паливо

1. **Паливо двигуна — склад із часток** (таблиця `part_fuel`): реактивний — Дейтерій 100 %; плазмовий — Дейтерій 100 %; іонний — Дейтерій 100 %; гравітаційний — Гелій-3 100 %; гіпердвигун — Темна матерія 30 % + Ефір 70 %; джамп-двигун — Темна матерія 70 % + Ефір 30 %. Сума часток двигуна = 1. (Ці склади **замінюють** паливо з 05a §4.1; квартан і стобій двигунам більше не потрібні — питання 4.)
2. **Витрата** — характеристика `char_fuel_use` двигуна: одиниць палива за секунду роботи двигуна (стартові значення підбирає виконавець за симулятором так, щоб переліт на 12 орбіт середнього корабля коштував кілька відсотків ємності палива його трюму; це баланс).
3. **Формула відрізка:** `fuel[ресурс] = Σ_двигунів_класу (char_fuel_use × share[ресурс] × t_роботи)`, де `t_роботи` — тривалість відрізка:
   - `normal` на `to_edge`/`from_edge`/`fly`: час відрізка за швидкістю з урахуванням **маси на старті відрізка**;
   - `hyper` на `ftl`: час відрізка;
   - `jump` на `ftl`: час **підготовки** (двигуни заряджаються); сам стрибок миттєвий.
4. **Витрата лише при перельоті.** Видобуток, скан, очікування, вантаження, розвантаження, бій, стоянка **нічого не споживають**.
5. Паливо — вантаж у спільному трюмі флоту: списується **на старті відрізка** за реальною масою (`FuelService.ConsumeAsync`, одним оновленням трюму в транзакції старту). Якщо на старті відрізка палива бракує (напр., гравець розвантажив) — відрізок блокується (`BeginState.Blocked`, код `fleet_fuel_short`), лист стає на паузу, як для інших блокувань.
6. **Жорстка перевірка перед відправкою.** При активації листа (кнопка «Відправити») і в оцінці `FuelPlanner` рахує **усі переліти листа + повернення додому**:
   - маса — **з повними трюмами** (`FleetSpeeds.Full`): найповільніше й найдорожче;
   - `Return` додому — відрізки від кінцевої точки листа до домашньої планети в тому ж режимі, що й остання політна команда листа (питання 2); якщо в листі польотів немає — резерву нема;
   - підсумок за кожним видом палива `need[ресурс]`; порівняння з наявним у трюмі флоту; будь-яка нестача → `GameException(fleet_fuel_short)` з `shortages:[{resourceTypeId, need, have}]`; флот **не відправляється**.
7. Гравітаційний двигун працює лише біля зірки (на відрізках `normal` усередині системи, включно з `to_edge`/`from_edge`); у вакуумі (відкритий космос) він не дає тяги й не палить (його внесок у `normal`-тягу там нуль).

## 3. Схема й налаштування

**Production** (міграція `AddPartFuel`):

| Таблиця | Колонки | Обмеження |
|---|---|---|
| `part_fuel` | `part_type_id int`, `resource_type_id int`, `share double precision` | PK `(part_type_id, resource_type_id)`; `CHECK (share > 0 AND share <= 1)`; індекс `(resource_type_id)` |

Сума часток двигуна = 1 перевіряється при завантаженні сіду й тестом (не обмеженням БД). Сід: рядки `part_fuel` для шести видів двигунів; атрибут `engine_kind` для всіх двигунів; `char_fuel_use` кожному двигуну; `char_jump_prep` = 900 джамп-двигунам. `ProductionCharacteristicKinds` — без змін (види вже є); каталог кораблів (`ShipCatalogDtos`) додає `EngineKind` і `Fuel: [{resourceTypeId, share}]` до модуля-двигуна — тими самими пакетними запитами (без N+1).

**Fleet** (міграція `AddFlightLegs`):

| Зміна | Тип | Призначення |
|---|---|---|
| `flight_plan_order.group_key` | `uuid NULL` | зв'язує відрізки однієї команди |
| `flight_plan_order.command` | `text NULL` | `hyperfly`/`jump`, початкова команда гравця |
| `flight_plan_order.leg` | `text NULL`, `CHECK (leg IN ('to_edge','ftl','from_edge'))` | роль відрізка |
| `fleet_flight.leg_kind` | `text NOT NULL DEFAULT 'normal'`, `CHECK (leg_kind IN ('normal','hyper','jump'))` | клас перельоту |
| `fleet_flight.prep_ends_at` | `timestamptz NULL` | кінець підготовки джампа |
| `fleet_flight.fuel_json` | `jsonb NOT NULL DEFAULT '{}'` | списане паливо `{resourceTypeId: amount}` (для звіту) |

Індекс `(group_key)` на `flight_plan_order`.

**Налаштування** (`FleetConfigKeys`, `FleetSettings`, сід визначень):

| Ключ | Тип | Старт | Межі | Опис |
|---|---|---|---|---|
| `fleet.hyper.speed_multiplier` | d | 10 | 1–1000 | Множник швидкості гіперперельоту до формули від тяги гіпердвигунів |
| `fleet.jump.reference_distance` | d | 1000 | 1–1000000 | Відстань, до якої підготовка стрибка постійна (од. карти) |
| `fleet.fuel.check_return_home` | b | true | | Додавати повернення додому до перевірки палива |

## 4. Серверна логіка

1. **`EngineKindsReader` + `FleetSpeedService`:** `FleetSpeeds` стає `FleetSpeeds { Normal{Empty,Current,Full}, Hyper{…}, JumpPrepSeconds }`; `ShipSpeed` викликається окремо з тягою класу. Читання тяги за класом — з каталогу (кеш), без запитів на корабель.
2. **`FlightLegPlanner`:** `Plan(FleetEntity fleet, FlightPlanOrderEntity command, WorldPlaceDto from)` → список відрізків (`to_edge`, `ftl`, `from_edge`) за 2.2; використовує `IWorldModule.GetSystemEdgePlace(systemId)` (нове: місце орбіти 12 системи з координатами системи) і `GetTravelDistance`.
3. **`FlightChainResolver`:** проходить лист, підставляючи місце кінця кожного відрізка як старт наступного (для оцінки й палива).
4. **`FuelPlanner`:** `Estimate(fleet, chain, massMode)` → `FuelEstimate { Legs[], NeedByResource, ReturnLegs[] }`; `FuelService.ConsumeAsync(fleet, fuelByResource)`; обидва читають паливо двигунів з каталогу.
5. **`FlightPlanService.SaveAsync`:** розкладає `hyperfly`/`jump` на відрізки (2.3), перевіряє склад двигунів (2.1, 2.2.5) і ліміт `fleet.flight_plan.max_orders` **за кількістю команд гравця**, не відрізків.
6. **`FlightPlanRunner`:** `ValidateAsync` і `BeginAsync` отримують гілки `Hyperfly`/`Jump`; `BeginFlightAsync` узагальнюється під клас двигуна (`leg_kind`); `Jump`: запис `FleetFlightEntity` з `prep_ends_at`, подія `fleet.arrival` на момент кінця підготовки, до того позиція флоту — місце старту (`FleetFlightService.CurrentPlaceAsync` для `leg_kind = jump` повертає старт до `ArrivesAt`); паливо списується в `BeginFlightAsync` перед записом польоту; активація листа викликає `FuelPlanner` (2.4.6).
7. **API:** `GET /fleet/flight-plans/estimate` (`FlightEstimateDto`) додає `legs:[{leg, kind, fromPlace, toPlace, distance, seconds, fuel:[{resourceTypeId, amount}]}]`, `returnLegs`, `fuelNeed:[{resourceTypeId, need, have}]`, `shortages`; `POST /fleet/flight-plans/{id}/activate` повертає `400 fleet_fuel_short` з `shortages`. Нові коди: `fleet_ftl_needs_normal_drive`, `fleet_ftl_same_system`, `fleet_ftl_ship_missing`, `fleet_fuel_short`.

## 5. Клієнт

Файли: `client/src/api/fleetApi.ts` (типи відрізків, палива), вікно польотного листа (`client/src/pages/fleets/…FlightPlan*`): команда «Гіперперельот» / «Стрибок» як **один рядок** з розгортанням на відрізки (час і відстань кожного), блок «Паливо» (потрібно / є за видами, підсвітка нестачі, повідомлення за кодом помилки), кнопка «Відправити» неактивна при нестачі. На карті флот у підготовці джампа має значок підготовки з таймером. Тексти — `client/src/texts`; лише токени й готові компоненти ui-art/05.

## 6. Документи, що змінюються цією задачею (рішення автора)

`game-design/05a-moduli-ta-komponenty.md` §4.1 (таблиця палива двигунів, класи двигунів, прибрати «двох швидкостей не вводимо», §359 п. 5), `game-design/14-balans-i-formuly.md` §2.6 і формули переліту (відрізки, гіпер ×10, підготовка джампа, паливо), `game-design/05-flot-ta-korabli.md` (склад двигунів), `references/perelik-3-moduli.md` (характеристики двигунів, паливо).

## 7. Тести (`server/tests/Aetheris.Tests/Fleet/`)

`FlightLegPlannerTests` (три відрізки; старт у космосі — без `to_edge`; ціль у космосі — без `from_edge`; та сама система — відмова); `HyperSpeedTests` (множник, лише тяга гіпердвигунів); `JumpPrepTests` (900 с до еталонної відстані, зростання понад неї; позиція під час підготовки = старт, після — призначення); `FuelFormulaTests` (кожен двигун і склад часток; джамп = за час підготовки; гравітаційний у вакуумі = 0); `FuelPlannerTests` (повні трюми, повернення додому, нестача за видом, режим останньої команди); `FlightPlanSaveTests` (команда замінюється на 2–3 записи зі спільним `group_key`; ліміт за командами); конструктор (проєкт зберігається, `warnings` містить `fleet_ftl_needs_normal_drive` зі слотами; з нормальним двигуном попередження немає); змішаний флот (`fleet_ftl_ship_missing`); списання палива на старті відрізка, блокування при нестачі; видобуток/скан/очікування/вантаження не споживають; сід `part_fuel` (суми = 1); кількість запитів оцінки не залежить від кількості кораблів. Клієнт: рядок групи й розгортання, нестача палива вимикає «Відправити».

## 8. Критерії приймання

1. Лист з гіпер-/джамп-командою зберігається як 2–3 відрізки; флот летить `to_edge` → `ftl` → `from_edge` з коректним часом; джамп миттєвий після підготовки.
2. Паливо рахується лише при перельоті, за складом палива кожного двигуна, і списується на старті відрізка; без палива флот не відправляється, відповідь вказує, чого бракує.
3. Проєкт з гіпер-/джамп-двигуном без звичайного зберігається, але конструктор і верф показують попередження `fleet_ftl_needs_normal_drive`; польотний лист з гіпер-/джамп-командою для такого флоту відхиляється.
4. У коді немає чисел балансу; `dotnet build/test/format` і `client` lint/build — зелені.

## 9. Результат виконання

### 9.1 Зроблено

**Production**

- Атрибут деталі `engine_kind` (`PartAttributeKeys.EngineKind`) і довідник значень `PartEngineKinds` (`normal`, `gravity`, `hyper`, `jump`). Гравітаційний двигун має окремий клас `gravity`, який належить до звичайного класу (`FlightEngineKinds.IsNormalClass`).
- Таблиця `part_fuel` (міграція `AddPartFuel`): PK `(part_type_id, resource_type_id)`, `share double precision`, `CHECK (share > 0 AND share <= 1)`, індекс `(resource_type_id)`. Сід: `engine_kind` шести двигунам, `char_jump_prep = 900` джамп-двигуну, `part_fuels`; сума часток перевіряється `CheckFuelShares`.
- Каталог кораблів: `ShipModuleDto.EngineKind` і `Fuel`; каталог конструктора (`ShipModuleViewDto.EngineKind`) теж передає клас двигуна клієнту.
- SQL-скрипт `server/scripts/db/task-00072-engine-fuel.sql` для наявної бази: гравітаційному двигуну задано `gravity`, як у сіді, додано підписи класу.

**World**

- `IWorldModule.GetSystemEdgePlaceAsync(systemId)`: місце останньої орбіти системи з даних.

**Fleet**

- Міграція `AddFlightLegs`: `flight_plan_order.group_key/command/leg`, `fleet_flight.leg_kind/prep_ends_at/fuel_json`, індекс `(group_key)`.
- Налаштування: `fleet.hyper.speed_multiplier` (10), `fleet.jump.reference_distance` (1000), `fleet.propellant.check_return_home` (true). Ключ третього налаштування названо без слова `fuel`, бо воно є старим кодом групи ресурсів і заборонене перевіркою нейтральних кодів (рішення записано в питаннях, п. 10). Описи всіх трьох налаштувань є в `client/src/admin/i18n/configUk.ts`.
- `EngineKindsReader` / `FleetEngines`: усі кораблі флоту (`ShipIds`) і їхні двигуни. Клас двигуна рахується окремо (`Of(class, inSystem)`), гравітаційні двигуни у відкритому космосі не працюють. `ShipsWithout(class)` повертає кораблі без двигуна класу (раніше повертав кораблі З ним).
- `FleetSpeedService`: `FleetSpeeds { Normal, NormalOpenSpace, Hyper, JumpPrepSeconds }`. Звичайна тяга кожного корабля береться лише зі звичайних двигунів (гіпер- і джамп-двигуни її не дають), у відкритому космосі без гравітаційних. Тяга проєкту в конструкторі (`ShipDesignSummary.DrivePush`) теж рахується лише від звичайного класу.
- `FlightPlanService.BuildLegsAsync`: команда `hyperfly`/`jump` зберігається як 2–3 відрізки зі спільним `group_key` і `command`. Кожен відрізок зберігає ціль команди; `to_edge` і `from_edge` записуються як `fly`, лише `ftl` як `hyperfly`/`jump`. Під час збереження перевіряється склад двигунів кожного корабля (`FlightPlanRunner.CheckFtlDrivesAsync`). Ліміт `fleet.flight_plan.max_orders` рахується за командами гравця; раніше ця перевірка лежала в методі, який ніхто не викликав, і не діяла.
- `FlightPlanRunner`: `to_edge` летить до краю системи, у якій флот стоїть на момент старту відрізка, `from_edge` до цілі команди, обидва звичайними двигунами й звичайним паливом. `ftl` летить гіпердвигунами або стрибає після підготовки. `ValidateAsync` перевіряє клас двигуна на кожному кораблі: `fleet_ftl_ship_missing` і `fleet_ftl_needs_normal_drive` з `details.ship_ids`.
- `FlightChainResolver`: група відрізків розкладається один раз (за `group_key`) від місця флоту до цілі команди; звичайні польоти мають реальну відстань (раніше 0, і їхнє паливо в перевірці було нульовим).
- `FuelPlanner`: перевірка перед відправкою й оцінка рахують з повними трюмами (`Full`). Гравітаційні двигуни у відкритому космосі не палять. Режим повернення додому береться з останнього польоту листа: звичайний `fly` після гіперперельоту повертає звичайний режим. Повернення звичайним режимом має реальну відстань.
- `FlightPlanService.CheckFuelAsync`: відмова `fleet_fuel_short` містить `details.shortages`.
- API: `GET /fleet/flight-plans/estimate` повертає ще `legs` (відрізки запитаного польоту з часом і паливом; для гіпер-/джамп-команди час і відстань оцінки це суми відрізків), `return_legs`, `fuel_need`, `shortages` збереженого листа. `GET /fleet/flight-plans/plan-estimate` (і колишній `fuel-estimate`) повертає лише паливо листа. Політ у DTO має `leg_kind` і `prep_ends_at`. Картка проєкту на верфі має `warnings`.

**Клієнт**

- Рядок групи відрізків із розгортанням, блок палива, неактивна «Виконати» при нестачі. Ключі текстів `plan.propellant.*` і `plan.leg.count` та класи `pl-propellant*` названо без слова `fuel` (перевірка нейтральних кодів клієнта).
- Конструктор: перевірка показує «Потрібен ще звичайний двигун…» і рамку слотів гіпер-/джамп-двигунів, коли в проєкті немає звичайного двигуна; збереження не блокується. Тяга проєкту лише від звичайних двигунів.
- Картка проєкту й картка збирання на верфі показують те саме попередження за `warnings` з сервера.
- Карта: флот у підготовці стрибка стоїть на місці старту, підпис «Стрибок через …» з таймером (клас `map-fleet-label--prep`, колір `--warning`).
- Виправлено помилку типів TS2345 у `client/src/pages/player/__tests__/FlightLegs.test.tsx`, яка ламала `npm run build`.

### 9.2 Тести

- Сервер, `server/tests/Aetheris.Tests/Fleet/FlightLegTests.cs`, 24 тести. Нові: `A_hyperflight_is_flown_leg_after_leg_to_its_target` (наскрізний гіперпереліт до прибуття), `A_jump_waits_for_its_preparation_and_then_takes_no_time` (наскрізний стрибок: підготовка, позиція під час підготовки, прибуття), `The_preparation_of_a_jump_grows_past_the_reference_distance`, `Open_space_has_no_leg_to_or_from_an_edge`, `A_fleet_with_a_ship_without_the_drive_of_the_command_is_refused` (змішаний флот, `details.ship_ids`), `A_ship_with_a_hyperdrive_alone_has_no_ordinary_speed`, `The_check_of_the_fuel_counts_with_full_holds`, `The_gravitational_drive_does_nothing_in_open_space`, `The_orders_that_do_not_fly_burn_nothing`, `A_leg_without_its_fuel_is_refused_and_the_hold_keeps_its_cargo`, `The_estimate_asks_the_database_the_same_for_one_ship_and_for_three`. Тест розкладання команди перевіряє §2.3: `to_edge`/`from_edge` записані як `fly`, `ftl` як `hyperfly`, ціль у кожного відрізка.
- Витрата палива береться з характеристики двигуна (`char_fuel_use`, рішення автора 13.11.2026). Тести, що перевіряють політ, а не його вартість (`FleetFlightTests`, `FleetMapTests`, наскрізні тести стрибка й гіперперельоту), ставлять витрату двигунів 0 через ті самі дані (`DriveConsumptionScope`) і відновлюють її після тесту; тест списання ставить витрату 1 од./с.
- Клієнт: `client/src/pages/player/__tests__/FlightLegs.test.tsx` (3 тести: рядок групи, блок палива, вимкнена кнопка) і `FtlDrives.test.ts` (5 тестів: попередження конструктора й слоти, клас двигуна, попередження сервера, позиція флоту під час підготовки стрибка й після нього).

### 9.3 Результати команд (13.11.2026)

- `dotnet build -c Release`: 0 помилок, 0 попереджень.
- `dotnet format --verify-no-changes --no-restore`: код виходу 0.
- `dotnet test -c Release --filter` за тестами флоту, `NeutralCodeScanTests` і `ConfigDescriptionTests`: 153 з 153.
- `dotnet test -c Release` (повний прогін на `aetheris_test`, без паралельних прогонів): 750 з 762. Падіння у файлах, яких задача не змінює: `BalancedEconomyTests` (3), `NoGameTermsTests`, `CharacteristicNameScanTests`, `RequestCostTests` (`/economy/`), `ChosenSeedTests`, `ProcessesTests`, `TechnologyReferenceTests`, `CurrencyDebtTests`, `UpgradedLauncherTests`, а також `FleetFlightTests.A_system_scanner_writes_a_snapshot_and_report_after_its_duration` (різниця 9:59.9999991 проти 10:00 у часі сканування; у вибірковому прогоні тест пройшов).
- `client`: `npm run lint` код виходу 0 (2 старі попередження в чужих файлах), `npm run build` код виходу 0, `vitest run` 543 з 550; ті самі 7 тестів (`Characteristics`, `OrderQueuesPage`, `ProductionPanel`, `ShipyardPage`) падають і на чистому `HEAD`.
