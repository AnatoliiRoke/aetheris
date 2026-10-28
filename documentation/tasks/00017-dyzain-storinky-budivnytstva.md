# Задача 00017 — Дизайн сторінки будівництва

> **Статус:** ВИКОНАНО автором (макет у `documentation/design/AETHERIS_4_pages`, 28.10.2026).
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** [задача 00016](00016-dyzain-storinky-planety.md) (сторінка планети) — та сама оболонка й стиль.
> **Документи:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [game-design/03 Розвиток планет](../game-design/03-rozvytok-planet.md), [задача 00011](../tasks/00011-vymohy-budivli-ta-cherha-budivnytstva.md)
> **Макет-джерело:** `documentation/design/aetheris-civ` — оболонка вже готова.

## 1. Мета

Макет **сторінки будівництва**: вибір будівлі, її параметри, черга й те, що заважає
поставити будівлю. Це екран, куди гравець іде, щоб поставити шахту, електростанцію чи
склад і побачити, що йому бракує.

## 2. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Шапка** | Та сама, що в `aetheris-civ` |
| **Ліве меню** | Те саме; активний пункт — «Планета» (будівництво вкладка планети) |
| **Заголовок** | «Будівництво» + назва планети, координати |
| **Черга будівництва** | Зверху, на всю ширину: що будується, таймер, наступні, скасування, місця |
| **Перелік будівель** | Плиткою, із заголовками груп: назва, рівень, поточні ефекти, вартість наступного рівня, час, кнопка. Без фільтрів і пошуку |
| **Причини недоступності** | Для закритої будівлі: чого бракує (вимога, рівень адмінцентру, ресурси, енергія, черга). Закриті будівлі показані приглушено з замком |
| **Нижня смужка** | Перемикач планет, чат |

## 3. Промпт для Design (англійською, передати як є)

