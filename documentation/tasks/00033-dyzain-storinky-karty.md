# Задача 00033 — Дизайн сторінки «Карта»

> **Статус:** ✅ ВИКОНАНО автором (макет у `documentation/design/aetheris-map-html`, 01.11.2026). Перенесення в клієнт: [00034](00034-perenesty-storinku-karty-v-kliyent.md).
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** [задача 00008](00008-dyzain-ekrana-keruvannya-tsyvilizatsiieiu.md) (екран керування цивілізацією), та сама оболонка й стиль, що в [00016](00016-dyzain-storinky-planety.md), [00017](00017-dyzain-storinky-budivnytstva.md), [00018](00018-dyzain-storinky-doslidzhen.md), [00019](00019-dyzain-ekrana-skladu.md), [00025](00025-dyzain-storinky-torhivli.md).
> **Основа:** [задача 00010](00010-ekran-keruvannia-tsyvilizatsiieiu.md) (оболонка гри: шапка, ліве меню з пунктом «Карта», смужка планет, координати «Сектор · Система · Орбіта») і [задача 00020](00020-perenesty-chotyry-novi-storinky-hry.md) (спільна рамка екранів, стандартні панелі й черги, сітка плашок, адаптивність).
> **Документи:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [ui-art/02 Екрани](../ui-art/02-ekrany-ta-navihatsiia.md) (розділ 5 «Карта»), [game-design/09 Світ і карта](../game-design/09-svit-ta-karta.md), [game-design/03 Розвиток планет](../game-design/03-rozvytok-planet.md), [game-design/05 Флот](../game-design/05-flot-ta-korabli.md), [game-design/07 PvE](../game-design/07-pve-piraty-ta-zahrozy.md).
> **Макет-джерело оболонки:** `documentation/design/aetheris-civ` та `documentation/design/AETHERIS_4_pages`: оболонку (шапка, ліве меню, нижня смужка) переносимо без змін. Готовий макет автор поклав у `documentation/design/aetheris-map-html`.

## 1. Мета
Макет **сторінки «Карта»**: екрана, де гравець бачить свій всесвіт у трьох масштабах (галактика → сектор → система), свої планети й те, що відкрили радари, обирає об'єкт і бачить його картку з діями. Пункт «Карта» в лівому меню зараз неактивний («Скоро»); макет робить його повноцінним екраном. Оболонка та сама, змінюється лише середня область.

## 2. Що вже є в грі (від чого відштовхується макет)

З [00010](00010-ekran-keruvannia-tsyvilizatsiieiu.md) і [00020](00020-perenesty-chotyry-novi-storinky-hry.md) і з даних світу ([00004](00004-heneratsiia-vsesvitu.md)):

| Що | Як воно є | Для макета |
|---|---|---|
| **Будова світу** | Галактика → сектор → зоряна система; координати мають три осі `(x, y, z)`, зараз `z = 0` | Три масштабу; карта плоска, але місце під третю вісь у даних є |
| **Сектор** | 50–150 систем, одна туманність, 5–10 вільних астероїдних полів; на старті 3 сектори, нові додає адмін; є «сектор новачків» | Багато точок: потрібне групування, підписи за масштабом, пошук |
| **Система** | Зірка і **12 орбіт**, на них 3–9 об'єктів (планети, газові гіганти); найзовнішня орбіта зарезервована під астероїдне поле; переліт усередині системи займає фіксований час | Концентричні орбіти, об'єкти на них, поле на зовнішній |
| **Об'єкти** | Типи планет (Землеподібна, Крижана, Вулканічна, Пустельна та інші) з готовими картинками типів; газові гіганти (лише станції); астероїдні поля (на орбіті або вільні в секторі); туманності (радар гірший, стрибка немає) | Свій знак для кожного класу, колір не єдиний носій |
| **Власність** | Планета вільна, моя або чужа (нік і раса власника); колонізувати можна лише вільну; тераформована планета лишається вільною | Позначки «моя», «чужа», «вільна», «тераформована» |
| **Туман війни** | Гравець бачить лише радіус своїх радарів і обсерваторій; решта карти: відомі зірки без подробиць; новачок на старті бачить лише свою планету | Три стани об'єкта: відкрито, відомо без подробиць, невідомо |
| **Координати в тексті** | Рядок «Сектор · Система · Орбіта» вже є в картках планет | Той самий рядок у картці об'єкта й у заголовку |
| **Флоти, пірати, клани, бій** | Ще не зроблені; у меню «Флоти» неактивний, «Бій» і «Клан» неактивні | Шари й дії для них у макеті є, але вимкнені з підказкою «Скоро» |
| **Перемикач планет** | У нижній смужці оболонки | Вибір планети центрує на ній карту |

