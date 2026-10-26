# Задача 00008 — Промт для дизайну екрана керування цивілізацією

> **Статус:** промт підготовлено (26.10.2026). Чекає на запуск у Design.
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Спільно з:** задача 00003 (перша публічна сторінка) — той самий візуальний стиль, токени й шрифти.

## 1. Мета

Макет **екрана керування цивілізацією** — головного ігрового екрана гравця, який уже увійшов
і заснував цивілізацію. Це **не** публічна сторінка (task 00003) і **не** бойовий екран.

Екран показує все, чим гравець керує: список планет з видобутком, черги будівництва та
дослідження, ресурси, швидкість видобутку, таймери й тривоги.

Ціль — один макет, з якого можна безпосередньо будувати клієнт.

## 2. Що має бути на екрані

| Блок | Зміст |
|---|---|
| **Верхня панель** | Ресурси: кредити, торгова валюта, енергія, населення. Тривоги: атака, бій, переповнене сховище. Пошта, налаштування, вихід |
| **Ліве меню** | Огляд, Планета, Виробництво, Верф, Флоти, Карта, Дослідження, Ринок, Рейтинг, Пошта, Довідка, Налаштування |
| **Основна область** | Плашки планет: зображення, назва, координати, видобуток за годину по кожному ресурсу, швидкість, статус |
| **Права панель** | Черга будівництва + черга дослідження, таймери, флоти, сповіщення |
| **Нижня панель** | Перемикач планет, чат |

## 3. Результат

1. Макет у Design: екран для робочого столу 1440–1920 px і для телефону 360–430 px.
2. Токени — ті самі, що в `client/src/styles/tokens.css` (задача 00003).
3. Опис компонентів у `documentation/ui-art/design/components.md`.
4. Експорт PNG у `documentation/ui-art/design/`.

## 4. Промпт для Design (англійською, передати як є)

