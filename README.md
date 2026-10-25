# AETHERIS

Онлайн космічна стратегія в реальному часі. Браузерний клієнт, сервер на ASP.NET Core,
PostgreSQL, один спільний всесвіт.

> **Стан:** задача 00001 — порожній, але робочий каркас сервера. Ігрової логіки ще немає.

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
│   └── tests/Aetheris.Tests/   ← xunit v3
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

38 тестів, зокрема:

- **Конфігурація без хардкоду** — зміна значення в базі змінює результат без перезбірки.
- **Ігровий годинник** — якір `(real0, game0, k)`, зміна множника, round-trip перетворень.
- **SignalR** — клієнт підключається й отримує подію з номером.
- **Архітектура** — модулі не посилаються один на одного.
- **Health** — ендпоїнт підтверджує з'єднання з базою.

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
| Dapper | 2.1.89 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 |
| Microsoft.AspNetCore.SignalR.Protocols.MessagePack | 10.0.13 |
| MessagePack | 3.1.12 |
| Serilog.AspNetCore | 10.0.0 |
| OpenTelemetry.Extensions.Hosting | 1.20.1 |
| Sentry.AspNetCore | 6.12.1 |
| FluentValidation | 12.2.0 |
| xunit.v3 | 4.0.2 |
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
