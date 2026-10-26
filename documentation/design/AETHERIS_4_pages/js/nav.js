/* Навігація між чотирма сторінками (статичні знімки екранів). */
(function () {
  var mob = /-mobile\.html$/.test(location.pathname);
  var sfx = mob ? '-mobile' : '';
  var map = { planet: 'planet', storage: 'storage', research: 'research', production: 'build',
              'Планета': 'planet', 'Склад': 'storage', 'Дослідження': 'research', 'Виробництво': 'build',
              'Planet': 'planet', 'Storage': 'storage', 'Research': 'research', 'Production': 'build' };
  document.querySelectorAll('a[href^="#"]').forEach(function (a) {
    var id = a.getAttribute('href').slice(1);
    var key = map[id] || map[(a.textContent || '').trim().replace(/\d+$/, '').trim()];
    if (key) a.setAttribute('href', key + sfx + '.html');
  });
})();
