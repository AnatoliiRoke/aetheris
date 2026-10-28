# Задача 00016 — Дизайн сторінки планети

> **Статус:** ВИКОНАНО автором (макет у `documentation/design/AETHERIS_4_pages`, 28.10.2026).
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** [задача 00008](00008-dyzain-ekrana-keruvannya-tsyvilizatsiieiu.md) (екран керування цивілізацією) — та сама оболонка, той самий стиль.
> **Пов'язані:** [задача 00019](00019-dyzain-ekrana-skladu.md) (екран складу — окремий пункт меню, на нього веде кнопка «Open» з панелі «Склад»), [задача 00017](00017-dyzain-storinky-budivnytstva.md) (сторінка будівництва), [задача 00018](00018-dyzain-storinky-doslidzhen.md) (сторінка дослідження).
> **Документи:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [ui-art/02 Екрани](../ui-art/02-ekrany-ta-navihatsiia.md), [game-design/02 Економіка](../game-design/02-ekonomika-ta-resursy.md), [game-design/03 Розвиток планет](../game-design/03-rozvytok-planet.md), [задача 00011](../tasks/00011-vymohy-budivli-ta-cherha-budivnytstva.md), [задача 00015](../tasks/00015-naselennia-kredyty-ta-torhova-valiuta.md)
> **Макет-джерело:** `documentation/design/aetheris-civ` — оболонка вже готова, її треба перенести без змін.

## 1. Мета

Макет **сторінки планети** — екрана, куди гравець заходить, щоб подивитися на свою планету:
назва, тип, координати, ресурси на складі, заповненість сховищ, енергія, населення.

Це **не** екран керування цивілізацією (00008): там огляд усіх планет, тут — одна
вибрана планета докладно. Оболонка (шапка, ліве меню, нижня смужка) та сама.

## 2. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Шапка** | Та сама, що в `aetheris-civ`: лого, нік, гліф раси, кредити, торгова валюта, енергія, тривоги, пошта, налаштування, вихід |
| **Ліве меню** | Те саме, що в `aetheris-civ`; активний пункт — «Планета» |
| **Заголовок** | Назва планети, тип (чіп), координати «сектор / система / орбіта» |
| **Планета** | Зображення планети (ART-12a…f), назва, тип, координати, показники |
| **Ресурси** | Таблиця: ресурс, кількість на складі, швидкість за годину |
| **Сховища** | Заповненість по групах (метали, кристали, паливо, вироблена продукція), тривога «склад заповнений» |
| **Енергія** | Виробіток, споживання, баланс, баланс після черги |
| **Населення** | Поточне / максимум, приріст за годину, час до максимуму |
| **Черга будівництва** | Що будується, таймер, наступні, скасування, кнопка «вибрати будівлю» (веде на 00017). Переліку всіх будівель тут немає |
| **Нижня смужка** | Перемикач планет, чат |

## 3. Промпт для Design (англійською, передати як є)