## 3. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Шапка, ліве меню, нижня смужка** | Ті самі, що в `aetheris-civ`; активний пункт «Карта» |
| **Заголовок** | «Карта», під ним хлібні крихти «Галактика › Сектор 3 › Система Ліра»; клік по кроці піднімає на цей масштаб |
| **Полотно карти** | Велика область (основа екрана); колесо миші й щипок міняють масштаб, перетягування рухає; кнопки «+», «−», «Моя планета» (центр на поточній) |
| **Галактика** | Сектори як великі комірки/області з назвою, позначкою «Сектор новачків», числом моїх планет; двічі клік або «Відкрити» веде в сектор |
| **Сектор** | Зоряні системи як зірки; туманність напівпрозорою хмарою (системи всередині позначені); вільні астероїдні поля між системами; мої системи підсвічені; радіус радара як тонке коло навколо моєї планети; відстані на лініях між системами при наведенні; на великій кількості точок зірки групуються й підписуються за масштабом |
| **Система** | Зірка в центрі, 12 тонких орбіт, об'єкти на орбітах (картинка типу планети, номер орбіти), газові гіганти, поле на зовнішній орбіті; планети лише візуально обертаються (карта статична) |
| **Стани об'єкта** | Відкрито (повна картка), відомо без подробиць (блідий знак, підпис «Невідомо: потрібен радар»), невідомо (не показується) |
| **Шари** | Перемикачі: «Мої», «Чужі колонії», «Вільні планети», «Астероїдні поля», «Туманності», «Газові гіганти»; вимкнені з підказкою «Скоро»: «Флоти», «Клан», «Вороги», «Пірати» |
| **Пошук** | Поле: система, планета або гравець за назвою або ніком; підказки списком; вибір центрує карту |
| **Картка об'єкта** | Права панель (на телефоні шторка знизу): картинка типу, назва, тип, розмір, координати «Сектор · Система · Орбіта», власник (нік, гліф раси), родовища, якщо розвідані (для поля: маса, склад після геологорозвідки), час польоту (усередині системи фіксований, між системами години), кнопки дій |
| **Дії картки** | «Відкрити планету» (моя, активна); «Колонізувати» (вільна планета), «Видобувати» (поле, газовий гігант), «Розвідати», «Атакувати», «Ескорт»: усі вимкнені з поясненням «Потрібен флот: флоти зʼявляться пізніше» (флоти ще не зроблені), але видно, що кожна дія колись буде |
| **Легенда** | Мала згорнута панель зі знаками об'єктів і станів |
| **Міні-карта** | Куток полотна: поточний вид серед усього масштабу |
| **Вид списком** | Перемикач «Карта / Список»: той самий набір об'єктів таблицею (назва, тип, власник, координати), доступна альтернатива полотну для клавіатури й читачів екрана |
| **Стани екрана** | Завантаження (скелет), порожній туман («Ви бачите лише свою планету. Збудуйте радар, щоб побачити більше» з кнопкою «Будівництво»), помилка з «Спробувати ще раз» |

## 4. Промпт для Design (англійською, передати як є)

