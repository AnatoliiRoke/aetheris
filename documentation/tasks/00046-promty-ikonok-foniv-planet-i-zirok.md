# Задача 00046 — Промпти й генерація іконок, фонів, планет і зірок для карти

> ⚠️ **ОБОВ'ЯЗКОВО ПРОЧИТАЙ ПЕРЕД РОБОТОЮ:** [tech/09 Зведені правила](../tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md), [tech/08](../tech/08-pravyla-kodu-dlia-ahentiv.md) і [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md). Картинки прив'язуються до даних через `image_key`, не до коду клієнта.
>
> **Статус:** ✏️ чернетка (08.11.2026)
> **Етап:** 1 (production/01)
> **Залежить від:** [00033](00033-dyzain-storinky-karty.md) і [00034](00034-perenesty-storinku-karty-v-kliyent.md) (карта, де ці зображення живуть), [00035](00035-promty-zobrazhen-obiektiv.md) (підхід до промптів, суфікс фотореалістичного стилю).
> **Документи:** [ui-art/01 Візуальний стиль](../ui-art/01-vizualnyi-styl.md), [ui-art/05 Єдиний стиль](../ui-art/05-yedynyi-styl-interfeisu.md), [game-design/09 Світ і карта](../game-design/09-svit-ta-karta.md), [задача 00003](00003-dyzain-pershoi-storinky-proektu.md) (мальований стиль фонів).
> **Формат:** усі 29 промптів — прямо в цій задачі (розділ 4), англійською, кожен самодостатній і передається генератору як є — так само, як промпти дизайну в задачах [00016](00016-dyzain-storinky-planety.md)–[00019](00019-dyzain-ekrana-skladu.md), [00044](00044-dyzain-storinok-verfi-ta-flotiv.md).

---

## 1. Мета

Створити повний набір зображень космосу для гри:

