# AETHERIS — публічна сторінка (лендинг + вхід/реєстрація)

Статичний сайт на чистому HTML/CSS/JS: без фреймворків, без збірки.

## Запуск

```bash
cd aetheris-web
python3 -m http.server 8080   # або будь-який статичний сервер
# http://localhost:8080
```

## Структура

```
index.html              Лендинг: хедер, hero, Про гру, Раси, Скриншоти, Рейтинг, Новини, FAQ, футер
auth.html               Вхід / реєстрація / «Перевір пошту» (перемикання через #hash)
assets/css/styles.css   Токени (:root) → база → компоненти → секції → auth
assets/js/i18n.js       UA/EN для меню та кнопок (data-i18n, data-i18n-aria, data-lang)
assets/js/main.js       Мобільне меню, галерея скриншотів, акордеон FAQ
assets/js/auth.js       Роутинг екранів, валідація, показ пароля, таймер повторного листа
assets/img/             Арти у WebP (1.3 МБ загалом), favicon, apple-touch-icon
```

## Екрани auth

| URL | Стан |
|---|---|
| `auth.html#signin` | Вхід |
| `auth.html#signup` | Реєстрація |
| `auth.html?state=error#signin` | Вхід зі станом помилки (демо) |
| `auth.html?email=name@mail.com#check` | «Перевір пошту» |

Бекенд: замініть заглушки `api.signIn`, `api.signUp`, `api.resend` у `auth.js` на свої запити. «Продовжити з Google» — у `initOAuth()`.
Демо: пароль `wrongpass` імітує невдалий вхід.

## Адаптивність і доступність

- Desktop 1440–1920: контейнер 1280 px. Гамбургер-меню вмикається нижче 1024 px. Mobile 360–430: без горизонтального скролу.
- Tap-target ≥ 44 px. Є skip-link, `:focus-visible` і `prefers-reduced-motion`.
- Помилки форм позначено рамкою, значком ⚠ і текстом; поля мають `aria-invalid` і `aria-describedby`.
- Раса завжди показана назвою та гліфом власної форми, не лише кольором.
- Мова UA/EN зберігається в `localStorage` (з try/catch).

## Арти

| Файл | ID |
|---|---|
| hero-bg.webp (дзеркально через CSS), stars.webp (blend: screen) | ART-01, ART-02 |
| about-*.webp | ART-03…06 |
| ship-*.webp | ART-07…10 |
| screen-*.webp | ART-11 |
| planet-*.webp (96 px) | ART-12 |
| news-*.webp (+ about-constructor для новини 0.9) | ART-13 |
| races-bg.webp | ART-14 |
| auth-side.webp | ART-15 |
| logo-mark.webp, favicon-32.png, apple-touch-icon.png | ART-16 |

Вихідні арти мають розмір 1200×896. Для hero і фону секції «Раси» на великих екранах краще 1920×1080 і 1920×800.

## TODO перед релізом

- Замінити `[STUDIO NAME]`, `support@example.com`, посилання `#terms`, `#privacy`, `#community`, `#ranking-full` та новини на реальні.
- Підключити рейтинг і новини до API (зараз це статична розмітка-заглушка).
- За бажанням — самохостинг шрифту Inter замість Google Fonts.
- Виправити «зламаний» текст на скриншотах конструктора й ринку.
