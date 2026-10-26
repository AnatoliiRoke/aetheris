/* Демо-дані. У бойовій версії приходять з API: GET /api/civilization.
   Таймери: rem — секунд до завершення на момент завантаження, total — повна тривалість, queued — тривалість черги. */
window.AE = window.AE || {};
AE.PLAYER = { nick: 'Kapitan_Zoria', race: 'terran', mail: 3 };
AE.TOP = [['credits', '48 210'], ['trade', '1 250'], ['energy', '4 860']];
AE.ATTACK_SECONDS = 760;
AE.QUEUE_CAP = { premium: 5, standard: 2 };

AE.TYPES = {
  earth:    { img: 'assets/img/planet-earth.webp',    glow: '80,170,255',  name: 'Земного типу' },
  volcanic: { img: 'assets/img/planet-volcanic.webp', glow: '255,107,61',  name: 'Вулканічна' },
  icy:      { img: 'assets/img/planet-icy.webp',      glow: '190,230,255', name: 'Крижана' },
  desert:   { img: 'assets/img/planet-desert.webp',   glow: '230,150,80',  name: 'Пустельна' },
  oceanic:  { img: 'assets/img/planet-oceanic.webp',  glow: '60,150,255',  name: 'Океанічна' },
  gas:      { img: 'assets/img/planet-gas.webp',      glow: '230,170,110', name: 'Газовий гігант' }
};

/* co: [сектор, система, орбіта]; en: [споживання, виробіток]; st: [на складі, місткість] */
AE.PLANETS = [
  { name: 'Нова Тесса', type: 'earth', co: [7, 'Kepler-442', 2], dep: ['iron', 'titanium', 'copper', 'quartz'],
    y: [['iron', 120], ['titanium', 45], ['copper', 80], ['quartz', 30]], pop: [742, 1000], grow: 12, en: [320, 400], st: [6400, 10000],
    build: [{ name: 'Житловий комплекс · рів. 5', i: 'house', rem: 4350, total: 9062 }, { name: 'Завод оптоволокна · рів. 2', i: 'fiber', queued: 5400 }, { name: 'Шахта титану · рів. 3', i: 'mine', queued: 7200 }, { name: 'Склад металів · рів. 4', i: 'box', queued: 9000 }],
    research: [{ name: 'Глибинний видобуток', rem: 2538, total: 7050, cost: [['quartz', 1200], ['credits', 3500]] }, { name: 'Кристалографія', queued: 7800, cost: [['quartz', 800], ['credits', 2400]] }, { name: 'Складська логістика', queued: 6600, cost: [['quartz', 600], ['credits', 2000]] }],
    act: [['lab', 'Дослідження «Енергетика» завершено.', '18 хв'], ['done', 'Житловий комплекс рів. 4 збудовано.', '3 год']] },
  { name: 'Горнило', type: 'volcanic', co: [7, 'Kepler-442', 1], dep: ['iron', 'copper', 'iridium', 'ruby'],
    y: [['iron', 160], ['copper', 95], ['iridium', 12], ['ruby', 22]], pop: [310, 600], grow: 5, en: [380, 450], st: [8000, 8000], badge: ['warning', 'storageFull'],
    build: [{ name: 'Шахта заліза · рів. 4', i: 'mine', done: true, key: 'gorn' }, { name: 'Склад металів · рів. 3', i: 'box', rem: 1800, total: 5400 }],
    act: [['warn', 'Склад металів заповнений — видобуток стоїть.', '2 год']] },
  { name: 'Кріон', type: 'icy', co: [7, 'Vega-3', 4], dep: ['iron', 'titanium', 'sapphire'],
    y: [['iron', 70], ['titanium', 60], ['sapphire', 18]], pop: [205, 500], grow: 3, en: [440, 400], st: [2460, 6000], badge: ['warning', 'Дефіцит енергії'],
    paused: 'Завод оптоволокна: бракує 40 енергії. Запуститься сам після електростанції.',
    build: [{ name: 'Електростанція · рів. 3', i: 'power', rem: 252, total: 3150 }, { name: 'Завод оптоволокна · рів. 2', i: 'fiber', queued: 4800 }],
    act: [['warn', 'Завод оптоволокна зупинено: бракує енергії.', '40 хв']] },
  { name: 'Дюна', type: 'desert', co: [9, 'Tauri-11', 3], dep: ['iron', 'copper', 'titanium', 'obsidian'],
    y: [['iron', 90], ['copper', 110], ['titanium', 30], ['obsidian', 8]], pop: [180, 400], grow: 2, en: [210, 300], st: [3300, 6000], badge: ['danger', 'Під атакою'],
    build: [{ name: 'Планетарні турелі · рів. 2', i: 'turret', rem: 20830, total: 21698 }, { name: 'Радар · рів. 1', i: 'radar', queued: 3600 }, { name: 'Бункер · рів. 1', i: 'bunker', queued: 6000 }],
    act: [['danger', 'Ворожий флот атакує орбіту.', '4 хв'], ['done', 'Планетарні турелі рів. 1 збудовано.', '1 год']] },
  { name: 'Таласса', type: 'oceanic', co: [9, 'Tauri-11', 2], dep: ['iron', 'copper', 'titanium', 'sapphire'],
    y: [['iron', 60], ['copper', 70], ['titanium', 25], ['sapphire', 20]], pop: [420, 800], grow: 8, en: [260, 350], st: [2400, 8000],
    build: [],
    research: [{ name: 'Демографія', rem: 5400, total: 9000, cost: [['sapphire', 600], ['credits', 1800]] }, { name: 'Колоніальна адміністрація', queued: 9600, cost: [['sapphire', 900], ['credits', 3000]] }],
    act: [['fleet', 'Флот «Сталевий лис» повернувся на орбіту.', '1 год']] },
  { name: 'Аквілон', type: 'gas', co: [9, 'Tauri-11', 6], dep: ['deuterium', 'plasma'],
    y: [['deuterium', 210], ['plasma', 40]], pop: null, en: [120, 200], st: [5760, 8000],
    build: [{ name: 'Паливна станція · рів. 4', i: 'fuel', rem: 6400, total: 14000 }],
    act: [['fleet', 'Конвой із дейтерієм вирушив на Нову Тессу.', '2 год']] }
];
