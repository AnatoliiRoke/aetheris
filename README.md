# AETHERIS

Онлайн космічна стратегія в реальному часі. Браузерний клієнт, сервер на ASP.NET Core,
PostgreSQL, один спільний всесвіт.

> **Стан:** задачі 00001 і 00002 — ядро проєкту без ігрової логіки та базові механізми безпеки.
> У коді, міграціях і конфігурації немає ігрових назв: це перевіряє тест
> `tests/Aetheris.Tests/Architecture/NoGameTermsTests.cs`.

---

## Вимоги

| Компонент | Версія | Примітка |
|---|---|---|
| .NET SDK | 10.0.402 (LTS) | Версія зафіксована в `server/global.json` |
| PostgreSQL | 17 | Локальна база, Docker не потрібен |
| Node.js | 23+ | Тільки для клієнта |
| EF Core CLI | 10.0.13 | `dotnet tool install --global dotnet-ef --version 10.0.13` |

Встановлення .NET SDK без права root (рекомендований спосіб для macOS):

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

---

## Структура репозиторію

```
.
├── documentation/          ← проєктна документація (не змінюється без запису в задачі)
│   ├── game-design/        ← що і як працює в грі
│   ├── tech/               ← як влаштована технічна частина
│   ├── ui-art/             ← візуальний стиль та інтерфейс
│   ├── production/         ← етапи, MVP, дорожня карта, ризики
│   ├── references/         ← референс StarFederation
│   └── tasks/              ← задачі
├── server/                 ← рішення .NET
│   ├── src/
│   │   ├── Aetheris.Host/      ← точка входу ASP.NET Core
│   │   ├── Aetheris.Core/      ← спільне: конфігурація, час гри, помилки
│   │   ├── Aetheris.Economy/   ┐
│   │   ├── Aetheris.Planets/   │
│   │   ├── Aetheris.Fleet/     ├ модулі-заготовки
│   │   ├── Aetheris.Market/    │
│   │   ├── Aetheris.Rating/    │
│   │   ├── Aetheris.Chat/      │
│   │   └── Aetheris.Admin/     ┘
│   ├── analyzers/          ← власні Roslyn-аналізатори (AETHERIS001)
│   └── tests/
│       ├── Aetheris.Tests/         ← xunit v3, тести сервера
│       └── Aetheris.Analyzers.Tests/ ← тести аналізатора
└── client/                 ← React + TypeScript + PixiJS (Vite)
```

Модулі спілкуються **лише через публічні інтерфейси**. Правило перевіряє архітектурний тест
(`tests/Aetheris.Tests/Architecture/ModuleIsolationTests.cs`).

---

## Підготовка бази

Потрібні дві бази на локальному PostgreSQL 17:

```bash
createdb aetheris_dev
createdb aetheris_test
```

Підключення задається рядком підключення. Він **ніколи не потрапляє в репозиторій**.

Варіант 1 — змінна середовища (пріоритетна):

```bash
export AETHERIS_DATABASE__CONNECTIONSTRING="Host=localhost;Port=5432;Database=aetheris_dev;Username=<користувач>;Password=<пароль>"
```

