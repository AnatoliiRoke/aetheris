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

### Окремий користувач для міграцій (рекомендовано)

Сервер може працювати під користувачем бази з мінімальними правами: лише читання й запис
рядків, без права змінювати схему, створювати бази чи ролі. Міграції тоді виконуються під
власником схеми через окремий рядок підключення `Database:MigrationConnectionString`
(змінна середовища `Database__MigrationConnectionString` або `appsettings.Local.json`).
Якщо його не задано, міграції йдуть через основний рядок.

Створити користувача застосунку (пароль у скрипті не зберігається, його задають окремо):

```bash
psql -d aetheris_dev -v app_role=aetheris_app -f server/scripts/db/create-app-role.sql
psql -d aetheris_dev -c '\password aetheris_app'
```

Скрипт також забирає в користувача застосунку права `UPDATE`, `DELETE` і `TRUNCATE` на
журнал адміністраторів `admin_audit_log`: туди можна лише додавати записи.

> **Тести** беруть той самий рядок, але **завжди** перемикають ім'я бази на `aetheris_test`.
> Тому тести ніколи не чіпають дані розробки, навіть якщо в змінній середовища вказано
> `aetheris_dev`.

### Що відбувається при першому запуску

1. Застосовуються міграції.
2. Додаються початкові параметри, яких ще немає (див. «Початкові параметри» і
   «Параметри безпеки»). Наявні значення не перезаписуються.
3. Конфігурація завантажується в пам'ять (ліміти безпеки читаються з неї).
4. Піднімається сервер.

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
| `/api/v1/auth/...` | Реєстрація, підтвердження email, вхід, оновлення сесії, вихід, скидання пароля, Google |
| `POST /api/v1/test/echo` | Нейтральна тестова команда: показує весь конвеєр захисту |
| `/api/admin/...` | Адмінка: вхід із TOTP, `ping`, зміна параметрів, коефіцієнт часу, журнал |
| `/hubs/game` | SignalR-хаб ігрових подій (лише для гравця, що увійшов) |

Перевірка:

```bash
curl http://localhost:5080/health
```

---

## Тести

```bash
cd server
dotnet test
```

Рядок підключення тести беруть так само, як сервер: зі змінної
`AETHERIS_DATABASE__CONNECTIONSTRING` або з `appsettings.Local.json`, і завжди перемикають
базу на `aetheris_test`.

106 тестів, зокрема:

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
- **Ядро без ігрових назв** — `NoGameTermsTests` сканує код, міграції й seed і падає на
  ігровому терміні (дозволені лише назви модулів-заготовок).
- **Безпека (задача 00002)** — `tests/Aetheris.Tests/Security`: вхід і блокування, права й
  403 на `/api/admin`, повтори за ідентифікатором запиту, rate limit 429 і журнал, заголовки,
  CORS, CSRF, ліміт розміру, SignalR без сесії й чужі підписки, TOTP і незмінний журнал
  адміністраторів, логи без паролів, токенів і email, приклад нового захищеного запиту.
- **Аналізатор SQL** — правило `AETHERIS002` ловить конкатенацію й інтерполяцію в сирому SQL.

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

## Правило параметризованого SQL

Усі запити до бази лише параметризовані (задача 00002, розділ 2.4). Правило `AETHERIS002`
(той самий проєкт `server/analyzers/Aetheris.Analyzers`) — **помилка збірки**, тому CI
падає на будь-якому SQL, зібраному рядками:

| Метод | Що вимагає |
|---|---|
| EF Core `ExecuteSqlRaw(Async)`, `FromSqlRaw`, `SqlQueryRaw` | текст SQL — константа, значення — параметри |
| Dapper `SqlMapper.*` | так само |
| ADO.NET `CommandText`, конструктор команди | так само |
| EF Core `ExecuteSql(Async)`, `FromSql` з інтерпольованим рядком | дозволено: кожне значення стає параметром |

---

## Безпека

Ядро містить загальні механізми захисту. Нова функція гри підключається до них, а не пише
свої (задача 00002, розділ 1а).