```
Design the PLANET SCREEN of an online browser game called "AETHERIS" — the screen a logged-in player opens when they click one of their planets. This is an in-game screen, not a landing page, not a battle screen, and not the empire overview.

IMPORTANT: this screen shares its frame with an existing design. Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel and card components, the same spacing, the same radii, the same shadows, the same icon style. Only the middle area changes. The shell is described below so you match it precisely.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships, trades and fights in turn-based tactical battles of 5-10 minutes. The planet screen is the place the player visits to check the state of one world: what is stored, how fast it fills, whether the energy balance is healthy, and what is currently being built. Timers run while the player is offline. Nothing is lost by neglect — only time is spent.

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

THE SHELL (reproduce exactly, do not redesign)
TOP BAR, 64 px tall, sticky, background rgba(7,11,22,0.92) with a 1 px bottom border of rgba(160,190,255,0.16). From left to right: a small square brand mark plus the word "AETHERIS" in Inter Bold 18 px with +4% letter spacing; then the player nickname in a rounded pill with a small race-colored glyph; a chip that reads "Standard" (or "Premium") in a muted border; then a centered row of resource counters, each a small distinct icon plus a number in tabular-nums: Credits, Trade Currency, Energy; then on the right three alert pills (incoming attack in danger red, battle in progress in danger red, storage full in warning yellow), a 44×44 mail icon button with a small accent badge showing the unread count, a 44×44 settings icon button, and a 44×44 sign-out icon button. A 1 px vertical separator before the icon buttons.
LEFT SIDEBAR, 220 px wide, sticky under the top bar, background rgba(11,16,32,0.6) with a 1 px right border. Thirteen rows, 44 px tall each, each with a 24×24 line icon on the left and a label: Overview, Planet, Storage, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Mail, Help, Settings. The active row ("Planet" on this screen) gets an accent-tinted background, accent-colored icon and label, and a 3 px accent bar on its left edge. The rows for Battle and Clan do not exist yet — do not show them.
BOTTOM BAR, thin, full width, above the bottom edge. On the left a horizontal strip of small planet thumbnails in a scrollable row, the current planet outlined in accent; on the right a chat text field with a placeholder and a 44×44 send button.
Icon style everywhere: line icons, 1.5 px stroke, 24×24 grid, color inherited from the text, always paired with a label or an aria-label.

THE MAIN AREA (this is what you design)
Page title at the top: the planet name in H1, with a type chip next to it ("Earth-like", "Volcanic", "Icy", "Desert", "Oceanic", or "Gas Giant") and, under the name, the coordinates in small secondary text formatted as "Sector 7 · System Kepler-442 · Orbit 2".

Below the title, a two-column layout on desktop. Left column (wider) holds the planet identity block and the resources. Right column (about 340 px) holds the construction queue.

PLANET IDENTITY BLOCK
A panel with the painted planet image on the left (a large round planet with a soft atmosphere glow, image fills the left third of the panel) and a list of facts on the right as a two-column definition list, label in secondary text and value in primary text: Planet type; Coordinates; Population (current / maximum, for example "742 / 1000"); Deposit richness (a multiplier, for example "1.2×"); Energy (a short summary "400 / 320" meaning output over consumption); Storage (a short summary of the fullest group, for example "Metals 6400 / 10000"). For a Gas Giant, the population row is replaced by "No population — extraction station only", and the panel is visually marked as a gas giant with a warm glow.

RESOURCES PANEL
A panel titled "Resources" with a banner at the top when any group is full: warning-colored, with a warning icon and the text "Metals storage is full — mining has stopped". Below it a table with three columns: Resource, Amount, Rate. Every row has a small resource icon and the resource name. Amount is a number in tabular-nums, right-aligned, with a thin space as the thousands separator (for example "6 400"). Rate is right-aligned and shows either "+120" for a mined resource or an em dash for a resource that is not produced here. Use real resource names: Iron, Titanium, Copper, Iridium (metals, gray icons), Quartz, Ruby, Sapphire, Obsidian (crystals, blue icons), Deuterium, Plasma, Antimatter (fuels, orange icons), Ether (gold). A planet never has Ether — do not show it.

STORAGE PANEL
Titled "Storage", directly under the resources, with a small secondary text button "Open" in the panel header that leads to the storage screen (a separate screen: a single table of quantities — this panel is only a summary). One row per resource group: the group name on the left, a horizontal meter in the middle, and the numbers "stored / capacity" on the right in tabular-nums. The meter track is a faint fill and the filled part is the group color: metals gray, crystals blue, fuel orange, products teal. A group that is full shows a small warning badge reading "Full" and its meter in the warning color. Show the four groups: Metals, Crystals, Fuel, Products.

ENERGY PANEL
Titled "Energy". Four rows of label and value: Output, Consumption, Balance, Balance after the queue. The balance value is colored green when positive and red when negative, and always carries a sign. A short explanatory line under it: "A building that would make the balance negative cannot be queued."

POPULATION PANEL
Titled "Population". A horizontal progress bar with the current value filled in accent and the track faint, the numbers "742 / 1000" in tabular-nums, and a line "Growth +50 / h · full in about 5 h 10 m". If the population is above the maximum, show a warning banner instead: "Above the limit — the excess is dying off at −1 / h".

CONSTRUCTION QUEUE PANEL (right column)
Titled "Construction" with a small muted counter on the right reading "2 / 2 slots used" and a "Cancel" text button on each entry. Entries are separated by 1 px borders. Each entry shows: a small building icon, the building name with its level ("Iron Mine · level 4"), a progress bar, a countdown in tabular-nums in the primary text, and a second muted line with the cost that was already paid (resource icons and amounts). The first entry is the one under construction and has an accent progress bar; the entries after it are queued and their bars are empty. One entry shows the state "Done — finishing…" with a success color and a soft accent glow. If the queue is empty, show a short empty state: "Nothing is being built. Pick a building from the planet overview."
At the bottom of the panel, a full-width secondary button "Choose a building to queue" that leads to the construction screen. This panel shows the queue only — the list of all buildings and the choice of what to build live on their own screen.

REQUIREMENTS AND BEHAVIOR
- WCAG AA contrast for all text. Color is never the only carrier of information: always pair color with an icon shape or a text label.
- All numbers, timers and counters use tabular-nums so digits do not jitter while a timer ticks down.
- Every interactive element has visible states: hover, focus (a 2 px accent ring with 2 px offset), pressed, disabled (opacity 0.45 with an explanation in the tooltip).
- Long English labels must not break the layout: columns grow, text wraps, nothing is truncated without a tooltip.
- UI copy in Ukrainian, short and clear for a newcomer, with an English variant of every heading, label and button — check that the English strings fit without breaking the layout.
- All data are made-up placeholders: planet names, coordinates, numbers. No real people or brands.

Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the panels stack in one column in the order: planet identity, resources, storage, energy, population, construction queue. No horizontal scrolling anywhere. All tap targets at least 44×44 px.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of the components you used with their states.
```