```
Design the CIVILIZATION MANAGEMENT SCREEN of an online browser game called "AETHERIS" — the main screen a logged-in player sees after creating their civilization. This is an in-game screen, not a landing page and not a battle screen.

CONTEXT OF THE GAME
AETHERIS is a free-to-play online 2D space strategy game in real time, with one shared universe for all players. The player develops planets, mines resources, builds spaceships from components and modules, trades with other players on a market, and fights turn-based tactical battles in cycles of 5-10 minutes. The management screen is the calm part of the game: the player drops in for 5-10 minutes, checks what has finished, queues new construction and research, and leaves. Timers run while they are offline. Nothing is ever destroyed by neglect — only time is spent.

Four playable races: Terran (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

VISUAL STYLE
Painted 2D, beautiful and atmospheric, dark UI only. Semi-transparent dark panels with thin light borders, soft glow, layered stars, subtle depth. Painted planets and ships, not flat vector, not pixel art. No heavy video; motion is implied by soft glow and parallax.

COLOR TOKENS (use exactly these)
bg-0 #070B16 (deepest page background), bg-1 #0B1020 (section background), bg-2 #141B33 (cards and panels), bg-3 #1C2544 (raised state, input fields), border rgba(160,190,255,0.16), border-strong rgba(160,190,255,0.28), text-1 #E8EEFF (primary text), text-2 #9AA7C7 (secondary text), text-soft #C3CCE4, accent #2EC4C4 (main accent, primary buttons, hover #4FDADA), on-accent #070B16 (text on accent), danger #FF6B3D (combat accent, errors), danger-text #FF8A66, success #4CD08A (allies, done), warning #F2C14E (neutral, warnings). Race colors: Terran #6C8DB8, Kvarr #D08A4E, Skolar #D2443C, Nexar #7FE0EE.

RESOURCE GROUPS AND THEIR COLORS (each resource group has its own color, and never rely on color alone — always pair it with a distinct icon shape and a text label)
Metals: gray — Iron, Titanium, Copper, Iridium. Crystals: blue — Quartz, Ruby, Sapphire, Obsidian. Fuel: orange — Deuterium, Plasma, Antimatter. Ether: gold. Credits: teal-green. Energy: yellow.

RESOURCE CATALOGUE (use these exact names; each needs its own distinct icon shape, not only a different color)
Iron, Titanium, Copper, Iridium (metals) / Quartz, Ruby, Sapphire, Obsidian (crystals) / Deuterium, Plasma, Antimatter (fuels) / Ether / Credits / Trade currency / Energy / Population.
Produced resources, shown in the Production section and in research costs: Building Materials, Optical Fiber, Microprocessors, Composites, Energy Rods.

TYPOGRAPHY
Plain and simple only, no decorative or stylized fonts. Inter for everything (headings Inter SemiBold 600 / Bold 700, body Inter Regular 400 / Medium 500, all numbers and timers with tabular-nums so digits do not jitter as they tick down). Fallbacks: Roboto, system-ui, Arial. Type scale desktop/mobile: H1 56/34, H2 36/26, H3 22/20, body 16/16, small 13/13. Line height 1.5 for body text, 1.2 for headings.

SCREEN STRUCTURE — design it as a four-region interface frame

TOP BAR (full width, sticky)
Left: the game logo "AETHERIS" in Inter Bold with +4% letter spacing, and beside it the player's nickname with a small race glyph in the player's race color.
Center: a horizontal row of resource counters, each a small distinct icon plus a number in tabular-nums: credits, trade currency, energy, population. Population shows current/max, for example "742 / 1000".
Right: three alert indicators that must be visible at a glance without being loud — an incoming attack (danger), a battle in progress (danger), a full storage (warning). Each uses an icon shape and a text label, never color alone. Then a mail icon with an unread badge, a settings icon, and a sign-out icon.

LEFT SIDEBAR (vertical icon-and-label menu, ~220 px wide on desktop)
Twelve entries in this order: Overview, Planet, Production, Shipyard, Fleets, Map, Research, Market, Ranking, Mail, Help, Settings. The current one is highlighted with the accent color and a thin accent bar on its left edge. Entries that are not implemented yet in stage 1 (Battle, Clan) are shown greyed out with a small "stage 2" chip so the menu does not look broken.

MAIN AREA — PLANET CARDS
This is the heart of the screen. A responsive grid of planet cards, 2 columns on desktop at 1440 px and 3 columns at 1920 px, 1 column on mobile.

Each planet card contains:
- A large painted planet image, top of the card, filling the card width, with a soft atmosphere glow. Use the appropriate planet art for the card's planet type.
- Planet name in H3, and directly under it the coordinates in small text in the secondary color, formatted as sector / system / orbit, for example "Sector 7 · System Kepler-442 · Orbit 2".
- A small type chip next to the name: Earth-like, Volcanic, Icy, Desert, Oceanic, or Gas Giant. Gas Giant cards must show a different treatment: no population, no buildings, only a fuel extraction station and a mining yield.
- A deposit line listing what the planet has, as small icon + name pairs: for example Iron, Titanium, Copper, Iridium and one crystal (Quartz, Ruby, Sapphire, Obsidian). A planet has exactly ONE crystal type, very rarely two — show one. Ether is NEVER on a planet, so never show it on a planet card.
- The mining yield per hour, one row per mined resource, each with a distinct icon, the resource name, and the per-hour number in tabular-nums. Example rows: Iron 120/h, Titanium 45/h, Copper 80/h, Quartz 30/h.
- A total yield summary line at the bottom of the card, for example "Total 275/h", with a subtle accent-tinted background.
- A small population row with a horizontal progress bar: current vs maximum, the bar in accent, and the filled part visually distinct from the track.
- A construction slot strip: small squares representing the planet's building slots, empty ones outlined, occupied ones filled with a tiny building glyph, and one showing a progress ring if something is building. Hovering is not needed in a static mockup — the states must be readable at a glance.
- A small status badge in the corner of the card when something needs attention: "Storage full" (warning), "Under attack" (danger), "Energy deficit" (warning).

Show four or five planet cards so the grid reads as a real set. Make one card visually distinct as the selected or hovered card (accent border). Include one gas giant card to prove that type renders differently.

RIGHT PANEL (~340 px wide on desktop)
Three stacked sections in one column:
1. Construction queue — the header says "Construction" with a badge showing the number of queued items. Each entry: a small building icon, the building name, a progress bar, a countdown timer in tabular-nums, and a "X / Y" slot indicator. Use real building names from this catalogue: Mine (one per metal), Crystal Mine, Fuel Station, Power Plant, Refinery, Building Materials Plant, Optical Fiber Plant, Microprocessor Factory, Composite Plant, Energy Rod Plant, Component Plant, Module Plant, Recycling Factory, Housing Complex, Medical Center, Metal Storage, Crystal Storage, Fuel Tank, Bunker, Shipyard, Spaceport, Planetary Turrets, Radar, Research Center, Trade Center, Portal, Administrative Center. Include one entry just starting (very little progress), one halfway, one nearly done, and one finished-but-not-collected state. Show a subtle accent glow on the completed one.
2. Research queue — the header says "Research" with a badge. Each entry: a small technology icon, the technology name, a progress bar, a countdown timer, and the cost in crystals and credits. Use real technology names from this catalogue: Deep Mining, Crystallography, Gas Engineering, Industrial Automation, Energy, Warehouse Logistics, Demography, Hull Classes, Ballistics, Lasers, Plasma Weapons, Armor, Shields, Sensors, Deuterium Engine, Plasma Engine, Warp Engine, Engine Efficiency, Fleet Command, Cargo Holds, Planetary Guns, Planetary Shield, Storage Masking, Early Warning, Colonial Administration, Colonial Transports, Asteroid Mining, Nebula Collectors, Ether Engineering, Trade Law, Portal Technology, Portal Stabilization, Convoy Escort. Show the current research at the top and one queued behind it.
3. Activity / alerts — a short list of recent events: a construction finished, a research completed, a fleet returned, a storage warning. Each line has a small icon, one sentence, and a relative timestamp.

BOTTOM BAR (full width, thin)
Left: the planet switcher — small planet thumbnails in a horizontal scroll strip, the current one outlined in accent.
Right: a chat entry field with a placeholder and a send button, and a label indicating a global chat for stage 1.

STATE AND DETAIL NOTES
- Timers are always visible and always in tabular-nums. This is a core principle of the game.
- Any important value must be reachable in one or two clicks from here.
- The screen must read clearly at a glance even before you read any numbers: color and shape carry the status, the numbers confirm it.
- Include subtle empty and edge states where they matter: a planet card whose storage is full (warning badge and a full storage bar), and a production entry paused because energy is short.

ILLUSTRATIONS
Do NOT draw the illustrations. Put a labeled placeholder with the correct size and aspect ratio wherever an illustration goes, and I will supply the final painted images separately. Use exactly these IDs:
- ART-12a Earth-like planet, square crop from a 1200x896 source
- ART-12b Volcanic planet, square crop from a 1200x896 source
- ART-12c Icy planet, square crop from a 1200x896 source
- ART-12d Desert planet, square crop from a 1200x896 source
- ART-12e Oceanic planet, square crop from a 1200x896 source
- ART-12f Gas giant, square crop from a 1200x896 source
- ART-11a screenshot mockup: empire overview, 1200x896 source
- ART-14 subtle dark nebula background (optional, behind the main area)

Note on the planet images: the supplied files are not isolated on a transparent background — each planet sits inside a rectangular space scene. For the card layout, crop each planet to a square around the planet itself, or place the card image on a background that matches the card so the rectangular edge is invisible. Design the card so the planet reads as a round object with an atmosphere glow, not as a pasted rectangular photograph.

REQUIREMENTS
Two layouts: desktop 1440-1920 px and mobile 360-430 px. On mobile the left sidebar becomes a bottom tab bar and the right panel becomes a separate screen reachable from a "Queues" button in the top bar; the planet cards become a single-column list. No horizontal scrolling anywhere. All tap targets at least 44 px. WCAG AA contrast for all text. Color is never the only carrier of information: always pair color with an icon shape or a text label. UI copy in Ukrainian, short and clear for a newcomer, with an English variant of the menu, section headers and button labels — make sure the English labels fit without breaking the layout. All data are made-up placeholders (planet names, numbers); no real people or brands.

Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of components with their states.
```

