/* AETHERIS — екран керування цивілізацією. Без залежностей і збірки: працює з file:// і з будь-якого статичного хостингу.
   Параметри URL: ?lang=en  ?premium=0 (2 місця в черзі) */
(function () {
  var A = window.AE, svg = A.svg, line = A.line;
  var qs = new URLSearchParams(location.search);
  var state = {
    lang: qs.get('lang') === 'en' ? 'en' : 'uk',
    premium: qs.get('premium') !== '0',
    sel: 0, collected: {}, chat: '', t: 0
  };
  var root = document.getElementById('app');
  var nf = function (n) { return n.toLocaleString('uk-UA'); };
  var mmss = function (x) { return Math.floor(x / 60) + ':' + String(x % 60).padStart(2, '0'); };
  var hms = function (x) { x = Math.max(0, x); return Math.floor(x / 3600) + ':' + String(Math.floor(x / 60) % 60).padStart(2, '0') + ':' + String(x % 60).padStart(2, '0'); };
  var T = function () { return A.L[state.lang]; };

  /* ---------- шматки розмітки ---------- */
  function topRes() {
    return A.TOP.map(function (r) {
      var n = A.RES[r[0]][0];
      return '<li title="' + n + '">' + A.res(r[0], 18) + '<span class="sr">' + n + ':</span>' + r[1] + '</li>';
    }).join('');
  }
  function alerts() {
    var t = T();
    return '<button type="button" class="pill pill--danger">' + line(A.ALERT.attack, 16, 2) + '<span data-attack>' + t.attack + ' · ' + mmss(Math.max(0, A.ATTACK_SECONDS - state.t)) + '</span></button>' +
      '<button type="button" class="pill pill--danger">' + line(A.ALERT.battle, 16, 2) + t.battle + '</button>' +
      '<button type="button" class="pill pill--warning">' + line(A.ALERT.storage, 16, 2) + t.storageFull + '</button>';
  }
  function mailBtn() {
    var t = T();
    return '<button type="button" class="ibtn" aria-label="' + t.mail + ': ' + A.PLAYER.mail + ' ' + t.unread + '">' + line(A.NAV.mail, 20) + '<span class="badge" aria-hidden="true">' + A.PLAYER.mail + '</span></button>';
  }
  function premChip() {
    var t = T(), p = state.premium;
    return '<button type="button" class="chip-prem ' + (p ? 'is-premium' : 'is-standard') + '" data-act="premium" aria-pressed="' + p + '" title="Demo: ' + t.premium + ' / ' + t.standard + '">' +
      svg(A.NAV.star, 12, { fill: 'currentColor' }) + (p ? t.premium : t.standard) + '</button>';
  }
  function header() {
    var t = T();
    return '<header class="topbar only-d">' +
      '<div class="topbar__id"><span class="brand"><img src="assets/img/logo-mark.webp" alt="" width="28" height="28"><b>AETHERIS</b></span>' +
      '<span class="nick"><span class="glyph-terran" aria-hidden="true"></span>' + A.PLAYER.nick + '<span class="sr">, ' + t.race + '</span></span>' + premChip() + '</div>' +
      '<ul class="res" aria-label="' + t.resources + '">' + topRes() + '</ul>' +
      '<div class="topbar__tools">' + alerts() + '<span class="sep-v" aria-hidden="true"></span>' + mailBtn() +
      '<button type="button" class="ibtn" aria-label="' + t.settings + '">' + line(A.NAV.settings, 20) + '</button>' +
      '<button type="button" class="ibtn" aria-label="' + t.signout + '">' + line(A.NAV.signout, 20) + '</button></div></header>' +
      '<header class="mtop only-m">' +
      '<div class="mtop__r1"><span class="brand"><img src="assets/img/logo-mark.webp" alt="" width="28" height="28"><span style="display:flex;flex-direction:column;line-height:1.2"><b>AETHERIS</b><span class="mtop__nick"><span class="glyph-terran" aria-hidden="true"></span>' + A.PLAYER.nick + '</span></span></span>' + premChip() + mailBtn() + '</div>' +
      '<ul class="res" aria-label="' + t.resources + '">' + topRes() + '</ul><div class="mtop__al">' + alerts() + '</div></header>';
  }
  function sidebar() {
    var t = T();
    var order = [['overview'], ['planet'], ['production'], ['shipyard'], ['fleets'], ['map'], ['research'], ['market'], ['ranking'], ['battle', 'off'], ['clan', 'off'], ['mail', 'sep'], ['help'], ['settings']];
    var items = order.map(function (x) {
      var id = x[0], off = x[1] === 'off', cur = id === 'overview';
      var hr = x[1] === 'sep' ? '<span class="nav-hr" aria-hidden="true"></span>' : '';
      if (off) return '<span class="nav-i is-off" aria-disabled="true">' + line(A.NAV[id], 20) + '<span class="lbl">' + t.nav[id] + '</span><span class="tag-st">' + t.stage2 + '</span></span>';
      return hr + '<a href="#' + id + '" class="nav-i"' + (cur ? ' aria-current="page"' : '') + '>' + line(A.NAV[id], 20) + '<span class="lbl">' + t.nav[id] + '</span>' + (id === 'mail' ? '<span class="cnt">' + A.PLAYER.mail + '</span>' : '') + '</a>';
    }).join('');
    var lang = ['uk', 'en'].map(function (l) { return '<button type="button" data-act="lang" data-v="' + l + '" aria-pressed="' + (state.lang === l) + '">' + (l === 'uk' ? 'UA' : 'EN') + '</button>'; }).join('');
    return '<nav class="side only-d" aria-label="' + t.gameMenu + '">' + items + '<div class="lang"><small>' + t.lang + '</small><div role="group" aria-label="Мова / Language">' + lang + '</div></div></nav>';
  }
  function thumbs(cls) {
    var t = T();
    return '<div role="group" aria-label="' + t.switcher + '" class="switch ' + (cls || '') + '">' + A.PLANETS.map(function (p, i) {
      return '<button type="button" class="thumb" data-act="pick" data-i="' + i + '" aria-pressed="' + (i === state.sel) + '" aria-label="' + p.name + '" title="' + p.name + '"><img src="' + A.TYPES[p.type].img + '" alt=""></button>';
    }).join('') + '</div>';
  }

  function bar(label, icon, textHtml, pct, fill) {
    return '<div class="bar-row"><div class="bar-row__h"><span>' + icon + label + '</span>' + textHtml + '</div>' +
      '<div class="track" role="progressbar" aria-label="' + label + '" aria-valuenow="' + pct + '" aria-valuemin="0" aria-valuemax="100"><i class="' + fill + '" style="width:' + pct + '%"></i></div></div>';
  }

  function buildItem(b, cap) {
    var t = T();
    var queued = !!b.queued, done = !!b.done, running = !queued && !done;
    var rem = running ? Math.max(0, b.rem - state.t) : (queued ? b.queued : 0);
    var pct = running ? Math.min(100, Math.round((1 - rem / b.total) * 100)) : (done ? 100 : 0);
    var ic = '<span class="tile ' + (done ? 'tile--ok' : queued ? 'tile--q' : '') + '">' + line(A.BUILD[b.i], 15, 1.9) + '</span>';
    var right = running ? '<span class="qi__timer" data-timer data-rem="' + b.rem + '" data-total="' + b.total + '">' + hms(rem) + '</span>' :
      queued ? '<span class="qi__timer is-queued">' + t.queued + ' · ' + hms(rem) + '</span>' :
      '<span class="qi__ok"><span aria-hidden="true">✓</span>' + t.ready + '</span>';
    return '<div class="qi ' + (done ? 'qi--done' : queued ? 'qi--queued' : '') + '"><div class="qi__row">' + ic + '<span class="qi__name">' + b.name + '</span>' + right + '</div>' +
      (running ? '<div class="track" role="progressbar" aria-label="' + b.name + '" aria-valuenow="' + pct + '" aria-valuemin="0" aria-valuemax="100"><i class="f-accent" data-bar style="width:' + pct + '%"></i></div>' : '') +
      (done ? '<button type="button" class="btn-collect" data-act="collect" data-key="' + b.key + '">' + t.collect + '</button>' : '') + '</div>';
  }
  function researchItem(r) {
    var t = T(), queued = !!r.queued;
    var rem = queued ? r.queued : Math.max(0, r.rem - state.t);
    var pct = queued ? 0 : Math.round((1 - rem / r.total) * 100);
    var cost = r.cost.map(function (c) { return '<span class="c">' + A.res(c[0], 12) + nf(c[1]) + '<span class="sr"> ' + A.RES[c[0]][0] + '</span></span>'; }).join('');
    return '<div class="qi qi--r ' + (queued ? 'qi--queued' : '') + '"><div class="qi__row"><span class="tile ' + (queued ? 'tile--q' : 'tile--act') + '">' + line(A.BUILD.lab, 15, 1.9) + '</span>' +
      '<span class="qi__name">' + r.name + '</span>' +
      (queued ? '<span class="qi__timer is-queued">' + hms(rem) + '</span>' : '<span class="qi__timer" data-timer data-rem="' + r.rem + '" data-total="' + r.total + '" data-research>' + hms(rem) + '</span>') + '</div>' +
      (queued ? '' : '<div class="track" role="progressbar" aria-label="' + r.name + '" aria-valuenow="' + pct + '" aria-valuemin="0" aria-valuemax="100"><i class="f-research" data-bar style="width:' + pct + '%"></i></div>') +
      '<div class="qi__cost">' + (queued ? '<span class="st">' + t.queued + ' · </span>' : '') + cost + '</div></div>';
  }

  function planetCard(p, i) {
    var t = T(), ty = A.TYPES[p.type], sel = i === state.sel, gas = p.type === 'gas';
    var cap = state.premium ? A.QUEUE_CAP.premium : A.QUEUE_CAP.standard;
    var stPct = Math.min(100, Math.round(p.st[0] / p.st[1] * 100)), full = p.st[0] >= p.st[1];
    var enPct = Math.min(100, Math.round(p.en[0] / p.en[1] * 100)), deficit = p.en[0] > p.en[1];
    var badge = '';
    if (p.badge) {
      var k = p.badge[0], txt = t[p.badge[1]] || p.badge[1];
      badge = '<span role="status" class="pc-badge pc-badge--' + k + '">' + line(k === 'danger' ? A.ALERT.attack : A.ACT.warn, 13, 2.2) + txt + '</span>';
    }
    var deps = p.dep.map(function (d) { return '<li class="dep">' + A.res(d, 12) + A.RES[d][0] + '</li>'; }).join('');
    var yields = p.y.map(function (y) { return '<div class="yield">' + A.res(y[0], 14) + '<span>' + A.RES[y[0]][0] + '</span><span>' + nf(y[1]) + t.perHour + '</span></div>'; }).join('') +
      (p.pop ? '<div class="yield">' + A.res('pop', 14) + '<span>' + t.pop + '</span><span>+' + nf(p.grow) + t.perHour + '</span></div>' : '');
    var bars = '';
    if (p.pop) { var pp = Math.round(p.pop[0] / p.pop[1] * 100); bars += bar(t.pop, A.res('pop', 12), '<b>' + nf(p.pop[0]) + ' / ' + nf(p.pop[1]) + '</b>', pp, 'f-accent'); }
    bars += bar(t.energy, svg(A.RES.energy[3], 12, { fill: A.GROUP.energy }), '<b class="' + (deficit ? 'warn' : '') + '">' + nf(p.en[0]) + ' / ' + nf(p.en[1]) + (deficit ? ' · ' + t.deficit + ' ' + nf(p.en[0] - p.en[1]) : '') + '</b>', enPct, deficit ? 'f-stripe' : 'f-energy');
    bars += bar(gas ? t.fuelTank : t.storage, line(A.BUILD.box, 12, 2), '<b class="' + (full ? 'warn' : '') + '">' + nf(p.st[0]) + ' / ' + nf(p.st[1]) + (full ? ' · ' + t.full : '') + '</b>', stPct, full ? 'f-stripe' : 'f-store');

    var build = p.build.filter(function (b) { return !(b.done && state.collected[b.key]); }).slice(0, cap);
    var research = (p.research || []).slice(0, cap);
    var act = p.act.slice();
    if (state.collected.gorn && i === 1) act.unshift(['done', state.lang === 'en' ? 'Iron mine level 4 is online.' : 'Шахту заліза рів. 4 введено в дію.', t.now]);
    var activity = act.slice(0, 3).map(function (e) { return '<li><span class="tile tile--a-' + e[0] + '">' + svg(A.ACT[e[0]], 12, { stroke: 'currentColor', sw: 2.4 }) + '</span><span class="tx">' + e[1] + '</span><span class="tm">' + e[2] + '</span></li>'; }).join('');

    return '<article class="planet' + (sel ? ' is-sel' : '') + '" aria-label="' + p.name + '">' +
      '<div class="pc-id"><div class="pc-art" style="--glow:' + ty.glow + '"><img src="' + ty.img + '" alt="" width="116" height="116">' + badge + (sel ? '<span class="pc-sel">' + t.selected + '</span>' : '') + '</div>' +
      '<div class="pc-meta"><h3><button type="button" data-act="pick" data-i="' + i + '" aria-pressed="' + sel + '">' + p.name + '</button></h3>' +
      '<span class="chip-type">' + ty.name + '</span>' +
      '<span class="coords">' + t.sector + ' ' + p.co[0] + ' · ' + t.system + ' ' + p.co[1] + '\n' + t.orbit + ' ' + p.co[2] + '</span>' +
      '<div class="deps"><span class="lab">' + t.deposits + '</span><ul>' + deps + '</ul></div></div></div>' +

      '<div class="pc-col pc-info">' +
      (gas ? '<div class="note note--fuel">' + line('M4 21V9h6v12 M10 13h4l2-6h4v14H4 M7 9V5', 16, 1.8).replace('currentColor', '#FF9F43') + '<span><strong>' + t.fuelStation + '</strong> · ' + t.fuelNote + '</span></div>' : '') +
      '<div class="yields"><span class="lab">' + t.yield + '</span>' + yields + '</div><div class="bars">' + bars + '</div>' +
      (p.paused ? '<div role="status" class="note note--warn">' + svg('M9 6v12M15 6v12', 14, { stroke: '#F2C14E', sw: 2.6 }) + '<span><strong style="color:var(--warning)">' + t.paused + '</strong> · ' + p.paused + '</span></div>' : '') + '</div>' +

      '<section class="pc-col" aria-label="' + t.construction + ' — ' + p.name + '"><div class="qh"><h4 class="h4">' + t.construction + '</h4><span class="qcount">' + build.length + ' / ' + cap + '</span></div>' +
      (build.length ? '<div class="qlist civ-scroll" tabindex="0" aria-label="' + t.construction + ': ' + t.queue + '">' + build.map(function (b) { return buildItem(b, cap); }).join('') + '</div>' :
        '<div class="empty-q"><span>' + t.queueEmpty + '</span><button type="button">+ ' + t.addBuild + '</button></div>') +
      (state.premium ? '' : '<span class="upsell">' + t.upsell + '</span>') + '</section>' +

      '<div class="pc-col"><section class="sec" aria-label="' + t.research + ' — ' + p.name + '"><div class="qh"><h4 class="h4">' + t.research + '</h4>' + (research.length ? '<span class="qcount">' + research.length + ' / ' + cap + '</span>' : '') + '</div>' +
      (research.length ? '<div class="qlist civ-scroll" tabindex="0" aria-label="' + t.research + ': ' + t.queue + '">' + research.map(researchItem).join('') + '</div>' : '<span class="muted">' + (gas ? t.noLabGas : t.noLab) + '</span>') + '</section>' +
      '<section class="act" aria-label="' + t.activity + ' — ' + p.name + '"><h4 class="h4">' + t.activity + '</h4><ul class="act">' + activity + '</ul></section></div></article>';
  }

  function footer() {
    var t = T();
    return '<footer class="bottombar only-d"><div class="bottombar__sw">' + thumbs() +
      '<span class="cur"><small>' + t.current + '</small><strong>' + A.PLANETS[state.sel].name + '</strong></span></div>' +
      '<form class="chat" data-form="chat"><label for="chat"><i aria-hidden="true"></i>' + t.chatLabel + '</label>' +
      '<input id="chat" type="text" value="' + state.chat.replace(/"/g, '&quot;') + '" placeholder="' + t.chatPh + '" autocomplete="off">' +
      '<button type="submit">' + line(A.NAV.send, 16, 2) + t.send + '</button></form></footer>';
  }
  function tabbar() {
    var t = T();
    var tabs = [['overview', t.nav.overview], ['planet', t.nav.planet], ['shipyard', t.nav.shipyard], ['map', t.nav.map], ['more', t.more]];
    return '<nav class="tabbar only-m" aria-label="' + t.gameMenu + '">' + tabs.map(function (x, i) { return '<a href="#' + x[0] + '"' + (i === 0 ? ' aria-current="page"' : '') + '>' + line(A.NAV[x[0]], 22) + '<span>' + x[1] + '</span></a>'; }).join('') + '</nav>';
  }

  /* ---------- рендер ---------- */
  function render() {
    var t = T();
    var keep = [].map.call(root.querySelectorAll('.civ-scroll'), function (e) { return e.scrollTop; });
    var winY = window.scrollY, hadChat = document.activeElement && document.activeElement.id === 'chat';
    document.documentElement.lang = state.lang === 'en' ? 'en' : 'uk';
    root.innerHTML = '<div class="app">' + header() +
      '<div class="body">' + sidebar() +
      '<main class="main"><img class="main__bg" src="assets/img/nebula.webp" alt="">' +
      '<div class="main__in"><div><h1>' + t.planets + ' <span>' + A.PLANETS.length + '</span></h1><p class="lead">' + t.lead + '</p></div>' +
      thumbs('only-m') + '<div class="plist">' + A.PLANETS.map(planetCard).join('') + '</div></div></main></div>' +
      footer() + tabbar() + '</div>';
    [].forEach.call(root.querySelectorAll('.civ-scroll'), function (e, i) { if (keep[i]) e.scrollTop = keep[i]; });
    window.scrollTo(0, winY);
    if (hadChat) { var c = document.getElementById('chat'); c.focus(); c.setSelectionRange(c.value.length, c.value.length); }
  }
  /* щосекундне оновлення таймерів без перемальовування (не збиває скрол і фокус) */
  function tick() {
    state.t++;
    [].forEach.call(root.querySelectorAll('[data-timer]'), function (el) {
      var rem = Math.max(0, +el.dataset.rem - state.t), total = +el.dataset.total;
      el.textContent = hms(rem);
      var f = el.closest('.qi').querySelector('[data-bar]');
      if (f) { var pct = Math.min(100, Math.round((1 - rem / total) * 100)); f.style.width = pct + '%'; f.parentNode.setAttribute('aria-valuenow', pct); }
    });
    var t = T(), txt = t.attack + ' · ' + mmss(Math.max(0, A.ATTACK_SECONDS - state.t));
    [].forEach.call(root.querySelectorAll('[data-attack]'), function (el) { el.textContent = txt; });
  }

  root.addEventListener('click', function (e) {
    var b = e.target.closest('[data-act]'); if (!b) return;
    var a = b.dataset.act;
    if (a === 'pick') state.sel = +b.dataset.i;
    else if (a === 'lang') state.lang = b.dataset.v;
    else if (a === 'premium') state.premium = !state.premium;
    else if (a === 'collect') state.collected[b.dataset.key] = true;
    render();
  });
  root.addEventListener('input', function (e) { if (e.target.id === 'chat') state.chat = e.target.value; });
  root.addEventListener('submit', function (e) { e.preventDefault(); state.chat = ''; render(); });

  render();
  setInterval(tick, 1000);
})();
