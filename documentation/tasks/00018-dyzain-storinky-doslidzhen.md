# Задача 00018 — Дизайн сторінки дослідження

> **Статус:** ВИКОНАНО автором (макет у `documentation/design/AETHERIS_4_pages`, 28.10.2026).
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** [задача 00016](00016-dyzain-storinky-planety.md) (сторінка планети), [задача 00017](00017-dyzain-storinky-budivnytstva.md) (сторінка будівництва) — та сама оболонка й стиль.
> **Документи:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [game-design/04 Технології](../game-design/04-tekhnolohii.md), [задача 00012](../tasks/00012-naukovyi-tsentr-ta-cherha-doslidzhen.md)
> **Макет-джерело:** `documentation/design/aetheris-civ` — оболонка вже готова.

## 1. Мета

Макет **сторінки дослідження**: дерево технологій за гілками, параметри кожної технології,
черга досліджень і те, що заважає почати. Це екран, куди гравець іде, щоб обрати
технологію, поставити її в чергу й бачити, що відкриється далі.

## 2. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Шапка** | Та сама, що в `aetheris-civ` |
| **Ліве меню** | Те саме; активний пункт — «Дослідження» |
| **Заголовок** | «Дослідження» + рівень наукового центру |
| **Черга досліджень** | Зверху, на всю ширину: що досліджується, таймер, наступні, скасування, місця |
| **Гілки** | Вкладки: Економіка, Двигуни й логістика, Колонізація, Торгівля. «Флот» і «Оборона» — порожні, з написом «скоро» |
| **Перелік технологій** | Плиткою: назва, тип, рівень, поточний ефект, вартість наступного рівня, час, вимоги, кнопка |
| **Стани технології** | Невідома, доступна, у черзі, досліджується, вивчена, недоступна з причиною |
| **Нижня смужка** | Перемикач планет, чат |

## 3. Промпт для Design (англійською, передати як є)