| Механізм | Де | Що робить |
|---|---|---|
| Облікові записи | `Auth/` | Identity, хеш PBKDF2 з кількістю ітерацій з бази, довжина й поширені паролі, підтвердження email, тимчасове блокування після серії невдач |
| Сесія | `Auth/SessionIssuer.cs` | Коротка cookie сесії + refresh-токен (хеш у базі, одноразовий, повторне використання відкликає всі сесії); cookie `HttpOnly`, `Secure`, `SameSite=Strict` |
| Google | `Security/SecurityServiceCollectionExtensions.cs` | Вмикається, лише якщо задані `Authentication:Google:ClientId` і `ClientSecret`; `state` і correlation cookie перевіряє обробник |
| Політики прав | `Aetheris.Core/Security/SecurityPolicies.cs` | `player`, `admin`, `admin.owner/balance/support/moderation`; адмінські ролі окремі від гравця |
| Власність об'єктів | `Commands/OwnershipChecker.cs` | Єдина перевірка «це мій об'єкт» для команд і підписок SignalR |
| Конвеєр команд | `Commands/CommandEndpoints.cs` | Політика → ліміт → ідентифікатор запиту → розмір → валідація → власність → транзакція з журналом змін і збереженою відповіддю |
| Повтори | таблиця `request_idempotency` | Той самий `Idempotency-Key` повертає збережену відповідь, команда не виконується вдруге |
| Rate limit | `Security/RateLimitingSetup.cs` | Глобально на IP + групи `auth` (суворіша, на IP), `command` і `admin` (на акаунт); 429 і запис у журнал безпеки |
| Валідація | `Security/ValidationFilter.cs` | FluentValidation для всіх API-ендпоїнтів; відповідь — коди правил, без значень |
| Заголовки | `Security/SecurityHeadersMiddleware.cs` | CSP, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, без `Server`; HSTS і HTTPS-редирект поза Development |
| CORS і CSRF | `Security/ClientOrigins.cs`, `Security/CsrfProtectionMiddleware.cs` | Лише адреси з `Cors:AllowedOrigins`; запит із cookie сесії потребує токена `X-XSRF-TOKEN` з `GET /api/v1/auth/csrf`; хаб перевіряє `Origin` |
| Помилки | `Security/ApiExceptionHandler.cs` | Назовні лише `{ "error": "код" }`, без стеків і таблиць |
| Журнал безпеки | таблиця `security_event` | Входи, невдачі, блокування, зміна пароля, ліміти, CSRF, чужі об'єкти; сповіщення (рівень Error → Sentry) на серію невдалих входів |
| Адмінка | `Admin/` | Вхід лише з TOTP; кожен запит пише `admin_audit_log` (хто, що, коли, старе й нове значення); журнал не змінюється ні застосунком, ні SQL (тригер) |
| Логи | `Observability/SensitiveDataMaskingEnricher.cs` | Маскує паролі, токени й email у кожній події Serilog |

### Параметри безпеки

Усі числа захисту лежать у `config_parameter` під ключами `security.*` і змінюються без
перезапуску (крім позначених «при старті»). Значення поза межами визначення
(`server/src/Aetheris.Host/Data/ConfigSeed.cs`) адмінка не приймає, а сервер підтягує до
найближчої межі.

| Ключ | Старт | Що задає |
|---|---|---|
| `security.password.min_length` / `max_length` | 12 / 128 | Довжина пароля |
| `security.password.min_unique_chars` | 5 | Мінімум різних символів |
| `security.password.hash_iterations` | 210000 | Ітерації PBKDF2 (при старті) |
| `security.account.email_max_length` / `token_max_length` | 256 / 2048 | Довжина email і токенів |
| `security.login.max_failed_attempts` | 5 | Невдалих входів до блокування |
| `security.login.lockout_minutes` | 15 | Тривалість блокування |
| `security.session.access_minutes` | 15 | Життя cookie сесії |
| `security.session.refresh_days` | 14 | Життя refresh-токена |
| `security.admin.session_minutes` | 30 | Життя адмінської сесії |
| `security.admin.max_clock_multiplier` | 100 | Найбільший коефіцієнт часу |
| `security.admin.audit_page_size` | 100 | Записів журналу за раз |
| `security.rate_limit.{ip,auth,command,admin}.permit_limit` | 300 / 20 / 120 / 60 | Запитів за вікно |
| `security.rate_limit.{group}.window_seconds` | 60 | Вікно ліміту |
| `security.request.max_body_bytes` | 65536 | Розмір тіла запиту |
| `security.signalr.max_message_bytes` | 65536 | Розмір повідомлення SignalR (при старті) |
| `security.idempotency.max_key_length` | 64 | Довжина `Idempotency-Key` |
| `security.idempotency.retention_hours` | 24 | Скільки пам'ятати ідентифікатор |
| `security.idempotency.cleanup_interval_minutes` | 60 | Інтервал чистки |
| `security.alerts.failed_sign_in_threshold` / `_window_minutes` | 20 / 10 | Поріг сповіщення про серію невдалих входів |
| `security.hsts.max_age_days` | 365 | HSTS (при старті) |
| `test.echo.max_length` | 256 | Нейтральний ліміт тестової команди `Echo` |

### Перший адміністратор

```bash
cd server
dotnet run --project src/Aetheris.Host -- create-admin admin@example.com owner
```

Пароль вводиться без відлуння (або береться зі змінної `AETHERIS_ADMIN_PASSWORD`, якщо
консолі немає). Команда друкує `otpauth://`-посилання з ключем: його додають у застосунок
автентифікації. Ролі: `owner`, `balance`, `support`, `moderation` (через кому). Без коду
TOTP вхід в адмінку (`POST /api/admin/auth/login`) неможливий.

### Листи

Служби пошти ще немає: `LoggingAccountEmailSender` лише пише в лог, що лист для акаунта не
доставлено (без адреси й токена). До задачі з поштою email у розробці підтверджують у базі:

```sql
UPDATE asp_net_users SET email_confirmed = true WHERE email = '<адреса>';
```

---

