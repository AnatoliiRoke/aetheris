# AETHERIS — Промпти зображень об'єктів гри

> **Статус:** v0.1 (01.11.2026, задача [00035](../tasks/00035-promty-zobrazhen-obiektiv.md)). Згенеровано скриптом `tools/art/build_object_prompts.py` з сидів гри; ті самі промпти в `documentation/art-local/object-prompts.json` для пакетного запуску.
> **Пов'язано:** [ui-art/05 Єдиний стиль](05-yedynyi-styl-interfeisu.md) (розділ 11), [задача 00003](../tasks/00003-dyzain-pershoi-storinky-proektu.md) (стиль малюнків і суфікс).

## Правила

1. **Один стиль для всіх.** Кожен промпт вже містить суфікс стилю з задачі 00003: `painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.` Нічого дописувати не треба.
2. **Один розмір: 1280x720 (16:9).** Плашки будівель, досліджень і рецептів показують картинку в рамці 16:9; предмет стоїть у середніх 60% кадру, тому картинку можна обрізати до 4:3 чи 1:1 для іконок і карток.
3. **Один предмет у кадрі.** Без тексту, літер, цифр і водяних знаків. Розмір кадру й суфікс не змінювати між об'єктами, щоб картинки лишалися єдиним набором.
4. **Назва файлу** це код об'єкта з даних гри + `.webp` (наприклад `building_12.webp`). Код стабільний, тому файл завжди відповідає своєму рядку довідника.
5. **Формат:** WebP, до 1 МБ (ліміт адмінки `admin.reference.image_max_bytes`); для прозорості не вимагається, фон завжди темно-синій.
6. **Куди класти.** Готові файли складаються в `documentation/art-local/images/objects/` і завантажуються в адмінці в поле «Картинка» відповідного рядка довідника (ресурс, будівля, технологія, деталь). Поки картинки немає, гра показує спільну заглушку (ART-17).
7. **Раси.** Кріокапсули рас різняться кольором і формою (Террани: сталево-сірий і синій; Літаріони: крижаний ціан і білий; Драккури: темний і червоно-помаранчевий; Вейрани: піщано-бурштиновий); решта об'єктів нейтральна й не належить жодній расі.

## 1. Ресурси

Ресурси (31): сировина, пальне, кристали, вироблені товари й баланси.

### Руди

**1. Залізна руда** — Iron ore (`resource_1`, файл `resource_1.webp`)

```
A heavy chunk of raw iron ore with rust-red and brown rock, metallic grey veins and a few bright steel flecks. Palette: rust red, brown, steel grey. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**2. Титанова руда** — Titanium ore (`resource_2`, файл `resource_2.webp`)

```
A rough lump of titanium ore, silver-blue metallic crystals growing out of dark grey rock. Palette: silver blue, cold grey. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**3. Іридієва руда** — Iridium ore (`resource_3`, файл `resource_3.webp`)

```
A pile of iridium ore, dense pale-gold and white lustrous nuggets with a rainbow sheen on the facets. Palette: pale gold, white, faint rainbow sheen. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**4. Кобальтова руда** — Cobalt ore (`resource_4`, файл `resource_4.webp`)

```
Cobalt ore, deep royal-blue mineral blocks with glassy blue highlights in black rock. Palette: deep blue, black. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**5. Уранова руда** — Uranium ore (`resource_5`, файл `resource_5.webp`)

```
Uranium ore, dark heavy rock streaked with glowing toxic yellow-green veins that cast a soft green light. Palette: yellow-green glow, charcoal. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**6. Силікорова руда** — Silicore ore (`resource_6`, файл `resource_6.webp`)

```
Silicore ore, a grey-violet rock cut by thin crystalline silicon veins, faintly reflective like polished chip silicon. Palette: grey violet, silver. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**7. Тектонітова руда** — Tectonite ore (`resource_7`, файл `resource_7.webp`)

```
Tectonite ore, dark basalt plates with thin cracks glowing orange like cooling magma. Palette: black basalt, orange glow. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Мінерали

**8. Кварцит** — Quartzite (`resource_8`, файл `resource_8.webp`)

```
Quartzite, a cluster of milky white and translucent quartz crystals and rounded pale stone. Palette: milky white, pale blue. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**9. Обсидіан** — Obsidian (`resource_9`, файл `resource_9.webp`)

```
Obsidian, sharp black volcanic glass shards with glossy edges that reflect a thin cyan light. Palette: glossy black, cyan reflections. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**10. Лінтрій** — Lintrium (`resource_10`, файл `resource_10.webp`)

```
Lintrium, a soft fibrous turquoise mineral that looks like bundled stone threads with a pearly sheen. Palette: turquoise, pearl. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**11. Орекс** — Orex (`resource_11`, файл `resource_11.webp`)

```
Orex, rough orange-red translucent mineral nuggets, warm like amber with darker inclusions. Palette: amber orange, deep red. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Пальне

**12. Дейтерій** — Deuterium (`resource_12`, файл `resource_12.webp`)

```
Deuterium fuel, a transparent canister of cold blue-white liquid with glowing droplets and frosty vapor. Palette: ice blue, white. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**13. Гелій-3** — Helium-3 (`resource_13`, файл `resource_13.webp`)

```
Helium-3 fuel, a glass flask with a slow pale pink-gold gas swirling inside and tiny sparks. Palette: pale pink, soft gold. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**14. Темна матерія** — Dark matter (`resource_14`, файл `resource_14.webp`)

```
Dark matter, a perfectly black floating sphere with a thin violet halo that bends the stars behind it. Palette: black, violet rim. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**15. Ефір** — Ether (`resource_15`, файл `resource_15.webp`)

