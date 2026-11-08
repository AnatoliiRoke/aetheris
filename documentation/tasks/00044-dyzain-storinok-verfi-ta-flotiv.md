# Задача 00044 — Дизайн сторінок «Верф» (проектування й складання кораблів) і «Флоти»

> **Статус:** ✅ ВИКОНАНО (запит автора 04.11.2026: «задача для генерації дизайну сторінки: 1) проектування, складання кораблів; 2) керування флотами»). Макети: `documentation/design/AETHERIS_shipyard` і `documentation/design/AETHERIS_fleets`.
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні). Два промпти, по одному на сторінку; макети кладуться в `documentation/design/aetheris-shipyard` і `documentation/design/aetheris-fleets`.
> **Спільно з:** та сама оболонка й стиль, що в [00016](00016-dyzain-storinky-planety.md)–[00019](00019-dyzain-ekrana-skladu.md), [00025](00025-dyzain-storinky-torhivli.md), [00033](00033-dyzain-storinky-karty.md).
> **Основа правил:** [game-design/05 Флот і кораблі](../game-design/05-flot-ta-korabli.md) (розділи 3 і 6), [game-design/05a Модулі](../game-design/05a-moduli-ta-komponenty.md), задачі [00038](00038-proektuvannia-korabliv.md) (проєкти), [00039](00039-skladannia-korabliv.md) (верф), [00040](00040-formuvannia-flotiv.md) (флоти), [00041](00041-modeliuvannia-korabliv.md) (числа слотів і трюма), [00042](00042-rozkladka-moduliv-po-korpusakh.md) (приналежність модулів до корпусів).
> **Документи стилю:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [ui-art/02 Екрани](../ui-art/02-ekrany-ta-navihatsiia.md).
> **Макет-джерело оболонки:** `documentation/design/aetheris-civ`, `documentation/design/AETHERIS_4_pages`, `documentation/design/aetheris-map-html`: оболонку (шапка, ліве меню, нижня смужка) переносимо без змін.

## 1. Мета
Макети двох сторінок гри, пункти яких у лівому меню зараз неактивні («Скоро»):
1. **«Верф»** — три вкладки: «Дизайн» (усі дизайни гравця, створення нового: корпус → слоти → модулі), «Збірка» (складання на верфі планети: черга, час, вартість, екіпаж) і «Розбирання» (розбір корабля).
2. **«Флоти»** — керування флотами: створення, склад, планета приписки, де флот зараз, ліміт флотів, командир; накази польоту поки вимкнені (польоти й бої в наступних етапах).

Оболонка та сама, змінюється лише середня область.

## 2. Правила гри, які показує макет (✅ рішення автора)

**Проєкт і конструктор**
- Корабель = **корпус** + **модулі**. Корпус це лише розмір і вмістимість: **кількість слотів**, базовий **трюм** (місткість у тоннах, для боєприпасів), міцність. Роль корабля створюють модулі.
- Слоти — **лише загальна кількість** (без розмірів і категорій). Модуль має бути **придатний цьому корпусу** (приналежність, задача 00042): непридатні модулі в списку видно, але з причиною.
- Обов'язково є **двигун** і **реактор**; сумарне **споживання енергії** модулів не більше **потужності реакторів**.
- **Бойові модулі** (зброя, броня, щити) не ставляться на мирні корпуси; **колоніальний модуль** лише на транспортні корпуси (Жук, Бик, Слон, Кит).
- Базовий трюм має вміщати **мінімальний боєкомплект**; трюм розширює модуль «Великий вантажний відсік».
- Конструктор показує **зведений список** модулів проєкту й те, що дав корпус (площин корабля поки немає).
- Під час зміни корпусу, якщо слотів стало менше, зайві модулі знімаються.
- Підсумок проєкту: маса, слоти (N з M), енергія (споживання / потужність), трюм, екіпаж, тяга й швидкість, сумарна шкода, міцність, щити; усі числа з характеристик деталей (дані).
- **Проєкти живуть у Конструкторському бюро**: кількість проєктів залежить від рівня бюро (до 30); проєкт належить гравцю й доступний на всіх планетах.
- **Дрони** — модулі, які не встановлюються в слот: їх возять у трюмі.
- Технологія або рівень верфі ще не відкрили корпус чи модуль — він видно закритим із вимогою.

