/* AETHERIS — auth page: view routing, validation, password toggle, resend timer.
   Views by hash: #signin (default) · #signup · #check
   Demo states:   auth.html?state=error#signin   — prefilled failed sign-in
                  auth.html?email=a@b.com#check  — "check your email" for that address
   Backend hooks: replace the bodies of api.signIn / api.signUp / api.resend with real requests.
*/
(function () {
  'use strict';

  var t = function (k) { return window.AE_I18N ? window.AE_I18N.t(k) : k; };
  var EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  var RESEND_SECONDS = 60;

  var MSG = {
    emailInvalid: 'Перевір адресу. Приклад: name@mail.com',
    pwEmpty: 'Введи пароль.',
    pwWeak: 'Пароль закороткий: потрібно 8+ символів і цифра.',
    pwWrong: 'Невірний пароль. Спробуй ще раз або віднови його.',
    nameInvalid: 'Ім’я має містити від 3 до 16 символів.',
    termsRequired: 'Щоб продовжити, прийми умови.',
    resent: 'Лист надіслано ще раз.'
  };

  /* ---- Backend stubs (replace with fetch calls) ---- */
  var api = {
    signIn: function (email, password) {
      // Demo: password "wrongpass" or ?state=error simulates bad credentials.
      return new Promise(function (resolve) {
        setTimeout(function () { resolve({ ok: password !== 'wrongpass' }); }, 400);
      });
    },
    signUp: function () {
      return new Promise(function (resolve) { setTimeout(function () { resolve({ ok: true }); }, 400); });
    },
    resend: function () {
      return new Promise(function (resolve) { setTimeout(function () { resolve({ ok: true }); }, 300); });
    }
  };

  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var params = new URLSearchParams(location.search);
  var lastEmail = params.get('email') || '';

  /* ---------- Field helpers ---------- */
  function setError(input, msgEl, text) {
    input.setAttribute('aria-invalid', text ? 'true' : 'false');
    if (!msgEl) return;
    if (msgEl.classList.contains('hint')) {
      msgEl.textContent = text || msgEl.getAttribute('data-default');
      msgEl.classList.toggle('hint--error', !!text);
      if (text) msgEl.classList.remove('hint--ok');
    } else {
      msgEl.textContent = text || '';
      msgEl.hidden = !text;
    }
  }
  function setOk(input, hintEl, ok) {
    if (!hintEl || input.getAttribute('aria-invalid') === 'true') return;
    hintEl.classList.toggle('hint--ok', ok);
  }
  function setBusy(form, busy) {
    var btn = form.querySelector('[type="submit"]');
    btn.disabled = busy;
    btn.setAttribute('aria-busy', String(busy));
  }

  /* ---------- Views ---------- */
  var views = Array.prototype.slice.call(document.querySelectorAll('[data-view]'));
  var TITLES = { signin: 'Вхід — AETHERIS', signup: 'Реєстрація — AETHERIS', check: 'Перевір пошту — AETHERIS' };

  function show(name, focus) {
    if (!TITLES[name]) name = 'signin';
    views.forEach(function (v) { v.hidden = v.getAttribute('data-view') !== name; });
    document.title = TITLES[name];
    if (name === 'check') startCheck();
    if (focus) {
      var h = $('[data-view="' + name + '"] h1');
      if (h) { h.setAttribute('tabindex', '-1'); h.focus({ preventScroll: true }); }
    }
  }
  window.addEventListener('hashchange', function () { show(location.hash.slice(1), true); });

  /* ---------- Password toggles ---------- */
  function initPwToggles() {
    document.querySelectorAll('[data-pw-toggle]').forEach(function (btn) {
      var input = document.getElementById(btn.getAttribute('data-pw-toggle'));
      btn.addEventListener('click', function () {
        var showPw = input.type === 'password';
        input.type = showPw ? 'text' : 'password';
        btn.setAttribute('aria-pressed', String(showPw));
        btn.setAttribute('data-i18n', showPw ? 'hide' : 'show');
        btn.textContent = t(showPw ? 'hide' : 'show');
      });
    });
  }

  /* ---------- Sign in ---------- */
  function initSignIn() {
    var form = $('#signin-form');
    var email = $('#si-email'), pw = $('#si-pw');
    var emailErr = $('#si-email-err'), pwErr = $('#si-pw-err');
    var alertErr = $('[data-signin-error]'), alertOk = $('[data-signin-ok]');

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      alertErr.hidden = true; alertOk.hidden = true;
      var bad = null;
      if (!EMAIL_RE.test(email.value.trim())) { setError(email, emailErr, MSG.emailInvalid); bad = bad || email; } else setError(email, emailErr, '');
      if (!pw.value) { setError(pw, pwErr, MSG.pwEmpty); bad = bad || pw; } else setError(pw, pwErr, '');
      if (bad) { bad.focus(); return; }

      setBusy(form, true);
      api.signIn(email.value.trim(), pw.value).then(function (res) {
        setBusy(form, false);
        if (res.ok) { alertOk.hidden = false; return; }
        alertErr.hidden = false;
        setError(pw, pwErr, MSG.pwWrong);
        pw.focus();
      });
    });

    [email, pw].forEach(function (inp) {
      inp.addEventListener('input', function () {
        if (inp.getAttribute('aria-invalid') === 'true') setError(inp, inp === email ? emailErr : pwErr, '');
      });
    });

    if (params.get('state') === 'error') {
      email.value = 'kapitan.zoria@example.com';
      pw.value = 'wrongpass';
      alertErr.hidden = false;
      setError(pw, pwErr, MSG.pwWrong);
    }
  }

  /* ---------- Sign up ---------- */
  function initSignUp() {
    var form = $('#signup-form');
    var email = $('#su-email'), pw = $('#su-pw'), name = $('#su-name'), terms = $('#su-terms');
    var emailErr = $('#su-email-err'), pwHint = $('#su-pw-hint'), nameHint = $('#su-name-hint'), termsErr = $('#su-terms-err');

    var pwValid = function () { return pw.value.length >= 8 && /\d/.test(pw.value); };
    var nameValid = function () { var n = name.value.trim(); return n.length >= 3 && n.length <= 16; };

    pw.addEventListener('input', function () { if (pwValid()) setError(pw, pwHint, ''); setOk(pw, pwHint, pwValid()); });
    name.addEventListener('input', function () { if (nameValid()) setError(name, nameHint, ''); setOk(name, nameHint, nameValid()); });
    email.addEventListener('input', function () { if (email.getAttribute('aria-invalid') === 'true') setError(email, emailErr, ''); });
    terms.addEventListener('change', function () { if (terms.checked) setError(terms, termsErr, ''); });

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      var bad = null;
      if (!EMAIL_RE.test(email.value.trim())) { setError(email, emailErr, MSG.emailInvalid); bad = bad || email; } else setError(email, emailErr, '');
      if (!pwValid()) { setError(pw, pwHint, MSG.pwWeak); bad = bad || pw; } else { setError(pw, pwHint, ''); setOk(pw, pwHint, true); }
      if (!nameValid()) { setError(name, nameHint, MSG.nameInvalid); bad = bad || name; } else { setError(name, nameHint, ''); setOk(name, nameHint, true); }
      if (!terms.checked) { setError(terms, termsErr, MSG.termsRequired); bad = bad || terms; } else setError(terms, termsErr, '');
      if (bad) { bad.focus(); return; }

      setBusy(form, true);
      api.signUp({ email: email.value.trim(), password: pw.value, displayName: name.value.trim() }).then(function (res) {
        setBusy(form, false);
        if (!res.ok) return;
        lastEmail = email.value.trim();
        location.hash = '#check';
      });
    });
  }

  /* ---------- Check email + resend timer ---------- */
  var timerId = null;
  function startCheck() {
    var out = $('[data-email-out]');
    out.textContent = lastEmail || 'твою пошту';
    runTimer(RESEND_SECONDS);
  }
  function runTimer(seconds) {
    var btn = $('[data-resend]'), label = $('[data-resend-timer]');
    var left = seconds;
    clearInterval(timerId);
    var tick = function () {
      if (left <= 0) {
        clearInterval(timerId);
        btn.disabled = false;
        label.textContent = '';
        return;
      }
      btn.disabled = true;
      label.textContent = ' · ' + Math.floor(left / 60) + ':' + String(left % 60).padStart(2, '0');
      left -= 1;
    };
    tick();
    timerId = setInterval(tick, 1000);
  }
  function initResend() {
    var btn = $('[data-resend]'), status = $('[data-resend-status]');
    btn.addEventListener('click', function () {
      btn.disabled = true;
      api.resend(lastEmail).then(function () {
        status.textContent = '✓ ' + MSG.resent;
        runTimer(RESEND_SECONDS);
      });
    });
  }

  /* ---------- OAuth stub ---------- */
  function initOAuth() {
    document.querySelectorAll('[data-oauth]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        // TODO: redirect to your OAuth endpoint, e.g. location.href = '/auth/google';
      });
    });
  }

  /* Keep "Show/Hide" labels correct after a language switch */
  document.addEventListener('ae:lang', function () {
    document.querySelectorAll('[data-pw-toggle]').forEach(function (btn) {
      btn.textContent = t(btn.getAttribute('aria-pressed') === 'true' ? 'hide' : 'show');
    });
  });

  function init() {
    initPwToggles();
    initSignIn();
    initSignUp();
    initResend();
    initOAuth();
    show(location.hash.slice(1) || 'signin', false);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
