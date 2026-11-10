# Задача 00052 — Коди з сіду без автогенерації, виправлення бази й ключів текстів

> **Статус:** ✅ ВИКОНАНО 10.11.2026 (чотири пункти задачі від автора; перевірено окремо: збірка, `reconcile-seed-codes --check`, пакет текстів нового сервера).
> **Етап:** 1 (production/01)
> **Документи:** [tech/08 Правила коду](../tech/08-pravyla-kodu-dlia-ahentiv.md), [tech/09 Зведені правила](../tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md) (розділи 3.2 і 4.1 про коди), [задача 00022](00022-neitralni-kody-dovidnykiv-ta-tablytsia-tekstiv.md), [задача 00045](00045-novyi-sid-z-danykh-balansu.md).

## 1. Мета
Сід є єдиним джерелом кодів довідників. Що в сіді називається `pg_8`, `part_m_8`, `recipe_r_113`, `char_thrust`, те саме має бути в базі, у ключах текстів і в усьому коді, що на нього посилається. Сьогодні перша ініціалізація бази замість кодів із сіду видає свої з лічильника (`part_group_8`, `part_8`), тому код, що посилається на коди сіду, працює частково, а клієнт показує сирі ключі замість назв.

## 2. Рішення
1. **Сід це істина для кодів.** Рядок, який є в сіді, у базі має код із сіду. Лічильник кодів (`ICodeSequence`) видає коди лише рядкам, створеним адміном після сіду (як і раніше), і його значення після сіду не менше за найбільший числовий суфікс кодів цієї групи, щоб новий код не збігся з кодом сіду.
2. **Виправлення наявних баз** робить не ручний SQL, а команда `reconcile-seed-codes` у консолі `Aetheris.Host` (за зразком `create-admin`): зіставляє рядки бази з сідом і приводить коди, ключі `name_key`, `description_key`, `unit_key` і ключі таблиці `localization_text` до кодів сіду в одній транзакції; піднімає версію перейменованих текстів, щоб пакет текстів змінився; виправляє лічильники. Для бази, що вже збігається з сідом, команда нічого не змінює й каже про це (ідемпотентна). Режим «лише перевірити» показує розбіжності без змін.
3. **Пакет текстів не може бути «старим при тих самих версіях».** Версія пакета й ETag залежать від змісту (ключів і текстів), а не лише від суми версій. Кеш пакета на сервері має короткий строк життя з конфігурації (`texts.pack_cache_seconds` з українським описом), після якого пакет збирається з бази заново, тож зміна, зроблена поза застосунком, доходить без перезапуску. Браузер отримує новий ETag і перечитує пакет.
4. **Жодних посилань на згенеровані коди.** Код і тести посилаються на коди з сіду (константи беруться з сіду або з довідника за кодом із сіду), не на формати лічильника.

## 3. Що робимо

### 3.1. Сидери
- Production: `ProductionReferenceSeed` і `ProductionSeedAdditions` (характеристики, групи деталей, деталі, рецепти), Processes: `ProcessReferenceSeed` (типи, рецепти, параметри), Market: `MarketReferenceSeed` (рівні ринку) беруть код із сіду. Інші сидери (World, Planets, Research) перевірити так само й привести до того самого правила, якщо вони теж видають коди з лічильника.
- Після сіду лічильники груп кодів вирівнюються (п. 2.1).
- Уніфікована перевірка в сидері: код із сіду обов'язковий, унікальний у таблиці, відповідає допустимому формату (літери, цифри, `_`); порожній або повторний код зупиняє сід із зрозумілою помилкою.

### 3.2. Команда `reconcile-seed-codes`
- Консольна команда з режимами «перевірити» і «виправити», виводить таблицю «таблиця, було, стало, кількість» без секретів.
- Зіставлення старих згенерованих кодів із кодами сіду: за порядком сіду в межах таблиці, коли в базі коди формату лічильника; коли код у базі вже дорівнює коду сіду, рядок не чіпається. Розбіжність, яку не можна однозначно зіставити (кількість рядків не збігається), зупиняє команду без змін і пояснює чому.
- Покриває Production (групи, деталі, рецепти, характеристики), Processes (типи, рецепти, параметри), Market (рівні) і будь-яку іншу таблицю, де знайдеться розбіжність.