**Складання на верфі**
- **Черга верфі своя на кожній планеті**, послідовна; місця залежать від рівня верфі (базово 2, до 4), преміум подвоює місця.
- Щоб поставити корабель у чергу, на **складі деталей цієї планети** мають бути корпус і всі модулі проєкту, а в планети — **екіпаж** (населення). Чого бракує — видно списком з кнопками «Виробити» (сторінка виробництва) або «Купити на ринку».
- **Вартість складання**: кредити, трохи матеріалів і **екіпаж** (населення планети); екіпаж береться одразу при постановці в чергу й повертається при скасуванні.
- **Час складання** від хвилин (легкий) до годин (надважкий); числа визначає моделювання.
- **Рівень верфі** обмежує класи корпусів (легкі → середні → важкі → надважкі).
- **Розбір корабля** на верфі: модулі повертаються на склад цілими, корпус — частиною ресурсів, екіпаж — у населення.
- Готовий корабель стоїть на планеті «вільним», доки його не додадуть у флот.

**Флоти**
- **Ліміт флотів** (старт 2; збільшують Космопорт, технологія й платне розширення).
- **Розмір флоту не обмежений**; мінімум 1 корабель.
- Кожен флот має **планету приписки** (базу) і **поточне місце** (де стоїть зараз; флот літає реальний час).
- **Командир** необов'язковий; джерела командирів ще немає — блок командира показаний, але вимкнений («Скоро»).
- Корабель може бути лише в одному флоті; кораблі додаються з вільних кораблів тієї планети, де стоїть флот.
- Флот можна перейменувати, розформувати (кораблі стають вільними), передати корабель між флотами на одній планеті.
- **Утримання**: кожен корабель щогодини коштує кредити; без кредитів флот не літає, але обороняє планету.
- Накази «Летіти», «Видобувати», «Атакувати», «Ескорт», «Колонізувати», «Повернутися» — у макеті є, але **вимкнені** з поясненням «Польоти з'являться пізніше».

## 3. Що має бути на екранах

### 3.1. «Верф»
| Блок | Зміст |
|---|---|
| Оболонка | Як у макетах; активний пункт «Верф» |
| Заголовок | «Верф» і назва планети; вкладки: «Дизайн», «Збірка», «Розбирання» |
| Дизайн: перелік | Плашки **всіх** дизайнів (проєктів) гравця; кнопка «Додати новий дизайн» **зверху й знизу** переліку; лічильник «N з M дизайнів» (від рівня Конструкторського бюро); сортування й фільтр **за типом корпусу** (Цивільні, Легкі, Середні, Важкі, Надважкі) |
| Плашка дизайну | Картинка корпусу, назва дизайну, корпус; **перелік встановлених модулів**; **характеристики**, які дають ці модулі (маса, слоти N з M, енергія, трюм, екіпаж, швидкість, шкода, міцність, щити); дії «Редагувати», «На збірку», «Видалити» |
| Новий дизайн: крок 1 | Порожнє вікно з переліком **корпусів**: плашки з картинкою й характеристиками (слоти, трюм, міцність, клас); закриті корпуси із замком і вимогою |
| Новий дизайн: крок 2 | Нове вікно: у **шапці** назва корпусу, його картинка й характеристики; **поле для назви** дизайну; **список слотів** корпусу, порожній слот — плашка з **«+»** усередині, зайнятий — модуль із кнопкою зняти; підсумок характеристик і перевірки (немає двигуна чи реактора, бракує енергії, трюм замалий, бойовий модуль на мирному корпусі); «Зберегти дизайн» |
| Вибір модуля | Натиск на «+» відкриває **попап із переліком модулів, як сторінка «Виробництво»**: ті самі кнопки сортування (Видобуток, Двигуни, Реактори, Зброя → Енергетична зброя, Кінетична зброя, Боєприпаси, Броня, Щити, Розвідка й маскування, Інші модулі) і плашки модулів з картинкою, назвою, Mk і характеристиками; «є на складі: N»; непридатні корпусу видно вимкненими з причиною |
| Збірка | Обраний дизайн, перевірка готовності (корпус і кожен модуль: потрібно / є на складі цієї планети; екіпаж: потрібно / вільно), вартість (кредити, матеріали), час, кнопки «Поставити в чергу», «Виробити бракуюче», «Купити на ринку»; черга верфі (спільний компонент черги, як у будівництві: номер, проєкт, час, прогрес, «Скасувати»); «Місць: N з M» |
| Готові кораблі | Список вільних кораблів планети (назва проєкту, корпус, екіпаж, утримання на годину) з кнопкою «Додати у флот» |
| Розбирання | Вибір вільного корабля, що повернеться (модулі цілими, частка ресурсів корпусу, екіпаж), кнопка «Розібрати» з підтвердженням |
| Стани | Немає Конструкторського бюро / верфі («Збудуйте …» з кнопкою «Будівництво»), немає проєктів, завантаження, помилка |

