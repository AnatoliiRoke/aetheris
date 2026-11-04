# AETHERIS — Карта документації

> **Статус:** звірено 04.11.2026. Документи кажуть лише те, що діє зараз; історія змін у git. Якщо документ і сід розходяться, правильний сід, а документ виправляється.

```
documentation/
├── 00-plan-dokumentatsii.md   ← цей файл
├── game-design/               ← правила гри
├── references/                ← переліки (єдині джерела списків) і довідкові матеріали
├── tech/                      ← як гра влаштована технічно, правила коду
├── ui-art/                    ← вигляд, єдиний стиль, локалізація, промпти зображень
├── production/                ← етапи, дорожня карта, ризики
├── design/                    ← макети сторінок (HTML)
├── tasks/                     ← задачі й рішення до них (<номер>-pytannia.md)
└── backlog/                   ← статус задач, аудит, рішення синхронізації
```

## 1. Єдині джерела списків
| Що | Документ |
|---|---|
| Ресурси (31) | [references/perelik-1-resursy.md](references/perelik-1-resursy.md) |
| Технології (21) | [references/perelik-2-tekhnolohii.md](references/perelik-2-tekhnolohii.md) |
| Модулі (84) | [references/perelik-3-moduli.md](references/perelik-3-moduli.md) |
| Корпуси (24 + 3 дрони) | [references/perelik-4-korpusy.md](references/perelik-4-korpusy.md) |
| Раси (4) | [references/perelik-5-rasy.md](references/perelik-5-rasy.md) |
| Будівлі (56) | [references/perelik-6-budivli.md](references/perelik-6-budivli.md) |
| Задачі, майбутні задачі, перевірки автора | [backlog/status-zadach.md](backlog/status-zadach.md) |

Зовнішні довідкові матеріали (інша гра, не правила AETHERIS): `references/starfederation-ohliad.md`, `references/starfederation_handbook.md`, `references/vybir-zi-zorianoi-federatsii.*`.

## 2. Ігровий дизайн
01 загальний опис · 02 економіка й ресурси · 03 розвиток планет · 04 технології · 05 флот і кораблі · 05a модулі · 08 раси · 09 світ і карта · 12 монетизація · 13 новачок · 14 баланс і формули.
Відкладено автором (04.11.2026) до своїх задач: 06 бої, 07 пірати й PvE, 10 клани, 11 рейтинг, 15 артефакти, 16 космічні споруди (крім корпусів споруд і модулів бази в сіді), ui-art/03 інтерфейс бою.

## 3. Обов'язкові правила для агентів
[tech/08](tech/08-pravyla-kodu-dlia-ahentiv.md), [tech/09](tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md), [ui-art/05](ui-art/05-yedynyi-styl-interfeisu.md), `CLAUDE.md` у корені репозиторію.
