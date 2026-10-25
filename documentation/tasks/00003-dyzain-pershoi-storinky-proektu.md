# Задача 00003 — Згенерувати дизайн першої сторінки проєкту (у Design)

> **Статус:** не розпочата (створено 25.10.2026, переписано того ж дня). Не залежить від задач 00001–00002.
> **Інструмент:** Claude Design (тип «Design»; візуальний макет на полотні).
> **Документи:** [ui-art/01 Візуальний стиль](../ui-art/01-vizualnyi-styl.md), [ui-art/02 Екрани та навігація](../ui-art/02-ekrany-ta-navihatsiia.md), [ui-art/04 Локалізація](../ui-art/04-lokalizatsiia.md), [game-design/01 Загальний опис](../game-design/01-zahalnyi-opys-hry.md), [game-design/08 Раси](../game-design/08-rasy.md), [tech/05 Безпека](../tech/05-bezpeka-ta-antychit.md)

## 1. Мета
Макет **першої (публічної) сторінки проєкту**: того, що бачить людина, яка щойно зайшла на сайт, ще не граючи. Це не екран керування цивілізацією (він окремою задачею пізніше). Сторінка має пояснити, що це за гра, показати «живий» проєкт (рейтинги, новини) і дати змогу увійти чи зареєструватися.

## 2. Що має бути на сторінці