### 3.2. «Флоти»
| Блок | Зміст |
|---|---|
| Оболонка | Як у макетах; активний пункт «Флоти» |
| Заголовок | «Флоти», лічильник «Флотів: N з M» (ліміт), кнопка «Створити флот» |
| Список флотів | Картки або рядки: назва, планета приписки, де зараз («На орбіті: …» / «У польоті: …» з таймером — приклад на майбутнє), кількість кораблів і склад за класами, командир або «Без командира», утримання на годину, стан («Готовий», «Немає кредитів: не може літати») |
| Картка флоту | Склад (кораблі з корпусом, проєктом, справністю, екіпажем), сумарні показники (швидкість найповільнішого, трюм, шкода, міцність, щити, дрони в трюмі), кнопки «Додати кораблі», «Перемістити корабель», «Перейменувати», «Розформувати»; блок командира вимкнений «Скоро»; накази (Летіти, Видобувати, Атакувати, Ескорт, Колонізувати, Повернутися) вимкнені з поясненням; кнопка «Показати на карті» |
| Вільні кораблі | Кораблі планети, що не в жодному флоті, з вибором і «Додати у флот» |
| Створення флоту | Діалог: назва, планета приписки, вибір щонайменше одного вільного корабля; причини відмови (ліміт флотів, немає вільних кораблів) |
| Стани | Немає кораблів («Зберіть корабель на верфі» з кнопкою «Верф»), ліміт вичерпано, завантаження, помилка |

## 4. Промпт 1 для Design: «Верф» (англійською, передати як є)