```
Ether, a golden luminous mist that gathers into a glowing cloud with fine bright threads. Palette: gold, warm white. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**16. Збагачений Уран** — Enriched uranium (`resource_16`, файл `resource_16.webp`)

```
Enriched uranium, a rack of neat glowing green fuel rods in a metal cradle with a soft toxic glow. Palette: bright green, dark steel. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Кристали

**17. Квартан** — Quartan (`resource_17`, файл `resource_17.webp`)

```
Quartan crystals, a cluster of clean hexagonal blue crystals with sharp facets and an inner glow. Palette: electric blue, white. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**18. Стобій** — Stobium (`resource_18`, файл `resource_18.webp`)

```
Stobium crystals, tall thin red-violet spikes that grow from a dark base and shimmer at the tips. Palette: ruby red, violet. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**19. Туманій** — Tumanium (`resource_19`, файл `resource_19.webp`)

```
Tumanium crystals, pale lilac and white misty crystal pillars with fog drifting between them. Palette: lilac, mist white. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Вироблені ресурси

**20. Будматеріали** — Building materials (`resource_20`, файл `resource_20.webp`)

```
Building materials, a neat stack of concrete slabs, steel beams and rebar bundles with yellow edge marks. Palette: concrete grey, steel, safety yellow. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**21. Оптоволокно** — Optical fiber (`resource_21`, файл `resource_21.webp`)

```
Optical fiber, a large spool wound with hair-thin fibers that carry running points of cyan and magenta light. Palette: cyan, magenta, black spool. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**22. Мікропроцесори** — Microprocessors (`resource_22`, файл `resource_22.webp`)

```
Microprocessors, several square chips on a dark board with gold pins and fine glowing circuit traces. Palette: silicon grey, gold, cyan traces. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**23. Композити** — Composites (`resource_23`, файл `resource_23.webp`)

```
Composites, layered sheets of woven dark carbon fabric and pale resin, fanned out like a stack of panels. Palette: carbon black, pale resin. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**24. Енергострижні** — Energy rods (`resource_24`, файл `resource_24.webp`)

```
Energy rods, a rack of glowing yellow-cyan rods with metal caps, light flowing along each rod. Palette: yellow, cyan, dark steel. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**25. Нановолокно** — Nanofiber (`resource_25`, файл `resource_25.webp`)

```
Nanofiber, a skein of shimmering ultra-fine threads that catch light in silver and violet. Palette: silver, violet shimmer. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**26. Титановий сплав** — Titanium alloy (`resource_26`, файл `resource_26.webp`)

```
Titanium alloy, three heavy ingots with stamped hexagon marks and a cool blue-silver metallic finish. Palette: blue silver, graphite. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Баланси цивілізації

**27. Кредити** — Credits (`resource_27`, файл `resource_27.webp`)

```
Credits, a small stack of round hexagon-stamped coins with a bright teal edge glow. Palette: teal, brushed silver. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**28. Торгова валюта** — Trade currency (`resource_28`, файл `resource_28.webp`)

```
Trade currency, a violet exchange token with two arrows curling round each other and a soft purple glow. Palette: violet, lilac glow. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**29. Енергія** — Energy (`resource_29`, файл `resource_29.webp`)

```
Energy, a glowing orb of yellow lightning held inside a ring, bright arcs reaching out. Palette: bright yellow, white. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**30. Населення** — Population (`resource_30`, файл `resource_30.webp`)

```
Population, a small crowd of calm human silhouettes standing in front of a glowing city dome. Palette: warm white, soft teal. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**31. Реальна валюта** — Premium currency (`resource_31`, файл `resource_31.webp`)

```
Premium currency, a rare crystalline golden-white coin that floats and throws sparkling light. Palette: gold, white sparkle. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

## 2. Будівлі

Будівлі (56) за категоріями гри.

### Головні й населення

**1. Адміністративний центр** — Administrative center (`building_1`, файл `building_1.webp`)

```
A broad administrative center with a central tower, glass wings and the flag mast, the seat of the whole colony. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**2. Житловий комплекс (Міста)** — Housing complex (`building_2`, файл `building_2.webp`)

```
A housing complex of tall residential blocks connected by sky bridges, hundreds of lit windows. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**3. Медичний центр** — Medical center (`building_3`, файл `building_3.webp`)

```
A medical center, a clean white building with a glowing cross-shaped atrium and a landing pad for ambulances. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**4. Банк** — Bank (`building_4`, файл `building_4.webp`)

```
A bank, a heavy dark building with a golden vault door visible through a glass facade and a ring of light above it. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**5. Будівельний комбінат** — Construction plant (`building_5`, файл `building_5.webp`)

```
A construction plant, cranes and gantries over a half-finished modular building, welding sparks. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**6. Космопорт** — Spaceport (`building_6`, файл `building_6.webp`)

```
A spaceport, wide landing fields with docked transport ships and a control tower. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Склади

**7. Склад** — Warehouse (`building_7`, файл `building_7.webp`)

```
A warehouse, a long hangar with wide doors, stacked cargo containers outside. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**8. Бункер** — Bunker (`building_8`, файл `building_8.webp`)

```
A bunker, a low reinforced dome half buried in the ground with thick armored doors and a small beacon. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Наука

**9. Науковий центр** — Science center (`building_9`, файл `building_9.webp`)

```
A science center, a round building with a rotating antenna dish and glowing laboratory windows. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**10. Конструкторське бюро** — Design bureau (`building_10`, файл `building_10.webp`)

```
A design bureau, an angular studio building with a large holographic blueprint of a ship floating in its atrium. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**11. Археологічний центр** — Archaeology center (`building_11`, файл `building_11.webp`)

```
An archaeology center, an excavation site with a lit tent complex and an ancient alien relic being lifted from the ground. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Видобуток на планеті

**12. Шахта залізної руди** — Iron ore mine (`building_12`, файл `building_12.webp`)