```
Design the RESEARCH SCREEN of an online browser game called "AETHERIS" — the screen where a logged-in player picks a technology to research and queues it. This is an in-game screen, not a landing page, not a battle screen.

IMPORTANT: this screen shares its frame with an existing design. Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel and card components, the same spacing, the same radii, the same shadows, the same icon style. Only the middle area changes. The shell is described below so you match it precisely.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships, trades and fights in turn-based tactical battles of 5-10 minutes. Technologies are the long-term progression: they unlock new buildings, new ship modules, new production recipes, and they give permanent percentage bonuses. Research belongs to the player, not to a planet — once learned, it applies to every colony. Research happens in the Science Center on the planet; the level of that building decides which technologies are reachable. There is no paid speed-up and the queue is strictly sequential.

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

THE SHELL (reproduce exactly, do not redesign)
TOP BAR, 64 px tall, sticky, background rgba(7,11,22,0.92) with a 1 px bottom border of rgba(160,190,255,0.16). From left to right: a small square brand mark plus the word "AETHERIS" in Inter Bold 18 px with +4% letter spacing; the player nickname in a rounded pill with a small race-colored glyph; a chip reading "Standard" (or "Premium") in a muted border; a centered row of resource counters, each a small distinct icon plus a number in tabular-nums: Credits, Trade Currency, Energy; then on the right three alert pills (incoming attack in danger red, battle in progress in danger red, storage full in warning yellow), a 44×44 mail icon button with a small accent badge showing the unread count, a 44×44 settings icon button, and a 44×44 sign-out icon button. A 1 px vertical separator before the icon buttons.
LEFT SIDEBAR, 220 px wide, sticky under the top bar, background rgba(11,16,32,0.6) with a 1 px right border. Thirteen rows, 44 px tall each, each with a 24×24 line icon on the left and a label: Overview, Planet, Storage, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Mail, Help, Settings. The active row ("Research" on this screen) gets an accent-tinted background, accent-colored icon and label, and a 3 px accent bar on its left edge.
BOTTOM BAR, thin, full width. On the left a horizontal strip of small planet thumbnails in a scrollable row, the current planet outlined in accent; on the right a chat text field with a placeholder and a 44×44 send button.
Icon style everywhere: line icons, 1.5 px stroke, 24×24 grid, color inherited from the text, always paired with a label or an aria-label.

PAGE HEADER
A small breadcrumb line "Research" in muted text, then the page title "Research" in H1 and, on the right, a compact status block showing the Science Center: a small building icon, the text "Science Center · level 2" and beneath it a muted line "Higher levels unlock more technologies". If the Science Center is not built yet, this block becomes a warning panel: "Build a Science Center level 2 to start researching" with a secondary button "Go to construction".

THE RESEARCH QUEUE — a full-width band at the top, above the branch list
A panel titled "Research" with a counter on the right reading "2 / 2 slots used" and, when the queue is not full, a muted hint "+1 slot available", or a warning line "The queue is full — cancel a research to queue another".
The queue is a single horizontal row of cards (on mobile it becomes a vertical list). Each card shows: a small technology icon, the technology name with the level ("Deep Mining · level 3"), a wide progress bar, a countdown in tabular-nums in the primary text, and a muted line with the cost that was already paid (resource icons and amounts — for example Quartz 1 200, Credits 3 500).
The first card is the one under research: accent progress bar and a small status line "Researching" in the accent color. The cards after it are waiting: an empty progress track and a muted status line "Waiting". The last card may show the state "Done — finishing…" in the success color with a soft accent glow.
Each queued card has a small 36 px secondary "Cancel" button in its top right corner. Under the queue, a muted note: "Cancelling returns the full cost. Research belongs to you — once learned, it applies to every colony."
If the queue is empty, the band shows a short empty state instead: "No research running. Choose a technology below."

BRANCH SELECTOR
A row of branch tabs directly under the queue band: Economy, Fleet, Engines and Logistics, Defence, Colonisation, Trade. The active tab is accent-colored with an accent underline, not by color alone. The tabs carry NO counters — no "6 unknown" badges.

THE TECHNOLOGY LIST — the main content
A responsive grid of technology cards: 3 columns on desktop at 1440 px, 4 columns at 1920 px, 1 column on mobile. Cards are of equal height, each with a 24 px gap.
Above the grid, a row of filter controls: a search field with the placeholder "Search technologies" and a state filter with chips — All, Available, In queue, Researching, Learned, Locked. The active chip is accent-colored with an accent underline.
Two branches are still empty: Fleet and Defence. Their technology records arrive with the tasks that implement ship production and combat. In the mockup, selecting one of those branches shows an empty state instead of cards: a small muted lab icon, the line "This branch opens with the next update" and a muted line "Ships and combat are coming later. For now, focus on the economy and logistics." Do not invent placeholder technology cards for them.

EACH TECHNOLOGY CARD contains, from top to bottom:
1. A header row: a 32×32 line icon of the technology in a rounded square tile with a subtle accent-tinted background, then the technology name in H3, and in the top right corner a state badge. Use these badges and states:
   - "Unknown" — a muted lock chip. The card is dimmed and shows only the name and the branch. A dark silhouette replaces the icon.
   - "Available" — a muted outline chip. The card is fully readable.
   - "In queue" — a warning-colored chip with a clock icon.
   - "Researching" — an accent-colored chip with a progress dot.
   - "Learned · level 4" — a success-colored chip with a check icon.
   - "Locked" — a danger-colored chip with a lock icon.
2. A one-line description in the soft secondary text, for example "Increases metal mining output by 8% per level."
3. An "Effect" block: one line with a small icon, the effect name and its value in tabular-nums. Real effect names: Metal mining output +8%; Crystal mining output +8%; Energy output +10%; Storage capacity +10%; Construction speed +10%; Population maximum +10%; Fuel consumption −6%; Fleet limit +1; Cargo capacity +10%; Market commission −5%; Portal rod consumption −10%. Also show a line "Unlocks: Building" or "Unlocks: Module" with a small icon when the technology opens a building, a module type, a recipe or a ship class.
4. A divider, then the "Next level" block in a slightly raised inner surface (bg-3, 1 px border, 10 px radius):
   - a small muted label "Next level 3";
   - a row of cost chips, each a small resource icon plus the amount in tabular-nums, for example Quartz 1 200, Credits 3 500, Microprocessors 240. A cost the player cannot afford is drawn in the danger color with a small warning icon, and the number is dimmed;
   - a row "Research time 1 h 12 m" in tabular-nums;
   - a row of requirement chips: a green chip with a check icon "Requirements met", or a red chip with a warning icon naming what is missing, for example "Required: Science Center, level 4" or "Required: Deep Mining, level 2".
5. A full-width action button at the bottom, 44 px tall: a primary-styled button reading "Research" for an available technology, or a secondary button reading "Queue" for one that is already in the queue. When the action is not allowed, the button is disabled (opacity 0.45) and the card shows, above the button, a short list of the exact reasons in danger text. Use these real reason strings: "Not enough resources", "Queue is full", "Required: Science Center, level 4", "Required: Deep Mining, level 2", "Required technology — it unlocks through research", "This level is not unlocked yet", "Highest level". A player must never have to guess why a button is disabled.

TECHNOLOGY NAMES
Use these real names across the cards, in the branches that are not empty: Deep Mining, Crystallography, Energy, Warehouse Logistics, Industrial Automation, Demography (Economy); Sensors, Deuterium Engine, Plasma Engine, Warp Engine, Engine Efficiency, Fleet Command, Cargo Holds (Engines and Logistics); Colonial Administration, Colonial Transports, Asteroid Mining, Nebula Collectors, Ether Engineering (Colonisation); Trade Law, Portal Technology, Portal Stabilization, Convoy Escort (Trade).
Do not use the names of the Fleet and Defence branches — those two branches are empty in this mockup and show an empty state instead. The names Hull Classes, Ballistics, Lasers, Plasma Weapons, Armor, Shields, Planetary Guns, Planetary Shield, Storage Masking and Early Warning belong to those branches and must not appear in the cards.

REQUIREMENTS AND BEHAVIOR
- WCAG AA contrast for all text. Color is never the only carrier of information: always pair color with an icon shape or a text label.
- All numbers, timers and counters use tabular-nums so digits do not jitter while a timer ticks down.
- Every interactive element has visible states: hover, focus (a 2 px accent ring with 2 px offset), pressed, disabled (opacity 0.45 with the reason in the tooltip).
- Long English labels must not break the layout: columns grow, text wraps, nothing is truncated without a tooltip.
- UI copy in Ukrainian, short and clear for a newcomer, with an English variant of every heading, label and button — check that the English strings fit without breaking the layout.
- All data are made-up placeholders: names, levels, costs, times. No real people or brands.

Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the queue band becomes a vertical list, the branch tabs scroll horizontally, and the technology grid becomes a single column. No horizontal scrolling anywhere. All tap targets at least 44×44 px.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of the components you used with their states.
```