```
Design the SHIPYARD SCREEN of an online browser game called "AETHERIS" — the screen a logged-in player opens from the "Shipyard" item of the left menu. It is where the player designs ships from a hull and modules, saves the designs, assembles ships in the shipyard queue of the current planet, and dismantles ships. This is an in-game screen, not a landing page and not a battle screen.

IMPORTANT: this screen shares its frame with the existing designs of the game (empire overview, planet, construction, research, storage, market, map). Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel, card, tab, queue and button components, the same spacing, radii, shadows and icon style. Only the middle area changes.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time with one shared universe. Players develop planets, mine resources, produce ship modules in factories, design and assemble spaceships, form fleets, trade on a market and later fight in turn-based tactical battles. Every action takes real time. Four playable races: Terrans (gray metal and blue), Litari (amber and terracotta), Drakkuri (red and black), Veirani (white and cyan). Tone: light, with a military flavor. Dark space style, deep navy backgrounds (#0B1020 to #070B16).

THE SHELL (reproduce exactly, do not redesign)
Top bar 64 px, sticky: brand mark and "AETHERIS", the player nickname pill with a race glyph, a "Standard"/"Premium" chip, resource counters (Credits, Trade currency, Energy) as small bordered plaques with an icon and a tabular number, alert pills, mail, settings and sign-out icon buttons. Left sidebar 220 px with 44 px rows (line icon + label): Overview, Planet, Construction, Research, Production, Storage, Shipyard, Fleets, Map, Market, Ranking, Mail, Help. The active row ("Shipyard") has an accent-tinted background, accent icon and label and a 3 px accent bar on the left. Bottom bar: a scrollable strip of planet thumbnails with the current planet outlined in accent, and a chat field with a send button. Line icons, 1.5 px stroke, 24×24.

THE MAIN AREA
Title "Shipyard" with the current planet name and place ("Sector 3 · System Lyra · Orbit 5") under it. Three tabs: "Design", "Assembly", "Dismantle".

TAB "DESIGN" — ALL DESIGNS OF THE PLAYER
The tab shows ALL ship designs the player has, as a grid of design cards (desktop 2-3 columns, mobile 1). A primary button "Add new design" stands both ABOVE and BELOW the list. Above the grid: a counter "Designs: 7 of 12" (the limit comes from the level of the Design Bureau building) and sorting / filtering by hull type as tabs or chips: "All", "Civilian", "Light", "Medium", "Heavy", "Super-heavy".
DESIGN CARD: the picture of the hull (16:9), the design name (for example "Barracuda — long range") and the hull name and class; the LIST OF INSTALLED MODULES (small icon, name, Mk, count, for example "Kinetic cannon Mk II ×2"); the CHARACTERISTICS these modules give as a compact grid of labelled values: mass, slots "18 / 24", energy use / reactor power, cargo hold, crew, speed, damage per round (energy / kinetic), durability, shield capacity. Actions: "Edit", "To assembly", "Delete".

ADD NEW DESIGN — STEP 1, CHOOSE A HULL
"Add new design" opens an empty window (a large modal on desktop, a full-screen sheet on mobile) with the list of HULLS: cards with the hull picture and its characteristics (class, number of slots, cargo hold in tonnes, durability). Hull class tabs on top ("Civilian", "Light", "Medium", "Heavy", "Super-heavy"). A hull not yet opened shows a lock and its requirement ("Shipyard level 4", "Mathematics 6"). Choosing a hull goes to step 2.

ADD NEW DESIGN — STEP 2, THE DESIGN WINDOW
A new window. HEADER: the hull name, its picture and its characteristics. Under it a text field "Design name". Then the LIST OF SLOTS of this hull as a grid of slot tiles: an empty slot is a dashed tile with a big "+" in the middle; a filled slot shows the module picture, name, Mk and its main characteristic, with a small remove button. A counter "Slots: 18 of 24". Drones are not installed in slots: they appear separately as cargo carried in the hold.
A summary of the design characteristics (mass, energy use / reactor power as a bar, cargo hold used and free, crew, speed, damage, durability, shields) and validation chips with an icon and text: "No engine", "No reactor", "Not enough reactor power: 140 / 120", "Cargo hold too small for the minimum ammunition", "Combat module on a civilian hull". Buttons "Save design" (primary) and "Cancel". Editing an existing design opens the same window; changing the hull warns: "The new hull has fewer slots: 3 modules will be removed".

MODULE PICKER POPUP
Pressing "+" on a slot opens a popup with the list of modules that looks like the PRODUCTION screen of the game: the same two rows of sort tabs — "Mining", "Engines", "Reactors", "Weapons", "Armour", "Shields", "Recon and stealth", "Other modules"; with "Weapons" selected a second row "Energy weapons", "Kinetic weapons", "Ammunition" — and the same module tiles: picture, name, Mk, characteristics (energy + or −, mass, thrust or damage or capacity), "In storage: 3" on this planet. Clicking a tile puts the module into the slot and closes the popup. A module that does not fit this hull stays visible but disabled with the reason: "Does not fit this hull", "Combat module on a civilian hull", "Colony module only on transport hulls", "Not researched: Physics 8".

TAB "ASSEMBLY"
A chosen design and a readiness checklist for THIS planet: the hull and every module with "needed / in storage" and a check or a cross; crew "needed 120 / free 860 population"; cost of assembly (credits and a small amount of materials) and time ("1 h 20 min"). Missing items have buttons "Produce" (goes to the Production screen) and "Buy on market". Primary button "Add to queue" (disabled with the reason when something is missing). Note: "The crew is taken from the population when the ship enters the queue and returns if you cancel."
Below: the shipyard queue in the same queue component as construction and research: order number, design name, hull thumbnail, remaining time in tabular-nums, progress bar, "Cancel"; the header "Slots: 2 of 4" (more slots with a higher shipyard level; premium doubles them).
Then "Ready ships on this planet": rows with the ship design name, hull, crew, upkeep per hour in credits, and a button "Add to fleet".

TAB "DISMANTLE"
Pick a free ship of this planet; show what returns: modules intact to the parts storage, part of the hull resources, the crew back to the population. Button "Dismantle" with a confirmation dialog.

EMPTY, LOADING AND ERROR STATES
No Design Bureau on the planet: "Build the Design Bureau to create designs" with a "Construction" button. No shipyard: "Build the Shipyard to assemble ships". No designs yet: illustration and the "Add new design" button. Skeleton loading, error with "Try again".

REQUIREMENTS
WCAG AA contrast; color is never the only carrier of information (icon or text label always). All numbers, timers and costs in tabular-nums; thousands separated by a thin space ("12 400"). Every interactive element has hover, focus (2 px accent ring, 2 px offset), pressed and disabled (opacity 0.45 with the reason in a tooltip) states. Respect reduced motion. UI copy in Ukrainian, short and clear, with an English variant of every heading, label and button that fits the layout. All names and numbers are made-up placeholders.
Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the design cards are one column, the hull list and the design window are full-screen sheets, the slot grid has 2-3 tiles per row, the module picker is a full-screen sheet and its sort tabs become horizontally scrollable chips. No horizontal page scrolling; tap targets at least 44×44 px.
Output a design-tokens table and a list of the components used with their states.
```