```
An iron ore mine, an open pit with a headframe tower and a conveyor, red-brown rock walls. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**13. Шахта титанової руди** — Titanium ore mine (`building_13`, файл `building_13.webp`)

```
A titanium ore mine, an excavation with silver-blue ore walls, drills and a loading crane. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**14. Шахта іридієвої руди** — Iridium ore mine (`building_14`, файл `building_14.webp`)

```
An iridium ore mine, a deep shaft with a pale-gold glint in the rock and a delicate extraction gantry. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**15. Шахта кобальтової руди** — Cobalt ore mine (`building_15`, файл `building_15.webp`)

```
A cobalt ore mine, a pit cut in deep blue rock with ore carts on rails. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**16. Шахта уранової руди** — Uranium ore mine (`building_16`, файл `building_16.webp`)

```
A uranium ore mine, a sealed shaft with warning lights, sheathed tunnels and a green glow from the rock. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**17. Шахта силікорової руди** — Silicore mine (`building_17`, файл `building_17.webp`)

```
A silicore mine, a terraced cut in grey-violet rock with precise cutting machines. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**18. Шахта тектонітової руди** — Tectonite ore mine (`building_18`, файл `building_18.webp`)

```
A tectonite ore mine, a mine in black basalt with glowing orange fissures and heat-shielded machines. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**19. Кар'єр кварциту** — Quartzite quarry (`building_19`, файл `building_19.webp`)

```
A quartzite quarry, a pale stepped quarry with cutting rigs and stacked white blocks. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**20. Кар'єр обсидіану** — Obsidian quarry (`building_20`, файл `building_20.webp`)

```
An obsidian quarry, a quarry of black glassy rock with sharp edges and a crane lifting a slab. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**21. Кар'єр лінтрію** — Lintrium quarry (`building_21`, файл `building_21.webp`)

```
A lintrium quarry, a turquoise fibrous rock face with long extraction arms. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**22. Кар'єр орексу** — Orex quarry (`building_22`, файл `building_22.webp`)

```
An orex quarry, an orange-red rock quarry with warm lights and haul trucks. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**23. Копальня квартану** — Quartan mine (`building_23`, файл `building_23.webp`)

```
A quartan mine, a cavern mouth lined with large blue crystals and light rails. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**24. Копальня стобію** — Stobium mine (`building_24`, файл `building_24.webp`)

```
A stobium mine, a shaft among tall red-violet crystal spikes with careful extraction tools. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**25. Копальня туманію** — Tumanium mine (`building_25`, файл `building_25.webp`)

```
A tumanium mine, a misty crater with pale lilac crystal pillars and drifting fog around the equipment. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**26. Станція дейтерію** — Deuterium station (`building_26`, файл `building_26.webp`)

```
A deuterium station, tall tanks and pipes on the shore of an ice-blue sea, vapor rising. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Енергетика

**27. Сонячна електростанція** — Solar power plant (`building_27`, файл `building_27.webp`)

```
A solar power plant, rows of large mirrored panels turned to a bright star, light flares on the glass. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**28. Теплова електростанція** — Thermal power plant (`building_28`, файл `building_28.webp`)

```
A thermal power plant, a heavy plant with big chimneys and glowing pipes, warm steam. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**29. Ядерна електростанція** — Nuclear power plant (`building_29`, файл `building_29.webp`)

```
A nuclear power plant, a containment dome with cooling towers and a soft blue glow from a service door. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**30. Термоядерна електростанція** — Fusion power plant (`building_30`, файл `building_30.webp`)

```
A fusion power plant, a huge ring reactor hall with a bright white-blue plasma core visible through the roof. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Виробництво ресурсів

**31. Завод будматеріалів** — Building-materials plant (`building_31`, файл `building_31.webp`)

```
A building-materials plant, mixers, silos and stacks of finished concrete slabs on a production line. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**32. Оптоволоконний завод** — Optical-fiber plant (`building_32`, файл `building_32.webp`)

```
An optical-fiber plant, a bright hall with long drawing towers and glowing fibers falling like threads of light. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**33. Фабрика мікропроцесорів** — Microprocessor factory (`building_33`, файл `building_33.webp`)

```
A microprocessor factory, a clean-room building with a glass front, wafer robots and cyan light. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**34. Завод композитів** — Composites plant (`building_34`, файл `building_34.webp`)

```
A composites plant, a long hall with autoclaves and rolls of dark carbon fabric being laid by arms. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**35. Завод енергострижнів** — Energy-rod plant (`building_35`, файл `building_35.webp`)

```
An energy-rod plant, a facility with charging towers where glowing rods are lowered into cradles. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**36. Завод нановолокна** — Nanofiber plant (`building_36`, файл `building_36.webp`)

```
A nanofiber plant, a compact tall plant with fine shimmering threads spun between vertical frames. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**37. Металургійний завод** — Metallurgical plant (`building_37`, файл `building_37.webp`)

```
A metallurgical plant, blast furnaces, a glowing ladle pouring molten metal and sparks in the dark. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**38. Завод збагачення урану** — Uranium enrichment plant (`building_38`, файл `building_38.webp`)

```
A uranium enrichment plant, rows of centrifuge halls behind security fences with a green glow. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**39. Перетворювач матерії** — Matter converter (`building_39`, файл `building_39.webp`)

```
A matter converter, a strange ring machine where a beam turns raw stone into glowing particles of other matter. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Виробництво модулів

**40. Фабрика двигунів** — Engine factory (`building_40`, файл `building_40.webp`)

```
An engine factory, a plant with test stands where a bright ion exhaust flares from a mounted engine. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**41. Завод реакторів** — Reactor plant (`building_41`, файл `building_41.webp`)

```
A reactor plant, an assembly hall with a half-built reactor core on a crane and blue arc light. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**42. Завод енергетичної зброї** — Energy-weapon plant (`building_42`, файл `building_42.webp`)