### 3.3. Пакет текстів
- Версія й ETag за змістом, кеш із строком із конфігурації, скидання кешу при змінах через застосунок лишається.

### 3.4. Аудит посилань
- Усі місця в `server/src`, `server/tests`, `client/src`, сидах і документах, які посилаються на коди чи ключі: літерали на кшталт `part_`, `recipe_`, `part_group_`, `characteristic_`, `production.*.*` ключі, формати ключів у `ProductionTextKeys`/`ProcessTextKeys`/`MarketTextKeys`, ефекти й вимоги, що посилаються на довідник за кодом. Знайдене виправлено або підтверджено, що воно посилається на код із сіду.

### 3.5. Документація
- `tech/09` (розділи 3.2 і 4.1) і README описують нове правило: сід є джерелом кодів, лічильник лише для рядків адміна, команда `reconcile-seed-codes`, пакет текстів за змістом.

## 4. Критерії приймання
1. Нова порожня база після сіду має в Production, Processes і Market коди, що збігаються з кодами сіду, а ключі текстів збудовані з цих кодів; кожен `name_key`, `description_key`, `unit_key` має текст у кожній мові (тест).
2. Лічильник кодів кожної групи після сіду не менший за найбільший числовий суфікс; код, створений адміном, не збігається з жодним кодом сіду (тест).
3. `reconcile-seed-codes` перейменовує базу, створену старим сидером (коди формату лічильника), у коди сіду разом з ключами текстів, і повторний запуск нічого не змінює; режим «перевірити» не пише в базу (тест на копії старого стану).
4. Пакет текстів змінює версію й ETag після перейменування ключів SQL-ом без зміни версій рядків, і сервер віддає новий пакет після строку кешу без перезапуску; браузер з старим ETag отримує 200, а не 304 (тест).
5. У коді, тестах, сидах і клієнті немає посилань на згенеровані коди замість кодів сіду; результат аудиту записано в розділі 6 (таблиця «було, стало»).
6. Клієнт показує назви, а не сирі ключі, для груп, деталей, рецептів і характеристик Production (тест на пакеті з кодами сіду).
7. `aetheris_dev` після виправлення збігається з сідом (команда в режимі «перевірити» каже «розбіжностей немає»).
8. `dotnet build -c Release`, `dotnet test -c Release` (крім 12 червоних тестів чужих задач, які задача не чіпає, якщо вони досі червоні), `dotnet format --verify-no-changes --no-restore`, у клієнті lint, тести й build чисті.

## 5. Що не входить
Перейменування кодів сіду (`pg_N` → `part_group_N` тощо): сід це істина, його коди не змінюються цією задачею.

## 6. Результат виконання