## 5. Промпт 2 для Design: «Флоти» (англійською, передати як є)

```
Design the FLEETS SCREEN of an online browser game called "AETHERIS" — the screen a logged-in player opens from the "Fleets" item of the left menu. It is where the player forms fleets from ready ships, sees their composition, home planet and current place, and (later) gives them orders. This is an in-game screen, not a battle screen and not the map.

IMPORTANT: this screen shares its frame with the existing designs of the game (empire overview, planet, construction, research, storage, market, map, shipyard). Reuse the same shell exactly: the same top bar, left sidebar, bottom bar, panels, cards, tabs, dialogs and buttons, the same spacing, radii, shadows and icon style. Only the middle area changes.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time with one shared universe. Ships are designed from a hull and modules and assembled in the shipyard of a planet; then the player groups them into fleets. Fleets fly between objects of the map in real time (flights and battles arrive in a later stage, so all orders are shown but disabled). Every ship costs credits per hour (upkeep); without credits a fleet cannot fly but still defends its planet. Four playable races: Terrans, Litari, Drakkuri, Veirani. Tone: light, with a military flavor. Dark space style, deep navy backgrounds.

THE SHELL (reproduce exactly, do not redesign)
Top bar 64 px with the brand, nickname pill with race glyph, "Standard"/"Premium" chip, resource plaques (Credits, Trade currency, Energy), alert pills, mail, settings and sign-out buttons. Left sidebar 220 px: Overview, Planet, Construction, Research, Production, Storage, Shipyard, Fleets, Map, Market, Ranking, Mail, Help; the active row "Fleets" has the accent background, accent icon and label and a 3 px accent bar. Bottom bar with the planet thumbnails strip and the chat field.

THE MAIN AREA
Title "Fleets" with a counter "Fleets: 2 of 3" (the limit grows with the Spaceport building, a technology and a paid extension) and a primary button "Create fleet" (disabled with the reason "Fleet limit reached" when the limit is used).
Filters: by home planet, by state ("Ready", "In flight", "No credits"), and a search by fleet name.

FLEET LIST
Cards (desktop two columns, mobile one): fleet name, home planet, current place ("On orbit: Lyra 5" or, as a future example, "In flight to Sector 3 · Vega, 01:42:10" with a progress bar), number of ships and composition by hull class as small chips ("Light ×6", "Medium ×2", "Transport ×1"), commander ("No commander"), upkeep per hour in credits, and a state chip with an icon: "Ready", "No credits: cannot fly". Selecting a card opens the fleet panel.

FLEET PANEL (right panel on desktop, full-screen sheet on mobile)
Header: fleet name (rename with a pencil button), home planet, current place, button "Show on map".
Summary: speed (the slowest ship), total cargo hold, total damage per round (energy / kinetic), durability, shield capacity, drones in the holds, total crew, upkeep per hour.
Ships table: ship name, design, hull class chip, condition percent bar, crew, actions "Move to another fleet" (only fleets on the same planet) and "Remove from fleet". A fleet always keeps at least one ship: removing the last ship asks to disband the fleet instead.
Commander block: an empty slot with a disabled "Assign commander" button and the tooltip "Coming soon: commanders arrive later".
Orders block: buttons "Fly", "Mine", "Attack", "Escort", "Colonize", "Return home" — real, finished buttons in the disabled state (opacity 0.45) with the tooltip "Flights arrive later".
Footer buttons: "Add ships", "Disband fleet" (confirmation dialog: "Ships stay on the planet as free ships").

FREE SHIPS
A section "Free ships" grouped by planet: ships that are in no fleet, with checkboxes and a button "Add to fleet…" that opens a picker of fleets on the same planet. Note: "A ship can be in only one fleet; ships join fleets on the planet where they stand."

CREATE FLEET DIALOG
Fields: name, home planet (select), and a list of free ships of that planet with checkboxes (at least one is required). Error reasons inline: "Fleet limit reached: 3 of 3", "No free ships on this planet". Buttons "Create" and "Cancel".

EMPTY, LOADING AND ERROR STATES
No ships at all: illustration and "Assemble a ship in the shipyard" with a "Shipyard" button. No fleets yet but free ships exist: "Create your first fleet". Skeleton loading and an error with "Try again".

REQUIREMENTS
WCAG AA contrast; color is never the only carrier of information. Numbers, timers and costs in tabular-nums with thin-space thousands separators. Visible hover, focus (2 px accent ring, 2 px offset), pressed and disabled states (opacity 0.45 plus the reason). Respect reduced motion. UI copy in Ukrainian with an English variant of every label that fits the layout. Made-up placeholder names and numbers only.
Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the sidebar becomes a bottom tab bar, the fleet list is one column, the fleet panel is a full-screen sheet, filters are scrollable chips. No horizontal page scrolling; tap targets at least 44×44 px.
Output a design-tokens table and a list of the components used with their states.
```

## 6. Що не входить
- Реалізація в клієнті й на сервері (задачі 00038, 00039, 00040 і окрема задача перенесення макетів у клієнт за зразком 00026/00034).
- Польоти, накази, бої, командири: у макетах лише вимкнені кнопки й блоки.
- Числа слотів, трюма, часу й вартості складання (визначає моделювання, 00041); у макеті вигадані приклади.
- Нові картинки корпусів і модулів: є в колекції зображень (задача 00035).

## 7. Результат виконання