## 4. Ключові рішення, які треба врахувати в макеті

- ✅ **Оболонка вже готова** — `documentation/design/aetheris-civ` має шапку, ліве меню й нижню
  смужку. Мету новий макет має бути лише наповнення, без зміни оболонки.
- ✅ **Дані реальні, а не вигадані**: `GET /api/v1/planets/{id}` повертає `resources`,
  `storageGroups`, `energy`, `queue`, `buildings`, `populationMax`
  (`client/src/api/planetApi.ts`). Промт спирається на ці поля.
- ✅ **Швидкість видобуток — «+N» за годину** (`ratePerHour`), ресурс без шахти — прочерк.
- ✅ **Сховище по групах**, заповнена група зупиняє видобуток і дає тривогу.
- ✅ **Енергія: баланс і баланс після черги** (`energy.balance`, `energy.queuedBalance`);
  будівля, що зробить баланс від'ємним, не ставиться в чергу.
- ✅ **Населення належить планеті**, максимум задають будівлі й технології; понад лімітом —
  вимирання, а не відхилення.
- ✅ **Черга послідовна**, базово 2 місця; ресурси списуються при постановці, скасування
  повертає 100% — тому в черзі показуємо **вже сплачену** вартість.
- ✅ **Газовий гігант не колонізується** (game-design/03, розділ 2) — без населення.
- ✅ **Етеру на планеті немає** (game-design/02, розділ 2.4).
- ✅ **Кольори груп ресурсів** уже є в `aetheris-civ/src/icons.js`: метали `#A7B1C2`,
  кристали `#6FA8FF`, паливо `#FF9F43`, ефір `#E9C46A`, кредити `#3DD6A6`.
- ✅ **Кожен ресурс має власну форму іконки** — колір не єдиний носій.

## 5. Рішення автора (26.10.2026)

1. ✅ **Перелік усіх будівель на сторінці планети не показуємо.** Тільки черга. Перелік
   будівель і вибір того, що будувати — окремий екран (задача 00017). Щоб не було
   дублювання, у правій панелі черги в кінці є рядок-посилання на нього: «Choose a building
   to queue» — вторинна кнопка, що веде на екран будівництва.
2. ✅ **Прогноз приросту населення показуємо**: «Growth +50 / h · full in about 5 h 10 m».
   Час до максимуму рахується на сервері (задача 00015, служба балансу); у клієнті такого
   поля ще немає — його треба додати разом із макетом.

## 6. Відкриті питання

1. ❓ Чи показувати на картці планети **коефіцієнт багатства родовища** (0,8–1,9). Він
   впливає на видобуток, але це технічна величина — гравцеві про неї не треба знати. У промті
   він є в блоці показників; за потреби прибираємо.
2. ❓ Чи показувати в блоці «Енергія» ще й **споживання** окремою позицією, чи лише
   виробіток і баланс. У промті всі чотири — так, як віддає сервер.
