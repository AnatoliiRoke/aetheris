# Задача 00025 — Дизайн сторінки торгівлі

> **Статус:** не розпочата (створено 30.10.2026). Виконує автор в Claude Design.
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** [задача 00008](00008-dyzain-ekrana-keruvannya-tsyvilizatsiieiu.md) (екран керування цивілізацією), та сама оболонка, той самий стиль, що в [00016](00016-dyzain-storinky-planety.md), [00017](00017-dyzain-storinky-budivnytstva.md), [00018](00018-dyzain-storinky-doslidzhen.md), [00019](00019-dyzain-ekrana-skladu.md).
> **Пов'язані:** [00024](00024-torhivlia-rynok-lot-portal-obmin-valiut.md) (функціонал торгівлі: з нього взято всі правила й поля), [00026](00026-perenesty-storinku-torhivli-v-kliyent.md) (перенесення макета в клієнт), [00015](00015-naselennia-kredyty-ta-torhova-valiuta.md) (баланси валют).
> **Документи:** [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [ui-art/02 Екрани](../ui-art/02-ekrany-ta-navihatsiia.md) (екран «Ринок»), [game-design/02 Економіка](../game-design/02-ekonomika-ta-resursy.md) (розділ 7), [game-design/12 Монетизація](../game-design/12-monetyzatsiia.md) (розділ 6).
> **Макет-джерело оболонки:** `documentation/design/aetheris-civ` та `documentation/design/AETHERIS_4_pages`: оболонку (шапка, ліве меню, нижня смужка) переносимо без змін. Готовий макет автор кладе в `documentation/design/AETHERIS_market`.

## 1. Мета
Макет **сторінки «Ринок»**: екрана, де гравець переглядає й купує лоти, виставляє свої, стежить за доставками й обмінює кредити на торгову валюту. Оболонка та сама, змінюється лише середня область.

## 2. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Шапка** | Та сама, що в `aetheris-civ`: лого, нік, гліф раси, кредити, торгова валюта (тут важлива), енергія, тривоги, пошта, налаштування, вихід |
| **Ліве меню** | Те саме; активний пункт «Ринок» |
| **Заголовок** | «Ринок», під ним поточний рівень ринку |
| **Рівні ринку** | Перемикач: Клановий (неактивний, «Немає клану», підказка), Секторний, Всегалактичний |
| **Вкладки** | «Купити», «Продати», «Мої лоти», «Доставки», «Обмін», «Історія» |
| **Купити** | Фільтри (вид предмета: ресурси, продукція, деталі; пошук; діапазон ціни) і таблиця лотів: предмет, кількість, ціна за одиницю, сума, продавець, час доставки, енергострижні, кнопка «Купити» |
| **Підтвердження купівлі** | Вікно: вибір планети призначення (з порталом), підсумок: ціна, комісія, енергострижні, час доставки, баланс після; кнопка «Підтвердити» |
| **Продати** | Форма: планета, предмет зі складу, кількість, ціна за одиницю, підказка допустимої межі ціни, підсумок виручки за мінусом комісії, лічильник «лотів зайнято / доступно», кнопка «Виставити» |
| **Мої лоти** | Таблиця власних лотів, кнопка «Скасувати» |
| **Доставки** | Вхідні й вихідні з таймерами й станом |
| **Обмін** | Напрямок (кредити → торгова валюта й назад), сума, поточний курс, комісія, підсумок, кнопка «Обміняти», коротка історія курсу |
| **Історія** | Мої угоди: дата, предмет, кількість, ціна, комісія, контрагент |
| **Нижня смужка** | Перемикач планет, чат |

## 3. Промт для Design (англійською, передати як є)

```
Design the MARKET SCREEN of an online browser game called "AETHERIS" — the screen a logged-in player opens from the "Market" item of the left menu. This is an in-game screen, not a landing page, not a battle screen, and not the empire overview.

IMPORTANT: this screen shares its frame with existing designs. Reuse the same shell exactly: the same top bar, the same left sidebar, the same bottom bar, the same panel and card components, the same spacing, the same radii, the same shadows, the same icon style. Only the middle area changes. The shell is described below so you match it precisely.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships, trades and fights in turn-based tactical battles of 5-10 minutes. Players trade with each other on a market: they list lots, buy lots of others, and the goods arrive on a chosen planet through a PORTAL after a timer. Prices are in Trade Currency (the second game currency; Credits are internal and are never used as a lot price). Credits and Trade Currency can be exchanged at a floating rate with a fee. Timers run while the player is offline.

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

THE SHELL (reproduce exactly, do not redesign)
TOP BAR, 64 px tall, sticky, background rgba(7,11,22,0.92) with a 1 px bottom border of rgba(160,190,255,0.16). From left to right: a small square brand mark plus the word "AETHERIS" in Inter Bold 18 px with +4% letter spacing; then the player nickname in a rounded pill with a small race-colored glyph; a chip that reads "Standard" (or "Premium") in a muted border; then a centered row of resource counters, each a small distinct icon plus a number in tabular-nums: Credits, Trade Currency, Energy; then on the right three alert pills (incoming attack in danger red, battle in progress in danger red, storage full in warning yellow), a 44×44 mail icon button with a small accent badge showing the unread count, a 44×44 settings icon button, and a 44×44 sign-out icon button. A 1 px vertical separator before the icon buttons.
LEFT SIDEBAR, 220 px wide, sticky under the top bar, background rgba(11,16,32,0.6) with a 1 px right border. Thirteen rows, 44 px tall each, each with a 24×24 line icon on the left and a label: Overview, Planet, Storage, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Mail, Help, Settings. The active row ("Market" on this screen) gets an accent-tinted background, accent-colored icon and label, and a 3 px accent bar on its left edge. The rows for Battle and Clan do not exist yet — do not show them.
BOTTOM BAR, thin, full width, above the bottom edge. On the left a horizontal strip of small planet thumbnails in a scrollable row, the current planet outlined in accent; on the right a chat text field with a placeholder and a 44×44 send button.
Icon style everywhere: line icons, 1.5 px stroke, 24×24 grid, color inherited from the text, always paired with a label or an aria-label.

THE MAIN AREA (this is what you design)
Page title at the top: "Market" in H1, and under it a small secondary line with the current market level, for example "Sector 7 market · prices in Trade Currency".

MARKET LEVEL SWITCH
Directly under the title, a segmented control with three segments: "Clan", "Sector", "Galaxy". The "Clan" segment is disabled (opacity 0.45) with a small lock icon and a tooltip "You are not in a clan yet — the clan market opens when clans arrive". "Sector" is selected in the main design. Next to the control, a small muted text with the fee of the selected level, for example "Fee 5%".

TABS
Below the switch, a tab row: "Buy", "Sell", "My lots", "Deliveries", "Exchange", "History". "Buy" is active in the main design. "My lots" and "Deliveries" carry a small count badge.

BUY TAB
A toolbar: a search field, a segmented filter of item kind (All, Resources, Products, Parts), and a price range. Below it a table of lots with columns: Item (small icon plus name; parts also show a condition percentage chip such as "Condition 80%"), Quantity, Price per unit (with a small Trade Currency icon), Total, Seller (nickname pill with race glyph), Delivery time (for example "12 min"), Energy rods (the number of Energy Rods the buyer must pay, with a small rod icon), and a primary button "Buy". A lot with price 0 shows the price as "Free (transfer)". The whole lot is bought at once: there is no quantity input. Rows are separated by 1 px borders; the table sorts by clicking a header; an empty state reads "No lots match your filters." A disabled "Buy" shows the reason in its tooltip: "No portal on the chosen planet", "Not enough Trade Currency", "Not enough Energy Rods", "This is your own lot".
Clicking "Buy" opens a confirmation dialog: a title with the item and quantity, a planet picker ("Deliver to": a list of the player's planets with a portal; planets without a portal are listed disabled with the reason), and a summary list: Price, Seller receives after fee (muted), Energy Rods to pay, Delivery time, Balance after purchase. Buttons "Confirm" (primary) and "Cancel" (secondary).

SELL TAB
A form panel: Planet (select; a planet without Trade Center and Portal is disabled with the reason), Item (select from the planet storage with the available amount), Quantity (number input with a "Max" button), Price per unit (number input in Trade Currency, with a helper line "Allowed range: 80–120 per unit" and a small link-style hint "Price 0 transfers the goods for free"), a summary: Total, Fee, You receive; a counter "Lots used: 3 / 7" and a primary button "List". Next to the form a small info panel "Delivery needs a portal on both planets; the buyer pays the Energy Rods."

MY LOTS TAB
A table of the player's own lots: Item, Quantity, Price per unit, Total, Level, Created, and a secondary button "Cancel lot" with a confirmation. Empty state: "You have no active lots."

DELIVERIES TAB
Two sections "Incoming" and "Outgoing", each a list of entries separated by 1 px borders: item and quantity, from or to planet, a progress bar with a countdown in tabular-nums, and a state chip: "In transit", "Delivered". Empty state: "No deliveries in progress."

EXCHANGE TAB
A centered panel: a direction switch "Credits → Trade Currency" / "Trade Currency → Credits", an amount input, a read-only result field, a line "Rate: 1 Credit = 0.82 Trade Currency", a line "Fee 5%", a muted line with the rate change over the last day (an up or down arrow icon plus a percentage, never color alone), a primary button "Exchange" and, under the panel, a small line chart of the rate over the last 7 days using the accent color, with axes labeled and a tooltip.

HISTORY TAB
A table of the player's deals: Date, Item, Quantity, Price per unit, Fee, Counterparty (nickname pill), Direction chip ("Bought" or "Sold").

REQUIREMENTS AND BEHAVIOR
- WCAG AA contrast for all text. Color is never the only carrier of information: always pair color with an icon shape or a text label.
- All numbers, timers and counters use tabular-nums so digits do not jitter while a timer ticks down. Large numbers use a thin space as the thousands separator (for example "12 400").
- Every interactive element has visible states: hover, focus (a 2 px accent ring with 2 px offset), pressed, disabled (opacity 0.45 with an explanation in the tooltip).
- Long English labels must not break the layout: columns grow, text wraps, nothing is truncated without a tooltip.
- UI copy in Ukrainian, short and clear for a newcomer, with an English variant of every heading, label and button — check that the English strings fit without breaking the layout.
- Include the states: loading (skeleton rows), empty, error with a retry button, and the state of a player who has no Trade Center or no portal (a banner at the top of the Sell tab explaining what to build, with a button that leads to the construction screen).
- All data are made-up placeholders: item names, nicknames, prices, planet names. No real people or brands.

Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar, the level switch and the tabs become horizontally scrollable rows with 44 px targets, the lot table turns into a list of cards (item and quantity on the first line, price and total on the second, seller and delivery time on the third, the "Buy" button full width), and dialogs become full-width bottom sheets. No horizontal scrolling of the page. All tap targets at least 44×44 px.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of the components you used with their states.
```

## 4. Ключові рішення, які треба врахувати в макеті

- ✅ **Ціна лота в торговій валюті**, кредити лише для обміну (00024 розділ 2).
- ✅ **Лот купується цілим**, поля кількості в купівлі немає; терміну життя лота немає.
- ✅ **Доставка порталом:** потрібен портал на планетах продавця й покупця; енергострижні платить покупець; планету призначення обирає покупець.
- ✅ **Клановий рівень неактивний** (клани на етапі 2): сегмент видно, але вимкнено з підказкою.
- ✅ **Ціна 0** дозволена як спосіб передачі ресурсів; показується як «Free (transfer)».
- ✅ **Межа ціни:** у формі продажу показуємо допустимий діапазон (еталон ±20% на старті, потім за ринком).
- ✅ **Комісія** залежить від рівня ринку й списується з виручки продавця.
- ✅ **Курс обміну плаваючий** з комісією; показуємо курс, зміну за добу й графік за 7 днів.
- ✅ **Оболонка не змінюється** (як у 00016–00019).

## 5. Відкриті питання

1. ❓ Чи показувати в таблиці «Купити» колонку **енергострижнів** (її видно покупцеві до підтвердження) чи лише у вікні підтвердження. У промті колонка є.
2. ❓ Чи потрібен на сторінці **графік курсу** і поле «зміна за добу» одразу, чи лише поточний курс. У промті обидва.
3. 💡 Показ історії цін предмета (середня ціна за 7 днів) у вікні покупки можна додати пізніше, коли з'являться реальні угоди.

## 6. Результат виконання

(заповнюється після отримання макета)