```
An energy-weapon plant, a factory with laser barrels on racks and a test range lit by a red beam. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**43. Завод кінетичної зброї** — Kinetic-weapon plant (`building_43`, файл `building_43.webp`)

```
A kinetic-weapon plant, a gun foundry with cannon barrels, shell conveyors and sparks. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**44. Ливарний завод** — Foundry (`building_44`, файл `building_44.webp`)

```
A foundry, a huge casting hall pouring bright metal into molds, heavy cranes and fiery glow. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**45. Завод щитів** — Shield plant (`building_45`, файл `building_45.webp`)

```
A shield plant, a hall with emitter rings where a shimmering energy dome is being tested. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**46. Фабрика боєприпасів** — Ammunition factory (`building_46`, файл `building_46.webp`)

```
An ammunition factory, assembly lines with missiles and shells under protective shields and warning lamps. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**47. Фабрика бурових установок** — Drill factory (`building_47`, файл `building_47.webp`)

```
A drill factory, big industrial drill bits and rigs being assembled under gantries. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**48. Завод точних приладів** — Precision-instrument plant (`building_48`, файл `building_48.webp`)

```
A precision-instrument plant, a bright laboratory-factory with tiny robotic arms and glass cabinets. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**49. Індустріальний комплекс** — Industrial complex (`building_49`, файл `building_49.webp`)

```
An industrial complex, a dense sprawl of chimneys, pipes, hangars and cranes under a hazy sky. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**50. Фабрика утилізації** — Recycling factory (`building_50`, файл `building_50.webp`)

```
A recycling factory, scrap heaps being fed into sorters, green status lights and falling metal sparks. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Флот і оборона

**51. Верф** — Shipyard (`building_51`, файл `building_51.webp`)

```
A shipyard, a huge dry dock with a half-built starship in a scaffolding frame and welding light. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**52. Радар** — Radar (`building_52`, файл `building_52.webp`)

```
A radar, a big dish antenna on a tower with a faint scanning beam sweeping the dark sky. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**53. Планетарна гармата** — Planetary gun (`building_53`, файл `building_53.webp`)

```
A planetary gun, a massive turret on a fortified base aiming at the sky, a muzzle glow ready. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**54. Планетарний щит** — Planetary shield (`building_54`, файл `building_54.webp`)

```
A planetary shield, a ring of tall emitter pylons raising a translucent teal dome over a small settlement. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Торгівля й логістика

**55. Торговий центр** — Trade center (`building_55`, файл `building_55.webp`)

```
A trade center, a modern market hall with glowing sign boards, cargo drones and a crowded square. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**56. Портал** — Portal (`building_56`, файл `building_56.webp`)

```
A portal, a large circular gate standing on a platform with a bright blue swirling opening inside. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

## 3. Дослідження

Технології (20): значок-емблема кожного дослідження.

### Фундаментальні науки

**1. Математика** — Mathematics (`tech_1`, файл `tech_1.webp`)

```
An emblem illustration for the research "Mathematics". Mathematics: glowing geometric solids, a golden spiral and floating equations over a dark grid. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**2. Фізика** — Physics (`tech_2`, файл `tech_2.webp`)

```
An emblem illustration for the research "Physics". Physics: a pendulum of light, a prism splitting a beam and orbiting particles in a faint field. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**3. Ядерна фізика** — Nuclear physics (`tech_3`, файл `tech_3.webp`)

```
An emblem illustration for the research "Nuclear physics". Nuclear physics: an atom with a bright nucleus, orbiting electrons and a green-yellow chain reaction. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**4. Термоядерний синтез** — Thermonuclear fusion (`tech_4`, файл `tech_4.webp`)

```
An emblem illustration for the research "Thermonuclear fusion". Thermonuclear fusion: two small suns merging into one bright white star inside a magnetic ring. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**5. Геологія** — Geology (`tech_5`, файл `tech_5.webp`)

```
An emblem illustration for the research "Geology". Geology: a cutaway of a planet crust with colorful layers, crystals and a drill probe. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**6. Електроніка** — Electronics (`tech_6`, файл `tech_6.webp`)

```
An emblem illustration for the research "Electronics". Electronics: a glowing circuit board with traces, a chip and flowing cyan current. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**7. Енергетика** — Power engineering (`tech_7`, файл `tech_7.webp`)

```
An emblem illustration for the research "Power engineering". Power engineering: a lightning bolt entering a power grid tower with arcs between coils. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**8. Будівництво** — Construction (`tech_8`, файл `tech_8.webp`)

```
An emblem illustration for the research "Construction". Construction: a holographic blueprint of a building with scaffolding and a crane silhouette. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**9. Кібернетика** — Cybernetics (`tech_9`, файл `tech_9.webp`)

```
An emblem illustration for the research "Cybernetics". Cybernetics: a human hand and a robotic hand almost touching, with neural light between them. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**10. Робототехніка** — Robotics (`tech_10`, файл `tech_10.webp`)

```
An emblem illustration for the research "Robotics". Robotics: a delicate robotic arm assembling a small glowing part on a workbench. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**11. Економіка** — Economics (`tech_11`, файл `tech_11.webp`)

```
An emblem illustration for the research "Economics". Economics: rising bars and a coin with trade arrows, a glowing market graph. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**12. Археологія** — Archaeology (`tech_12`, файл `tech_12.webp`)

```
An emblem illustration for the research "Archaeology". Archaeology: an ancient alien relic with glowing carved symbols half uncovered in sand. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**13. Перетворення матерії** — Matter conversion (`tech_18`, файл `tech_18.webp`)

```
An emblem illustration for the research "Matter conversion". Matter conversion: a beam turning a rough stone into glowing particles that form a different object. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Економіка

**14. Кристалографія** — Crystallography (`tech_13`, файл `tech_13.webp`)