### 6.1. Що зроблено
1. **Сидери беруть коди з сіду.** Production (`ProductionReferenceSeed`, `ProductionSeedAdditions`: групи, деталі, рецепти, характеристики), Processes (`ProcessReferenceSeed`: типи, рецепти, параметри) і Market (`MarketReferenceSeed`: рівні) пишуть `Code` із поля `code` сіду; ключі текстів будуються з нього. Єдина валідація `SeedCodeRules.Validate` (`Aetheris.Core.Data`): код обов'язковий, унікальний у таблиці, формат `[A-Za-z0-9_]+`; порожній, повторний чи недопустимий код зупиняє сід зрозумілою помилкою (виклик у `Load()` World/Planets/Research і в `ValidateCodes` Production/Processes/Market). Після сіду лічильники піднімаються `SeedCodeRules.RaiseAsync` до найбільшого числового суфікса кодів групи (`recipe_r_113` дає 113); `ProductionSeedAdditions` піднімає їх і при кожному старті. Тепер `ProductionSeedAdditions` не додає рядки вдруге на базі зі старими кодами: коли таблиця має рядки, але жодного коду сіду, крок зупиняється з вказівкою запустити `reconcile-seed-codes` (`SeedCodeRules.EnsureAligned`). `IProductionModule.FindPartIdsByFileCodeAsync` шукає деталь за кодом сіду напряму (раніше мапив файл у `part_<індекс>`). World, Planets, Research вже брали коди із сіду (`NextAsync` у їхніх сидерах немає), додана лише валідація.
2. **Команда `reconcile-seed-codes [--check]`** (`Aetheris.Host/Admin/SeedCodeCommandLine.cs`, рушій `Aetheris.Host/Data/SeedCodeReconciliation.cs`, зіставлення `SeedCodeMatcher` у Core). Біжить після міграцій і сіду конфігурації, до сидерів, не стартує веб-сервер. Кожен модуль (Production, Processes, Market) дає свої таблиці через `ISeedCodeSource`/`ISeedCodeTable`; читає й пише свої таблиці сам через свій `DbContext` і `UnitOfWork`, тож Host чужих таблиць не торкається. Зіставлення: усі коди сіду вже є в таблиці, рядок не чіпається; формат лічильника й однакова кількість рядків, k-й рядок за `id` отримує k-й код сіду; інакше стоп без змін з поясненням. В одній транзакції (одне читання й один `SaveChanges` на таблицю, без запиту на рядок) змінює `code`, `name_key`, `description_key`, `unit_key`, ключі `localization_text` у кожній мові (`version` + 1, `updated_by = reconcile_seed_codes`; текст нового ключа, що вже існує, лишається, старий відкидається), піднімає лічильники. Друкує таблицю «таблиця, було, стало, кількість» (без рядка підключення). `--check` нічого не пише й повертає код виходу 1, коли знайшов розбіжність; ідемпотентна.
3. **Пакет текстів за змістом.** Версія й `ETag` пакета `"{мова}-{сума версій}-{хеш}"`, хеш SHA-256 від відсортованих пар «ключ, текст, версія» (12 hex-символів), рахується при збиранні пакета з бази (`LocalizationTextStore.ContentHash`). Кеш пакета на сервері має строк життя з `texts.pack_cache_seconds` (0 до 3600, за замовчуванням 30, український опис у `TextKeys` і `configUk.ts`); після строку пакет збирається з бази заново, скидання при змінах через застосунок лишилось. `Cache-Control: private, no-cache` і 304 за збігом ETag не змінені. У тестовому хості `texts.pack_cache_seconds` = 0 (`AetherisTestFactory.TestLimits`).
4. **Аудит посилань** (таблиця нижче).
5. **Документи:** `tech/09` (розділи 3 і 4, п. 3.1, 3.2, 3.5, 4.1, 4.4) і `README.md` (розділ «Коди зі стартових даних і команда reconcile-seed-codes», стартові дані Production) описують нове правило.
6. **Тести** (розділ 6.4).

### 6.2. Рішення
1. Процеси мали в сіді лише внутрішні ключі зв'язків (`key`), а не коди. До кожного рядка типів, рецептів і параметрів дописано `code` у форматі лічильника в порядку файла (`process_type_N`, `process_recipe_N`, `process_param_N`): вони збігаються з наявними базами, тож перейменувань немає. Поле `key` лишилось для зв'язків усередині файла.
2. Формат лічильника не збігається з форматом сіду для жодної групи, тож колізії немає: `part_group_N` проти `pg_N`, `part_N` проти `part_m_N`/`part_h_N`, `characteristic_N` проти `char_<назва>`, `recipe_N` лічильника проти `recipe_N` сіду перекривається за формою, але лічильник піднято до 119 (найбільший суфікс `recipe_N` і `recipe_r_N` сіду), тож перший код адміна `recipe_120`; тест перевіряє це для всіх груп.
3. `--check` повертає 1 на розбіжності (як `git diff --exit-code`), щоб скрипт міг зупинитись.
4. Запис у таблиці модулів робить сам модуль типізованим EF (аналізатор AETHERIS002 забороняє складений SQL), через `UnitOfWork` модуля, який входить у транзакцію Host.
5. Таблиці World, Planets, Research у `aetheris_dev` збігаються із сідом, розбіжностей кодів там немає, тож reconcile їх не охоплює.

