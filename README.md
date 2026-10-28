# AETHERIS

Онлайн космічна стратегія в реальному часі. Браузерний клієнт, сервер на ASP.NET Core,
PostgreSQL, один спільний всесвіт.

> **Стан:** задачі 00001 і 00002 — ядро проєкту та базові механізми безпеки; задача 00004 —
> перший модуль з ігровими правилами `Aetheris.World` (генерація всесвіту, розділ «Світ»).
> У ядрі, хості й модулях-заготовках немає ігрових назв: це перевіряє тест
> `tests/Aetheris.Tests/Architecture/NoGameTermsTests.cs` (слова світу дозволені лише в модулі
> World і його тестах).

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
│   │   ├── Aetheris.Admin/     ┘
│   │   ├── Aetheris.World/         ← генерація всесвіту
│   │   ├── Aetheris.Requirements/  ← спільна таблиця вимог
│   │   └── Aetheris.Research/      ← технології та черга досліджень
│   ├── analyzers/          ← власні Roslyn-аналізатори (AETHERIS001)
│   └── tests/
│       ├── Aetheris.Tests/         ← xunit v3, тести сервера
│       └── Aetheris.Analyzers.Tests/ ← тести аналізатора
└── client/                 ← React + TypeScript + PixiJS (Vite)
```

Модулі спілкуються **лише через публічні інтерфейси**. Правило перевіряє архітектурний тест
(`tests/Aetheris.Tests/Architecture/ModuleIsolationTests.cs`).

---

## Швидкий старт: одна команда

Після «Підготовки бази» (нижче) сервер і клієнт запускаються разом:

```bash
./dev.sh               # сервер http://localhost:5080 + клієнт http://localhost:5173, відкриває браузер
./dev.sh --no-browser  # те саме без браузера
```

Скрипт (`dev.sh` у корені, macOS/zsh, без Docker):

- перевіряє .NET SDK (також у `~/.dotnet`, тоді додає його в `PATH` лише для свого процесу),
  Node.js 23+, наявність рядка підключення (змінна `AETHERIS_DATABASE__CONNECTIONSTRING` або
  `appsettings.Local.json`; сам рядок не виводиться), доступність PostgreSQL і вільні порти;
- коли чогось бракує, пише, чого саме і як це виправити;
- за відсутності `client/node_modules` виконує `npm ci`;
- запускає обидва процеси, позначає їхні рядки `[server]` і `[client]`, чекає на
  `/health` і відкриває <http://localhost:5173>;
- **Ctrl+C** зупиняє обидва процеси разом з їхніми дочірніми й перевіряє, що порти вільні; якщо
  один процес упав, зупиняє й другий.

Порти можна змінити, якщо стандартні зайняті іншою копією:
`AETHERIS_SERVER_PORT=5081 AETHERIS_CLIENT_PORT=5174 ./dev.sh`.

Глобальні налаштування терміналу й профіль оболонки скрипт не змінює.

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
| `/api/v1/auth/...` | Реєстрація, повторний лист підтвердження, підтвердження email, вхід, оновлення сесії, вихід, скидання пароля, Google, `providers` (чи увімкнено Google) |
| `POST /api/v1/test/echo` | Нейтральна тестова команда: показує весь конвеєр захисту |
| `/api/admin/...` | Адмінка (задача 00007): вхід (TOTP за перемикачем `admin.require_totp`), сесія, параметри з історією й відкатом, журнал, генерація секторів у фоні, таблиці світу |
| `/admin` (клієнт) | Сторінка адміністратора: окремий модуль клієнта, посилання з меню гравця немає |
| `/api/v1/world/...` | Світ для гравця: довідники, сектори, системи, вміст системи, вільні планети, стартова планета |
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

224 тести (204 тести сервера і 20 тестів аналізаторів), зокрема:

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
- **Світ (задача 00004)** — `tests/Aetheris.Tests/World`: детермінованість за зерном,
  інваріанти на 100 зернах (FsCheck), розподіли типів і розмірів на >1000 планет, зміна
  параметра в базі без перезбірки, новий ресурс і кристал лише даними, z ≠ 0 і тривимірні
  відстані, іменування `_id` / `_ids` у схемі й JSON, перший і повторний запуск, вичерпання
  поля й нове поле подією планувальника на прискореному годиннику, 100 паралельних новачків
  без спільної планети, продуктивність (сектор зі 150 систем до 2 с, читання системи до 20 мс),
  адмін-команди з роллю, TOTP і журналами, видимість «лише своя планета».
- **Адмін-панель (задача 00007)** — `tests/Aetheris.Tests/Admin` і `World/AdminGenerationTests.cs`:
  відмова всім адмін-маршрутам без сесії й гравцю, завершення за бездіяльністю й вихід,
  суворіші ліміти входу, перемикач TOTP в обох станах, кожен ендпоінт — право головної ролі,
  ендпоінт без коду дії не проходить, параметри (тип, межі, причина, лише читання, дія без
  перезапуску, журнал, відкат), фонова генерація й «одна одночасно», той самий seed — той самий
  результат, таблиці з пошуком/фільтрами/пагінацією, відсутність хешів, токенів і листів у відповідях.

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
| Політики прав | `Aetheris.Core/Security/SecurityPolicies.cs` | `player`, `admin`; права адмінів — таблиці `admin_role`, `admin_permission`, `admin_role_permission`, `admin_account_role` (задача 00007) |
| Власність об'єктів | `Commands/OwnershipChecker.cs` | Єдина перевірка «це мій об'єкт» для команд і підписок SignalR |
| Конвеєр команд | `Commands/CommandEndpoints.cs` | Політика → ліміт → ідентифікатор запиту → розмір → валідація → власність → транзакція з журналом змін і збереженою відповіддю |
| Повтори | таблиця `request_idempotency` | Той самий `Idempotency-Key` повертає збережену відповідь, команда не виконується вдруге |
| Rate limit | `Security/RateLimitingSetup.cs` | Глобально на IP + групи `auth` (суворіша, на IP), `command` і `admin` (на акаунт); 429 і запис у журнал безпеки |
| Валідація | `Security/ValidationFilter.cs` | FluentValidation для всіх API-ендпоїнтів; відповідь — коди правил, без значень |
| Заголовки | `Security/SecurityHeadersMiddleware.cs` | CSP, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`, без `Server`; HSTS і HTTPS-редирект поза Development |
| CORS і CSRF | `Security/ClientOrigins.cs`, `Security/CsrfProtectionMiddleware.cs` | Лише адреси з `Cors:AllowedOrigins`; запит із cookie сесії потребує токена `X-XSRF-TOKEN` з `GET /api/v1/auth/csrf`; хаб перевіряє `Origin` |
| Помилки | `Security/ApiExceptionHandler.cs` | Назовні лише `{ "error": "код" }`, без стеків і таблиць |
| Журнал безпеки | таблиця `security_event` | Входи, невдачі, блокування, зміна пароля, ліміти, CSRF, чужі об'єкти; сповіщення (рівень Error → Sentry) на серію невдалих входів |
| Адмінка | `Admin/` | Вхід з окремими суворішими лімітами (`admin_auth`, блокування після 3 невдач); TOTP за перемикачем `admin.require_totp`; серверна сесія `admin_session` завершується за бездіяльністю; кожен запит перевіряє право з БД за кодом дії (ім'я ендпоінта) і пише `admin_audit_log` (хто, що, коли, старе й нове значення, причина); журнал не змінюється ні застосунком, ні SQL (тригер) |
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
| `security.admin.session_minutes` | 30 | Хвилин бездіяльності, після яких адмінська сесія завершується |
| `security.admin.session_max_hours` | 12 | Найдовше життя адмінської сесії навіть з активністю |
| `admin.require_totp` | true (у dev — false) | Чи потрібен код TOTP для входу адміна; у панелі лише читання |
| `security.admin.login_max_failed_attempts` / `login_lockout_minutes` | 3 / 30 | Блокування адмін-акаунта (суворіше, ніж для гравця) |
| `security.rate_limit.admin_auth.permit_limit` / `window_seconds` | 5 / 60 | Ліміт запитів входу в адмінку на IP |
| `security.admin.page_size` / `search_max_length` | 50 / 100 | Розмір сторінки й довжина пошуку адмін-таблиць |
| `security.admin.reason_min_length` / `reason_max_length` | 3 / 500 | Довжина обов'язкової причини зміни |
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
| `scheduler.poll_seconds` | 5 | Як часто планувальник перевіряє події, що настали |
| `scheduler.batch_size` | 100 | Скільки подій виконується за один прохід |

### Перший адміністратор

```bash
cd server
dotnet run --project src/Aetheris.Host -- create-admin                      # email спитає в терміналі, роль owner
dotnet run --project src/Aetheris.Host -- create-admin admin@example.com owner
```

Email можна передати аргументом або ввести в терміналі; пароль вводиться без відлуння (або
береться зі змінної `AETHERIS_ADMIN_PASSWORD`, якщо консолі немає). Роль за замовчуванням —
головна `owner`; інші коди беруться з таблиці `admin_role` (у seed також `balance`, `support`,
`moderation` з 00002). Команда друкує `otpauth://`-посилання з ключем для застосунку
автентифікації. Після цього вхід — на сторінці `/admin`.

**TOTP-перемикач.** Параметр `admin.require_totp`: у розробці seed пише `false`
(`appsettings.Development.json`, розділ `ConfigSeed:Overrides`), в інших середовищах — `true`.
У панелі параметр лише читається. **Перед публічним запуском обов'язково ввімкнути:**

```sql
UPDATE config_parameter SET value = 'true', version = version + 1, updated_at = now()
WHERE key = 'admin.require_totp';
```

Після ввімкнення відкриті сесії без TOTP завершуються, вхід вимагає код.

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

## Як додати новий вид довідника

Інструкція для агентів (задача 00023). Довідник — це таблиці модуля, який ним володіє, плюс
**опис виду** і **один сервіс**; клієнт і Host не змінюються: пункт меню «Довідники», список і
форма з'являються самі. Зразки в коді: `server/src/Aetheris.World/Services/WorldReferenceAdminService.cs`
(прості види), `server/src/Aetheris.Planets/Services/PlanetsReferenceAdminService.cs` (рівні,
вартість, ефекти, вимоги).

**1. Таблиця.** Рядок має `code` (нейтральний код `<група>_N` від `ICodeSequence`), `name_key`,
`description_key`, за потреби `image_key`, а також `is_archived` (bool, false) і `version`
(int, 1). Видалення немає: лише архівування.

**2. Опис виду** — `AdminReferenceDescriptor` (`Aetheris.Core.Admin`): логічний код виду,
група кодів, префікс ключів текстів, ключ назви в меню, місце в меню, маршрут, коди дій і поля
(`AdminReferenceField`: `text` з мовами, `number`, `select`, `reference`, `image`, `flag`,
`levels`/`costs`/`effects`/`requirements` зі своїми колонками). Вимоги рівня беріть готовим
полем `RequirementAdminFields.Field`.

**3. Сервіс** реалізує `IAdminReferenceService`: список (пошук і фільтр архіву —
`AdminReferenceRules.Page`), запис форми, створення, зміна з версією (`FOR UPDATE` +
`AdminReferenceRules.CheckVersion`), архівування. Тексти — `ILocalizationTexts.AddMissingAsync`
/ `SetAsync` у тій самій транзакції; після коміту скиньте кеш каталогу модуля
(`Invalidate()`). Помилки — `AdminReferenceException` з кодом поля.

**4. Реєстрація** у модулі:

```csharp
services.AddScoped<ThingReferenceAdminService>();
services.AddScoped<IAdminReferenceService>(sp => sp.GetRequiredService<ThingReferenceAdminService>());

// у Map…AdminEndpoints(RouteGroupBuilder secured), який Host викликає в Admin/AdminEndpoints.cs:
secured.MapAdminReference<ThingReferenceAdminService>(ThingReferenceAdminService.ThingDescriptor);
```

Коди дій виду (читання списку і запису — screen, створення, зміна, архів, повернення —
action) потрапляють у сид дозволів автоматично; роль із `all_permissions` отримує їх одразу.

**5. Підписи полів** — ключі `admin.reference.*` у вбудованому файлі модуля
`Data/admin-reference-texts.json` (українською й англійською); сид додає відсутні й не
перезаписує змінені в «Текстах».

**6. Тести** — як у `Planets/BuildingReferenceTests.cs`: створення доходить до гравця, зміна
діє без перезапуску, правила полів (400/422), архів, 409 за старої версії, 20 паралельних
створень, сид не перезаписує, рядок, доданий прямо в таблицю, доходить до гри.

---

## Клієнт

Публічний сайт за дизайном `documentation/design/aetheris-web 1` (задача 00003): лендинг і
екрани акаунта. React + TypeScript + Vite, без бібліотек роутингу й локалізації.

```bash
# термінал 1: сервер на http://localhost:5080
cd server && dotnet run --project src/Aetheris.Host

# термінал 2: клієнт на http://localhost:5173
cd client
npm ci
npm run dev
```

Vite пересилає `/api`, `/hubs` і `/health` на сервер (`vite.config.ts`, змінна
`AETHERIS_SERVER_URL`), тому сторінка й API мають одне походження й cookie сесії лишаються
своїми. Cookie сесії мають прапорець `Secure`: Chrome і Firefox приймають його на
`http://localhost`; для Safari запускайте сервер із профілем `https`.

| Адреса | Сторінка |
|---|---|
| `/` | Лендинг: шапка з UA/EN, hero, про гру, раси, скриншоти, топ-10, новини, FAQ, футер |
| `/login` | Вхід; помилки пароля, блокування, непідтверджена пошта, ліміт спроб |
| `/register` | Реєстрація з підказкою правил пароля й згодою з умовами |
| `/forgot-password` | Запит посилання для нового пароля |
| `/check-email` | «Перевір пошту» з повторним листом через 60 с |
| `/confirm-email?userId=&token=` | Ціль посилання з листа підтвердження |
| `/reset-password?userId=&token=` | Ціль посилання з листа скидання пароля |

| Що | Де |
|---|---|
| Токени дизайну (CSS-змінні) | `src/styles/tokens.css`; решта стилів — `base`, `components`, `sections`, `auth` |
| Тексти uk / en | `src/i18n/uk.ts`, `src/i18n/en.ts` (компілятор вимагає однакових ключів) |
| Клієнт API | `src/api/http.ts`: cookie, `X-XSRF-TOKEN` з `/api/v1/auth/csrf`, `Idempotency-Key` для команд |
| Рейтинг і новини | інтерфейс `src/api/contentApi.ts`, заглушка `src/api/mockContentApi.ts` до появи ендпоїнтів |
| Арти | `src/assets/images` (лінива підвантажка), hero і зорі в `public/` (preload) |

Перевірки: `npm run lint`, `npm test` (Vitest + Testing Library), `npm run build`.

Змінні збірки: `VITE_API_BASE_URL` (за замовчуванням те саме походження), `VITE_HUB_URL`,
`VITE_STUDIO_NAME`, `VITE_SUPPORT_EMAIL`. Для production статичний хостинг має віддавати
`index.html` на всі невідомі шляхи (маршрути клієнта).

---

## CI

Workflow: `.github/workflows/ci.yml`. Три джоби:

| Job | Що робить |
|---|---|
| `secrets` | gitleaks сканує всю історію репозиторію (`.gitleaks.toml`) |
| `server` | Збірка (з `AETHERIS002` як помилкою) -> анотації `AETHERIS001` -> тести на PostgreSQL 17 (служба GitHub Actions) -> `dotnet format --verify-no-changes` -> `dotnet list package --vulnerable` |
| `client` | `npm ci` -> `npm audit --audit-level=high` -> `npm run lint` -> `npm test` -> `npm run build` |

Dependabot (`.github/dependabot.yml`) щотижня перевіряє NuGet, npm і GitHub Actions.
Версії зафіксовані: NuGet у `server/Directory.Packages.props`, npm — точні версії в
`client/package.json` (`.npmrc`: `save-exact=true`) і `package-lock.json`.

Локально цей цикл відтворюється так:

```bash
cd server && dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes
cd client && npm run lint && npm test && npm run build
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

# Нова міграція модуля World (свій контекст і свої таблиці)
dotnet ef migrations add <Назва> --project src/Aetheris.World --startup-project src/Aetheris.Host --context WorldDbContext --output-dir Data/Migrations

# Нова міграція модуля Planets (будівлі, черги, баланс планет)
dotnet ef migrations add <Назва> --project src/Aetheris.Planets --startup-project src/Aetheris.Host --context PlanetsDbContext --output-dir Data/Migrations

# Нова міграція спільного модуля вимог
dotnet ef migrations add <Назва> --project src/Aetheris.Requirements --startup-project src/Aetheris.Host --context RequirementsDbContext --output-dir Data/Migrations

# Нова міграція модуля технологій (технології, рівні, ефекти, черга досліджень)
dotnet ef migrations add <Назва> --project src/Aetheris.Research --startup-project src/Aetheris.Host --context ResearchDbContext --output-dir Data/Migrations
```

Сервер застосовує міграції ядра і модулів World, Planets, Requirements та Research сам при старті. Усі набори пишуть
історію в спільну таблицю `__EFMigrationsHistory`.

### Модуль Planets (задача 00011)

`server/src/Aetheris.Planets` володіє будівлями планет, спільним механізмом вимог, чергами
будівництва й балансом ресурсів планети. Світ (планети, родовища, ресурси) він читає лише через
`IWorldModule`; залежність одностороння Planets → World. Типи будівель, рівні (вартість, час,
ефекти) і вимоги лежать у таблицях `building_type`, `building_level*`, `requirement`; стартові
значення в `Data/planets-reference-seed.json` записуються лише в порожню базу. Параметри
`planets.*` (максимальний рівень, місця черги, кеш таблиць, стартова будівля) у `config_parameter`.
Читання планети: `GET /api/v1/planets/{planetId}`; команди: `POST /api/v1/planets/construction/enqueue`
і `/cancel`. Видобуток рахується ліниво за ігровим часом, завершення будівлі робить планувальник
подій (`planets.construction_complete`).

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

Ядро не містить правил гри, тому від нього в базу при старті додаються лише нейтральні
тестові параметри `test.*`, параметри механізмів безпеки `security.*` (див. «Безпека») і
планувальника подій `scheduler.*`. Модуль World додає свої `world.*` (див. «Світ»). Значення, задані вручну, ніколи не
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

## Світ: як працює генерація і як змінити параметри

Модуль `server/src/Aetheris.World` (задача 00004) володіє таблицями світу і правилами його
існування. Інші модулі звертаються до світу лише через `IWorldModule`.

### Будова

Галактика → сектори → зоряні системи → об'єкти. Усі координати мають три осі `(x, y, z)`,
зараз `z = 0` (параметр `world.sector.depth`), відстані тривимірні (`WorldGeometry.Distance`).
Координати системи локальні в секторі, сектор має свій початок у галактиці; відстань між
системами різних секторів рахується в координатах галактики. Сектори лежать на квадратній
спіралі: новий сектор завжди отримує наступний індекс, тобто стає на околицю.

| Таблиця | Що зберігає |
|---|---|
| `galaxy`, `sector`, `star_system` | Галактика (зерно), сектори (зерно, індекс на спіралі, початок, ознака новачків), системи (номер, координати, `in_nebula`, кількість новачків) |
| `celestial_object` | Будь-який об'єкт на мапі: зірка, тіло на орбіті, поле, туманність (тип, орбіта, координати) |
| `planet` + `deposit` | Планета або газовий гігант (розмір, власник `owner_player_id`) і родовища: рядок на ресурс із коефіцієнтом |
| `asteroid_field` + `field_composition` | Поле: загальна маса, залишок, `depleted_at`; склад — частки ресурсів, решта пуста порода |
| `nebula` + `nebula_composition` | Туманність: радіус, заборона стрибкового двигуна, склад |
| `start_planet_allocation` | Стартова планета гравця (ключ на гравці: видається один раз) |
| `world_generation_log` | Журнал генерації: дія, зерно, хто, деталі |

Довідники (дані, не код): `resource_group`, `resource_type`, `celestial_object_type`,
`planet_size_class`, `generation_rule_group`, `generation_rule`. Стартові рядки лежать у
`server/src/Aetheris.World/Data/world-reference-seed.json` і записуються лише в порожню базу;
змінені потім рядки сервер не перезаписує.

### Генератор

`SectorGenerator.Generate(seed, name, config)` — чиста функція: те саме зерно й та сама
конфігурація дають той самий сектор. Випадковість — власний xoshiro256** (не залежить від
версії .NET). Зерно сектора виводиться із зерна галактики `world.galaxy.seed` та індексу
сектора або задається адміністратором. Результат записується однією транзакцією, з
рядком у `world_generation_log`.

Що робить генератор:

1. Розставляє `systems_min..systems_max` систем у квадраті `world.sector.size` з мінімальною
   відстанню `min_system_distance`.
2. Ставить `nebula_count` туманностей; системи всередині радіуса позначаються `in_nebula`.
3. У кожній системі: зірка (тип за вагами), `bodies_min..bodies_max` тіл на внутрішніх орбітах
   з `orbit_count` (зовнішня лишається для поля), газові гіганти з імовірністю
   `gas_giant_probability` (не більше `gas_giant_max`), поле на зовнішній орбіті з
   імовірністю `system.field_probability`.
4. Вільні поля в секторі: `free_fields_min..free_fields_max`, але так, щоб усього полів було
   не менше `world.sector.min_fields`.
5. Родовища й склад — за групами правил типу об'єкта: група бере `min_picks` ресурсів
   завжди і кожен наступний до `max_picks` з імовірністю `extra_pick_probability`
   (так «один вид кристалів, рідко два»). Ресурс групи з `allowed_on_planet = false`
   (етер) на тіла не потрапляє, а таке правило не проходить перевірку.

Перед генерацією `WorldConfigValidator` перевіряє узгодженість: діапазони, імовірності,
наявність типів із додатною вагою, правила груп. Неузгоджена конфігурація дає помилку
`world_config_invalid` і нічого не генерує.

### Правила існування

- **Стартова планета** — команда `POST /api/v1/world/start-planet` (конвеєр команд). Вільна
  планета типу `world.start.planet_type_code` у секторі новачків (коли там немає місця — у
  будь-якому), у системі, де новачків менше за `world.start.max_newbies_per_system`; спершу
  системи, де новачки вже є. Атомарно: блокування системи й планети, одна планета ніколи не
  дістається двом гравцям, повторний виклик повертає ту саму.
- **Поля вичерпуються.** `IWorldModule.ExtractFromFieldAsync` (для майбутніх флотів): маса
  розподіляється за складом, решта — порода; поле втрачає всю перероблену масу; повний трюм
  бере лише те, що влізло; «хто встиг, той забрав». Порожнє поле зникає (`depleted_at`), і в
  планувальник ставиться подія `world.field_respawn` через `world.field.respawn_delay_seconds`
  ігрового часу. Подія додає поля, доки в секторі знову не буде `world.sector.min_fields`
  (щонайменше одне).
- **Видимість.** `IWorldVisibility` — місце підключення радара. Поки радара немає, гравець
  бачить лише свої планети; `world.visibility.reveal_all = true` відкриває всю мапу (режим
  розробки).
- **Переліт у системі** має фіксований час `world.travel.in_system_seconds`.

### Перший запуск

На порожній базі сервер записує довідники, створює галактику і
`world.galaxy.initial_sector_count` секторів (перші `newbie_sector_count` — для новачків).
Повторний запуск нічого не дублює. Вимкнути це при старті можна налаштуванням
`World:GenerateOnStartup=false` (так робить тестовий хост).

### Адмінка

Усі дії — у групі `/api/admin` (сесія з TOTP, ліміт, запис у `admin_audit_log`) і в
`world_generation_log`:

| Запит | Роль | Що робить |
|---|---|---|
| `POST /api/admin/world/sectors` `{ "seed": 123, "is_newbie": false }` | `owner` | Новий сектор на околиці (зерно необов'язкове) |
| `POST /api/admin/world/sectors/{id}/regenerate` `{ "seed": 5 }` | `owner` | Перегенерувати сектор без власників |
| `GET /api/admin/world/sectors/{id}/stats` | будь-яка | Кількість об'єктів за типами, вільні й зайняті планети, поля та їхня маса |

Генерація з панелі (задача 00007): `GET /api/admin/world/generation/estimate?sector_count=N`
(підсумок для підтвердження), `POST /api/admin/world/generation-runs`
`{ "sector_count": 2, "seed": 42, "rule_set": "current" }` (202, виконується планувальником у фоні;
друга одночасна — 409), `GET /api/admin/world/generation-runs[/{id}]` (статус, підсумок, історія).
Ліміти — `world.admin.max_sectors_per_run` і `world.admin.max_systems_per_run` (очікувана кількість
систем = сектори × середнє `systems_min`/`systems_max`). Таблиці: `GET /api/admin/world/sectors`,
`/systems`, `/objects`, `/fields` з `page`, `page_size`, `search` (число — пошук за ID),
`sort`, `direction` і фільтрами (`sector_id`, `system_id`, `object_class`, `owned`, `free`, `depleted`...).

### Як змінити параметри

Числа — у `config_parameter` під ключами `world.*` (межі в
`server/src/Aetheris.World/Configuration/WorldConfigKeys.cs`), змінюються через
`PUT /api/admin/config/{key}` `{ "value": "80", "reason": "..." }` (сторінка «Параметри» в `/admin`, з історією й відкатом) або SQL і діють на наступну генерацію без перезбірки:

```sql
UPDATE config_parameter SET value = '80', version = version + 1, updated_at = now()
WHERE key = 'world.sector.systems_max';
```

Типи, ресурси й правила — рядки довідників. Новий ресурс або вид кристалів:

```sql
INSERT INTO resource_type (code, resource_group_id, name_key)
SELECT 'adamantite', id, 'world.resource.adamantite' FROM resource_group WHERE code = 'extractable';

-- Правило: адамантит у металах вулканічних планет
INSERT INTO generation_rule (rule_group_id, resource_id, weight, coefficient_min, coefficient_max)
SELECT g.id, r.id, 10, 0.5, 1.0
FROM generation_rule_group g
JOIN celestial_object_type t ON t.id = g.object_type_id AND t.code = 'volcanic'
JOIN resource_type r ON r.code = 'adamantite'
WHERE g.code = 'metals';
```

Наступний згенерований сектор отримає новий ресурс у родовищах (тест
`A_new_resource_added_to_the_tables_reaches_the_stored_world`).

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
- `documentation/tasks/00004-heneratsiia-vsesvitu.md` — генерація всесвіту