Варіант 2 — локальний файл `server/src/Aetheris.Host/appsettings.Local.json` (у `.gitignore`):

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=aetheris_dev;Username=<користувач>;Password=<пароль>"
  }
}
```

Інших способів задати рядок підключення немає: сервер перевіряє обидва і видає
зрозумілу помилку, якщо рядок не знайдено.

> **Тести** беруть той самий рядок, але **завжди** перемикають ім'я бази на `aetheris_test`.
> Тому тести ніколи не чіпають дані розробки, навіть якщо в змінній середовища вказано
> `aetheris_dev`.

### Що відбувається при першому запуску

1. Застосовуються міграції.
2. Додаються початкові параметри, яких ще немає (див. «Початкові параметри»).
   Наявні значення не перезаписуються.
3. Піднімається сервер.

---

## Запуск сервера

Одна команда: міграції застосовуються автоматично, потім піднімається сервер.

```bash
cd server
dotnet run --project src/Aetheris.Host
```

Сервер доступний на <http://localhost:5080>.

| Адреса | Призначення |
|---|---|
| `GET /health` | Стан сервера й бази (JSON) |
| `GET /api/v1/ping` | Проста перевірка живості |
| `/hubs/game` | SignalR-хаб ігрових подій |

Перевірка:

```bash
curl http://localhost:5080/health
```

---

## Тести

```bash
cd server
export AETHERIS_DATABASE__CONNECTIONSTRING="Host=localhost;Port=5432;Database=aetheris_test;Username=<користувач>;Password=<пароль>"
dotnet test
```

59 тестів, зокрема:

- **Конфігурація без хардкоду** — зміна значення в базі змінює результат без перезбірки.
- **Ігровий годинник** — якір `(real0, game0, k)`, зміна множника, round-trip перетворень.
- **Property-тести годинника** (FsCheck) — інваріанти для будь-яких вхідних даних, а не лише
  для кількох прикладів.
- **SignalR** — клієнт підключається й отримує подію з номером.
- **Архітектура** — модулі не посилаються один на одного; реєстрація модулів перевіряється
  у реальному контейнері Host, а не в окремому.
- **Health** — ендпоінт підтверджує з'єднання з базою.
- **Очищення бази** — тестова база очищається перед прогоном; тест на це перевірено
  штучною поломкою очищення.
- **Аналізатор магічних чисел** — правило `AETHERIS001` повідомляє про числа, які залишилися
  в коді, і не повідомляє про числа з іменованих констант, розмірів колекцій та інших
  випадків, де число не є правилом гри.

> **Ізоляція тестів.** Усі тести, що працюють із базою, живуть у спільній колекції
> `database` і виконуються послідовно: одна тестова база — значить, тести не можуть
> виконуватися паралельно.

---

## Правило магічних чисел

Баланс гри живе в базі (`documentation/game-design/14-balans-i-formuly.md`), тому число в
коді — це майже завжди помилка: його не змінити без релізу й неможливо переглянути як
рішення дизайнера. Замість числа має бути іменована константа або параметр конфігурації.

Це правило реалізовано власним Roslyn-аналізатором `server/analyzers/Aetheris.Analyzers`:

| | |
|---|---|
| Ідентифікатор | `AETHERIS001` |
| Категорія | `Aetheris.Balance` |
| Серйозність | Warning (не ламає збірку) |
| Проєкт | `server/analyzers/Aetheris.Analyzers` (netstandard2.0) |
| Тести | `server/tests/Aetheris.Analyzers.Tests` |

Аналізатор підключений до всіх проєктів рішення через `Directory.Build.props`, тому
нові числа в коді видно одразу під час збірки. Тестові проєкти виключені: числа в тесті —
це дані тесту, а не правила гри.

**Що правило ігнорує** (число не є правилом):

- `-1`, `0` і `1` — індекси, лічильники, прапорці.
- Числа, що є частиною імені, наприклад `Base64`.
- Члени enum та їхні аргументи.
- Аргументи атрибутів.
- Бітові маски та зсуви.
- Розміри колекцій і буферів, зокрема `new byte[256]`, `HasMaxLength(128)`.
- Числа, порівняні з іменованою константою.
- Значення, з яких складається іменована константа.

Підняти серйозність до помилки, коли код балансу буде готовий:

```bash
# server/Directory.Build.props
<WarningsNotAsErrors>$(WarningsNotAsErrors);AETHERIS001</WarningsNotAsErrors>
```

Прибрати цей рядок — і правило стане помилкою, що ламає збірку.

---

## Клієнт

```bash
cd client
npm install
npm run dev
```

Клієнт за замовчуванням звертається до `http://localhost:5080`. Переопределення:

```bash
VITE_API_BASE_URL=http://localhost:5080 VITE_HUB_URL=http://localhost:5080/hubs/game npm run dev
```

---

## CI

Workflow: `.github/workflows/ci.yml`. Два джоби:

| Job | Що робить |
|---|---|
| `server` | Збірка -> тести на PostgreSQL 17 (служба GitHub Actions) -> `dotnet format --verify-no-changes` |
| `client` | `npm ci` -> `npm run lint` -> `npm run build` |

Локально цей цикл відтворюється так:

```bash
cd server && dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes
cd client && npm run lint && npm run build
```

> **Статус:** workflow готовий, але ще не відпрацював на GitHub — гілку не було запушено.
> Перевірити результат: вкладка **Actions** у репозиторії `AnatoliiRoke/aetheris`.

---

## Спостережуваність

Вимірювання зроблені з першого дня (documentation/tech/01, розділ 3.4):

| Складова | Що дає | Увімкнення |
|---|---|---|
| **Serilog** | Логи у консолі | Увімкнено завжди |
| **OpenTelemetry** | Tracing запитів, метрики ASP.NET Core та HTTP-клієнта | Увімкнено завжди |
| **Prometheus** | Метрики на `/metrics` | Увімкнено завжди |
| **OTLP** | Export трас у колектор | Лише якщо задано `OpenTelemetry:OtlpEndpoint` або `OTEL_EXPORTER_OTLP_ENDPOINT` |
| **Sentry** | Помилки в продакшені | Лише якщо задано `Sentry:Dsn` |