```
An emblem illustration for the research "Crystallography". Crystallography: a perfect lattice of crystals with beams of light splitting across the facets. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**15. Газова інженерія** — Gas engineering (`tech_14`, файл `tech_14.webp`)

```
An emblem illustration for the research "Gas engineering". Gas engineering: pressure tanks, pipes and a swirling glowing gas cloud. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**16. Логістика складів** — Warehouse logistics (`tech_15`, файл `tech_15.webp`)

```
An emblem illustration for the research "Warehouse logistics". Warehouse logistics: a grid of containers with a holographic route and a loader drone. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**17. Демографія** — Demography (`tech_16`, файл `tech_16.webp`)

```
An emblem illustration for the research "Demography". Demography: a glowing tree of small human silhouettes growing like branches over a city. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**18. Колоніальне управління** — Colonial administration (`tech_17`, файл `tech_17.webp`)

```
An emblem illustration for the research "Colonial administration". Colonial administration: a planet with small flags and a network of lines linking colonies. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**19. Торговельне право** — Trade law (`tech_19`, файл `tech_19.webp`)

```
An emblem illustration for the research "Trade law". Trade law: balanced scales with a coin on one side and a contract scroll on the other, soft golden light. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**20. Стабілізація порталу** — Portal stabilization (`tech_20`, файл `tech_20.webp`)

```
An emblem illustration for the research "Portal stabilization". Portal stabilization: a swirling portal ring held steady by four glowing anchor beams. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

## 4. Модулі

Модулі кораблів і орбітальної бази (83), разом із чотирма кріокапсулами рас.

### Двигуни

**1. Реактивний двигун** — Jet engine (`part_m_1`, файл `part_m_1.webp`)

```
A jet engine, a compact metal nozzle with a bright orange exhaust flame and heat shimmer. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**2. Плазмовий двигун** — Plasma engine (`part_m_2`, файл `part_m_2.webp`)

```
A plasma engine, a wide nozzle with a magnetic ring and a violet-white plasma jet. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**3. Іонний двигун** — Ion engine (`part_m_3`, файл `part_m_3.webp`)

```
An ion engine, a long slim engine with grid rings and a thin cold blue ion beam. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**4. Гравітаційний двигун** — Gravity engine (`part_m_4`, файл `part_m_4.webp`)

```
A gravity engine, a spherical drive floating inside rings that bend the light around it. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**5. Гіпердвигун** — Hyperdrive (`part_m_5`, файл `part_m_5.webp`)

```
A hyperdrive, a sleek engine with a glowing spiral core and streaking stars behind it. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**6. Джамп-двигун** — Jump drive (`part_m_6`, файл `part_m_6.webp`)

```
A jump drive, a ring-shaped drive with a bright tear in space opening in its center. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Реактори

**7. Ядерний реактор** — Nuclear reactor (`part_m_7`, файл `part_m_7.webp`)

```
A nuclear reactor, a compact heavy core with cooling fins and a steady blue glow. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**8. Імпульсний реактор** — Pulse reactor (`part_m_8`, файл `part_m_8.webp`)

```
A pulse reactor, a reactor that flashes rhythmic bright pulses along its coils. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**9. Термоядерний реактор** — Fusion reactor (`part_m_9`, файл `part_m_9.webp`)

```
A fusion reactor, a toroidal ring with a bright white plasma line in the middle. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**10. Реактор холодного синтезу** — Cold-fusion reactor (`part_m_10`, файл `part_m_10.webp`)

```
A cold-fusion reactor, a clean frosted capsule reactor with a calm cyan light and ice crystals on it. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**11. Гравітаційний реактор** — Gravity reactor (`part_m_11`, файл `part_m_11.webp`)

```
A gravity reactor, a dark sphere held by rings, small bright matter streams spiraling in. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**12. Важкий реактор** — Heavy reactor (`part_m_12`, файл `part_m_12.webp`)

```
A heavy reactor, a massive armored reactor block with thick pipes and a hot orange core. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Енергетична зброя

**13. Легкий лазер** — Light laser (`part_m_13`, файл `part_m_13.webp`)

```
A light laser, a slim turret with a thin red beam. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**14. Важкий лазер** — Heavy laser (`part_m_14`, файл `part_m_14.webp`)

```
A heavy laser, a large turret with a long barrel and a thick red-orange beam. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**15. Імпульсний лазер** — Pulse laser (`part_m_15`, файл `part_m_15.webp`)

```
A pulse laser, a turret firing a train of short bright red pulses. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**16. Плазмова гармата** — Plasma cannon (`part_m_16`, файл `part_m_16.webp`)

```
A plasma cannon, a thick barrel launching a glowing green-white plasma bolt. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**17. Іонна гармата** — Ion cannon (`part_m_17`, файл `part_m_17.webp`)

```
An ion cannon, a ringed barrel emitting a crackling blue ion arc. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**18. Мікрохвильовий випромінювач** — Microwave emitter (`part_m_18`, файл `part_m_18.webp`)

```
A microwave emitter, a dish emitter with faint rippling waves of heat in the air. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**19. Частинковий прискорювач** — Particle accelerator (`part_m_19`, файл `part_m_19.webp`)

```
A particle accelerator, a long ring cannon with bright particle streams circulating inside. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**20. Орексова гармата** — Orex cannon (`part_m_20`, файл `part_m_20.webp`)

```
An orex cannon, a heavy cannon fed by orange-red orex crystals that glow in its breech. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Кінетична зброя

**21. Кінетична гармата** — Kinetic cannon (`part_m_21`, файл `part_m_21.webp`)

```
A kinetic cannon, a sturdy steel gun with a muzzle flash. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**22. Скорострільна гармата** — Autocannon (`part_m_22`, файл `part_m_22.webp`)

