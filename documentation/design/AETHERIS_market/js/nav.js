/* Навігація між статичними знімками: вкладки ринку та бічне меню. */
(function () {
  var mob = /-mobile\.html$/.test(location.pathname);
  var sfx = mob ? '-mobile' : '';
  var tabs = { buy: 'kupyty', sell: 'prodaty', mine: 'moi-loty', del: 'dostavky', ex: 'obmin', hist: 'istoriia' };
  document.querySelectorAll('[role="tab"]').forEach(function (b) {
    var id = (b.id || '').replace('mk-tab-', '');
    if (tabs[id]) b.addEventListener('click', function () { location.href = tabs[id] + sfx + '.html'; });
  });
  document.querySelectorAll('a[href="#market"]').forEach(function (a) { a.setAttribute('href', 'kupyty' + sfx + '.html'); });
})();
