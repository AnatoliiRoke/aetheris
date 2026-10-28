# Задача 00019 — Дизайн екрана складу

> **Статус:** ВИКОНАНО автором (макет у `documentation/design/AETHERIS_4_pages`, 28.10.2026).
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** [задача 00016](00016-dyzain-storinky-planety.md), [задача 00017](00017-dyzain-storinky-budivnytstva.md), [задача 00018](00018-dyzain-storinky-doslidzhen.md) — та сама оболонка й стиль.
> **Документи:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [game-design/02 Економіка](../game-design/02-ekonomika-ta-resursy.md), [задача 00011](../tasks/00011-vymohy-budivli-ta-cherha-budivnytstva.md), [задача 00013](../tasks/00013-vyrobnytstvo-retsepty-zavody-sklad-detalei.md)
> **Макет-джерело:** `documentation/design/aetheris-civ` — оболонка вже готова.

## 1. Мета

Макет **окремого екрана складу** — пункту в лівому меню, між «Планета» та «Виробництво».
Одна таблиця, яка відповідає на одне питання: **скільки чого лежить на складі і скільки
прибуває за годину.**

На сторінці планети вже є блоки «Ресурси» (швидкість) і «Склад» (місткість по групах).
Цей екран зводить усе в одне місце й показує повний перелік, без потреби гортати сторінку.

## 2. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Шапка** | Та сама, що в `aetheris-civ` |
| **Ліве меню** | Те саме + новий пункт «Склад» (активний) |
| **Заголовок** | «Склад» + назва планети, координати |
| **Підсумок** | Заповнення по групах: «Метали 6 400 / 10 000» тощо |
| **Таблиця** | Ресурс, кількість, приріст за годину |
| **Групи** | Метали, Кристали, Паливо, Продукція |
| **Повна група** | Позначка «Заповнено — видобуток зупинено» |
| **Нижня смужка** | Перемикач планет, чат |

## 3. Промпт для Design (англійською, передати як є)

```
Design the STORAGE SCREEN of an online browser game called "AETHERIS" — a screen a logged-in player opens from the left menu to see everything they have in store on their planet. This is an in-game screen, not a landing page, not a battle screen, and not a modal dialog: it is a full screen in the same frame as the other game screens.

IMPORTANT: this screen shares its frame with an existing design. Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel and card components, the same spacing, the same radii, the same shadows, the same icon style. Only the middle area changes. The shell is described below so you match it precisely.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships, trades and fights in turn-based tactical battles of 5-10 minutes. Every planet has a limited storage. When a group of resources fills up, the mines of that group stop producing and the player is told about it. So the player comes here to answer one question at a glance: how much of each resource do I have, and how much more will I have in an hour.

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

THE SHELL (reproduce exactly, do not redesign)
TOP BAR, 64 px tall, sticky, background rgba(7,11,22,0.92) with a 1 px bottom border of rgba(160,190,255,0.16). From left to right: a small square brand mark plus the word "AETHERIS" in Inter Bold 18 px with +4% letter spacing; the player nickname in a rounded pill with a small race-colored glyph; a chip reading "Standard" (or "Premium") in a muted border; a centered row of resource counters, each a small distinct icon plus a number in tabular-nums: Credits, Trade Currency, Energy; then on the right three alert pills (incoming attack in danger red, battle in progress in danger red, storage full in warning yellow), a 44×44 mail icon button with a small accent badge showing the unread count, a 44×44 settings icon button, and a 44×44 sign-out icon button. A 1 px vertical separator before the icon buttons.
LEFT SIDEBAR, 220 px wide, sticky under the top bar, background rgba(11,16,32,0.6) with a 1 px right border. Rows 44 px tall, each with a 24×24 line icon on the left and a label. The order is: Overview, Planet, Storage, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Battle, Clan, then a separator, then Mail, Help, Settings. The rows for Battle and Clan are greyed out with a small "stage 2" chip. The active row on this screen is "Storage" — it gets an accent-tinted background, an accent-colored icon and label, and a 3 px accent bar on its left edge. Add a "Storage" row to the menu, with a simple crate or box icon.
BOTTOM BAR, thin, full width. On the left a horizontal strip of small planet thumbnails in a scrollable row, the current planet outlined in accent; on the right a chat text field with a placeholder and a 44×44 send button.
Icon style everywhere: line icons, 1.5 px stroke, 24×24 grid, color inherited from the text, always paired with a label or an aria-label.

THE MAIN AREA
Page header: the page title "Storage" in H1, with the planet name in the secondary text next to it, and on the right the planet coordinates in small muted text — "Sector 7 · System Kepler-442 · Orbit 1".
Below the header, a summary row of four small panels, one per resource group, each showing the group name, the numbers "stored / capacity" in tabular-nums, and a thin horizontal meter underneath — the filled part in the group color, the track faint. A group that is full shows its meter in the warning color and a small warning badge reading "Full". The four groups are Metals, Crystals, Fuel, Products.

Then the table — the whole point of the screen. A single table with three columns:

Column 1 — RESOURCE. A small 24×24 resource icon followed by the resource name. Left-aligned.
Column 2 — AMOUNT. The real quantity in store, right-aligned, tabular-nums, in the primary text at body size, not large. Use a thin space as the thousands separator: "4 200". This is the real amount the player owns — not a percentage, not a multiplier, not a rate.
Column 3 — PER HOUR. What the planet adds every hour, right-aligned, tabular-nums. Either "+160" in the accent color for a resource that is being mined, or an em dash "—" in muted text for a resource that is not produced on this planet. This is the growth of the amount, not a multiplier of the deposit.

The table is grouped by resource group, with a quiet group heading row between the sections. A group heading row spans all three columns and reads like "METALS" in small uppercase secondary text, with a small colored dot in the group color at its left. Use exactly these four group headings: METALS, CRYSTALS, FUEL, PRODUCTS.

Use these real resource names and their icons, in the right groups:
Metals (gray #A7B1C2): Iron, Titanium, Copper, Iridium.
Crystals (blue #6FA8FF): Quartz, Ruby, Sapphire, Obsidian.
Fuel (orange #FF9F43): Deuterium, Plasma, Antimatter.
Products (teal #3DD6A6): Building Materials, Optical Fiber, Microprocessors, Composites, Energy Rods.
A planet never has Ether — do not show it. Show the resources that are actually on this planet: several metals, exactly one crystal type (very rarely two), fuel only if the planet has such a deposit, and the products that have been manufactured here.

ROW STATES
A group that is completely full gets a warning treatment: a small warning badge next to the group heading reading "Full", and a muted line under the heading reading "Mining has stopped — build more storage". The rows of that group keep their normal values.
A resource with a zero amount is shown muted with a "0" and an em dash rate — the player still sees that the resource exists on this planet.
A group with no resources at all shows a single quiet row instead of the heading: "No crystals on this planet" in the muted text, with no meter.

EMPTY STATE
If the planet has no resources at all, the table is replaced by a short empty state: a small crate icon, the line "The store is empty", and a muted line "Send a mining ship to a deposit or trade on the market."

REQUIREMENTS AND BEHAVIOR
- WCAG AA contrast for all text. Color is never the only carrier of information: always pair color with an icon shape or a text label.
- All numbers use tabular-nums so digits do not jitter as the amounts grow.
- Every interactive element has visible states: hover, focus (a 2 px accent ring with 2 px offset), pressed.
- The table header row uses the secondary text at 13 px, and the rows are separated by 1 px borders. Row height about 44 px so the table is comfortable to read and to tap.
- Long English labels must not break the layout: columns grow, text wraps, nothing is truncated without a tooltip.
- UI copy in Ukrainian, short and clear for a newcomer, with an English variant of every heading and label — check that the English strings fit without breaking the layout.
- All data are made-up placeholders: names, numbers. No real people or brands.

Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the summary panels stack two per row, and the table scrolls inside its own container with a sticky header row — never the page itself. No horizontal scrolling of the page anywhere. All tap targets at least 44×44 px.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of the components you used with their states.
```