### 6.3. Аудит посилань на згенеровані коди (місце, було, стало)
| Місце | Було | Стало |
|---|---|---|
| `ProductionReferenceSeed.cs` (характеристики, групи, деталі, рецепти) | `codes.NextAsync(...)` | код із сіду (`.Code`), лічильник піднімається після сіду |
| `ProductionSeedAdditions.cs` | `DatabaseCodes`: `part_<індекс>`, `recipe_<індекс>`, `part_group_<індекс>`, `characteristic_<індекс>`; `NextAsync` для нових характеристик | прямий пошук за кодом сіду, нова характеристика з кодом сіду, перевірка «коди бази ≠ коди сіду» |
| `ProductionModule.FindPartIdsByFileCodeAsync` | код файла мапився у `part_<індекс>` | пошук за кодом сіду |
| `ProcessReferenceSeed.cs` (типи, рецепти, параметри) | `NextAsync`, у сіді кодів немає | поле `code` у сіді, код із сіду |
| `MarketReferenceSeed.cs` | `NextAsync`, підйом лічильника за кількістю | код із сіду, підйом за суфіксом |
| `ProductionTextKeys`, `ProcessTextKeys`, `MarketTextKeys` | формати ключів від коду рядка | без змін: ключ будується з коду рядка, а це тепер код сіду |
| World, Planets, Research (сидери, `Load()`) | коди із сіду, без перевірки | коди із сіду, додано `SeedCodeRules.Validate` |
| `Fleet/Data/hull-shipyard-levels.json` (`part_h_N`) | коди сіду, працювали лише через мапінг у `part_<індекс>` | коди сіду, шукаються напряму |
| `ProcessesTests` (`Hull`, `Engine`, `LifeSupport`), `TerraformingTests`, `ProcessActiveOrdersTests`, `ProcessStubTests` | `part_1`, `part_4`, `part_10`, `part_34` | `part_m_1`, `part_m_4`, `part_m_10`, `part_m_34` |
| `PartCompatibilityTests` | `part_1`, `part_2`, `part_10`, `part_84`, `part_110` | `part_m_1`, `part_m_2`, `part_m_10`, `part_h_1`, `part_h_27` |
| `PartGroupTreeTests` | `part_group_3/4/7/19/20/21/22` | `pg_3/4/7/19/20/21/22` |
| `AmmunitionDamageTests` | `characteristic_6` | `char_rate` |
| `UpgradedLauncherTests` | `part_<кількість>`, `recipe_<кількість>` | останні код деталі й рецепта із сіду |
| `NoGameTermsTests` | дозволені префікси ключів сіду без `texts.` | додано `texts.` (ключ `texts.pack_cache_seconds`) |
| `RequestCostTests`, `AetherisTestFactory` | пакет текстів із кешем за замовчуванням | тест вартості ставить `texts.pack_cache_seconds` = 30, тестовий хост 0 |
| `client/src/pages/player/productionStock.ts` | порядок інгредієнтів-деталей за числом з `part_<n>` у ключі | деталь стоїть за `partTypeId` (коди сіду `part_m_N`/`part_h_N` порядку не несуть); код ресурсу `resource_N` у форматі сіду, без змін |
| `client/src/pages/player/fleets/fleetExtraction.ts` | `pg_8` | вже код сіду, без змін |
| Клієнт: фікстури `renderPage.tsx` та інші тести з `part_N`, `part_group_N`, `characteristic_N` | довільні непрозорі ключі мок-даних | без змін: клієнт коди не розбирає; ключі сіду перевіряє новий `SeedCodeTexts.test.tsx` |
| Сіди World/Planets/Research/Market/Fleet (посилання за кодом: `requirements.target`, `part_hulls`, `capsule_race`, `factory`) | коди сіду (`building_N`, `tech_N`, `race_N`, `part_h_N`) | без змін |
| Адмін-сервіси (`*ReferenceAdminService`, `NextAsync`) | лічильник для рядка адміна | без змін: так і задумано |
| `tech/09`, `README.md` | «кожен рядок сіду отримує код від лічильника» | сід джерело кодів, лічильник лише для адміна, команда, пакет за змістом |