```
Design the CONSTRUCTION SCREEN of an online browser game called "AETHERIS" — the screen where a logged-in player chooses what to build on their planet and queues it. This is an in-game screen, not a landing page, not a battle screen.

IMPORTANT: this screen shares its frame with an existing design. Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel and card components, the same spacing, the same radii, the same shadows, the same icon style. Only the middle area changes. The shell is described below so you match it precisely.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships, trades and fights in turn-based tactical battles of 5-10 minutes. The construction screen is where the player decides what a planet becomes: which mines to dig, how much power to build, how large the storage should be. Every action takes real time. There is no paid speed-up, and the queue is strictly sequential — one building is built at a time, the rest wait their turn. The player drops in for 5-10 minutes, checks what finished, queues the next thing, and leaves; the buildings finish while they are offline.

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

THE SHELL (reproduce exactly, do not redesign)
TOP BAR, 64 px tall, sticky, background rgba(7,11,22,0.92) with a 1 px bottom border of rgba(160,190,255,0.16). From left to right: a small square brand mark plus the word "AETHERIS" in Inter Bold 18 px with +4% letter spacing; the player nickname in a rounded pill with a small race-colored glyph; a chip reading "Standard" (or "Premium") in a muted border; a centered row of resource counters, each a small distinct icon plus a number in tabular-nums: Credits, Trade Currency, Energy; then on the right three alert pills (incoming attack in danger red, battle in progress in danger red, storage full in warning yellow), a 44×44 mail icon button with a small accent badge showing the unread count, a 44×44 settings icon button, and a 44×44 sign-out icon button. A 1 px vertical separator before the icon buttons.
LEFT SIDEBAR, 220 px wide, sticky under the top bar, background rgba(11,16,32,0.6) with a 1 px right border. Thirteen rows, 44 px tall each, each with a 24×24 line icon on the left and a label: Overview, Planet, Storage, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Mail, Help, Settings. The active row ("Planet" on this screen) gets an accent-tinted background, accent-colored icon and label, and a 3 px accent bar on its left edge.
BOTTOM BAR, thin, full width. On the left a horizontal strip of small planet thumbnails in a scrollable row, the current planet outlined in accent; on the right a chat text field with a placeholder and a 44×44 send button.
Icon style everywhere: line icons, 1.5 px stroke, 24×24 grid, color inherited from the text, always paired with a label or an aria-label.

PAGE HEADER
A small breadcrumb line "Planet / Nova Tessa" in muted text, then the page title "Construction" in H1 with the planet name in the secondary text next to it, and on the right a muted line with the planet coordinates "Sector 7 · System Kepler-442 · Orbit 2".

THE CONSTRUCTION QUEUE — a full-width band at the top, above the building list
A panel titled "Construction" with a counter on the right reading "2 / 2 slots used" and, when the queue is not full, a muted hint "+1 slot available" or a warning line "The queue is full — cancel a building to queue another".
The queue is a single horizontal row of cards (on mobile it becomes a vertical list). Each card shows: a small building icon, the building name with the level ("Iron Mine · level 4"), a wide progress bar, a countdown in tabular-nums in the primary text, and a muted line with the cost that was already paid (resource icons and amounts).
The first card is the one under construction: accent progress bar, and a small status line "Building" in the accent color. The cards after it are waiting: an empty progress track, a muted status line "Waiting", and the time until they start is not shown. The last card may show the state "Done — finishing…" in the success color with a soft accent glow.
Each queued card has a small 36 px secondary "Cancel" button in its top right corner. Hovering it reveals a muted note under the queue: "Cancelling returns the full cost."
If the queue is empty, the band shows a short empty state instead: "Nothing is being built. Choose a building below."

THE BUILDING LIST — the main content, below the queue band
A responsive grid of building cards: 3 columns on desktop at 1440 px, 4 columns at 1920 px, 1 column on mobile. Cards are of equal height, each with a 24 px gap.
Above the grid there are NO filters, no search field and no sorting controls — the list of buildings is short enough to read at a glance, and every extra control is one more thing to understand. The list is simply ordered by category, with a small muted section heading before each group: Mining, Energy, Production, Storage, Population, Fleet, Defence, Research, Trade. Each heading is a plain muted label, not a filter or a chip.

EACH BUILDING CARD contains, from top to bottom:
1. A header row: a 32×32 line icon of the building in a rounded square tile with a subtle accent-tinted background, then the building name in H3, and in the top right corner a level badge — for a built building "Level 4", for a building that does not exist yet "Not built". The badge is a pill with a 999 px radius in muted colors.
2. A one-line description in the soft secondary text, for example "Extracts iron from the planet's deposit."
3. An "Effects" block: a short list of the CURRENT effect values only, each with a small icon, the effect name and its value in tabular-nums. Use these effect names and their icons exactly: Mining output (a pickaxe) +120 / h; Storage capacity (a crate) +2 000; Energy output (a lightning bolt) +80; Energy consumption (a lightning bolt) −30; Population limit (a person) +250; Building level cap (a shield) "others up to level 3". An effect that is zero is shown muted with an em dash. The card does NOT show the effects of the next level — only what the building does right now.
4. A divider, then the "Next level" block in a slightly raised inner surface (bg-3, 1 px border, 10 px radius):
   - a small muted label "Next level 5";
   - a row of cost chips, each a small resource icon plus the amount in tabular-nums, for example Iron 240, Copper 120, Credits 1 500. A cost the player cannot afford is drawn in the danger color with a small warning icon next to it, and the number is dimmed;
   - a row "Build time 2 h 14 m" in tabular-nums;
   - a row "Energy after completion 380 / 320" where the balance is green when positive and red when negative;
   - a row of requirement chips: a green chip with a check icon "Requirements met", or a red chip with a warning icon naming what is missing, for example "Required: Administrative Center, level 2" or "Required: a deposit of Iron".
5. A full-width action button at the bottom, 44 px tall: a primary-styled button reading "Build" for a building that does not exist yet, or a secondary button reading "Upgrade" for one that does. When the action is not allowed, the button is disabled (opacity 0.45) and the card shows, above the button, a short list of the exact reasons in danger text. Use these real reason strings: "Not enough resources", "Queue is full", "Not enough energy — build or upgrade a power plant", "First upgrade the Administrative Center", "Required: Iron Mine, level 2", "Required: a deposit of Iron", "Required technology — it unlocks through research", "This level is not unlocked yet", "Highest level". A player must never have to guess why a button is disabled.

CLOSED BUILDINGS
Buildings that are not yet unlocked are still shown in the list but visually dimmed, with a small lock chip in the corner and the reason as a muted line. This teaches the player what is coming. Use these building names across the cards, and keep the names exactly as written here: Administrative Center, Science Center, Residential Complex, Iron Mine, Titanium Mine, Copper Mine, Iridium Mine, Crystal Mine, Fuel Station, Power Plant, Refinery, Building Materials Plant, Optical Fiber Plant, Microprocessor Factory, Composite Plant, Energy Rod Plant, Component Plant, Module Plant, Recycling Factory, Medical Center, Metals Storage, Crystal Storage, Fuel Tank, Bunker, Shipyard, Spaceport, Planetary Turrets, Radar, Trade Center, Portal.

REQUIREMENTS AND BEHAVIOR
- WCAG AA contrast for all text. Color is never the only carrier of information: always pair color with an icon shape or a text label.
- All numbers, timers and counters use tabular-nums so digits do not jitter while a timer ticks down.
- Every interactive element has visible states: hover, focus (a 2 px accent ring with 2 px offset), pressed, disabled (opacity 0.45 with the reason in the tooltip).
- Long English labels must not break the layout: columns grow, text wraps, nothing is truncated without a tooltip.
- UI copy in Ukrainian, short and clear for a newcomer, with an English variant of every heading, label and button — check that the English strings fit without breaking the layout.
- All data are made-up placeholders: names, levels, costs, times. No real people or brands.

Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the queue band becomes a vertical list, and the building grid becomes a single column. No horizontal scrolling anywhere. All tap targets at least 44×44 px.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of the components you used with their states.
```