## 4. Ключові рішення, які треба врахувати в макеті

- ✅ **Окремий екран у лівому меню**, не модалка. Пункт «Склад» — між «Планета» та
  «Виробництво».
- ✅ **Склад = реальна кількість + приріст за годину.** Три колонки: ресурс, кількість,
  «+N/год». Множник багатства родовища (1,5×) — **не показуємо**: це технічна величина,
  гравцеві потрібне фактичне число.
- ✅ **Підсумок по групах** — чотири маленькі панелі з місткістю, над таблицею. Так само,
  як на згенерованому макеті сторінки планети.
- ✅ **Групи — чотири**, включно з «Продукція» (Будматеріали, Оптоволокно,
  Мікропроцесори, Композити, Енергострижні). Вони з'являться з задачі 00013; макет
  показує кінцевий стан.
- ✅ **Кольори груп** — з `aetheris-civ/src/icons.js`: метали `#A7B1C2`, кристали
  `#6FA8FF`, паливо `#FF9F43`, продукти `#3DD6A6`.
- ✅ **Іконки ресурсів уже намальовані** — 24×24, лінійні, `AE.RES` в `icons.js`. Дизайнер
  має їх повторити, а не малювати нові. Для п'яти вироблених ресурсів іконки ще немає —
  для них потрібні прості піктограми, схожі на наявні.
- ✅ **Заповнена група зупиняє видобуток** (00011, п. 8) — показуємо і бейдж «Full», і
  рядок «Mining has stopped — build more storage».
- ✅ **Один кристал на планеті** (game-design/02, розділ 2.2) — одна позиція кристалів,
  рідко дві.
- ✅ **Етеру на планеті немає** (game-design/02, розділ 2.4; `allowed_on_planet: false`).
- ✅ **Стиль таблиці вже є** у клієнті: `planet-table` (`client/src/styles/planet.css`) —
  заголовок `text-2` 13 px, рядки з розділювачем `border`, числа `tabular-nums` праворуч.

## 5. Рішення автора (26.10.2026)

1. ✅ **Окремий екран у меню**, а не модальне вікно. Модалка накладлася на заголовок
   і дублювала вже наявний блок «Склад» на сторінці планети.
2. ✅ **Склад — це реальна кількість і приріст за годину.** Множник родовища не показуємо.
3. ✅ **Група «Продукція» лишається** в макеті, попри те що групи в довіднику ще немає.

## 6. Відкриті питання

1. ❓ Чи показувати в підзаголовку **координати планети** («сектор / система / орбіта»).
   У промті показані — щоб було видно, для якої планети склад.
2. ❓ Чи додати **окремий рядок «Загальна місткість»** (сума по всіх групах) під
   підсумковими панелями. У промті такого рядка немає.
3. ❓ Чи показувати в підсумку **швидкість приросту групи** (сума «+N/год» по ресурсах
   групи). У промті лише «заповнено / місткість».