Без `Sentry:Dsn` і без OTLP-колектора сервер запускається без помилок — це зручно для
локальної розробки.


---

## Робота з базою

```bash
cd server

# Нова міграція
dotnet ef migrations add <Назва> --project src/Aetheris.Host --output-dir Data/Migrations

# Застосувати
dotnet ef database update --project src/Aetheris.Host

# Відкотити
dotnet ef migrations remove --project src/Aetheris.Host
```

Імена таблиць і колонок — `snake_case`, як описано в `documentation/tech/03-model-danykh.md`.

---

## Конфігурація гри

Правила гри читають значення **лише** через `IGameConfig`. Параметри лежать у таблиці
`config_parameter` і перезавантажуються в пам'ять кожні 30 секунд.

```sql
INSERT INTO config_parameter (key, value, version, updated_at, updated_by)
VALUES ('test.hello_multiplier', '2', 1, now(), 'seed');
```

Зміна в базі підхоплюється без перезапуску та без перезбірки.

### Початкові параметри

Ядро не містить правил гри, тому в базу при старті додаються лише нейтральні тестові
параметри `test.*` і параметри механізмів безпеки `security.*` (див. «Безпека»). Ігрові
параметри з'являться разом із відповідними функціями. Значення, задані вручну, ніколи не
перезаписуються. Межі кожного параметра (мінімум і максимум) описані в
`server/src/Aetheris.Host/Data/ConfigSeed.cs`.

| Ключ | Значення | Що задає |
|---|---|---|
| `test.hello_multiplier` | 1 | Нейтральний приклад: тест змінює його в базі й бачить новий результат |

```sql
-- Змінити значення
UPDATE config_parameter
SET value = '15', version = version + 1, updated_at = now()
WHERE key = 'test.hello_multiplier';
```

Сервер підхопить зміну протягом 30 секунд. Перевірити без очікування:

```bash
psql -d aetheris_dev -c "SELECT key, value, version FROM config_parameter ORDER BY key;"
```

---

## Змінні середовища

| Змінна | Призначення | Обов'язкова |
|---|---|---|
| `AETHERIS_DATABASE__CONNECTIONSTRING` | Рядок підключення до PostgreSQL | так (або `appsettings.Local.json`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` | ні |
| `ASPNETCORE_URLS` | Адреса прослуховування | ні |
| `SENTRY_DSN` | Sentry для помилок | ні |
| `VITE_API_BASE_URL` | Адреса API для клієнта | ні |
| `VITE_HUB_URL` | Адреса SignalR-хаба для клієнта | ні |

Жоден секрет не зберігається в репозиторії.

---

## Версії бібліотек

Зафіксовані в `server/Directory.Packages.props` (25.10.2026):

| Пакет | Версія |
|---|---|
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 |
| Microsoft.EntityFrameworkCore | 10.0.13 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 |
| Microsoft.AspNetCore.SignalR.Protocols.MessagePack | 10.0.13 |
| MessagePack | 3.1.12 |
| Serilog.AspNetCore | 10.0.0 |
| OpenTelemetry.Extensions.Hosting | 1.20.1 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.20.1 |
| OpenTelemetry.Instrumentation.Http | 1.20.1 |
| OpenTelemetry.Exporter.Prometheus.AspNetCore | 1.20.1-beta.1 |
| OpenTelemetry.Exporter.Console | 1.20.1 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.20.1 |
| Sentry.AspNetCore | 6.12.1 |
| FluentValidation | 12.2.0 |
| xunit.v3 | 4.0.2 |
| xunit.runner.visualstudio | 4.0.1 |
| Microsoft.NET.Test.Sdk | 18.11.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 |
| Microsoft.AspNetCore.TestHost | 10.0.13 |
| Microsoft.AspNetCore.SignalR.Client | 10.0.13 |
| FsCheck | 3.6.0 |
| pixi.js (клієнт) | 8.22.0 |
| react / react-dom (клієнт) | 19.3.1 |

---

## Довідкові документи

- `documentation/tech/01-arkhitektura.md` — архітектура, таймери, масштабування
- `documentation/tech/02-tekhnolohichnyi-stek.md` — вибір стеку та обґрунтування
- `documentation/tech/03-model-danykh.md` — сутності та таблиці
- `documentation/tech/04-api.md` — REST та WebSocket
- `documentation/tech/06-testuvannia-ta-symuliatory.md` — рівні тестів і симулятори
- `documentation/tech/07-planuvalnyk-podii.md` — планувальник подій
- `documentation/tasks/00001-zibraty-stek-i-zapustyty-server.md` — поточна задача
