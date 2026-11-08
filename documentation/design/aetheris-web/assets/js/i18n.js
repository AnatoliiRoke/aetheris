/* AETHERIS — i18n for menu & buttons (UA / EN).
   Text nodes:  data-i18n="key"        → textContent
   Aria labels: data-i18n-aria="key"   → aria-label
   Switchers:   button[data-lang="uk|en"] (aria-pressed)
*/
(function () {
  'use strict';

  var DICT = {
    uk: {
      'skip': 'Перейти до змісту',
      'mainNav': 'Головне меню',
      'menuOpen': 'Відкрити меню',
      'menuClose': 'Закрити меню',
      'nav.about': 'Про гру',
      'nav.races': 'Раси',
      'nav.ranking': 'Рейтинг',
      'nav.news': 'Новини',
      'nav.help': 'Допомога',
      'signin': 'Увійти',
      'play': 'Грати',
      'playFree': 'Грати безкоштовно',
      'fullRanking': 'Повний рейтинг',
      'readMore': 'Читати далі',
      'home': 'На головну',
      'create': 'Створити акаунт',
      'google': 'Продовжити з Google',
      'forgot': 'Забули пароль?',
      'toSignup': 'Зареєструватися',
      'back': 'Повернутися до входу',
      'resend': 'Надіслати лист ще раз',
      'show': 'Показати',
      'hide': 'Сховати'
    },
    en: {
      'skip': 'Skip to content',
      'mainNav': 'Main menu',
      'menuOpen': 'Open menu',
      'menuClose': 'Close menu',
      'nav.about': 'About',
      'nav.races': 'Races',
      'nav.ranking': 'Ranking',
      'nav.news': 'News',
      'nav.help': 'Help',
      'signin': 'Sign in',
      'play': 'Play',
      'playFree': 'Play for free',
      'fullRanking': 'Full ranking',
      'readMore': 'Read more',
      'home': 'Home',
      'create': 'Create account',
      'google': 'Continue with Google',
      'forgot': 'Forgot password?',
      'toSignup': 'Sign up',
      'back': 'Back to sign in',
      'resend': 'Resend email',
      'show': 'Show',
      'hide': 'Hide'
    }
  };

  var STORAGE_KEY = 'aetheris.lang';
  var current = 'uk';

  function read() {
    try { return localStorage.getItem(STORAGE_KEY); } catch (e) { return null; }
  }
  function write(v) {
    try { localStorage.setItem(STORAGE_KEY, v); } catch (e) { /* storage unavailable — ignore */ }
  }

  function t(key) {
    var d = DICT[current] || DICT.uk;
    return Object.prototype.hasOwnProperty.call(d, key) ? d[key] : (DICT.uk[key] || key);
  }

  function apply(lang) {
    current = DICT[lang] ? lang : 'uk';
    // Page copy stays Ukrainian; only menu & buttons switch, so <html lang> stays "uk".
    document.querySelectorAll('[data-i18n]').forEach(function (el) {
      el.textContent = t(el.getAttribute('data-i18n'));
    });
    document.querySelectorAll('[data-i18n-aria]').forEach(function (el) {
      el.setAttribute('aria-label', t(el.getAttribute('data-i18n-aria')));
    });
    document.querySelectorAll('[data-lang]').forEach(function (btn) {
      btn.setAttribute('aria-pressed', String(btn.getAttribute('data-lang') === current));
    });
    document.dispatchEvent(new CustomEvent('ae:lang', { detail: { lang: current } }));
  }

  function init() {
    document.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-lang]');
      if (!btn) return;
      var lang = btn.getAttribute('data-lang');
      write(lang);
      apply(lang);
    });
    var saved = read();
    apply(saved && DICT[saved] ? saved : 'uk');

    document.querySelectorAll('[data-year]').forEach(function (el) {
      el.textContent = String(new Date().getFullYear());
    });
  }

  window.AE_I18N = { t: t, apply: apply, get lang() { return current; } };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