### 6.4. Тести
Нові (`server/tests/Aetheris.Tests/Localization/SeedCodeTests.cs`, на `aetheris_test`, 8 тестів, усі зелені): свіжа база має коди сіду в Production, Processes і Market, ключі збудовані з них, кожен ключ має текст у кожній мові; лічильники не нижче суфікса й код адміна не збігається з кодом сіду; порожній, повторний і недопустимий код зупиняє сід; зіставлення кодів (`SeedCodeMatcher`) для всіх випадків; reconcile на базі «старого сидера» (коди й ключі формату лічильника відтворено) повертає коди сіду й ключі текстів, `--check` не пише й повертає 1, повторний запуск нічого не змінює й каже «Розбіжностей немає», версія текстів росте; таблиця з більшою кількістю рядків зупиняє команду без змін, а сидер на такій базі не дублює рядки; пакет змінює ETag після перейменування ключів SQL-ом без зміни версій, сервер віддає старий пакет до кінця строку кешу й новий після, запит зі старим ETag отримує 200, з новим 304; хеш пакета залежить від змісту, а не від порядку.
Клієнт (`client/src/pages/player/__tests__/SeedCodeTexts.test.tsx`, 2 тести, зелені): пакет з кодами сіду (`pg_2`, `part_m_8`, `recipe_r_113`, `char_thrust`, `char_fuel_use`) дає назви групи, деталі, рецепта й характеристик, а не сирі ключі.
Переписані за змістом (код сіду замість формату лічильника): `ProcessesTests`, `TerraformingTests`, `ProcessActiveOrdersTests`, `ProcessStubTests`, `PartCompatibilityTests`, `PartGroupTreeTests`, `AmmunitionDamageTests`, `UpgradedLauncherTests`, `NoGameTermsTests` (префікс `texts.`), `RequestCostTests` і `AetherisTestFactory` (кеш пакета).

### 6.5. Команди й результати
- `reconcile-seed-codes --check` на `aetheris_dev` (двічі, до і після остаточної збірки): `Розбіжностей немає: коди бази збігаються з кодами сіду.`; у базі 748 ключів назв, описів і одиниць цих таблиць, кожен має текст у обох мовах.
- `dotnet build -c Release --no-incremental`: 0 помилок, 0 попереджень. `dotnet format --verify-no-changes --no-restore`: без змін, код виходу 0.
- `dotnet test -c Release`: 638 тестів. Повний прогін до виправлення префікса `texts.` дав 11 червоних; після нього (повторний прогін класів `NoGameTermsTests`, `RequestCostTests`, `ConfigDescriptionTests`, `TextsCacheTests`, `SeedCodeTests`) тест `The_seed_holds_only_core_and_module_parameters` зелений. Лишаються червоні з причин не цієї задачі (чужі задачі або чужі зміни сіду в робочому дереві): `BalancedEconomyTests.The_quantities_of_the_minerals_are_level_among_buildings_technologies_and_modules`, `BalancedEconomyTests.Every_asteroid_field_has_the_three_crystals_and_at_least_seventy_per_cent_of_resources`, `ChosenSeedTests.The_chosen_catalog_is_complete_and_reachable`, `NoGameTermsTests.The_server_sources_contain_no_game_terms` (`AdminPlayerEndpoints`, `PlayerAdminCodes`, `admin-access-seed.json`), `CharacteristicNameScanTests.The_server_holds_no_name_of_a_characteristic` (`admin-reference-texts.json`), `RequestCostTests.The_main_reads_of_a_player_stay_inside_their_budgets` (`GET /api/v1/economy/` 9 запитів при бюджеті 8; рядок пакета текстів у звіті вартості: 3 запити, у бюджеті), `ProcessesTests.An_order_keeps_the_real_levels_it_was_made_at` (рецепт корпусу в сіді містить `resource_2`, який тест бере за ціль конвертації), `TechnologyReferenceTests.A_new_branch_of_the_technologies_is_added_from_the_admin_panel` (`PlayerTechnologyAdminService.cs` називає гілку `economy`), `CurrencyDebtTests.A_currency_with_the_flag_may_go_below_zero_and_an_incoming_amount_pays_the_debt`, `UpgradedLauncherTests.The_upgraded_launcher_is_the_light_launcher_with_the_next_mark` (значення `char_range` і `char_launcher_size` у сіді не більші за базові). Жоден із них не пов'язаний із кодами (перевірено за текстом помилок).
- Клієнт: `npm run lint` без помилок (попередження в чужих файлах), `npx tsc -b` чисто, `npm run build` успішний. `npm test`: 8 червоних тестів із чужих змін (`Characteristics.test.tsx` ×4, `ProductionPanel.test.tsx` ×1, `OrderQueuesPage.test.tsx` ×1, `ShipyardPage.test.tsx` ×2); `Characteristics.test.tsx` і `ProductionPanel.test.tsx` перевірено окремо: вони падають і зі старою регуляркою `productionStock.ts`, і зі старими ключами фікстур (картки складу не показують характеристик і посилання «Відкрити виробництво»); `OrderQueuesPage` падає на тому самому посиланні, `ShipyardPage` на рядку «енерг. · кінет.».