## 4. Ключові рішення, які треба врахувати в макеті

- ✅ **Оболонка та сама** — `aetheris-civ`; наповнення інше.
- ✅ **Дослідження належать гравцю**, не планеті: діють на всі колонії, їх не можна втратити.
  Тому в черзі це прямо зазначено.
- ✅ **Черга одна на гравця**, послідовна, базово **2 місця** (+1 платно, до +2) — так само,
  як черга будівництва, але спільна для всіх планет.
- ✅ **Ресурси списуються при постановці**, скасування повертає 100%.
- ✅ **Рівень наукового центру визначає, що можна досліджувати** (00012, п. 1, 3) — це і є
  головна вимога на рівень технології.
- ✅ **Вимоги до кожного рівня окремо**: потрібний рівень наукового центру, інші технології
  й рівні, вартість, час.
- ✅ **Ефекти «відкриває» і «бонус»** (00012, п. 4) — тому в картці є рядок «Unlocks: …».
- ✅ **Старт: гравець не знає жодної технології** (00012, п. 5) — тому стан «Unknown» є
  початковим, а не рідкісним; картки таких технологій приглушені.
- ✅ **Рівневі технології — до 10 рівнів**, ключові — 1 рівень (00012, п. 2). Тому показуємо
  «Next level N» і «Learned · level N».
- ✅ **Платити можна кристалами, кредитами, а на вищих рівнях мікропроцесорами, оптоволокном
  та етером** (game-design/04, розділ 2) — тому в кшталті вартості є різні ресурси.
- ✅ **Причина недоступності обов'язкова** (ui-art/05 §8) — кнопка неактивна, причини видно.
- ✅ **Гілки** (game-design/04, розділ 3): Економіка, Флот, Двигуни й логістика, Оборона,
  Колонізація й космос, Торгівля. У промті шість вкладок — відповідає.
- ✅ **Науковий центр**, а не «Дослідницький центр» — назва змінена в 00012, п. 1, скрізь.

## 5. Рішення автора (26.10.2026)

1. ✅ **Гілки «Флот» і «Оборона» в макеті порожні** — з написом «скоро» і коротким
   поясненням. Це точно відповідає стану сервера (задача 00012: технології цих гілок
   додаються разом із відповідними задачами). Вигаданих карток для них не робимо.
2. ✅ **Невідомі технології показуємо** приглушеною карткою з силуетом іконки. Гравець
   бачить структуру дерева й напрям розвитку, а силует не розкриває суті.
3. ✅ **Лічильників у вкладках гілок немає** — жодних «6 невідомих». Серверу не треба
   віддавати підрахунок по гілках.

## 6. Відкриті питання

1. ❓ Чи залишати **пошук і фільтр за станами** на сторінці дослідження, чи прибрати,
   як на сторінці будівництва. У промті вони є: технологій понад 30, тож фільтр
   «Locked» / «In queue» допомагає.
2. ❓ Чи показувати в картці технології рядок **«Unlocks: Building / Module»** з конкретним
   назвою (напр. «Unlocks: Titanium Mine») чи тільки тип («Unlocks: Building»). Назва
   потребує даних, тип — лише переліку довідника.