```
Design the MAP SCREEN of an online browser game called "AETHERIS" — the screen a logged-in player opens from the "Map" item of the left menu. This is an in-game screen, not a landing page, not a battle screen, and not the empire overview.

IMPORTANT: this screen shares its frame with existing designs. Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel and card components, the same spacing, the same radii, the same shadows, the same icon style. Only the middle area changes. The shell is described below so you match it precisely.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships, trades and fights in turn-based tactical battles of 5-10 minutes. Fleets fly between objects on the map in real time, and the flight takes real time even while the player is offline. 

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

THE SHELL (reproduce exactly, do not redesign)
TOP BAR, 64 px tall, sticky, background rgba(7,11,22,0.92) with a 1 px bottom border of rgba(160,190,255,0.16). From left to right: a small square brand mark plus the word "AETHERIS" in Inter Bold 18 px with +4% letter spacing; then the player nickname in a rounded pill with a small race-colored glyph; a chip that reads "Standard" (or "Premium") in a muted border; then a centered row of resource counters, each a small distinct icon plus a number in tabular-nums: Credits, Trade Currency, Energy; then on the right three alert pills (incoming attack in danger red, battle in progress in danger red, storage full in warning yellow), a 44×44 mail icon button with a small accent badge showing the unread count, a 44×44 settings icon button, and a 44×44 sign-out icon button. A 1 px vertical separator before the icon buttons.
LEFT SIDEBAR, 220 px wide, sticky under the top bar, background rgba(11,16,32,0.6) with a 1 px right border. Thirteen rows, 44 px tall each, each with a 24×24 line icon on the left and a label: Overview, Planet, Storage, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Mail, Help, Settings. The active row ("Map" on this screen) gets an accent-tinted background, accent-colored icon and label, and a 3 px accent bar on its left edge. The rows for Battle and Clan do not exist yet — do not show them.
BOTTOM BAR, thin, full width, above the bottom edge. On the left a horizontal strip of small planet thumbnails in a scrollable row, the current planet outlined in accent; on the right a chat text field with a placeholder and a 44×44 send button.
Icon style everywhere: line icons, 1.5 px stroke, 24×24 grid, color inherited from the text, always paired with a label or an aria-label.

THE MAIN AREA (this is what you design)
Page title at the top: "Map" in H1, and under it breadcrumbs that double as zoom-out buttons: "Galaxy › Sector 3 › Lyra system". On the right of the title row a segmented control "Map / List".

THE MAP CANVAS
The canvas takes all the free area under the title row (on desktop about 1000-1500 px wide and at least 560 px tall) with a deep navy space background (#0B1020 to #070B16), a few faint stars, rounded corners and a 1 px border. The map is 2D and painted, in the same dark space style as the rest of the game. Zoom with the mouse wheel or a pinch, drag to pan. Controls in the corner of the canvas: round 44×44 buttons "+", "−" and "My planet" (centers the map on the current planet), a small scale label. A mini-map in the opposite corner shows the current view inside the whole scale level. A collapsed "Legend" button opens a small panel with every symbol.

THREE SCALES (show all three as separate states of the same screen)
1. GALAXY: sectors as large softly outlined regions placed on a plane, each with its name ("Sector 1"), a small "Newcomers" badge on the sector where new players start, and the number of the player's own planets. Hover highlights a sector and shows a tooltip. Click selects it, double click or the "Open" button zooms in.
2. SECTOR: 50-150 star systems as small stars of different sizes; one translucent nebula cloud in violet and teal (systems inside it carry a tiny cloud mark, and the nebula is labeled "Radar is weaker here, no jump drive"); 5-10 free asteroid fields as small clusters of dots between the systems. The player's systems have an accent-colored ring; the radar range of the player's planets is a thin dashed accent circle; objects outside the radar are only faint known stars without names. At far zoom, nearby stars merge into numbered cluster bubbles; at closer zoom they split and show names. On hover over a system a tooltip shows the name, the number of bodies and the distance from the current planet.
3. SYSTEM: a star in the center (glowing), twelve thin concentric orbits with small orbit numbers, objects on the orbits: planets drawn with the picture of their type (earth-like, ice, volcanic, desert and others) in several sizes, gas giants as large banded planets with a small station icon when a station stands there, and an asteroid field as a ring of dots on the outermost orbit. 3 to 9 objects per system, so many orbits are empty. Planets only rotate around the star visually, the positions are fixed.

OBJECT STATES AND OWNERSHIP (never rely on color alone: pair every color with a shape, an icon or a text label)
- Own planet: accent ring plus a small house icon.
- Another player's colony: a nickname pill with a race glyph and a small flag icon, neutral outline.
- Free planet: dashed outline plus a small "plus" icon, meaning it can be colonized.
- Terraformed free planet: the same plus a small leaf icon.
- Known without details: a pale dim symbol and a "?" mark, tooltip "Unknown: a radar is needed".
- Unknown objects are not drawn at all.
- Asteroid field: dots symbol; gas giant: banded symbol; nebula: cloud outline.

LAYERS PANEL
A compact panel over the canvas (a collapsible chip row on mobile) with toggles: "Mine", "Other colonies", "Free planets", "Asteroid fields", "Nebulae", "Gas giants" (all on by default), and four disabled toggles with a small clock icon and the tooltip "Coming soon": "Fleets", "Clan", "Enemies", "Pirates".

SEARCH
A search field above the canvas: find a system, a planet or a player by name or nickname. A dropdown lists results with a type icon and the place ("Sector 3 · System Lyra · Orbit 5"). Choosing a result centers the map on it and opens its card.

OBJECT CARD
Clicking an object opens a card in a right panel 340 px wide (on mobile a bottom sheet, 60% of the screen height, draggable to full). Content: the picture of the object type at the top (16:9, or a neutral placeholder emblem when there is none), name, type chip, size, the place "Sector 3 · System Lyra · Orbit 5", the owner (nickname pill with race glyph) or "Free", a short list of known deposits (small resource icons with names; "Not scanned yet" when unknown), for an asteroid field the remaining mass and the composition after a geological survey, and the travel time: "Inside a system: 10 min" for a flight inside a system, hours between systems. Action buttons in a column, each 44 px tall: "Open planet" (primary, only for the player's own planets), and secondary "Colonize" (free planet), "Mine" (asteroid field or gas giant), "Scout", "Attack", "Escort". Every action except "Open planet" is disabled in this version with the tooltip "A fleet is needed: fleets arrive later" — design them as real, finished buttons in the disabled state (opacity 0.45) so the author sees where they will be. A route line with a timer on the map is part of the design for the future fleets: draw one example route as a dashed line with a small ship icon and a countdown in tabular-nums, clearly marked as an example in the design notes only.

LIST VIEW
The "List" segment replaces the canvas by a table of the same objects at the current scale: icon and name, type, owner, place, distance from the current planet, and a "Show on map" button. It has the same search and layer filters. Keyboard users can select an object and open its card from here.

EMPTY, LOADING AND ERROR STATES
Loading: skeleton canvas with a pulsing dim star field. Empty fog: a newcomer sees only their own planet and its radar circle; a banner at the top says "You see only your planet. Build a Radar to see more." with a button "Construction". Error: a message with a "Try again" button.

REQUIREMENTS AND BEHAVIOR
- WCAG AA contrast for all text. Color is never the only carrier of information.
- All numbers, timers, coordinates and distances use tabular-nums. Large numbers use a thin space as the thousands separator (for example "12 400").
- Every interactive element has visible states: hover, focus (a 2 px accent ring with 2 px offset), pressed, disabled (opacity 0.45 with an explanation in the tooltip). The canvas itself is focusable and can be moved with the arrow keys and zoomed with "+" and "-".
- Respect reduced motion: no zoom animation or rotation when the user asks for less motion.
- UI copy in Ukrainian, short and clear for a newcomer, with an English variant of every heading, label and button — check that the English strings fit without breaking the layout.
- All data are made-up placeholders: sector, system and planet names, nicknames. No real people or brands.

Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the canvas takes the full width, the object card becomes a bottom sheet, the layers panel becomes a horizontally scrollable chip row, the zoom buttons stay in the corner, and the map is also pinch-zoomable. No horizontal scrolling of the page. All tap targets at least 44×44 px.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of the components you used with their states.
```