```
An autocannon, a multi-barrel rotary cannon with a stream of tracer rounds. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**23. Рейкова гармата** — Railgun (`part_m_23`, файл `part_m_23.webp`)

```
A railgun, a long twin-rail gun with blue sparks running along the rails. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**24. РПУ-X1** — Launcher X1 (`part_m_24`, файл `part_m_24.webp`)

```
A missile launcher X1, a small launcher rack with a single missile ready. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**25. РПУ-X2** — Launcher X2 (`part_m_25`, файл `part_m_25.webp`)

```
A missile launcher X2, a medium launcher with two missiles in side-by-side tubes. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**26. РПУ-X3** — Launcher X3 (`part_m_26`, файл `part_m_26.webp`)

```
A missile launcher X3, a large launcher with three heavy missiles in a tube cluster. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Броня

**27. Сталева плита** — Steel plate (`part_m_27`, файл `part_m_27.webp`)

```
A steel plate, a thick dull steel armor plate with bolts and scratches. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**28. Сегментна броня** — Segmented armor (`part_m_28`, файл `part_m_28.webp`)

```
Segmented armor, overlapping armor segments like a shell, each with a small highlight. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**29. Кобальтова броня** — Cobalt armor (`part_m_29`, файл `part_m_29.webp`)

```
Cobalt armor, deep blue armor plates with a glossy finish. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**30. Титанова плита** — Titanium plate (`part_m_30`, файл `part_m_30.webp`)

```
A titanium plate, a clean silver-blue plate with a hexagon pattern. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**31. Композитна броня** — Composite armor (`part_m_31`, файл `part_m_31.webp`)

```
Composite armor, layered dark composite panels with visible weave. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**32. Іридієва плита** — Iridium plate (`part_m_32`, файл `part_m_32.webp`)

```
An iridium plate, a bright pale-gold plate that looks very dense and polished. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**33. Наноплита** — Nanoplate (`part_m_33`, файл `part_m_33.webp`)

```
A nanoplate, a plate with a faintly shimmering nano-lattice surface that seems to move. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**34. Адаптивна броня** — Adaptive armor (`part_m_34`, файл `part_m_34.webp`)

```
Adaptive armor, plates in motion that shift color toward the threat, with soft color ripples. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Щити

**35. Силовий щит** — Force shield (`part_m_35`, файл `part_m_35.webp`)

```
A force shield, a shield emitter projecting a translucent teal dome. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**36. Кварцовий щит** — Quartz shield (`part_m_36`, файл `part_m_36.webp`)

```
A quartz shield, a shield made of clear quartz facets that refract light. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**37. Магнітний щит** — Magnetic shield (`part_m_37`, файл `part_m_37.webp`)

```
A magnetic shield, an emitter with visible curved field lines in blue and violet. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**38. Гравітаційний щит** — Gravity shield (`part_m_38`, файл `part_m_38.webp`)

```
A gravity shield, a dark sphere surrounded by bent light and orbiting small debris. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**39. Нейтронний щит** — Neutron shield (`part_m_39`, файл `part_m_39.webp`)

```
A neutron shield, a dense white-grey shield surface with fine particle sparks. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**40. Плазмовий щит** — Plasma shield (`part_m_40`, файл `part_m_40.webp`)

```
A plasma shield, a shield glowing like hot plasma in pink and white. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**41. Орексовий щит** — Orex shield (`part_m_41`, файл `part_m_41.webp`)

```
An orex shield, an orange-red crystal-shaped energy shield around an orex core. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**42. Ефірний щит** — Ether shield (`part_m_42`, файл `part_m_42.webp`)

```
An ether shield, a golden luminous veil of ether with soft waves. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Боєприпаси

**43. Сталевий снаряд** — Steel shell (`part_m_43`, файл `part_m_43.webp`)

```
A steel shell, a plain steel artillery shell on a stand. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**44. Кобальтовий снаряд** — Cobalt shell (`part_m_44`, файл `part_m_44.webp`)

```
A cobalt shell, a deep-blue shell with a sharp tip. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**45. Рейковий стрижень** — Rail slug (`part_m_45`, файл `part_m_45.webp`)

```
A rail slug, a dense metal slug with a glowing blue trail. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**46. Стріла-X1** — Arrow X1 (`part_m_46`, файл `part_m_46.webp`)

```
Arrow X1 missile, a small slim missile with small fins and an orange exhaust. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**47. Блискавка-X1** — Lightning X1 (`part_m_47`, файл `part_m_47.webp`)

```
Lightning X1 missile, a fast missile surrounded by crackling electric arcs. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**48. Спис-X2** — Spear X2 (`part_m_48`, файл `part_m_48.webp`)

```
Spear X2 missile, a long armor-piercing missile with a sharp needle nose. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**49. Валькірія-X3** — Valkyrie X3 (`part_m_49`, файл `part_m_49.webp`)

```
Valkyrie X3 missile, a large heavy missile with winged fins and a white-gold exhaust. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**50. Термоядерна торпеда** — Fusion torpedo (`part_m_50`, файл `part_m_50.webp`)

```
A fusion torpedo, a large torpedo with a glowing fusion core visible along its body. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**51. Протонна торпеда** — Proton torpedo (`part_m_51`, файл `part_m_51.webp`)

```
A proton torpedo, a torpedo with a pulsing bright blue-white proton warhead. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**52. Уранова торпеда** — Uranium torpedo (`part_m_52`, файл `part_m_52.webp`)

```
A uranium torpedo, a heavy dark torpedo with green glowing warhead sections. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Видобуток

**53. Модуль геологорозвідки** — Survey module (`part_m_53`, файл `part_m_53.webp`)

```
A survey module, a scanning pod with a slim antenna array and a ground-scanning cone of light. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**54. Бурова установка** — Drill rig (`part_m_54`, файл `part_m_54.webp`)