1. **Малі іконки** всіх типів об'єктів світу для карти (маркер `.map-body` — кружальце 32 px).
2. **Великі візуалізації** типів планет, зірок, полів і туманності (картка об'єкта на карті, `CardArt`, 16:9).
3. **Фони** для трьох масштабів карти (галактика, сектор, система) й суміжних екранів.

## 2. Поточний стан (перевірено 08.11.2026)

1. У світі **13 типів об'єктів** (`world-reference-seed.json`): 4 зірки (`object_type_1`–`object_type_4`: жовтий карлик, червоний карлик, білий карлик, блакитний гігант), 5 планет (`object_type_5`–`object_type_9`), газовий гігант (`object_type_10`), 2 астероїдні поля (`object_type_11`, `object_type_12`), туманність (`object_type_13`).
2. `image_key` є лише в планет і газового гіганта й вказує на **арт лендингу** (`art-12a`–`art-12f`) — це сцени з задачі 00003, а не окремі зображення типів; зірки, поля й туманність зображення не мають узагалі.
3. Карта малює об'єкти **векторними кружальцями** (CSS); маркер `.map-body` (32×32, `border-radius: 50%`, `object-fit: cover`) уже готовий показувати картинку — потрібні лише квадратні іконки, що читаються на 32 px.
4. Фон карти один (`map-bg.webp`), туманність намальована окремим фоном (`map-nebula.jpg`); масштаби «галактика» і «система» власних фонів не мають.
5. `documentation/art-local/icons/` порожній.

## 3. Що робимо

### 3.1. Малі іконки об'єктів (13 штук)

- **Одна іконка на тип об'єкта**, файл за нейтральним кодом: `icon_object_type_1.webp` … `icon_object_type_13.webp` (код стабільний, як у 00035).
- **Розмір джерела 512×512**, квадрат; предмет у центральному колі (~80% кадру), бо маркер обрізає картинку до кола й показує на 32 px (щільність ретини ×2–×4 закладена запасом).
- **Читається на 32 px**: один предмет, сильний силует, контрастні кольори, без дрібних деталей, тексту й рамок.
- **Фон кожної іконки темний** (`bg-0` #070B16), прозорість не потрібна: маркер сам обрізає до кола.
- Промпти: розділ 4.1.

### 3.2. Великі візуалізації (13 штук)

- **1280×720 (16:9)**, предмет у середніх 60% кадру — той самий кадр і суфікс фотореалістичного стилю, що в 00035, щоб картка об'єкта (`CardArt`) і плашки лишалися одним набором із зображеннями ресурсів і будівель.
- Файл за кодом: `object_type_1.webp` … `object_type_13.webp`. Для планет і газового гіганта ці файли **замінюють** посилання на арт лендингу (`art-12a`–`art-12f` лишаються на лендингу, їх не чіпаємо).
- Промпти: розділ 4.2.

### 3.3. Фони (3 штуки)

- **Мальований 2D-стиль задачі 00003** (як наявний `map-bg.webp`), без тексту, темна палітра токенів (`bg-0`/`bg-1` як основа), 1920×1080, WebP.
  1. `bg-map-galaxy.webp` — вид на галактику згори: спіральні рукави, мало деталей (фон для мінікарти галактики).
  2. `bg-map-sector.webp` — зоряне поле середньої глибини (заміна/уточнення наявного фону сектора, якщо автор схвалить новий варіант; наявний файл не видаляємо).
  3. `bg-map-system.webp` — ближнє зоряне небо з планетарним сяйвом збоку (фон виду системи).
- Бюджет трафіку (tech/02 §5): кожен фон ≤ 400 КБ після стиснення; перевірити вагу файла перед здачею.
- Промпти: розділ 4.3.

### 3.4. Файли й підключення до гри

- Готові файли: іконки в `documentation/art-local/icons/`, великі візуалізації в `documentation/art-local/images/objects/`, фони в `documentation/art-local/images/backgrounds/`.
- Сід світу: типам, у яких `image_key` порожній (зірки, поля, туманність), записати `image_key` нових файлів; планетам і газовому гіганту замінити `art-12*` на `object_type_N`. Сід пише лише відсутні рядки, тому на наявній базі зміна `image_key` виконується разом з оновленням сіду тим самим механізмом, що й інші правки довідників (без ручного SQL).
- Клієнт: маркер об'єкта на карті показує **малу іконку** (`icon_<code>.webp`) всередині `.map-body` замість суцільного кружальця; картка об'єкта й список — **велику візуалізацію** за `image_key` (механізм уже є: `typeImage` → `artImage` → `CardArt`). Якщо файла ще немає — нинішній векторний вигляд лишається заглушкою (правило 00010: без фальшивих даних).
- Фони підключаються в `client/src/styles/map.css` токенами шляхів; кожен масштаб показує свій фон.
- Оптимізація: іконки вантажаться ліниво разом із шаром карти; жодного попереднього завантаження всіх 29 файлів на старті.

## 4. Промпти (англійською, кожен передати генератору як є)

Загальні правила, уже вшиті в кожен промпт:

- іконки: квадрат 512×512, предмет у колі ~80% кадру, темне тло #070B16, читається на 32 px;
- великі візуалізації: 16:9, 1280×720, предмет у середніх 60% кадру (переживає кроп до 4:3 і 1:1), фотореалістичний суфікс із 00035;
- фони: 1920×1080, мальований суфікс із 00003, низький контраст (під інтерфейс).

### 4.1. Малі іконки (файл `icon_object_type_N.webp`, 512×512)

**ICON-01 — `icon_object_type_1.webp` — Жовтий карлик (yellow dwarf star)**

```
Square 512x512 game map icon: a sun-like yellow dwarf star, a bright golden-yellow sphere with a visible granulated surface and a warm corona glow. The star is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no surrounding objects, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-02 — `icon_object_type_2.webp` — Червоний карлик (red dwarf star)**

```
Square 512x512 game map icon: a small red dwarf star, a deep orange-red sphere with a dimmer, moodier glow than a sun-like star and a faint reddish corona. The star is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no surrounding objects, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-03 — `icon_object_type_3.webp` — Білий карлик (white dwarf star)**

```
Square 512x512 game map icon: a white dwarf star, a small intense white-blue sphere, very bright and compact, with a cold bluish halo. The star is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no surrounding objects, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-04 — `icon_object_type_4.webp` — Блакитний гігант (blue giant star)**

```
Square 512x512 game map icon: a massive blue giant star, a large radiant blue-white sphere with a powerful icy-blue corona and short prominences. The star is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no surrounding objects, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-05 — `icon_object_type_5.webp` — Землеподібна планета (earth-like planet)**

```
Square 512x512 game map icon: an earth-like planet, a sphere with blue oceans, green-brown continents and white cloud swirls, thin glowing atmosphere rim. The planet is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no moons or ships, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-06 — `icon_object_type_6.webp` — Вулканічна планета (volcanic planet)**

```
Square 512x512 game map icon: a volcanic planet, a dark crust sphere with glowing orange lava cracks and dull ash clouds, faint heat haze on the rim. The planet is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no moons or ships, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-07 — `icon_object_type_7.webp` — Крижана планета (ice planet)**

```
Square 512x512 game map icon: an ice planet, a white and pale-blue frozen sphere with visible cracks in the ice sheet and a thin cold atmosphere rim. The planet is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no moons or ships, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-08 — `icon_object_type_8.webp` — Пустельна планета (desert planet)**

```
Square 512x512 game map icon: a desert planet, an orange and sand-colored sphere with dune patterns and a thin dusty atmosphere rim. The planet is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no moons or ships, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-09 — `icon_object_type_9.webp` — Океанічна планета (ocean planet)**

```
Square 512x512 game map icon: an ocean planet, a sphere fully covered by deep blue water with swirling white clouds and a bright atmosphere rim. The planet is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no moons or ships, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-10 — `icon_object_type_10.webp` — Газовий гігант (gas giant)**

```
Square 512x512 game map icon: a gas giant planet, a large sphere with colorful horizontal storm bands in cream, ochre and brown and one large vortex, faint thin rings. The planet is a single object centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no small details, no moons or ships, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-11 — `icon_object_type_11.webp` — Звичайне астероїдне поле (common asteroid field)**

```
Square 512x512 game map icon: a small cluster of gray rocky asteroids of different sizes grouped into one dense clump, matte stone surfaces. The cluster is a single compact group centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no scattered debris across the frame, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-12 — `icon_object_type_12.webp` — Багате астероїдне поле (rich asteroid field)**

```
Square 512x512 game map icon: a small cluster of dark rocky asteroids grouped into one dense clump, with bright crystalline veins and sparkling mineral glints on several rocks. The cluster is a single compact group centered inside a circle that fills about 80% of the frame, with a strong silhouette and high contrast so it stays readable when scaled down to 32 px; no scattered debris across the frame, no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate materials and lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

**ICON-13 — `icon_object_type_13.webp` — Звичайна туманність (common nebula)**

```
Square 512x512 game map icon: a soft round nebula cloud in teal and violet with a few tiny stars glowing inside, one compact cloud shape. The nebula is a single object centered inside a circle that fills about 80% of the frame, with a clear outer contour and enough contrast to stay readable when scaled down to 32 px; no text, no letters, no numbers, no watermark. Dark space background (#070B16). Photorealistic cinematic render, physically accurate lighting, sharp focus, subtle volumetric glow, not a cartoon, not stylized.
```

### 4.2. Великі візуалізації (файл `object_type_N.webp`, 1280×720)

**ART-01 — `object_type_1.webp` — Жовтий карлик (yellow dwarf star)**

```
A sun-like yellow dwarf star seen from space: a bright golden sphere with granulated surface, small prominences on the limb and a warm corona; a few faint distant stars around. 16:9 frame, 1280x720; the star stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-02 — `object_type_2.webp` — Червоний карлик (red dwarf star)**

```
A red dwarf star seen from space: a compact deep orange-red sphere with dark starspots and a dim reddish corona, noticeably calmer and dimmer than a sun-like star; a few faint distant stars around. 16:9 frame, 1280x720; the star stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-03 — `object_type_3.webp` — Білий карлик (white dwarf star)**

```
A white dwarf star seen from space: a small, fiercely bright white-blue sphere with an intense point-like core and a cold bluish halo, surrounded by the faint remnant glow of a dispersed planetary shell; a few faint distant stars around. 16:9 frame, 1280x720; the star stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-04 — `object_type_4.webp` — Блакитний гігант (blue giant star)**

```
A massive blue giant star seen from space: a huge radiant blue-white sphere with a boiling surface, short powerful prominences and an icy-blue corona that lights up nearby thin gas; a few faint distant stars around. 16:9 frame, 1280x720; the star stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-05 — `object_type_5.webp` — Землеподібна планета (earth-like planet)**

```
An earth-like planet seen from orbit: blue oceans, green and brown continents, white cloud systems, a glowing atmosphere rim, partly in soft sunlight with a visible night side. 16:9 frame, 1280x720; the planet stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-06 — `object_type_6.webp` — Вулканічна планета (volcanic planet)**

```
A volcanic planet seen from orbit: dark basalt crust split by glowing orange lava rivers, ash plumes from volcanoes, a dull smoke-tinted atmosphere and ember glow on the night side. 16:9 frame, 1280x720; the planet stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-07 — `object_type_7.webp` — Крижана планета (ice planet)**

```
An ice planet seen from orbit: a white and pale-blue frozen surface with cracked ice sheets, frost fields and a thin cold atmosphere rim glittering in the light of a distant sun. 16:9 frame, 1280x720; the planet stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-08 — `object_type_8.webp` — Пустельна планета (desert planet)**

```
A desert planet seen from orbit: orange and sand-colored dune seas, rocky plateaus, thin dust storms and a hazy dry atmosphere rim. 16:9 frame, 1280x720; the planet stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-09 — `object_type_9.webp` — Океанічна планета (ocean planet)**

```
An ocean planet seen from orbit: a sphere fully covered by deep blue water with swirling white storm systems, no continents, a bright humid atmosphere rim. 16:9 frame, 1280x720; the planet stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-10 — `object_type_10.webp` — Газовий гігант (gas giant)**

```
A gas giant seen from space: a huge sphere with colorful horizontal storm bands in cream, ochre and brown, one large vortex storm, faint thin rings and a soft atmospheric glow on the limb. 16:9 frame, 1280x720; the planet stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-11 — `object_type_11.webp` — Звичайне астероїдне поле (common asteroid field)**

```
A common asteroid field in space: a wide drifting belt of gray rocky asteroids of different sizes, matte stone surfaces with craters, sparse distribution, no bright minerals. 16:9 frame, 1280x720; the densest part of the belt stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-12 — `object_type_12.webp` — Багате астероїдне поле (rich asteroid field)**

```
A rich asteroid field in space: a dense cluster of dark rocky asteroids with bright crystalline veins, exposed metal ore and sparkling mineral glints catching the light on many rocks. 16:9 frame, 1280x720; the densest part of the cluster stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**ART-13 — `object_type_13.webp` — Звичайна туманність (common nebula)**

```
A common nebula in deep space: a large soft cloud of teal and violet gas with darker dust lanes and several young stars glowing inside it, gentle gradients. 16:9 frame, 1280x720; the core of the nebula stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. Photorealistic cinematic render, physically accurate lighting, fine detail in the gas structure, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### 4.3. Фони карти (файл `bg-map-*.webp`, 1920×1080, мальовані)

**BG-01 — `bg-map-galaxy.webp` — масштаб «галактика»**

```
Top-down view of a spiral galaxy: two or three graceful spiral arms around a bright core, scattered star clusters, very few details, large calm dark areas between the arms, low contrast, suitable as a full-screen background behind interface panels. Painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouettes, no text, no letters, no numbers, no watermark. 1920x1080.
```

**BG-02 — `bg-map-sector.webp` — масштаб «сектор»**

```
A medium-depth starfield: many small and medium stars, a few brighter ones, very subtle teal and violet dust clouds in the far distance, even distribution, low contrast, suitable as a full-screen background behind interface panels and map markers. Painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouettes, no text, no letters, no numbers, no watermark. 1920x1080.
```

**BG-03 — `bg-map-system.webp` — масштаб «система»**

```
A close starry sky inside a star system: nearby bright star glow entering softly from one side, faint warm light gradient, sparse stars, a hint of a distant planet's atmosphere glow on the opposite edge, large calm dark center, low contrast, suitable as a full-screen background behind interface panels and map markers. Painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouettes, no text, no letters, no numbers, no watermark. 1920x1080.
```

## 5. Критерії приймання

1. Є промпт для кожного з 13 типів об'єктів у двох розмірах (іконка + велика) і для 3 фонів: усього 29 промптів у розділі 4 цієї задачі; кожен самодостатній і передається генератору як є.
2. Усі файли згенеровано, вони лежать у своїх папках у `art-local`, назви збігаються з кодами (`icon_object_type_N.webp`, `object_type_N.webp`, `bg-map-*.webp`), формат WebP, розміри й вага в межах розділу 3.
3. Іконка читається на 32 px: тип об'єкта впізнається без підпису (перевірка на знімку карти в обох масштабах «сектор» і «система»).
4. Карта показує іконки й картки з нових файлів; без файлів — нинішня векторна заглушка; `image_key` у сіді відповідає новим файлам.
5. Фони трьох масштабів карти підключені, кожен ≤ 400 КБ; стартовий бюджет клієнта не виріс (фони й іконки вантажаться зі сторінкою карти, не з лендингом).
6. `dotnet build -c Release` і `npm run lint && npm run build` зелені; стилі лише на токенах (ui-art/05).

## 6. Що не входить

1. Зображення 217 об'єктів задачі 00035 (ресурси, будівлі, дослідження, модулі, корпуси) — окремий процес автора.
2. Лого, картинки рас, арти лендингу, музика.
3. Зміни механіки карти (шари, пошук, маршрути) — задача лише про зображення й їхнє підключення.
4. Генерація інших розмірів і ретуш у графічному редакторі — окремим рішенням автора після перегляду першого набору.
5. Окремий документ `ui-art/07` і скрипти-збирачі промптів — промпти живуть прямо в цій задачі, як у 00044.

## 7. Рішення

1. Промпти зберігаються прямо в тілі задачі (розділ 4), а не в окремому документі: один файл читається й виконується без переходів; формат узятий із задач 00016–00019 і 00044.
2. Іконки й великі візуалізації іменуються за нейтральним кодом типу (`icon_object_type_N.webp`, `object_type_N.webp`), а не за назвою: код стабільний і не залежить від перейменувань (tech/09 §3).
3. Стиль великих візуалізацій і іконок — фотореалістичний суфікс 00035 (єдність із картками об'єктів); стиль фонів — мальований 2D задачі 00003 (єдність із наявним фоном карти й лендингом).
4. Джерело іконки 512×512 (не 256): карта масштабується й на ретині 32 px вимагає 64–128 px, запас ×4 дає чіткість без другого розміру.
5. Арт лендингу `art-12a`–`art-12f` не перевикористовується в грі й не змінюється: типи планет отримують власні зображення.
6. Кожен промпт самодостатній (розмір, композиція, тло й суфікс стилю вшиті в текст): його можна вставити в генератор без жодного довідника.

## 8. Результат виконання

_(дописується після виконання)_