## 5. Ключові рішення, які треба врахувати в макеті

- ✅ **Три масштабу:** галактика → сектор → система ([game-design/09](../game-design/09-svit-ta-karta.md), розділ 2); хлібні крихти й колесо/щипок.
- ✅ **Карта статична:** планети обертаються навколо зірки лише візуально; позиції фіксовані.
- ✅ **12 орбіт у системі, 3–9 об'єктів**; поле на зовнішній орбіті або вільно в секторі.
- ✅ **Туман війни:** гравець бачить лише радіус радарів; решта: відомі зірки без подробиць; новачок на старті бачить лише свою планету.
- ✅ **Туманність:** радар гірший, стрибка немає, системи всередині позначені.
- ✅ **Вільні планети:** колонізувати можна лише вільні; тераформована лишається вільною.
- ✅ **Дії картки з [ui-art/02](../ui-art/02-ekrany-ta-navihatsiia.md):** «Атакувати», «Розвідати», «Видобувати», «Колонізувати», «Ескорт»; усі, крім «Відкрити планету», поки вимкнені з поясненням.
- ✅ **Шари:** свої / клан / вороги / пірати / ресурси; клан, вороги й пірати «Скоро».
- ✅ **Оболонка не змінюється** (як у 00016–00019 і 00025); пункти «Бій» і «Клан» у макеті оболонки не показуємо (як у 00025).
- ✅ **Колір не єдиний носій інформації:** знак, іконка або підпис; доступна альтернатива полотну: вид списком і керування клавіатурою.
- ✅ **Технічна основа (для наступної задачі, не для макета):** полотно малюється PixiJS, панелі й картка звичайним React на спільних токенах; усі координати вже тривимірні `(x, y, z)`, при `z = 0` карта плоска.