```
A drill rig, a robust drilling head with rotating teeth and sparks. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**55. Кристалічний екстрактор** — Crystal extractor (`part_m_55`, файл `part_m_55.webp`)

```
A crystal extractor, a precise claw-and-laser tool holding a glowing blue crystal. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**56. Збирач ефіру** — Ether collector (`part_m_56`, файл `part_m_56.webp`)

```
An ether collector, a funnel-shaped collector drawing golden ether mist into a container. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**57. Туманнісний збирач** — Nebula collector (`part_m_57`, файл `part_m_57.webp`)

```
A nebula collector, a wide net-like scoop trailing violet-teal nebula gas. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Розвідка й маскування

**58. Астросканер** — Astroscanner (`part_m_58`, файл `part_m_58.webp`)

```
An astroscanner, a telescopic scanner with a lens array and faint star maps projected in front of it. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**59. Корабельний радар** — Ship radar (`part_m_59`, файл `part_m_59.webp`)

```
A ship radar, a rotating compact radar dish with a sweeping green-cyan beam. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**60. Археологічний модуль** — Archaeology module (`part_m_60`, файл `part_m_60.webp`)

```
An archaeology module, a delicate scanning arm brushing an ancient relic with soft light. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**61. Радіооптичне маскування** — Radio-optical camouflage (`part_m_61`, файл `part_m_61.webp`)

```
Radio-optical camouflage, a ship panel half transparent, bending the background light around it. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**62. Детектор маскування** — Camouflage detector (`part_m_62`, файл `part_m_62.webp`)

```
A camouflage detector, a sensor head with several lenses revealing a hidden ghost outline. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Інші корабельні

**63. Відновлювач щитів** — Shield restorer (`part_m_63`, файл `part_m_63.webp`)

```
A shield restorer, a module with a glowing teal coil that rebuilds a broken shield ring. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**64. Бортовий комп'ютер** — Onboard computer (`part_m_64`, файл `part_m_64.webp`)

```
An onboard computer, a rack of glowing processor blocks and a small holographic display. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**65. Збирач уламків** — Debris collector (`part_m_65`, файл `part_m_65.webp`)

```
A debris collector, a magnetic scoop pulling small wreckage into a bin. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**66. Вантажний відсік** — Cargo hold (`part_m_66`, файл `part_m_66.webp`)

```
A cargo hold, a boxy container module with a open door and crates inside. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**67. Великий вантажний відсік** — Large cargo hold (`part_m_67`, файл `part_m_67.webp`)

```
A large cargo hold, a very big container module with sliding doors and rows of crates. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**68. Протиракетна система** — Missile-defence system (`part_m_68`, файл `part_m_68.webp`)

```
A missile-defence system, a turret with small quick guns and tracking beams aimed at incoming missiles. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**69. Модуль передачі** — Transfer module (`part_m_69`, файл `part_m_69.webp`)

```
A transfer module, a docking tube with a glowing connector passing cargo between two ships. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**70. Колоніальний модуль** — Colony module (`part_m_70`, файл `part_m_70.webp`)

```
A colony module, a sealed capsule with a landing frame and a small green seedling dome. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**71. Кріокапсула (Террани)** — Cryocapsule (Terrans) (`part_m_80`, файл `part_m_80.webp`)

```
A cryocapsule of the Terrans, a sleek steel-grey and blue capsule with a frosted window showing sleeping human figures, a small blue status light. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**72. Кріокапсула (Літаріони)** — Cryocapsule (Litari) (`part_m_81`, файл `part_m_81.webp`)

```
A cryocapsule of the Litari, an icy cyan and white crystalline capsule with a frosted window showing sleeping figures, glittering frost. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**73. Кріокапсула (Драккури)** — Cryocapsule (Drakkuri) (`part_m_82`, файл `part_m_82.webp`)

```
A cryocapsule of the Drakkuri, a dark capsule with red-orange ember lines and a window showing sleeping reptilian figures, warm glow. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**74. Кріокапсула (Вейрани)** — Cryocapsule (Veirani) (`part_m_83`, файл `part_m_83.webp`)

```
A cryocapsule of the Veirani, a sand-amber capsule with elegant curves and a window showing sleeping tall figures, golden light. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Модулі бази

**75. Житловий модуль** — Housing module (`part_m_71`, файл `part_m_71.webp`)

```
A housing module, a stacked living module with rows of small lit windows. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**76. Орбітальний склад** — Orbital warehouse (`part_m_72`, файл `part_m_72.webp`)

```
An orbital warehouse, a station module with big docking arms and stacked containers. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**77. Ремонтні майстерні** — Repair workshops (`part_m_73`, файл `part_m_73.webp`)

```
Repair workshops, a station bay with robotic arms welding a damaged armor plate. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**78. Школа командирів** — Commander school (`part_m_74`, файл `part_m_74.webp`)

```
A commander school, a training module with a holographic tactical map table and cadet silhouettes. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**79. Торговий центр** — Trade center (`part_m_75`, файл `part_m_75.webp`)

```
A trade center module, a station market ring with glowing signs and docked cargo ships. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**80. Орбітальний термоядерний реактор** — Orbital fusion reactor (`part_m_76`, файл `part_m_76.webp`)

```
An orbital fusion reactor, a big station fusion ring with a bright core and cooling panels. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**81. Збагачувач ізотопів гелію** — Helium-isotope enricher (`part_m_77`, файл `part_m_77.webp`)

```
A helium-isotope enricher, a station module of spinning centrifuges and glass tanks of pale gas. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**82. Екстрактор темної матерії** — Dark-matter extractor (`part_m_78`, файл `part_m_78.webp`)

```
A dark-matter extractor, a heavy station module with a containment ring around a black sphere. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**83. Джамп-модуль** — Jump module (`part_m_79`, файл `part_m_79.webp`)

```
A jump module, a station gate frame with a bright rift forming in the middle. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

## 5. Корпуси (додатково)

