# AETHERIS — Промпти зображень об'єктів гри

> **Статус:** v0.1 (01.11.2026, задача [00035](../tasks/00035-promty-zobrazhen-obiektiv.md)). Згенеровано скриптом `tools/art/build_object_prompts.py` з сидів гри; ті самі промпти в `documentation/art-local/object-prompts.json` для пакетного запуску.
> **Пов'язано:** [ui-art/05 Єдиний стиль](05-yedynyi-styl-interfeisu.md) (розділ 11), [задача 00003](../tasks/00003-dyzain-pershoi-storinky-proektu.md) (стиль малюнків і суфікс).

## Правила

1. **Один стиль для всіх.** Кожен промпт вже містить суфікс стилю: `photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.` Нічого дописувати не треба. Стиль **реалістичний** (рішення автора 01.11.2026 для картинок об'єктів); це відрізняється від мальованого 2D з задачі 00003, яким зроблені лендінг і фони, тому суфікс тут свій.
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
A heavy chunk of raw iron ore with rust-red and brown rock, metallic grey veins and a few bright steel flecks. Palette: rust red, brown, steel grey. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**2. Титанова руда** — Titanium ore (`resource_2`, файл `resource_2.webp`)

```
A rough lump of titanium ore, silver-blue metallic crystals growing out of dark grey rock. Palette: silver blue, cold grey. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**3. Іридієва руда** — Iridium ore (`resource_3`, файл `resource_3.webp`)

```
A pile of iridium ore, dense pale-gold and white lustrous nuggets with a rainbow sheen on the facets. Palette: pale gold, white, faint rainbow sheen. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**4. Кобальтова руда** — Cobalt ore (`resource_4`, файл `resource_4.webp`)

```
Cobalt ore, deep royal-blue mineral blocks with glassy blue highlights in black rock. Palette: deep blue, black. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**5. Уранова руда** — Uranium ore (`resource_5`, файл `resource_5.webp`)

```
Uranium ore, dark heavy rock streaked with glowing toxic yellow-green veins that cast a soft green light. Palette: yellow-green glow, charcoal. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**6. Силікорова руда** — Silicore ore (`resource_6`, файл `resource_6.webp`)

```
Silicore ore, a grey-violet rock cut by thin crystalline silicon veins, faintly reflective like polished chip silicon. Palette: grey violet, silver. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**7. Тектонітова руда** — Tectonite ore (`resource_7`, файл `resource_7.webp`)

```
Tectonite ore, dark basalt plates with thin cracks glowing orange like cooling magma. Palette: black basalt, orange glow. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Мінерали

**8. Кварцит** — Quartzite (`resource_8`, файл `resource_8.webp`)

```
Quartzite, a cluster of milky white and translucent quartz crystals and rounded pale stone. Palette: milky white, pale blue. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**9. Обсидіан** — Obsidian (`resource_9`, файл `resource_9.webp`)

```
Obsidian, sharp black volcanic glass shards with glossy edges that reflect a thin cyan light. Palette: glossy black, cyan reflections. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**10. Лінтрій** — Lintrium (`resource_10`, файл `resource_10.webp`)

```
Lintrium, a soft fibrous turquoise mineral that looks like bundled stone threads with a pearly sheen. Palette: turquoise, pearl. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**11. Орекс** — Orex (`resource_11`, файл `resource_11.webp`)

```
Orex, rough orange-red translucent mineral nuggets, warm like amber with darker inclusions. Palette: amber orange, deep red. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Пальне

**12. Дейтерій** — Deuterium (`resource_12`, файл `resource_12.webp`)

```
Deuterium fuel, a transparent canister of cold blue-white liquid with glowing droplets and frosty vapor. Palette: ice blue, white. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**13. Гелій-3** — Helium-3 (`resource_13`, файл `resource_13.webp`)

```
Helium-3 fuel, a glass flask with a slow pale pink-gold gas swirling inside and tiny sparks. Palette: pale pink, soft gold. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**14. Темна матерія** — Dark matter (`resource_14`, файл `resource_14.webp`)

```
Dark matter, a perfectly black floating sphere with a thin violet halo that bends the stars behind it. Palette: black, violet rim. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**15. Ефір** — Ether (`resource_15`, файл `resource_15.webp`)

```
Ether, a golden luminous mist that gathers into a glowing cloud with fine bright threads. Palette: gold, warm white. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**16. Збагачений Уран** — Enriched uranium (`resource_16`, файл `resource_16.webp`)

```
Enriched uranium, a rack of neat glowing green fuel rods in a metal cradle with a soft toxic glow. Palette: bright green, dark steel. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Кристали

**17. Квартан** — Quartan (`resource_17`, файл `resource_17.webp`)

```
Quartan crystals, a cluster of clean hexagonal blue crystals with sharp facets and an inner glow. Palette: electric blue, white. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**18. Стобій** — Stobium (`resource_18`, файл `resource_18.webp`)

```
Stobium crystals, tall thin red-violet spikes that grow from a dark base and shimmer at the tips. Palette: ruby red, violet. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**19. Туманій** — Tumanium (`resource_19`, файл `resource_19.webp`)

```
Tumanium crystals, pale lilac and white misty crystal pillars with fog drifting between them. Palette: lilac, mist white. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Вироблені ресурси

**20. Будматеріали** — Building materials (`resource_20`, файл `resource_20.webp`)

```
Building materials, a neat stack of concrete slabs, steel beams and rebar bundles with yellow edge marks. Palette: concrete grey, steel, safety yellow. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**21. Оптоволокно** — Optical fiber (`resource_21`, файл `resource_21.webp`)

```
Optical fiber, a large spool wound with hair-thin fibers that carry running points of cyan and magenta light. Palette: cyan, magenta, black spool. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**22. Мікропроцесори** — Microprocessors (`resource_22`, файл `resource_22.webp`)

```
Microprocessors, several square chips on a dark board with gold pins and fine glowing circuit traces. Palette: silicon grey, gold, cyan traces. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**23. Композити** — Composites (`resource_23`, файл `resource_23.webp`)

```
Composites, layered sheets of woven dark carbon fabric and pale resin, fanned out like a stack of panels. Palette: carbon black, pale resin. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**24. Енергострижні** — Energy rods (`resource_24`, файл `resource_24.webp`)

```
Energy rods, a rack of glowing yellow-cyan rods with metal caps, light flowing along each rod. Palette: yellow, cyan, dark steel. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**25. Нановолокно** — Nanofiber (`resource_25`, файл `resource_25.webp`)

```
Nanofiber, a skein of shimmering ultra-fine threads that catch light in silver and violet. Palette: silver, violet shimmer. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**26. Титановий сплав** — Titanium alloy (`resource_26`, файл `resource_26.webp`)

```
Titanium alloy, three heavy ingots with stamped hexagon marks and a cool blue-silver metallic finish. Palette: blue silver, graphite. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Баланси цивілізації

**27. Кредити** — Credits (`resource_27`, файл `resource_27.webp`)

```
Credits, a small stack of real-looking metal coins with an embossed hexagon pattern and a bright teal edge reflection, shallow depth of field. Palette: teal, brushed silver. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**28. Торгова валюта** — Trade currency (`resource_28`, файл `resource_28.webp`)

```
Trade currency, a violet-anodized metal exchange token with an engraved pair of arrows on its face, resting on a dark surface with a soft purple reflection. Palette: violet, lilac glow. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**29. Енергія** — Energy (`resource_29`, файл `resource_29.webp`)

```
Energy, a heavy industrial capacitor bank with a bright yellow plasma arc between two electrodes and real cables, a glowing spark shower. Palette: bright yellow, white. A photorealistic cinematic scene with believable real-world detail, one clear focal point, soft rim light and atmospheric depth. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**30. Населення** — Population (`resource_30`, файл `resource_30.webp`)

```
Population, a realistic crowd of ordinary colonists in a plaza at dusk with a lit habitat dome behind them, seen from a slightly lowered viewpoint. Palette: warm white, soft teal. A photorealistic cinematic scene with believable real-world detail, one clear focal point, soft rim light and atmospheric depth. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**31. Реальна валюта** — Premium currency (`resource_31`, файл `resource_31.webp`)

```
Premium currency, a rare faceted gold-and-crystal token like a precious gem, realistic reflections and sparkles on a dark velvet-like surface. Palette: gold, white sparkle. A photorealistic macro product shot of a single subject centered on a dark rocky or metallic surface, studio lighting with a soft rim light, realistic textures such as crystal facets, rock grain and metal wear, shallow depth of field. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

## 2. Будівлі

Будівлі (56) за категоріями гри.

### Головні й населення

**1. Адміністративний центр** — Administrative center (`building_1`, файл `building_1.webp`)

```
A broad administrative center with a central tower, glass wings and the flag mast, the seat of the whole colony. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**2. Житловий комплекс (Міста)** — Housing complex (`building_2`, файл `building_2.webp`)

```
A housing complex of tall residential blocks connected by sky bridges, hundreds of lit windows. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**3. Медичний центр** — Medical center (`building_3`, файл `building_3.webp`)

```
A medical center, a clean white building with a glowing cross-shaped atrium and a landing pad for ambulances. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**4. Банк** — Bank (`building_4`, файл `building_4.webp`)

```
A bank, a heavy dark building with a golden vault door visible through a glass facade and a ring of light above it. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**5. Будівельний комбінат** — Construction plant (`building_5`, файл `building_5.webp`)

```
A construction plant, cranes and gantries over a half-finished modular building, welding sparks. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**6. Космопорт** — Spaceport (`building_6`, файл `building_6.webp`)

```
A spaceport, wide landing fields with docked transport ships and a control tower. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Склади

**7. Склад** — Warehouse (`building_7`, файл `building_7.webp`)

```
A warehouse, a long hangar with wide doors, stacked cargo containers outside. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**8. Бункер** — Bunker (`building_8`, файл `building_8.webp`)

```
A bunker, a low reinforced dome half buried in the ground with thick armored doors and a small beacon. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Наука

**9. Науковий центр** — Science center (`building_9`, файл `building_9.webp`)

```
A science center, a round building with a rotating antenna dish and glowing laboratory windows. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**10. Конструкторське бюро** — Design bureau (`building_10`, файл `building_10.webp`)

```
A design bureau, an angular studio building with a large holographic blueprint of a ship floating in its atrium. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**11. Археологічний центр** — Archaeology center (`building_11`, файл `building_11.webp`)

```
An archaeology center, an excavation site with a lit tent complex and an ancient alien relic being lifted from the ground. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Видобуток на планеті

**12. Шахта залізної руди** — Iron ore mine (`building_12`, файл `building_12.webp`)

```
An iron ore mine, an open pit with a headframe tower and a conveyor, red-brown rock walls. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**13. Шахта титанової руди** — Titanium ore mine (`building_13`, файл `building_13.webp`)

```
A titanium ore mine, an excavation with silver-blue ore walls, drills and a loading crane. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**14. Шахта іридієвої руди** — Iridium ore mine (`building_14`, файл `building_14.webp`)

```
An iridium ore mine, a deep shaft with a pale-gold glint in the rock and a delicate extraction gantry. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**15. Шахта кобальтової руди** — Cobalt ore mine (`building_15`, файл `building_15.webp`)

```
A cobalt ore mine, a pit cut in deep blue rock with ore carts on rails. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**16. Шахта уранової руди** — Uranium ore mine (`building_16`, файл `building_16.webp`)

```
A uranium ore mine, a sealed shaft with warning lights, sheathed tunnels and a green glow from the rock. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**17. Шахта силікорової руди** — Silicore mine (`building_17`, файл `building_17.webp`)

```
A silicore mine, a terraced cut in grey-violet rock with precise cutting machines. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**18. Шахта тектонітової руди** — Tectonite ore mine (`building_18`, файл `building_18.webp`)

```
A tectonite ore mine, a mine in black basalt with glowing orange fissures and heat-shielded machines. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**19. Кар'єр кварциту** — Quartzite quarry (`building_19`, файл `building_19.webp`)

```
A quartzite quarry, a pale stepped quarry with cutting rigs and stacked white blocks. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**20. Кар'єр обсидіану** — Obsidian quarry (`building_20`, файл `building_20.webp`)

```
An obsidian quarry, a quarry of black glassy rock with sharp edges and a crane lifting a slab. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**21. Кар'єр лінтрію** — Lintrium quarry (`building_21`, файл `building_21.webp`)

```
A lintrium quarry, a turquoise fibrous rock face with long extraction arms. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**22. Кар'єр орексу** — Orex quarry (`building_22`, файл `building_22.webp`)

```
An orex quarry, an orange-red rock quarry with warm lights and haul trucks. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**23. Копальня квартану** — Quartan mine (`building_23`, файл `building_23.webp`)

```
A quartan mine, a cavern mouth lined with large blue crystals and light rails. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**24. Копальня стобію** — Stobium mine (`building_24`, файл `building_24.webp`)

```
A stobium mine, a shaft among tall red-violet crystal spikes with careful extraction tools. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**25. Копальня туманію** — Tumanium mine (`building_25`, файл `building_25.webp`)

```
A tumanium mine, a misty crater with pale lilac crystal pillars and drifting fog around the equipment. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**26. Станція дейтерію** — Deuterium station (`building_26`, файл `building_26.webp`)

```
A deuterium station, tall tanks and pipes on the shore of an ice-blue sea, vapor rising. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Енергетика

**27. Сонячна електростанція** — Solar power plant (`building_27`, файл `building_27.webp`)

```
A solar power plant, rows of large mirrored panels turned to a bright star, light flares on the glass. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**28. Теплова електростанція** — Thermal power plant (`building_28`, файл `building_28.webp`)

```
A thermal power plant, a heavy plant with big chimneys and glowing pipes, warm steam. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**29. Ядерна електростанція** — Nuclear power plant (`building_29`, файл `building_29.webp`)

```
A nuclear power plant, a containment dome with cooling towers and a soft blue glow from a service door. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**30. Термоядерна електростанція** — Fusion power plant (`building_30`, файл `building_30.webp`)

```
A fusion power plant, a huge ring reactor hall with a bright white-blue plasma core visible through the roof. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Виробництво ресурсів

**31. Завод будматеріалів** — Building-materials plant (`building_31`, файл `building_31.webp`)

```
A building-materials plant, mixers, silos and stacks of finished concrete slabs on a production line. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**32. Оптоволоконний завод** — Optical-fiber plant (`building_32`, файл `building_32.webp`)

```
An optical-fiber plant, a bright hall with long drawing towers and glowing fibers falling like threads of light. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**33. Фабрика мікропроцесорів** — Microprocessor factory (`building_33`, файл `building_33.webp`)

```
A microprocessor factory, a clean-room building with a glass front, wafer robots and cyan light. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**34. Завод композитів** — Composites plant (`building_34`, файл `building_34.webp`)

```
A composites plant, a long hall with autoclaves and rolls of dark carbon fabric being laid by arms. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**35. Завод енергострижнів** — Energy-rod plant (`building_35`, файл `building_35.webp`)

```
An energy-rod plant, a facility with charging towers where glowing rods are lowered into cradles. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**36. Завод нановолокна** — Nanofiber plant (`building_36`, файл `building_36.webp`)

```
A nanofiber plant, a compact tall plant with fine shimmering threads spun between vertical frames. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**37. Металургійний завод** — Metallurgical plant (`building_37`, файл `building_37.webp`)

```
A metallurgical plant, blast furnaces, a glowing ladle pouring molten metal and sparks in the dark. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**38. Завод збагачення урану** — Uranium enrichment plant (`building_38`, файл `building_38.webp`)

```
A uranium enrichment plant, rows of centrifuge halls behind security fences with a green glow. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**39. Перетворювач матерії** — Matter converter (`building_39`, файл `building_39.webp`)

```
A matter converter, a strange ring machine where a beam turns raw stone into glowing particles of other matter. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Виробництво модулів

**40. Фабрика двигунів** — Engine factory (`building_40`, файл `building_40.webp`)

```
An engine factory, a plant with test stands where a bright ion exhaust flares from a mounted engine. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**41. Завод реакторів** — Reactor plant (`building_41`, файл `building_41.webp`)

```
A reactor plant, an assembly hall with a half-built reactor core on a crane and blue arc light. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**42. Завод енергетичної зброї** — Energy-weapon plant (`building_42`, файл `building_42.webp`)

```
An energy-weapon plant, a factory with laser barrels on racks and a test range lit by a red beam. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**43. Завод кінетичної зброї** — Kinetic-weapon plant (`building_43`, файл `building_43.webp`)

```
A kinetic-weapon plant, a gun foundry with cannon barrels, shell conveyors and sparks. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**44. Ливарний завод** — Foundry (`building_44`, файл `building_44.webp`)

```
A foundry, a huge casting hall pouring bright metal into molds, heavy cranes and fiery glow. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**45. Завод щитів** — Shield plant (`building_45`, файл `building_45.webp`)

```
A shield plant, a hall with emitter rings where a shimmering energy dome is being tested. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**46. Фабрика боєприпасів** — Ammunition factory (`building_46`, файл `building_46.webp`)

```
An ammunition factory, assembly lines with missiles and shells under protective shields and warning lamps. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**47. Фабрика бурових установок** — Drill factory (`building_47`, файл `building_47.webp`)

```
A drill factory, big industrial drill bits and rigs being assembled under gantries. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**48. Завод точних приладів** — Precision-instrument plant (`building_48`, файл `building_48.webp`)

```
A precision-instrument plant, a bright laboratory-factory with tiny robotic arms and glass cabinets. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**49. Індустріальний комплекс** — Industrial complex (`building_49`, файл `building_49.webp`)

```
An industrial complex, a dense sprawl of chimneys, pipes, hangars and cranes under a hazy sky. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**50. Фабрика утилізації** — Recycling factory (`building_50`, файл `building_50.webp`)

```
A recycling factory, scrap heaps being fed into sorters, green status lights and falling metal sparks. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Флот і оборона

**51. Верф** — Shipyard (`building_51`, файл `building_51.webp`)

```
A shipyard, a huge dry dock with a half-built starship in a scaffolding frame and welding light. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**52. Радар** — Radar (`building_52`, файл `building_52.webp`)

```
A radar, a big dish antenna on a tower with a faint scanning beam sweeping the dark sky. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**53. Планетарна гармата** — Planetary gun (`building_53`, файл `building_53.webp`)

```
A planetary gun, a massive turret on a fortified base aiming at the sky, a muzzle glow ready. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**54. Планетарний щит** — Planetary shield (`building_54`, файл `building_54.webp`)

```
A planetary shield, a ring of tall emitter pylons raising a translucent teal dome over a small settlement. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Торгівля й логістика

**55. Торговий центр** — Trade center (`building_55`, файл `building_55.webp`)

```
A trade center, a modern market hall with glowing sign boards, cargo drones and a crowded square. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**56. Портал** — Portal (`building_56`, файл `building_56.webp`)

```
A portal, a large circular gate standing on a platform with a bright blue swirling opening inside. Photorealistic concept art, a wide establishing view of the building on an alien planet at dusk, believable real-world engineering and architecture, small people-sized details for scale, atmospheric haze, one clear main structure. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

## 3. Дослідження

Технології (20): значок-емблема кожного дослідження.

### Фундаментальні науки

**1. Математика** — Mathematics (`tech_1`, файл `tech_1.webp`)

```
An emblem illustration for the research "Mathematics". Mathematics: a real research hall with a large holographic display of equations and geometric solids, a physical brass-and-glass model of a polyhedron on the desk in the foreground. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**2. Фізика** — Physics (`tech_2`, файл `tech_2.webp`)

```
An emblem illustration for the research "Physics". Physics: a physics laboratory with a glass prism splitting a laser beam into a clean spectrum on a dark optical table, a pendulum and instruments around it. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**3. Ядерна фізика** — Nuclear physics (`tech_3`, файл `tech_3.webp`)

```
An emblem illustration for the research "Nuclear physics". Nuclear physics: a cutaway nuclear research lab, a shielded reactor test cell behind thick glass with a faint blue Cherenkov glow and technicians' instruments in front. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**4. Термоядерний синтез** — Thermonuclear fusion (`tech_4`, файл `tech_4.webp`)

```
An emblem illustration for the research "Thermonuclear fusion". Thermonuclear fusion: the inside of a tokamak-like fusion chamber with a ring of superconducting coils and a thin bright plasma filament in the middle. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**5. Геологія** — Geology (`tech_5`, файл `tech_5.webp`)

```
An emblem illustration for the research "Geology". Geology: a core-drilling site with a cylindrical rock core cut open on a table, colorful mineral layers, hand tools and a handheld scanner. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**6. Електроніка** — Electronics (`tech_6`, файл `tech_6.webp`)

```
An emblem illustration for the research "Electronics". Electronics: a macro shot of a real circuit board with soldered components and fine copper traces, a faint cyan light along the traces and an oscilloscope behind it. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**7. Енергетика** — Power engineering (`tech_7`, файл `tech_7.webp`)

```
An emblem illustration for the research "Power engineering". Power engineering: a high-voltage substation at night with transformers, insulators and a crackling arc between two conductors. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**8. Будівництво** — Construction (`tech_8`, файл `tech_8.webp`)

```
An emblem illustration for the research "Construction". Construction: a building site at dusk with a tower crane lifting a prefabricated module, steel frames, welding sparks and engineers with a tablet blueprint. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**9. Кібернетика** — Cybernetics (`tech_9`, файл `tech_9.webp`)

```
An emblem illustration for the research "Cybernetics". Cybernetics: a realistic prosthetic robotic hand with fine servo joints beside a human hand, a medical lab with neural interface cables and soft screens. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**10. Робототехніка** — Robotics (`tech_10`, файл `tech_10.webp`)

```
An emblem illustration for the research "Robotics". Robotics: an industrial robotic arm assembling a small part in a clean factory cell, realistic joints, cables and a bright work light. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**11. Економіка** — Economics (`tech_11`, файл `tech_11.webp`)

```
An emblem illustration for the research "Economics". Economics: a trading floor in a space colony, rows of live market screens with charts, a glass wall showing a docked freighter outside. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**12. Археологія** — Archaeology (`tech_12`, файл `tech_12.webp`)

```
An emblem illustration for the research "Archaeology". Archaeology: an excavation of an ancient alien structure in dusty ground, a half-uncovered carved relic, brushes, a scanner and portable lamps. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**13. Перетворення матерії** — Matter conversion (`tech_18`, файл `tech_18.webp`)

```
An emblem illustration for the research "Matter conversion". Matter conversion: a laboratory chamber where a focused beam transforms a rough stone sample into a gleaming different material, particles visible in a vacuum window. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Економіка

**14. Кристалографія** — Crystallography (`tech_13`, файл `tech_13.webp`)

```
An emblem illustration for the research "Crystallography". Crystallography: a lab bench with a large natural crystal cluster, a laser beam passing through it and splitting into fine lines, a microscope beside it. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**15. Газова інженерія** — Gas engineering (`tech_14`, файл `tech_14.webp`)

```
An emblem illustration for the research "Gas engineering". Gas engineering: an industrial gas processing site with pressure tanks, gauges, insulated pipes and a plume of vapor in cold light. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**16. Логістика складів** — Warehouse logistics (`tech_15`, файл `tech_15.webp`)

```
An emblem illustration for the research "Warehouse logistics". Warehouse logistics: a vast automated warehouse with tall racks, autonomous loaders and a conveyor carrying containers under bright lines of light. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**17. Демографія** — Demography (`tech_16`, файл `tech_16.webp`)

```
An emblem illustration for the research "Demography". Demography: a busy colony plaza at evening with many people of all ages, apartment blocks around it and warm windows, a census terminal in the foreground. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**18. Колоніальне управління** — Colonial administration (`tech_17`, файл `tech_17.webp`)

```
An emblem illustration for the research "Colonial administration". Colonial administration: a colony command room with a large wall map of a planet showing settlements, officers at consoles and a flag on the wall. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**19. Торговельне право** — Trade law (`tech_19`, файл `tech_19.webp`)

```
An emblem illustration for the research "Trade law". Trade law: a wooden-and-metal courtroom desk with a brass scale, a sealed contract and a stack of coins under warm lamp light, a city window behind. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**20. Стабілізація порталу** — Portal stabilization (`tech_20`, файл `tech_20.webp`)

```
An emblem illustration for the research "Portal stabilization". Portal stabilization: a control platform with large instrument racks aiming four anchor beams at a ring-shaped portal, cables on the floor and technicians' silhouettes. A realistic scientific scene or a physically plausible holographic projection rendered with real materials and lighting, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

## 4. Модулі

Модулі кораблів і орбітальної бази (83), разом із чотирма кріокапсулами рас.

### Двигуни

**1. Реактивний двигун** — Jet engine (`part_m_1`, файл `part_m_1.webp`)

```
A jet engine, a compact metal nozzle with a bright orange exhaust flame and heat shimmer. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**2. Плазмовий двигун** — Plasma engine (`part_m_2`, файл `part_m_2.webp`)

```
A plasma engine, a wide nozzle with a magnetic ring and a violet-white plasma jet. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**3. Іонний двигун** — Ion engine (`part_m_3`, файл `part_m_3.webp`)

```
An ion engine, a long slim engine with grid rings and a thin cold blue ion beam. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**4. Гравітаційний двигун** — Gravity engine (`part_m_4`, файл `part_m_4.webp`)

```
A gravity engine, a spherical drive floating inside rings that bend the light around it. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**5. Гіпердвигун** — Hyperdrive (`part_m_5`, файл `part_m_5.webp`)

```
A hyperdrive, a sleek engine with a glowing spiral core and streaking stars behind it. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**6. Джамп-двигун** — Jump drive (`part_m_6`, файл `part_m_6.webp`)

```
A jump drive, a ring-shaped drive with a bright tear in space opening in its center. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Реактори

**7. Ядерний реактор** — Nuclear reactor (`part_m_7`, файл `part_m_7.webp`)

```
A nuclear reactor, a compact heavy core with cooling fins and a steady blue glow. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**8. Імпульсний реактор** — Pulse reactor (`part_m_8`, файл `part_m_8.webp`)

```
A pulse reactor, a reactor that flashes rhythmic bright pulses along its coils. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**9. Термоядерний реактор** — Fusion reactor (`part_m_9`, файл `part_m_9.webp`)

```
A fusion reactor, a toroidal ring with a bright white plasma line in the middle. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**10. Реактор холодного синтезу** — Cold-fusion reactor (`part_m_10`, файл `part_m_10.webp`)

```
A cold-fusion reactor, a clean frosted capsule reactor with a calm cyan light and ice crystals on it. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**11. Гравітаційний реактор** — Gravity reactor (`part_m_11`, файл `part_m_11.webp`)

```
A gravity reactor, a dark sphere held by rings, small bright matter streams spiraling in. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**12. Важкий реактор** — Heavy reactor (`part_m_12`, файл `part_m_12.webp`)

```
A heavy reactor, a massive armored reactor block with thick pipes and a hot orange core. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Енергетична зброя

**13. Легкий лазер** — Light laser (`part_m_13`, файл `part_m_13.webp`)

```
A light laser, a slim turret with a thin red beam. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**14. Важкий лазер** — Heavy laser (`part_m_14`, файл `part_m_14.webp`)

```
A heavy laser, a large turret with a long barrel and a thick red-orange beam. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**15. Імпульсний лазер** — Pulse laser (`part_m_15`, файл `part_m_15.webp`)

```
A pulse laser, a turret firing a train of short bright red pulses. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**16. Плазмова гармата** — Plasma cannon (`part_m_16`, файл `part_m_16.webp`)

```
A plasma cannon, a thick barrel launching a glowing green-white plasma bolt. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**17. Іонна гармата** — Ion cannon (`part_m_17`, файл `part_m_17.webp`)

```
An ion cannon, a ringed barrel emitting a crackling blue ion arc. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**18. Мікрохвильовий випромінювач** — Microwave emitter (`part_m_18`, файл `part_m_18.webp`)

```
A microwave emitter, a dish emitter with faint rippling waves of heat in the air. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**19. Частинковий прискорювач** — Particle accelerator (`part_m_19`, файл `part_m_19.webp`)

```
A particle accelerator, a long ring cannon with bright particle streams circulating inside. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**20. Орексова гармата** — Orex cannon (`part_m_20`, файл `part_m_20.webp`)

```
An orex cannon, a heavy cannon fed by orange-red orex crystals that glow in its breech. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Кінетична зброя

**21. Кінетична гармата** — Kinetic cannon (`part_m_21`, файл `part_m_21.webp`)

```
A kinetic cannon, a sturdy steel gun with a muzzle flash. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**22. Скорострільна гармата** — Autocannon (`part_m_22`, файл `part_m_22.webp`)

```
An autocannon, a multi-barrel rotary cannon with a stream of tracer rounds. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**23. Рейкова гармата** — Railgun (`part_m_23`, файл `part_m_23.webp`)

```
A railgun, a long twin-rail gun with blue sparks running along the rails. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**24. РПУ-X1** — Launcher X1 (`part_m_24`, файл `part_m_24.webp`)

```
A missile launcher X1, a small launcher rack with a single missile ready. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**24a. РПУ-X1B** — Launcher X1B (`part_m_84`, файл `part_111.webp`): власна картинка автора (04.11.2026), промпт не потрібен.

**25. РПУ-X2** — Launcher X2 (`part_m_25`, файл `part_m_25.webp`)

```
A missile launcher X2, a medium launcher with two missiles in side-by-side tubes. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**26. РПУ-X3** — Launcher X3 (`part_m_26`, файл `part_m_26.webp`)

```
A missile launcher X3, a large launcher with three heavy missiles in a tube cluster. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Броня

**27. Сталева плита** — Steel plate (`part_m_27`, файл `part_m_27.webp`)

```
A steel plate, a thick dull steel armor plate with bolts and scratches. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**28. Сегментна броня** — Segmented armor (`part_m_28`, файл `part_m_28.webp`)

```
Segmented armor, overlapping armor segments like a shell, each with a small highlight. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**29. Кобальтова броня** — Cobalt armor (`part_m_29`, файл `part_m_29.webp`)

```
Cobalt armor, deep blue armor plates with a glossy finish. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**30. Титанова плита** — Titanium plate (`part_m_30`, файл `part_m_30.webp`)

```
A titanium plate, a clean silver-blue plate with a hexagon pattern. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**31. Композитна броня** — Composite armor (`part_m_31`, файл `part_m_31.webp`)

```
Composite armor, layered dark composite panels with visible weave. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**32. Іридієва плита** — Iridium plate (`part_m_32`, файл `part_m_32.webp`)

```
An iridium plate, a bright pale-gold plate that looks very dense and polished. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**33. Наноплита** — Nanoplate (`part_m_33`, файл `part_m_33.webp`)

```
A nanoplate, a plate with a faintly shimmering nano-lattice surface that seems to move. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**34. Адаптивна броня** — Adaptive armor (`part_m_34`, файл `part_m_34.webp`)

```
Adaptive armor, plates in motion that shift color toward the threat, with soft color ripples. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Щити

**35. Силовий щит** — Force shield (`part_m_35`, файл `part_m_35.webp`)

```
A force shield, a shield emitter projecting a translucent teal dome. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**36. Кварцовий щит** — Quartz shield (`part_m_36`, файл `part_m_36.webp`)

```
A quartz shield, a shield made of clear quartz facets that refract light. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**37. Магнітний щит** — Magnetic shield (`part_m_37`, файл `part_m_37.webp`)

```
A magnetic shield, an emitter with visible curved field lines in blue and violet. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**38. Гравітаційний щит** — Gravity shield (`part_m_38`, файл `part_m_38.webp`)

```
A gravity shield, a dark sphere surrounded by bent light and orbiting small debris. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**39. Нейтронний щит** — Neutron shield (`part_m_39`, файл `part_m_39.webp`)

```
A neutron shield, a dense white-grey shield surface with fine particle sparks. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**40. Плазмовий щит** — Plasma shield (`part_m_40`, файл `part_m_40.webp`)

```
A plasma shield, a shield glowing like hot plasma in pink and white. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**41. Орексовий щит** — Orex shield (`part_m_41`, файл `part_m_41.webp`)

```
An orex shield, an orange-red crystal-shaped energy shield around an orex core. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**42. Ефірний щит** — Ether shield (`part_m_42`, файл `part_m_42.webp`)

```
An ether shield, a golden luminous veil of ether with soft waves. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Боєприпаси

**43. Сталевий снаряд** — Steel shell (`part_m_43`, файл `part_m_43.webp`)

```
A steel shell, a plain steel artillery shell on a stand. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**44. Кобальтовий снаряд** — Cobalt shell (`part_m_44`, файл `part_m_44.webp`)

```
A cobalt shell, a deep-blue shell with a sharp tip. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**45. Рейковий стрижень** — Rail slug (`part_m_45`, файл `part_m_45.webp`)

```
A rail slug, a dense metal slug with a glowing blue trail. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**46. Стріла-X1** — Arrow X1 (`part_m_46`, файл `part_m_46.webp`)

```
Arrow X1 missile, a small slim missile with small fins and an orange exhaust. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**47. Блискавка-X1** — Lightning X1 (`part_m_47`, файл `part_m_47.webp`)

```
Lightning X1 missile, a fast missile surrounded by crackling electric arcs. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**48. Спис-X2** — Spear X2 (`part_m_48`, файл `part_m_48.webp`)

```
Spear X2 missile, a long armor-piercing missile with a sharp needle nose. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**49. Валькірія-X3** — Valkyrie X3 (`part_m_49`, файл `part_m_49.webp`)

```
Valkyrie X3 missile, a large heavy missile with winged fins and a white-gold exhaust. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**50. Термоядерна торпеда** — Fusion torpedo (`part_m_50`, файл `part_m_50.webp`)

```
A fusion torpedo, a large torpedo with a glowing fusion core visible along its body. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**51. Протонна торпеда** — Proton torpedo (`part_m_51`, файл `part_m_51.webp`)

```
A proton torpedo, a torpedo with a pulsing bright blue-white proton warhead. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**52. Уранова торпеда** — Uranium torpedo (`part_m_52`, файл `part_m_52.webp`)

```
A uranium torpedo, a heavy dark torpedo with green glowing warhead sections. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Видобуток

**53. Екстрактор гелію** — Helium extractor (`part_m_53`, файл `part_m_53.webp`)

```
A helium extractor, a heavy cryogenic intake manifold with frosted cold-blue condenser coils and a glowing pale-blue helium tank. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**54. Бурова установка** — Drill rig (`part_m_54`, файл `part_m_54.webp`)

```
A drill rig, a robust drilling head with rotating teeth and sparks. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**55. Кристалічний екстрактор** — Crystal extractor (`part_m_55`, файл `part_m_55.webp`)

```
A crystal extractor, a precise claw-and-laser tool holding a glowing blue crystal. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**56. Екстрактор ефіру** — Ether extractor (`part_m_56`, файл `part_m_56.webp`)

```
An ether extractor, a funnel-shaped collector drawing golden ether mist into a container. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**57. Екстрактор матерії** — Matter extractor (`part_m_57`, файл `part_m_57.webp`)

```
A matter extractor, a wide net-like scoop trailing violet-teal nebula gas. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Розвідка й маскування

**58. Астросканер** — Astroscanner (`part_m_58`, файл `part_m_58.webp`)

```
An astroscanner, a telescopic scanner with a lens array and faint star maps projected in front of it. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**59. Корабельний радар** — Ship radar (`part_m_59`, файл `part_m_59.webp`)

```
A ship radar, a rotating compact radar dish with a sweeping green-cyan beam. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**60. Археологічний модуль** — Archaeology module (`part_m_60`, файл `part_m_60.webp`)

```
An archaeology module, a delicate scanning arm brushing an ancient relic with soft light. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**61. Радіооптичне маскування** — Radio-optical camouflage (`part_m_61`, файл `part_m_61.webp`)

```
Radio-optical camouflage, a ship panel half transparent, bending the background light around it. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**62. Детектор маскування** — Camouflage detector (`part_m_62`, файл `part_m_62.webp`)

```
A camouflage detector, a sensor head with several lenses revealing a hidden ghost outline. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Інші корабельні

**63. Відновлювач щитів** — Shield restorer (`part_m_63`, файл `part_m_63.webp`)

```
A shield restorer, a module with a glowing teal coil that rebuilds a broken shield ring. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**64. Бортовий комп'ютер** — Onboard computer (`part_m_64`, файл `part_m_64.webp`)

```
An onboard computer, a rack of glowing processor blocks and a small holographic display. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**65. Збирач уламків** — Debris collector (`part_m_65`, файл `part_m_65.webp`)

```
A debris collector, a magnetic scoop pulling small wreckage into a bin. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**66. Вантажний відсік** — Cargo hold (`part_m_66`, файл `part_m_66.webp`)

```
A cargo hold, a boxy container module with a open door and crates inside. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**67. Великий вантажний відсік** — Large cargo hold (`part_m_67`, файл `part_m_67.webp`)

```
A large cargo hold, a very big container module with sliding doors and rows of crates. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**68. Протиракетна система** — Missile-defence system (`part_m_68`, файл `part_m_68.webp`)

```
A missile-defence system, a turret with small quick guns and tracking beams aimed at incoming missiles. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**69. Модуль передачі** — Transfer module (`part_m_69`, файл `part_m_69.webp`)

```
A transfer module, a docking tube with a glowing connector passing cargo between two ships. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**70. Колоніальний модуль** — Colony module (`part_m_70`, файл `part_m_70.webp`)

```
A colony module, a sealed capsule with a landing frame and a small green seedling dome. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**71. Кріокапсула (Террани)** — Cryocapsule (Terrans) (`part_m_80`, файл `part_m_80.webp`)

```
A cryocapsule of the Terrans, a sleek steel-grey and blue capsule with a frosted window showing sleeping human figures, a small blue status light. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**72. Кріокапсула (Літаріони)** — Cryocapsule (Litari) (`part_m_81`, файл `part_m_81.webp`)

```
A cryocapsule of the Litari, an icy cyan and white crystalline capsule with a frosted window showing sleeping figures, glittering frost. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**73. Кріокапсула (Драккури)** — Cryocapsule (Drakkuri) (`part_m_82`, файл `part_m_82.webp`)

```
A cryocapsule of the Drakkuri, a dark capsule with red-orange ember lines and a window showing sleeping reptilian figures, warm glow. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**74. Кріокапсула (Вейрани)** — Cryocapsule (Veirani) (`part_m_83`, файл `part_m_83.webp`)

```
A cryocapsule of the Veirani, a sand-amber capsule with elegant curves and a window showing sleeping tall figures, golden light. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Модулі бази

**75. Житловий модуль** — Housing module (`part_m_71`, файл `part_m_71.webp`)

```
A housing module, a stacked living module with rows of small lit windows. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**76. Орбітальний склад** — Orbital warehouse (`part_m_72`, файл `part_m_72.webp`)

```
An orbital warehouse, a station module with big docking arms and stacked containers. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**77. Ремонтні майстерні** — Repair workshops (`part_m_73`, файл `part_m_73.webp`)

```
Repair workshops, a station bay with robotic arms welding a damaged armor plate. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**78. Школа командирів** — Commander school (`part_m_74`, файл `part_m_74.webp`)

```
A commander school, a training module with a holographic tactical map table and cadet silhouettes. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**79. Торговий центр** — Trade center (`part_m_75`, файл `part_m_75.webp`)

```
A trade center module, a station market ring with glowing signs and docked cargo ships. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**80. Орбітальний термоядерний реактор** — Orbital fusion reactor (`part_m_76`, файл `part_m_76.webp`)

```
An orbital fusion reactor, a big station fusion ring with a bright core and cooling panels. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**81. Збагачувач ізотопів гелію** — Helium-isotope enricher (`part_m_77`, файл `part_m_77.webp`)

```
A helium-isotope enricher, a station module of spinning centrifuges and glass tanks of pale gas. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**82. Екстрактор темної матерії** — Dark-matter extractor (`part_m_78`, файл `part_m_78.webp`)

```
A dark-matter extractor, a heavy station module with a containment ring around a black sphere. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**83. Джамп-модуль** — Jump module (`part_m_79`, файл `part_m_79.webp`)

```
A jump module, a station gate frame with a bright rift forming in the middle. A hard-surface photorealistic render of a single ship module shown alone on a dark pedestal, three-quarter view, real metal, panel lines, bolts, cables and slight wear, soft rim light, no ship hull around it. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

## 5. Корпуси (додатково)

Корпуси кораблів і станцій (27). Їх не було в переліку автора, додано, бо їм теж потрібні картинки; за потреби розділ можна не генерувати.

### Легкі

**1. Винищувач «Сапсан»** — Fighter Sapsan (`part_h_1`, файл `part_h_1.webp`)

```
A small agile fighter, a sharp single-seat starfighter with swept wings in flight. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**2. Важкий винищувач «Шершень»** — Heavy fighter Hornet (`part_h_2`, файл `part_h_2.webp`)

```
A heavy fighter, a sturdy armored starfighter with twin engines and heavy guns. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**3. Торпедоносець «Гадюка»** — Torpedo bomber Viper (`part_h_3`, файл `part_h_3.webp`)

```
A torpedo bomber, a compact bomber with a big torpedo slung under its belly. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**4. Важкий торпедоносець «Кобра»** — Heavy torpedo bomber Cobra (`part_h_4`, файл `part_h_4.webp`)

```
A heavy torpedo bomber, a wide bomber with two large torpedoes and thick armor. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Дрони

**5. «Ґедзь»** — Gadfly (`part_h_5`, файл `part_h_5.webp`)

```
A tiny gadfly-like drone craft with thin wings, quick and light. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**6. «Цикада»** — Cicada (`part_h_6`, файл `part_h_6.webp`)

```
A cicada-like scout craft with a long body and a pair of lifted wing panels. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**7. «Кліщ»** — Tick (`part_h_7`, файл `part_h_7.webp`)

```
A tick-like boarding craft, a low round body with grabbing clamps. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Середні

**8. Корвет «Коршун»** — Corvette Kite (`part_h_8`, файл `part_h_8.webp`)

```
A corvette, a small fast warship with a clean hull and a ring of turrets. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**9. Фрегат «Барракуда»** — Frigate Barracuda (`part_h_9`, файл `part_h_9.webp`)

```
A frigate, a long slim warship with a row of turrets and a bright engine block. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**10. Есмінець «Акула»** — Destroyer Shark (`part_h_10`, файл `part_h_10.webp`)

```
A destroyer, a sharp-nosed warship with a wedge hull and rows of weapons. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**11. Легкий крейсер «Ягуар»** — Light cruiser Jaguar (`part_h_11`, файл `part_h_11.webp`)

```
A light cruiser, a balanced warship with a long deck and a raised command tower. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**12. Крейсер «Носоріг»** — Cruiser Rhino (`part_h_12`, файл `part_h_12.webp`)

```
A cruiser, a big warship with heavy armor, a central tower and many turrets. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**13. Важкий крейсер «Бегемот»** — Heavy cruiser Behemoth (`part_h_13`, файл `part_h_13.webp`)

```
A heavy cruiser, a massive armored warship with a broad hull and large guns. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Важкі

**14. Броненосець «Панцирник»** — Armored ship Pangolin (`part_h_14`, файл `part_h_14.webp`)

```
An armored ship, a squat heavily plated warship with thick shell-like plates. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**15. Лінкор «Кракен»** — Battleship Kraken (`part_h_15`, файл `part_h_15.webp`)

```
A battleship, a huge warship with big main guns on its bow and a tall bridge. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**16. Важкий лінкор «Мамонт»** — Heavy battleship Mammoth (`part_h_16`, файл `part_h_16.webp`)

```
A heavy battleship, an enormous warship with layered armor and giant cannons. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**17. Дредноут «Горгона»** — Dreadnought Gorgon (`part_h_17`, файл `part_h_17.webp`)

```
A dreadnought, a gigantic warship with an imposing front and a serpent-like head ornament. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Надважкі

**18. Дестроєр «Каратель»** — Devastator Punisher (`part_h_18`, файл `part_h_18.webp`)

```
A devastator, a bulky siege warship with a huge forward cannon. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**19. Супер-лінкор «Тиранозавр»** — Super battleship Tyrannosaurus (`part_h_19`, файл `part_h_19.webp`)

```
A super battleship, a colossal predator-shaped warship with big jaws-like prow weapons. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**20. Супер-дредноут «Левіафан»** — Super dreadnought Leviathan (`part_h_20`, файл `part_h_20.webp`)

```
A super dreadnought, the biggest flagship, a leviathan hull with huge engines and a shining command spire. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Цивільні

**21. Малий транспорт «Жук»** — Small transport Beetle (`part_h_21`, файл `part_h_21.webp`)

```
A small transport, a boxy cargo ship with a small cockpit and a container on its back. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**22. Середній транспорт «Бик»** — Medium transport Bull (`part_h_22`, файл `part_h_22.webp`)

```
A medium transport, a strong hauler with a clear cargo frame and two engines. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**23. Великий транспорт «Слон»** — Large transport Elephant (`part_h_23`, файл `part_h_23.webp`)

```
A large transport, a very long hauler with a huge cargo spine and big engines. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**24. Важкий транспорт «Кит»** — Heavy transport Whale (`part_h_24`, файл `part_h_24.webp`)

```
A heavy transport, a vast whale-shaped carrier with big cargo doors. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

### Корпуси споруд

**25. Орбітальна база** — Orbital base (`part_h_25`, файл `part_h_25.webp`)

```
An orbital base, a compact ring station with docking arms around a planet's edge. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**26. Космобаза** — Space base (`part_h_26`, файл `part_h_26.webp`)

```
A space base, a large multi-ring space station with solar wings and many docks. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
```

**27. Видобувна станція** — Mining station (`part_h_27`, файл `part_h_27.webp`)

```
A mining station, a station with big drilling arms and a stream of ore into a processing core. A photorealistic render of a single starship in empty space, three-quarter view, realistic hull plating, panel lines and weathering, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. 16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1. photorealistic cinematic render, physically accurate materials and lighting, fine surface detail, realistic scale and proportions, sharp focus, dark space atmosphere with a deep navy background (#0B1020), subtle volumetric glow, no text, no letters, no numbers, no watermark, not a cartoon, not stylized.
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