## 5. Ключові рішення, які треба врахувати в макеті

- ✅ **Етер на планеті бути не може** (game-design/02, розділ 2.4) — на картці планети його не показуємо.
- ✅ **На планеті рівно один вид кристалів** (game-design/03, розділ 1) — показуємо один, рідко два.
- ✅ **Газовий гігант не колонізується** (game-design/03, розділ 2) — на його картці немає населення і будівель, лише станція й видобуток палива.
- ✅ **Населення відновлюється автоматично** (game-design/02, розділ 3.1) — показуємо «поточне / максимум» зі смужкою.
- ✅ **Робітників немає** (game-design/02, розділ 3.1) — у макеті немає рядка «робітники».
- ✅ **Паливо видобувається переважно на газових гігантах** (game-design/02, розділ 2.3).
- ✅ **Видобуток іде в реальному часі** (game-design/02, розділ 4.1) — показуємо «/год» у всіх рядках.
- ✅ **Черга будівництва: базово 2 места** (game-design/03, розділ 3.1) — показуємо смужку слотів.
- ✅ **Черга досліджень: 1 безкоштовно** (game-design/04, розділ 2) — показуємо поточне й одне в черзі.
- ✅ **Дослідження належать гравцеві, не планеті** (game-design/04, розділ 2) — черга одна на всі колонії.
- ✅ **Таймери видно завжди** (ui-art/02, розділ 2, принцип 3).
- ✅ **Тривоги помітні** (ui-art/02, розділ 2, принцип 4) — атака, бій, переповнене сховище.
- ✅ **Координати: сектор → система → орбіта** (game-design/09, розділ 2).
- ✅ **Ресурсів багато** (game-design/02, розділ 2.6) — на картці показуємо родовище й кристал, решта ресурсів у панелі ресурсів.
- ✅ **Етап 1** (ui-art/02, розділ 8): Бій і Клан — етап 2, тому в меню сірі з міткою.
- ✅ **Колір не єдиний носій інформації** (ui-art/01, розділ 3; ui-art/02, розділ 7) — скрізь форма іконки й підпис.

## 6. Відкриті питання

1. ❓ Чи показувати на картці планети повний список родовищ (4 металі + кристал), чи лише
   видобувані з них? У макеті закладено повний список родовищ.
2. ❓ Чи показувати в нижній смужці перемикач планет, чи замінити його на список
   «Планети» у лівому меню? У макеті обидва є.
3. ❓ Чи потрібна окрема іконка для кожного ресурсу (13 ресурсів) — це багато дрібних
   малюнків. У макеті використано прості векторні піктограми-заглушки з підписом.