Корпуси кораблів і станцій (27). Їх не було в переліку автора, додано, бо їм теж потрібні картинки; за потреби розділ можна не генерувати.

### Легкі

**1. Винищувач «Сапсан»** — Fighter Sapsan (`part_h_1`, файл `part_h_1.webp`)

```
A small agile fighter, a sharp single-seat starfighter with swept wings in flight. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**2. Важкий винищувач «Шершень»** — Heavy fighter Hornet (`part_h_2`, файл `part_h_2.webp`)

```
A heavy fighter, a sturdy armored starfighter with twin engines and heavy guns. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**3. Торпедоносець «Гадюка»** — Torpedo bomber Viper (`part_h_3`, файл `part_h_3.webp`)

```
A torpedo bomber, a compact bomber with a big torpedo slung under its belly. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**4. Важкий торпедоносець «Кобра»** — Heavy torpedo bomber Cobra (`part_h_4`, файл `part_h_4.webp`)

```
A heavy torpedo bomber, a wide bomber with two large torpedoes and thick armor. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Дрони

**5. «Ґедзь»** — Gadfly (`part_h_5`, файл `part_h_5.webp`)

```
A tiny gadfly-like drone craft with thin wings, quick and light. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**6. «Цикада»** — Cicada (`part_h_6`, файл `part_h_6.webp`)

```
A cicada-like scout craft with a long body and a pair of lifted wing panels. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**7. «Кліщ»** — Tick (`part_h_7`, файл `part_h_7.webp`)

```
A tick-like boarding craft, a low round body with grabbing clamps. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Середні

**8. Корвет «Коршун»** — Corvette Kite (`part_h_8`, файл `part_h_8.webp`)

```
A corvette, a small fast warship with a clean hull and a ring of turrets. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**9. Фрегат «Барракуда»** — Frigate Barracuda (`part_h_9`, файл `part_h_9.webp`)

```
A frigate, a long slim warship with a row of turrets and a bright engine block. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**10. Есмінець «Акула»** — Destroyer Shark (`part_h_10`, файл `part_h_10.webp`)

```
A destroyer, a sharp-nosed warship with a wedge hull and rows of weapons. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**11. Легкий крейсер «Ягуар»** — Light cruiser Jaguar (`part_h_11`, файл `part_h_11.webp`)

```
A light cruiser, a balanced warship with a long deck and a raised command tower. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**12. Крейсер «Носоріг»** — Cruiser Rhino (`part_h_12`, файл `part_h_12.webp`)

```
A cruiser, a big warship with heavy armor, a central tower and many turrets. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**13. Важкий крейсер «Бегемот»** — Heavy cruiser Behemoth (`part_h_13`, файл `part_h_13.webp`)

```
A heavy cruiser, a massive armored warship with a broad hull and large guns. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Важкі

**14. Броненосець «Панцирник»** — Armored ship Pangolin (`part_h_14`, файл `part_h_14.webp`)

```
An armored ship, a squat heavily plated warship with thick shell-like plates. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**15. Лінкор «Кракен»** — Battleship Kraken (`part_h_15`, файл `part_h_15.webp`)

```
A battleship, a huge warship with big main guns on its bow and a tall bridge. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**16. Важкий лінкор «Мамонт»** — Heavy battleship Mammoth (`part_h_16`, файл `part_h_16.webp`)

```
A heavy battleship, an enormous warship with layered armor and giant cannons. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**17. Дредноут «Горгона»** — Dreadnought Gorgon (`part_h_17`, файл `part_h_17.webp`)

```
A dreadnought, a gigantic warship with an imposing front and a serpent-like head ornament. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Надважкі

**18. Дестроєр «Каратель»** — Devastator Punisher (`part_h_18`, файл `part_h_18.webp`)

```
A devastator, a bulky siege warship with a huge forward cannon. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**19. Супер-лінкор «Тиранозавр»** — Super battleship Tyrannosaurus (`part_h_19`, файл `part_h_19.webp`)

```
A super battleship, a colossal predator-shaped warship with big jaws-like prow weapons. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**20. Супер-дредноут «Левіафан»** — Super dreadnought Leviathan (`part_h_20`, файл `part_h_20.webp`)

```
A super dreadnought, the biggest flagship, a leviathan hull with huge engines and a shining command spire. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Цивільні

**21. Малий транспорт «Жук»** — Small transport Beetle (`part_h_21`, файл `part_h_21.webp`)

```
A small transport, a boxy cargo ship with a small cockpit and a container on its back. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**22. Середній транспорт «Бик»** — Medium transport Bull (`part_h_22`, файл `part_h_22.webp`)

```
A medium transport, a strong hauler with a clear cargo frame and two engines. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**23. Великий транспорт «Слон»** — Large transport Elephant (`part_h_23`, файл `part_h_23.webp`)

```
A large transport, a very long hauler with a huge cargo spine and big engines. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**24. Важкий транспорт «Кит»** — Heavy transport Whale (`part_h_24`, файл `part_h_24.webp`)

```
A heavy transport, a vast whale-shaped carrier with big cargo doors. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

### Корпуси споруд

**25. Орбітальна база** — Orbital base (`part_h_25`, файл `part_h_25.webp`)

```
An orbital base, a compact ring station with docking arms around a planet's edge. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**26. Космобаза** — Space base (`part_h_26`, файл `part_h_26.webp`)

```
A space base, a large multi-ring space station with solar wings and many docks. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

**27. Видобувна станція** — Mining station (`part_h_27`, файл `part_h_27.webp`)

```
A mining station, a station with big drilling arms and a stream of ore into a processing core. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark.
```

## Підсумок

| Вид | Кількість |
|---|---|
| Ресурси | 31 |
| Будівлі | 56 |
| Дослідження | 20 |
| Модулі | 83 |
| Корпуси (додатково) | 27 |
| **Разом** | **217** |
