# Задача 00069 — Екран бою в клієнті

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12–13.11.2026):** перенесення макета екрана бою в клієнт.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md), [ui-art/03](../ui-art/03-interfeis-boiu.md).
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/ui-art/05-yedynyi-styl-interfeisu.md`.
> **Залежить від:** макет [00068](00068-dyzain-ekrana-boiu.md), API [00065](00065-tsykly-boiu-na-serveri.md), формації [00071](00071-boiovyi-poriadok-flotu.md).

## 1. Як є зараз (перевірено 13.11.2026)

1. Клієнт: `client/src/{api,components,pages,router,session,styles,texts,i18n,game,config}`; API-модулі — `client/src/api/*Api.ts` з `http.ts`, `context.ts`, `useApi.ts`; токени — `client/src/styles/tokens.css`.
2. Карта на PixiJS і флоти на карті — задачі 00034, 00059 (`client/src/game`).
3. Подій реального часу — клієнт підписаний на `GameHub` (метод `gameEvent`, `GameEventEnvelope { seq, type, payload, createdAt }`).
4. Екрана бою немає.

## 2. Рішення

### 2.1 Маршрути й структура файлів

```
client/src/api/combatApi.ts                 запити й типи DTO (дзеркало 00065 §3)
client/src/api/combatEvents.ts              підписка на combat.* з наявного каналу
client/src/pages/combat/CombatPage.tsx      /combat/:id, React.lazy
client/src/pages/combat/CombatList.tsx      /combat (мої бої, GET /combat/active)
client/src/pages/combat/store/useCombatStore.ts   (zustand або наявний патерн проєкту) стан: view, cycle, phase, layers, selection, draftOrders
client/src/pages/combat/scene/CombatScene.ts      PixiJS-сцена (клас, не React)
client/src/pages/combat/scene/{Camera,FieldLayer,ZonesLayer,SquadronLayer,OrdersLayer,TrackLayer,FogLayer,RangeLayer,LastKnownLayer}.ts
client/src/pages/combat/panels/{OrdersPanel,SquadronPanel,FormationSwitcher,HoldPanel,LogPanel,PhaseBar,CycleBrowser,LayerToggles}.tsx
client/src/pages/combat/model/{buildTracks.ts,buildPositions.ts,hit.ts}      чисті функції (тестуються без Pixi)
```

Маршрути додаються в `client/src/router` (ліниво). Вхід: значок бою на флоті (карта), сторінка флоту, `CombatList`. Оболонка (шапка, меню, футер) у власному шарі й не перемальовується (спільний layout, сторінка — вкладений `Outlet`).

### 2.2 Стан і дані

- Початок: `GET /combat/{id}`; кожен наступний стан — лише після події (`combat.phase_changed`, `combat.segment_ready`, `combat.finished`) → один `GET` потрібного ресурсу. Опитування за таймером немає; таймер фази — локальний відлік до `phaseEndsAt` (сервер — джерело правди, лічильник не звертається до мережі).
- Чернетка наказів тримається в сторі й відправляється одним `POST /orders` («Готово» або автозбереження після 1.5 с бездіяльності, налаштування в `client/src/config`, не число в компоненті).
- Після відповіді сервера з помилкою (`combat_phase_invalid`, `combat_order_invalid`…) — повідомлення з i18n за кодом; чернетка відкочується до серверного стану.

### 2.3 Сцена PixiJS

- Шари знизу вгору: поле й сітка орієнтирів → астероїдні поля → планети → зони розстановки → `LastKnown` (напівпрозорі) → ескадрильї (значок групи; на масштабі ≥ порогу — кораблі) → `OrdersLayer` (стрілки маршрутів, лінії до цілей, значки поведінки) → `TrackLayer` (сліди «мій рух»/«рух ворога») → `RangeLayer` (кола дальності зброї й радара обраної ескадрильї) → `FogLayer` (маска невидимого, затемнення).
- Камера: перетягування, колесо/щипок, межі поля, збереження масштабу в сторі.
- Ескадрилья: значок (форма — група корпусу, колір — токен сторони `--color-side-own/enemy`), смуги щит/броня/корпус (малі при віддаленні), число кораблів, боєкомплект; вибір кліком; для обраної — кола.
- Режим наказу: клік по полю — додати точку маршруту; перетягування точки — змінити; клік по точці з Shift — видалити; клік по ворогу — ціль фокусу (лише видимий; перевірка локальна й на сервері).
- Розстановка (`deploy`): перетягування ескадрильї в межах своєї зони (клітинка-обмежувач у координатах сцени = зона з `myZone`); відсікання на краю зони.

### 2.4 Результат циклу без анімації

`GET /combat/{id}/segments/{cycle}` → `{positionsBefore, positionsAfter, myTracks, enemyTracks, log}`. `buildPositions` ставить ескадрильї в `positionsAfter` одразу (без твіну); `buildTracks` будує ламані з масивів точок (`combat.track_step_seconds` вже застосовано на сервері); шари «рух ворога» й «мій рух» вмикаються `LayerToggles`; значки знищень — з `log`. Кнопки `◀ ▶` (`CycleBrowser`) вантажать `segments/{cycle}` для обраного циклу (кеш у сторі), керування наказами — лише в поточному.

### 2.5 Продуктивність

1. **Статичний рендер:** `app.ticker` зупинений (`autoStart:false`), кадр малюється лише з `scene.invalidate()` (накази, новий цикл, камера, зміна шару); `requestAnimationFrame` злиття запитів у один кадр.
2. Один текстурний атлас спрайтів (значки груп, стрілки, маркери) у `client/src/assets/combat/atlas.json`; `ParticleContainer`/`Sprite` пакетно, `Graphics` лише для ліній (переписуються при зміні).
3. LOD: при `zoom < lodThreshold` ескадрилья — один значок; кораблі лише при наближенні; кола дальності малюються для обраної.
4. `resolution = min(devicePixelRatio, 2)` на ПК, `min(devicePixelRatio, 1.5)` на мобільних (`navigator.userAgentData?.mobile`/`pointer: coarse`); без фільтрів світіння/blur на мобільних; `antialias: false` на мобільних.
5. Цілі: 600 кораблів, ≥ 50 к/с на ПК з вбудованою графікою, ≥ 30 к/с на бюджетному Android; вбудований лічильник кадрів у режимі розробника (`?perf=1`, `scene/FpsMeter.ts`) показує середній і мінімальний FPS і час кадру.
6. Якщо WebGL недоступний — повідомлення «Бій потребує WebGL» замість поля (`pixi.js` `isWebGLSupported`).
7. Лінива довантажена сторінка; Pixi імпортується динамічно (`import('pixi.js')`) лише на екрані бою, чанк не потрапляє в основний бандл.

### 2.6 Панелі

- `PhaseBar`: фаза, «Цикл N з M», таймер, «Готово»/«Не готово».
- `FormationSwitcher` (лише `deploy`): кнопки-назви формацій кожного флоту з позначкою типової; клік → `POST /formation`, поле оновлюється відповіддю; «Зберегти як порядок флоту».
- `HoldPanel` (лише `deploy`): таблиця кораблів × (дрони, кожен тип боєприпасів) із «зайнято / місткість», кнопка «Автоматично».
- `SquadronPanel`: поведінка й правило (довідники з `contentApi`), ціль фокусу, дрони «Запустити/Відкликати», «Відступ», «Розділити/Об'єднати».
- `LogPanel`: текстовий журнал цикла (шаблони з i18n за `type`).
- Мобільна версія (≤ breakpoint з `tokens.css`): поле на весь екран, панелі — висувна нижня шухляда з вкладками.

### 2.7 Тексти й стиль

Усі підписи — `client/src/texts` (ключі `combat.*`), помилки за кодами сервера; кольори, відступи, радіуси лише з `tokens.css`; кольори сторін — токени; компоненти — готові з ui-art/05.

## 3. Що зробити

1. `combatApi.ts` + типи, `combatEvents.ts`.
2. Сторінки, сцена, шари, панелі, стор, чисті функції моделі, `FpsMeter`.
3. Атлас і ресурси; мобільна компоновка з макета 00068.
4. Тести (Vitest + Testing Library): `buildTracks`/`buildPositions` з прикладу відповіді; `OrdersPanel` (додати/видалити точку, фокус, дрони); `PhaseBar` (таймер, «Готово»); `FormationSwitcher` (виклик і оновлення поля); `HoldPanel` (понад місткість — помилка); відсутність опитування (мок: після завантаження жодного повторного `GET` без події); сцена-тест без WebGL (мок Pixi) перевіряє виклики `invalidate`.
5. `npm run lint`, `npm run build` — зелені; перевірка бандла: Pixi бойового екрана в окремому чанку.

## 4. Критерії приймання

1. Усі елементи макета 00068 для екрана бою на місці.
2. Гравець проводить бій від розстановки до кінця лише через цей екран.
3. Опитування сервера за таймером немає; оболонка не перемальовується (тест рендерів).
4. Вимоги 2.5 перевірені вбудованим лічильником на сцені 600 кораблів; ручна перевірка в Chrome і на бюджетному Android — у `00069-pytannia.md`, якщо виконавець не може.
5. Lint і build зелені.

## 5. Результат виконання

_(заповнює виконавець)_