## 4. Ключові рішення, які треба врахувати в макеті

- ✅ **Оболонка та сама** — `aetheris-civ`; наповнення інше.
- ✅ **Черга послідовна**: базово **2 місця** (`queue.slotCount`), платне розширення додає по 1
  (до +2). Тому показуємо «2 / 2» і стан «черга заповнена» як окрему причину.
- ✅ **Ресурси списуються при постановці**, скасування повертає 100% — у черзі показуємо
  **вже сплачену** вартість, а не майбутню.
- ✅ **Скасування доступне** і в черзі, і для будівлі, що будується.
- ✅ **Причина недоступності обов'язкова**: кнопка неактивна, але гравець бачить список причин
  (вимога, рівень адмінцентру, нестача ресурсів, енергія, черга). Це правило ui-art/05 §8.
- ✅ **Рівень будівлі не може перевищувати рівень адміністративного центру** (00011, п. 2) —
  це окрема вимога з окремим текстом підказки.
- ✅ **Енергія**: будівля, що зробить баланс від'ємним, не ставиться — показуємо
  «Energy after completion» і відповідну причину.
- ✅ **Множник багатства родовища** (0,8–1,9) впливає на видобуток — ефект «Mining output»
  показує вже підсумкове значення.
- ✅ **Будівлі без слотів** (00011, п. 1): на планеті можна збудувати всі доступні будівлі,
  по одному екземпляру кожного типу; ростуть **рівнями** (максимум 20). Тому в картці
  «Not built» / «Level N», а не «1/3 слота».
- ✅ **Створені будівлі** (00011, п. 4): адмінцентр, науковий центр, житловий комплекс,
  електростанція, шахти заліза й міді, склади — відкриті без технологій; решта — за
  технологіями.
- ✅ **Усі числа з БД** — у макеті це заглушки, але без ключів у коді (tech/08).

## 5. Рішення автора (26.10.2026)

1. ✅ **Фільтрів немає** — ні пошуку, ні чипів за групами, ні сортування. Перелік і так
   короткий, а кожен контрол — це ще одне, що треба зрозуміти. Групи показані **заголовками
   розділів** перед відповідною групою карток: це не фільтр, а орієнтир.
2. ✅ **У картці будівлі тільки поточні ефекти.** Що зміниться на наступному рівні —
   не показуємо. Картка не роздувається, а гравець бачить поточну користь.
3. ✅ **Закриті будівлі показуємо приглушено** — з замком у куті й причиною. Гравець
   бачить, що відкриється далі, і чому ще не може.

## 6. Відкриті питання

1. ❓ Чи показувати в картці **коефіцієнт багатства родовища** (0,8–1,9). Він впливає на
   видобуток шахти, але це технічна величина.
2. ❓ Чи робити окремий **заголовок розділу** для кожної групи будівель (9 заголовків) чи
   лише для великих (видобуток, енергія, виробництво, сховище, решта).