## 6. Що не входить
- Реалізація в клієнті (окрема задача після макета, за зразком 00026): ендпоінти для карти з урахуванням радара й туману війни, полотно, пошук, картки, вид списком, тести.
- Флоти й їхні маршрути, пірати, клани, бої, стрибкові брами: на макеті лише як вимкнені шари й дії.
- Радар як будівля з радіусом видимості на сервері (окрема задача; макет лише показує його коло й порожній стан).
- Картинки об'єктів: типи планет уже мають готові малюнки, для решти стоїть спільна заглушка (ART-17); нових малюнків ця задача не вимагає.

## 7. Відкриті питання
1. ❓ Чи показувати на карті сектора **всі** відомі системи сектора, чи лише відкриті радаром (на старті новачок бачить лише свою планету, тож сектор для нього майже порожній)? **За замовчуванням у макеті:** відомі зірки без подробиць видно, решта ні.
2. ❓ Як виглядає галактика, коли секторів лише три: комірки в ряд чи розкид за координатами `(x, y)`? **За замовчуванням:** за координатами, місце для нових секторів по краях.
3. ❓ Чи потрібен на карті системи показ відстані в часі польоту до кожного об'єкта? **За замовчуванням:** лише в картці об'єкта.

## 8. Результат виконання

Макет зроблено автором у Claude Design й лежить у `documentation/design/aetheris-map-html` (статичний HTML + `map.css`, класи згенеровано як `.mN`): `index.html` (зміст), `map-galaxy`, `map-sector`, `map-system` (і англійський `map-system-en`), `map-list`, `map-loading`, `map-fog`, `map-error`, `map-mobile-system`, `map-mobile-sector` (англійська). Усі три масштаби, вид списком, стани й мобільні розкладки є; картка об'єкта, шари, легенда, міні-карта й пошук показані. Три відкриті питання розділу 7 закрито в [00034](00034-perenesty-storinku-karty-v-kliyent.md) (рішення за замовчуванням).
