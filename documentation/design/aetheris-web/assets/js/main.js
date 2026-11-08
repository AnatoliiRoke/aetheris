/* AETHERIS — landing page behaviour: mobile menu, screenshot gallery, FAQ accordion. */
(function () {
  'use strict';

  var t = function (k) { return window.AE_I18N ? window.AE_I18N.t(k) : k; };
  var DESKTOP_MQ = window.matchMedia('(min-width: 1024px)');

  /* ---------- Mobile menu ---------- */
  function initMenu() {
    var toggle = document.querySelector('.menu-toggle');
    var menu = document.getElementById('mobile-menu');
    if (!toggle || !menu) return;

    function setOpen(open) {
      toggle.setAttribute('aria-expanded', String(open));
      toggle.setAttribute('data-i18n-aria', open ? 'menuClose' : 'menuOpen');
      toggle.setAttribute('aria-label', t(open ? 'menuClose' : 'menuOpen'));
      menu.hidden = !open;
    }

    toggle.addEventListener('click', function () {
      setOpen(toggle.getAttribute('aria-expanded') !== 'true');
    });
    menu.addEventListener('click', function (e) {
      if (e.target.closest('a')) setOpen(false);
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && toggle.getAttribute('aria-expanded') === 'true') {
        setOpen(false);
        toggle.focus();
      }
    });
    var onMq = function (e) { if (e.matches) setOpen(false); };
    if (DESKTOP_MQ.addEventListener) DESKTOP_MQ.addEventListener('change', onMq);
    else DESKTOP_MQ.addListener(onMq);
  }

  /* ---------- Screenshot gallery ---------- */
  function initGallery() {
    var root = document.querySelector('[data-gallery]');
    if (!root) return;
    var main = root.querySelector('[data-gallery-main]');
    var caption = root.querySelector('[data-gallery-caption]');
    var thumbs = Array.prototype.slice.call(root.querySelectorAll('.thumb'));

    function select(i) {
      var btn = thumbs[i];
      if (!btn) return;
      thumbs.forEach(function (b) { b.setAttribute('aria-pressed', String(b === btn)); });
      var title = btn.getAttribute('data-title');
      main.src = btn.getAttribute('data-src');
      main.alt = 'Скриншот гри: ' + title;
      caption.textContent = 'Кадр ' + (i + 1) + ' з ' + thumbs.length + ' — ' + title;
    }

    thumbs.forEach(function (btn, i) {
      btn.addEventListener('click', function () { select(i); });
      btn.addEventListener('keydown', function (e) {
        if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
        e.preventDefault();
        var next = (i + (e.key === 'ArrowRight' ? 1 : -1) + thumbs.length) % thumbs.length;
        thumbs[next].focus();
        select(next);
      });
    });
  }

  /* ---------- FAQ accordion (one open at a time) ---------- */
  function initAccordion() {
    document.querySelectorAll('[data-accordion]').forEach(function (list) {
      var buttons = Array.prototype.slice.call(list.querySelectorAll('.faq__btn'));
      buttons.forEach(function (btn) {
        btn.addEventListener('click', function () {
          var willOpen = btn.getAttribute('aria-expanded') !== 'true';
          buttons.forEach(function (b) {
            var open = b === btn ? willOpen : false;
            b.setAttribute('aria-expanded', String(open));
            var panel = document.getElementById(b.getAttribute('aria-controls'));
            if (panel) panel.hidden = !open;
          });
        });
      });
    });
  }

  function init() {
    initMenu();
    initGallery();
    initAccordion();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
