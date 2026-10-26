/* SVG-контури. Група ресурсу = колір, ресурс = унікальна форма (колір ніколи не єдиний носій). */
window.AE = window.AE || {};
AE.GROUP = { metal:'#A7B1C2', crystal:'#6FA8FF', fuel:'#FF9F43', ether:'#E9C46A', credits:'#3DD6A6', trade:'#B79CFF', energy:'#FFD84D', pop:'#C3CCE4' };
/* [назва, група, f=заливка | s=контур, path] */
AE.RES = {
  iron:['Залізо','metal','f','M5 5h14v14H5z'],
  titanium:['Титан','metal','f','M12 3l8 4.5v9L12 21l-8-4.5v-9z'],
  copper:['Мідь','metal','f','M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16z'],
  iridium:['Іридій','metal','f','M12 3l9 9-9 9-9-9z'],
  quartz:['Кварц','crystal','f','M10 2h4l4 7-6 13-6-13z'],
  ruby:['Рубін','crystal','f','M6 4h12l4 6-10 11L2 10z'],
  sapphire:['Сапфір','crystal','f','M12 2l7 10-7 10-7-10z'],
  obsidian:['Обсидіан','crystal','f','M4 20L12 3l8 17z'],
  deuterium:['Дейтерій','fuel','f','M12 3c4 5 6 8 6 11a6 6 0 0 1-12 0c0-3 2-6 6-11z'],
  plasma:['Плазма','fuel','f','M12 2c1 4 6 6 6 12a6 6 0 0 1-12 0c0-3 2-5 3-7 1 2 2 3 3 3-1-3 0-6 0-8z'],
  antimatter:['Антиматерія','fuel','s','M12 7a5 5 0 1 0 0 10a5 5 0 1 0 0-10z M12 2v3M12 19v3M2 12h3M19 12h3'],
  ether:['Ефір','ether','f','M12 2l2.6 6.9L22 9.3l-5.8 4.6 2 7.1L12 17l-6.2 4 2-7.1L2 9.3l7.4-.4z'],
  credits:['Кредити','credits','s','M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v10 M9 9.5h4.5a1.5 1.5 0 0 1 0 3H10a1.5 1.5 0 0 0 0 3h5'],
  trade:['Торгова валюта','trade','s','M12 3l8 9-8 9-8-9z M12 8l4 4-4 4-4-4z'],
  energy:['Енергія','energy','f','M13 2L4 14h7l-1 8 9-12h-7z'],
  pop:['Населення','pop','s','M12 12a4 4 0 1 0 0-8a4 4 0 1 0 0 8z M4 21a8 8 0 0 1 16 0']
};
AE.NAV = {
  overview:'M3 3h8v8H3z M13 3h8v5h-8z M13 10h8v11h-8z M3 13h8v8H3z',
  planet:'M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16z M2 13c4 3 16 3 20-2',
  production:'M3 21V10l6 4V10l6 4V6h6v15z',
  shipyard:'M12 2l4 8v8l-4 4-4-4v-8z M8 14l-4 3v3h4 M16 14l4 3v3h-4',
  fleets:'M2 12l8-4v8z M12 5l8-4v8z M12 19l8-4v8z',
  map:'M3 6l6-3 6 3 6-3v15l-6 3-6-3-6 3z M9 3v15 M15 6v15',
  research:'M9 3h6 M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3',
  market:'M3 9l2-5h14l2 5 M3 9h18v11H3z M9 20v-6h6v6',
  ranking:'M8 21h8 M12 17v4 M6 4h12v4a6 6 0 0 1-12 0z M6 6H3a3 3 0 0 0 3 4 M18 6h3a3 3 0 0 1-3 4',
  battle:'M14.5 17.5L3 6V3h3l11.5 11.5 M13 19l6-6 M16 16l4 4 M19 21l2-2',
  clan:'M12 2l8 3v6c0 5-3.5 9-8 11-4.5-2-8-6-8-11V5z',
  mail:'M3 5h18v14H3z M3 6l9 7 9-7',
  help:'M12 21a9 9 0 1 0 0-18a9 9 0 1 0 0 18z M9.5 9a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6V14 M12 17.5v.01',
  settings:'M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8z M12 2v3 M12 19v3 M2 12h3 M19 12h3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M4.9 19.1L7 17 M17 7l2.1-2.1',
  more:'M5 12h.01 M12 12h.01 M19 12h.01',
  signout:'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4 M16 17l5-5-5-5 M21 12H9',
  send:'M3 11l18-8-8 18-2-8z',
  star:'M12 2l2.6 6.9L22 9.3l-5.8 4.6 2 7.1L12 17l-6.2 4 2-7.1L2 9.3l7.4-.4z'
};
AE.BUILD = {
  mine:'M14 4l6 6 M4 20l9-9 M12 3c3 0 6 1 9 4-3-1-6-1-9 0',
  power:'M13 2L4 14h7l-1 8 9-12h-7z',
  house:'M4 21V9l8-6 8 6v12z M9 21v-6h6v6',
  box:'M3 7l9-4 9 4v10l-9 4-9-4z M3 7l9 4 9-4 M12 11v10',
  turret:'M4 20h16 M6 20v-5h12v5 M9 15v-3h6v3 M12 12V4l6-1',
  fiber:'M3 9c4-4 8 4 12 0s4 0 6 0 M3 15c4-4 8 4 12 0s4 0 6 0',
  fuel:'M4 21V9h6v12 M10 13h4l2-6h4v14H4 M7 9V5',
  lab:'M9 3h6 M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3 M7.5 14h9',
  radar:'M12 12l7-7 M12 3a9 9 0 1 0 9 9 M12 8a4 4 0 1 0 4 4',
  bunker:'M3 20h18 M5 20v-6a7 7 0 0 1 14 0v6 M9 20v-4h6v4'
};
AE.ACT = {
  done:'M5 12l4 4 10-10',
  lab:'M9 3h6 M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3',
  fleet:'M2 12l8-4v8z M10 12h12',
  warn:'M12 3l10 18H2z M12 10v4 M12 17.5v.01',
  danger:'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v3M12 14v3M7 12h3M14 12h3'
};
AE.ALERT = {
  attack:'M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 7v3M12 14v3M7 12h3M14 12h3',
  battle:'M14.5 17.5L3 6V3h3l11.5 11.5 M13 19l6-6 M16 16l4 4 M19 21l2-2',
  storage:'M3 7l9-4 9 4v10l-9 4-9-4z M3 7l9 4 9-4 M12 11v10'
};
AE.svg = function (d, size, o) {
  o = o || {};
  return '<svg width="' + size + '" height="' + size + '" viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="' + d + '" fill="' + (o.fill || 'none') + '" stroke="' + (o.stroke || 'none') + '" stroke-width="' + (o.sw || 2) + '" stroke-linecap="round" stroke-linejoin="round"/></svg>';
};
/* іконка ресурсу за ключем */
AE.res = function (key, size) {
  var r = AE.RES[key], c = AE.GROUP[r[1]];
  return AE.svg(r[3], size, { fill: r[2] === 'f' ? c : 'none', stroke: r[2] === 's' ? c : 'none' });
};
AE.line = function (d, size, sw) { return AE.svg(d, size, { stroke: 'currentColor', sw: sw || 1.8 }); };