## Як додати новий захищений запит

Інструкція для агентів. Захист не пишеться заново: новий запит — **один клас і один рядок
реєстрації**. Приклад у коді — `server/src/Aetheris.Host/Commands/EchoCommand.cs`, приклад
у тестах — `server/tests/Aetheris.Tests/Infrastructure/TestNoteCommand.cs`.

**1. Клас команди.** Успадкуйте `Command<TRequest, TResponse>` (`Aetheris.Core.Commands`).
Запит і відповідь — вкладені записи; запит містить лише намір, ніколи результат.

```csharp
public sealed class RenameThingCommand : Command<RenameThingCommand.Request, RenameThingCommand.Response>
{
    public override string Route => "/api/v1/things/rename";

    // Необов'язково: за замовчуванням SecurityPolicies.Player і RateLimitGroups.Command.
    public override string Policy => SecurityPolicies.Player;
    public override string RateLimitGroup => RateLimitGroups.Command;

    // Валідація. Числа беруться з конфігурації (через SecuritySettings або IGameConfig).
    public override void DefineRules(AbstractValidator<Request> rules) =>
        rules.RuleFor(request => request.Name).NotEmpty().MaximumLength(_settings.ThingNameMaxLength);

    // Об'єкт, яким має володіти гравець. Чужий об'єкт → 403 not_your_object.
    public override OwnedObjectReference? GetTarget(Request request) => new("thing", request.ThingId);

    // Виконується в транзакції; зміни пишіть через IChangeJournal.
    public override Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken) => ...;

    public sealed record Request(string ThingId, string Name);
    public sealed record Response(string Name);
}
```

**2. Рядок реєстрації** (у `Security/SecurityServiceCollectionExtensions.cs` або в модулі):

```csharp
services.AddCommand<RenameThingCommand>();
```

Усе інше команда отримує автоматично: вхід і політику, rate limit, CSRF для cookie, ліміт
розміру, валідацію, перевірку власності, `Idempotency-Key` (обов'язковий заголовок),
транзакцію, журнал змін і помилки без внутрішніх деталей.

**Якщо з'являється новий тип об'єктів** (наприклад, `thing`) — один раз на тип зареєструйте,
хто ним володіє:

```csharp
services.AddOwnedObjectType<ThingOwnerResolver>(); // IOwnedObjectResolver з ObjectType = "thing"
```

Той самий резолвер перевіряє підписки SignalR (`Subscribe("thing", id)`).

**Нові числа** (ліміти, довжини) — у `ConfigSeed` з межами, ключ у `security.*` або в
префіксі функції. Хардкод чисел ловить `AETHERIS001`.

**Новий тип ліміту** — додайте назву в `RateLimitGroups.EndpointGroups` і два параметри
`security.rate_limit.<група>.permit_limit` і `.window_seconds` у `ConfigSeed`.

**Адмінська дія** — ендпоїнт у групі `secured` в `Admin/AdminEndpoints.cs` з `.WithName("admin.<дія>")`
і потрібною політикою ролі. Запис у `admin_audit_log` робить фільтр групи; дія лише
викликає `AdminAuditScope.Record(ціль, старе, нове)`.

**Тест** — як `ProtectedRequestExampleTests`: без сесії 401, чужий об'єкт 403, невалідний
запит 400, без CSRF 403, повтор того самого ключа не виконує команду вдруге.

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

Workflow: `.github/workflows/ci.yml`. Три джоби:

| Job | Що робить |
|---|---|
| `secrets` | gitleaks сканує всю історію репозиторію (`.gitleaks.toml`) |
| `server` | Збірка (з `AETHERIS002` як помилкою) -> анотації `AETHERIS001` -> тести на PostgreSQL 17 (служба GitHub Actions) -> `dotnet format --verify-no-changes` -> `dotnet list package --vulnerable` |
| `client` | `npm ci` -> `npm audit --audit-level=high` -> `npm run lint` -> `npm run build` |

Dependabot (`.github/dependabot.yml`) щотижня перевіряє NuGet, npm і GitHub Actions.
Версії зафіксовані: NuGet у `server/Directory.Packages.props`, npm — точні версії в
`client/package.json` (`.npmrc`: `save-exact=true`) і `package-lock.json`.

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
| `Database__MigrationConnectionString` | Окремий рядок підключення для міграцій | ні |
| `Authentication__Google__ClientId` / `Authentication__Google__ClientSecret` | Вхід через Google | ні (без них Google вимкнено) |
| `Cors__AllowedOrigins__0` ... | Адреси клієнта для CORS і перевірки `Origin` | у Production так |
| `AETHERIS_ADMIN_PASSWORD` | Пароль для `create-admin` без консолі | ні |
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
| Microsoft.AspNetCore.Authentication.Google | 10.0.13 |
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
- `documentation/tech/05-bezpeka-ta-antychit.md` — безпека та античит
- `documentation/tasks/00001-zibraty-stek-i-zapustyty-server.md` — ядро проєкту
- `documentation/tasks/00002-bazova-bezpeka-servera.md` — базова безпека сервера