| Блок | Зміст |
|---|---|
| **Шапка** | Лого «AETHERIS», меню (Про гру, Раси, Рейтинг, Новини, Довідка), перемикач мови, кнопки «Увійти» і «Грати» |
| **Головний екран (hero)** | Мальована космічна сцена (планета, флот), заголовок і короткий опис гри, головна кнопка «Грати безкоштовно», під нею «Увійти» |
| **Про гру** | 3–4 картки: розвиток планет і видобуток; конструктор кораблів із компонентів і модулів; ринок між гравцями; тактичні бої по циклах (5–10 хв, етап 2). Коротко, мова легка |
| **Раси** | 4 раси: Террани, Кварр, Сколар, Нексар; кожна з кольором, настроєм і силуетом корабля (ui-art/01 розділ 4) |
| **Скріншоти** | Галерея з 3–4 кадрів гри (поки заглушки: огляд імперії, карта, конструктор) |
| **Рейтинг** | Топ-10 гравців (місце, ім'я, раса, очки), перемикач «за розвитком»; на етапі 1 лише розвиток (production/01); кнопка «Весь рейтинг» |
| **Новини** | 3 останні новини: дата, заголовок, початок тексту, «Читати далі» |
| **Авторизація** | Модальне вікно або сторінка: вхід (email + пароль), «Увійти через Google», реєстрація (email, пароль, ім'я, раса за вибором на пізніше), «Забули пароль?»; повідомлення про помилки; згода з умовами |
| **Підтвердження email** | Екран «Перевірте пошту» |
| **Часті питання** | 5–6 питань (безкоштовно? потрібно бути онлайн? що таке цикли бою?) |
| **Футер** | Мова, умови користування, політика конфіденційності, контакти, соцмережі, версія |

## 3. Вимоги

### 3.1. Стиль (✅ мальований 2D, ui-art/01)
- Темний космічний фон (#0B1020–#141B33), мальовані планети й кораблі, напівпрозорі панелі, акцент бірюзовий. Відчуття «краси»: світіння, зоряні шари, м'який рух (паралакс на hero, без важкого відео).
- Тон легкий, з відтінком військового настрою; тексти короткі, зрозумілі новачку.
- Колір не єдиний носій інформації (форма іконки, підпис).

### 3.2. Адаптивність і швидкість
- Два макети: **робочий стіл 1440–1920 px** і **телефон 360–430 px**; меню на телефоні згортається в «гамбургер».
- Швидке перше завантаження (бюджет 2–3 МБ, ui-art розділ 6а): важкі картинки підвантажуються по прокручуванню, hero в стислому форматі.
- Розмітка придатна для пошукових систем (заголовки, описи, зображення з alt).

### 3.3. Мови
- Українська та англійська (ui-art/04); макет враховує довші англійські/інші підписи.

### 3.4. Дані на макеті
Правдоподібні заглушки: рейтинг із 10 вигаданих імен, 3 новини, 4 скріншоти. Не реальні особи й не реальні бренди.

## 4. Шрифти й кольори (✅ вимога автора: шрифти прості, кольори темні)

### 4.1. Шрифти
Прості, читабельні, з кирилицею, безкоштовні (Google Fonts). Жодних декоративних і «стилізованих» шрифтів.

| Роль | Шрифт | Запасні |
|---|---|---|
| Заголовки і кнопки | **Inter** (SemiBold 600 / Bold 700) | Roboto, system-ui, Arial |
| Основний текст | **Inter** (Regular 400 / Medium 500) | Roboto, system-ui, Arial |
| Числа, таймери, таблиця рейтингу | **Inter** з цифрами однакової ширини (`tabular-nums`) | Roboto Mono |

Розміри (робочий стіл / телефон): H1 56 / 34, H2 36 / 26, H3 22 / 20, основний 16 / 16, дрібний 13 / 13; міжрядковий інтервал 1.5 для тексту, 1.2 для заголовків.

### 4.2. Кольори (темні)
| Токен | Колір | Де |
|---|---|---|
| `bg-0` | #070B16 | Найглибший фон сторінки |
| `bg-1` | #0B1020 | Основний фон секцій |
| `bg-2` | #141B33 | Картки, панелі |
| `bg-3` | #1C2544 | Піднятий стан, поля вводу |
| `border` | rgba(160,190,255,0.16) | Тонкі рамки |
| `text-1` | #E8EEFF | Основний текст |
| `text-2` | #9AA7C7 | Другорядний текст |
| `accent` | #2EC4C4 | Основний акцент (бірюзовий), кнопка «Грати» |
| `accent-hover` | #4FDADA | Наведення |
| `danger` | #FF6B3D | Бойовий акцент, помилки |
| `success` | #4CD08A | Успіх, союзники |
| `warning` | #F2C14E | Нейтральне, попередження |
| Раси | Террани #6C8DB8, Кварр #D08A4E, Сколар #D2443C, Нексар #7FE0EE | Картки рас |

Правила: фон завжди темний; світлі плями лише для акценту й світіння; контраст тексту не нижчий за WCAG AA.

## 5. Перелік 2D-малюнків і промпти для них (✅ вимога автора)

У Design на місці кожного малюнка ставимо **заглушку з ідентифікатором** (`ART-01` ...). Малюнки генеруємо окремо за промптами нижче (англійською), потім підставляємо. Загальний суфікс для всіх: `painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no watermark`.

| ID | Де | Розмір | Промпт |
|---|---|---|---|
| ART-01 | Hero, фон | 1920×1000, широкий | `Epic wide space scene: a large ocean planet with a glowing atmosphere in the foreground left, a small fleet of sleek spaceships flying toward it, distant nebula in teal and violet, layered stars for parallax, empty space on the right for text` + суфікс |
| ART-02 | Hero, шар зірок | 1920×1000, прозорий PNG | `Seamless transparent starfield layer, small and medium stars, very subtle colored dust` + суфікс |
| ART-03 | Картка «Розвиток планет» | 480×320 | `A colony on a rocky planet seen from orbit: mining towers, domes, glowing lights on the night side` + суфікс |
| ART-04 | Картка «Конструктор кораблів» | 480×320 | `A starship blueprint on a dark holographic table, ship hull with glowing module slots` + суфікс |
| ART-05 | Картка «Ринок» | 480×320 | `A busy space trade station with cargo ships docking, crates and glowing signs without text` + суфікс |
| ART-06 | Картка «Тактичні бої» | 480×320 | `Two small fleets exchanging laser fire in space, shield bubbles flashing, orange-red explosions` + суфікс |
| ART-07 | Раса Террани | 400×500 | `Terran warship: gray metal hull with blue accents, angular naval silhouette, bridge and hull numbers, side view` + суфікс |
| ART-08 | Раса Кварр | 400×500 | `Kvarr warship: crystalline faceted hull, hexagonal armor plates, amber and terracotta colors, fortress-like, side view` + суфікс |
| ART-09 | Раса Сколар | 400×500 | `Skolar warship: sharp predatory asymmetric blades, red and black, aggressive raider, side view` + суфікс |
| ART-10 | Раса Нексар | 400×500 | `Nexar warship: smooth geometric hull, glowing cyan lines and rings, white and cyan, high-tech, side view` + суфікс |
| ART-11 | Скріншот 1–3 (заглушки) | 1280×720 | `Stylized game UI screenshot placeholder: top resource bar, planet cards, dark panels` (генерує Design як макет UI, не картинка) |
| ART-12 | Планети для рейтингу й карток (6 типів) | 256×256, прозорі | `Round planet, [earth-like / volcanic / icy / desert / ocean / gas giant] with atmosphere glow, painted texture, transparent background` + суфікс |
| ART-13 | Новини (мініатюри ×3) | 400×225 | `Abstract space scene for a news card: [update / event / patch notes], simple composition, one focal object` + суфікс |
| ART-14 | Фон блоків і футера | 1920×600 | `Subtle dark nebula background with soft teal and violet clouds, very low contrast, suitable behind text` + суфікс |
| ART-15 | Вхід/реєстрація (бічна картинка) | 600×800 | `Lone starship leaving an orbital station toward a bright planet at dawn, vertical composition` + суфікс |
| ART-16 | Лого | SVG | Текстове «AETHERIS», без малюнка (тимчасово), Inter Bold, літерний інтервал +4% |

## 6. Промпт для Design (англійською, передати як є)

```
Design the FIRST PUBLIC PAGE of an online browser game called "AETHERIS" (landing page + sign-in/sign-up). This is NOT an in-game screen; it is what a person sees before they start playing.

About the game: a free-to-play online 2D space strategy in real time, one shared universe. Players develop planets, mine resources, build spaceships from components and modules, trade with other players on a market, and fight turn-based tactical battles in cycles of 5-10 minutes. Four playable races: Terrans (gray metal and blue, reliable, military), Kvarr (amber and terracotta, crystals and armor), Skolar (red and black, sharp aggressive raiders), Nexar (white and cyan, smooth shapes, glowing lines, technology). Tone: light, with a military flavor.

Visual style: painted 2D, beautiful and atmospheric. Dark UI only. Use these color tokens: bg-0 #070B16, bg-1 #0B1020, bg-2 #141B33, bg-3 #1C2544, border rgba(160,190,255,0.16), text-1 #E8EEFF, text-2 #9AA7C7, accent #2EC4C4 (hover #4FDADA), danger #FF6B3D, success #4CD08A, warning #F2C14E. Race colors: Terran #6C8DB8, Kvarr #D08A4E, Skolar #D2443C, Nexar #7FE0EE. Fonts: simple and plain only, no decorative fonts: Inter for everything (headings Inter SemiBold/Bold, body Inter Regular/Medium, numbers with tabular-nums; fallbacks Roboto, system-ui, Arial). Type scale desktop/mobile: H1 56/34, H2 36/26, H3 22/20, body 16/16, small 13/13. Logo: text "AETHERIS" in Inter Bold with +4% letter spacing.

Page structure, top to bottom: (1) header: logo, menu (About, Races, Ranking, News, Help), language switch UA/EN, buttons "Sign in" and "Play"; (2) hero: big space scene, headline, 1-2 sentences, primary button "Play for free", secondary "Sign in"; (3) "About the game": 4 cards with illustrations (planet development, ship constructor, market, tactical battles); (4) "Races": 4 cards with ship illustration, race color and mood; (5) "Screenshots": gallery of 4 frames; (6) "Ranking": top-10 players table (place, name, race, score) and a "Full ranking" button; (7) "News": 3 cards (date, title, excerpt, "Read more"); (8) FAQ accordion with 5-6 questions; (9) footer: language, terms, privacy policy, contacts, social links, version.
Also design the auth screens: sign in (email, password, "Forgot password?", "Continue with Google"), sign up (email, password, display name, terms checkbox), an error state, and a "Check your email" screen.

Illustrations: do NOT draw them. Put a labeled placeholder (ART-01 ... ART-16) with the correct size and aspect ratio wherever an illustration goes; I will supply the final painted images separately. Use this ID list: ART-01 hero background, ART-02 star layer, ART-03..06 the four "About" cards, ART-07..10 race ships, ART-11 screenshots, ART-12 planet icons, ART-13 news thumbnails, ART-14 section background, ART-15 auth side image, ART-16 logo.

Requirements: two layouts, desktop 1440-1920 px and mobile 360-430 px (hamburger menu, tap targets at least 44 px, no horizontal scroll); WCAG AA contrast; color is never the only carrier of information; UI copy in Ukrainian, short and clear for a newcomer, with an English variant of the menu and buttons; all data are made-up placeholders (player names, news), no real people or brands. Output a design-tokens table (colors, fonts, spacing, radii, shadows) and a list of components.
```

## 7. Результат
1. Макет у Design: головна сторінка для робочого столу й телефону, екрани входу, реєстрації, помилки й «Перевірте пошту».
2. Токени (кольори, шрифти, відступи, радіуси, тіні) у `documentation/ui-art/design/tokens.md`.
3. Експорт макетів (PNG) у `documentation/ui-art/design/`.
4. Опис компонентів (картка, кнопка, таблиця рейтингу, картка новини, форма) у `documentation/ui-art/design/components.md`.
5. Посилання на макет у `documentation/ui-art/design/README.md`.
6. Остаточні малюнки ART-01…ART-15 (за промптами розділу 5) складаються в `documentation/art-local/images/`, назви файлів за ідентифікаторами.

## 8. Критерії приймання
1. Усі блоки розділу 2 присутні на обох макетах.
2. Телефонний макет читається без горизонтального прокручування; цілі натискання не менші за 44 px.
3. Контраст тексту проходить WCAG AA.
4. Екрани авторизації охоплюють: вхід, Google, реєстрацію, помилку, «Забули пароль?», «Перевірте пошту».
5. Токени таблицею вичерпні: клієнт будуємо з них.
6. Автор схвалив стиль або записав правки (розділ 9).

## 9. Правки автора
(порожньо, заповнюється після перегляду макета)

## 10. Відкрито
1. ❓ Чи потрібна на старті сторінка «Про команду / контакти» і соцмережі.
2. ❓ Лого й шрифт: тимчасові чи окрема задача на айдентику.
3. ❓ Джерело графіки (ui-art/01 розділ 8): у макеті заглушки, остаточні картинки окремо.
4. 💡 Рейтинг кланів з'являється з етапом 2 (клани), на першій сторінці до цього лише рейтинг гравців за розвитком.
5. 💡 Реєстрація пов'язана із задачею 00002 (вхід, Google, підтвердження email): макет має відповідати її сценаріям.
