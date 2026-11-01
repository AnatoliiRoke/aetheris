#!/usr/bin/env python3
"""Builds the prompts of the pictures of the objects of the game (task 00035).

Reads the names from the seeds of the game (resources, buildings, technologies, ship modules and
hulls) and the visual motifs from motifs_*.py, and writes:
  documentation/art-local/object-prompts.json   one row per object, for a batch run
  documentation/ui-art/06-promty-zobrazhen-obiektiv.md   the same prompts to read and copy

Run from the root of the repository: python3 tools/art/build_object_prompts.py
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from motifs_buildings import BUILDINGS  # noqa: E402
from motifs_modules import HULLS, MODULES, TECHNOLOGIES  # noqa: E402
from motifs_resources import RESOURCES  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STYLE = "painted 2D digital illustration, dark space palette, deep navy background (#0B1020), soft glow, clean readable silhouette, no text, no letters, no numbers, no watermark."
FRAME = "16:9 frame, 1280x720; the subject stays inside the middle 60% of the frame so the picture survives a crop to 4:3 or 1:1."
SIZE = "1280x720"


def load(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as handle:
        return json.load(handle)


def row(kind, code, group_uk, name, prompt):
    return {"kind": kind, "code": code, "file": f"{code}.webp", "size": SIZE, "group": group_uk, "name_uk": name["uk"], "name_en": name["en"], "prompt": prompt}


def main():
    world = load("server/src/Aetheris.World/Data/world-reference-seed.json")
    planets = load("server/src/Aetheris.Planets/Data/planets-reference-seed.json")
    research = load("server/src/Aetheris.Research/Data/research-reference-seed.json")
    production = load("server/src/Aetheris.Production/Data/production-reference-seed.json")

    resource_groups = {group["code"]: group["name"]["uk"] for group in world["resource_groups"]}
    categories = {category["code"]: category["name"]["uk"] for category in planets["categories"]}
    branches = {branch["code"]: branch["name"]["uk"] for branch in research["branches"]}
    part_groups = {group["code"]: group["name"]["uk"] for group in production["part_groups"]}

    rows = []

    for resource in world["resources"]:
        subject, palette = RESOURCES[resource["code"]]
        prompt = f"{subject}. Palette: {palette}. A single still-life subject centered on a dark rocky or metallic surface with a soft rim light and a shallow depth of field, a game resource illustration. {FRAME} {STYLE}"
        rows.append(row("resource", resource["code"], resource_groups[resource["group"]], resource["name"], prompt))

    for building in planets["building_types"]:
        subject = BUILDINGS[building["code"]]
        prompt = f"{subject}. A wide establishing view of the building on an alien planet at dusk, small details for scale, one clear main structure, soft rim light, a game building illustration. {FRAME} {STYLE}"
        rows.append(row("building", building["code"], categories[building["category"]], building["name"], prompt))

    for technology in research["technologies"]:
        subject = TECHNOLOGIES[technology["code"]]
        prompt = f"An emblem illustration for the research \"{technology['name']['en']}\". {subject}. Holographic and symbolic, glowing teal and cyan light accents (#2EC4C4), a calm composition with one clear central motif and a faint hexagon frame behind it. {FRAME} {STYLE}"
        rows.append(row("technology", technology["code"], branches[technology["branch"]], technology["name"], prompt))

    for part in production["parts"]:
        code = part["code"]
        group = part_groups.get(part["group"], part["group"])

        if code.startswith("part_m_"):
            subject = MODULES[code]
            prompt = f"{subject}. A single ship module shown alone on a dark holographic pedestal, three-quarter view, a clean technical illustration with soft rim light and a few small bright details, no ship hull around it. {FRAME} {STYLE}"
            rows.append(row("module", code, group, part["name"], prompt))
        else:
            subject = HULLS[code]
            prompt = f"{subject}. A single starship in empty space, three-quarter view, a neutral design with no race symbols and no lettering, lit by a distant star with a teal rim light. {FRAME} {STYLE}"
            rows.append(row("hull", code, group, part["name"], prompt))

    with open(os.path.join(ROOT, "documentation/art-local/object-prompts.json"), "w", encoding="utf-8") as handle:
        json.dump(rows, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    titles = {
        "resource": ("1. Ресурси", "Ресурси (31): сировина, пальне, кристали, вироблені товари й баланси."),
        "building": ("2. Будівлі", "Будівлі (56) за категоріями гри."),
        "technology": ("3. Дослідження", "Технології (20): значок-емблема кожного дослідження."),
        "module": ("4. Модулі", "Модулі кораблів і орбітальної бази (83), разом із чотирма кріокапсулами рас."),
        "hull": ("5. Корпуси (додатково)", "Корпуси кораблів і станцій (27). Їх не було в переліку автора, додано, бо їм теж потрібні картинки; за потреби розділ можна не генерувати."),
    }
    lines = [
        "# AETHERIS — Промпти зображень об'єктів гри",
        "",
        "> **Статус:** v0.1 (01.11.2026, задача [00035](../tasks/00035-promty-zobrazhen-obiektiv.md)). Згенеровано скриптом `tools/art/build_object_prompts.py` з сидів гри; ті самі промпти в `documentation/art-local/object-prompts.json` для пакетного запуску.",
        "> **Пов'язано:** [ui-art/05 Єдиний стиль](05-yedynyi-styl-interfeisu.md) (розділ 11), [задача 00003](../tasks/00003-dyzain-pershoi-storinky-proektu.md) (стиль малюнків і суфікс).",
        "",
        "## Правила",
        "",
        f"1. **Один стиль для всіх.** Кожен промпт вже містить суфікс стилю з задачі 00003: `{STYLE}` Нічого дописувати не треба.",
        f"2. **Один розмір: {SIZE} (16:9).** Плашки будівель, досліджень і рецептів показують картинку в рамці 16:9; предмет стоїть у середніх 60% кадру, тому картинку можна обрізати до 4:3 чи 1:1 для іконок і карток.",
        "3. **Один предмет у кадрі.** Без тексту, літер, цифр і водяних знаків. Розмір кадру й суфікс не змінювати між об'єктами, щоб картинки лишалися єдиним набором.",
        "4. **Назва файлу** це код об'єкта з даних гри + `.webp` (наприклад `building_12.webp`). Код стабільний, тому файл завжди відповідає своєму рядку довідника.",
        "5. **Формат:** WebP, до 1 МБ (ліміт адмінки `admin.reference.image_max_bytes`); для прозорості не вимагається, фон завжди темно-синій.",
        "6. **Куди класти.** Готові файли складаються в `documentation/art-local/images/objects/` і завантажуються в адмінці в поле «Картинка» відповідного рядка довідника (ресурс, будівля, технологія, деталь). Поки картинки немає, гра показує спільну заглушку (ART-17).",
        "7. **Раси.** Кріокапсули рас різняться кольором і формою (Террани: сталево-сірий і синій; Літаріони: крижаний ціан і білий; Драккури: темний і червоно-помаранчевий; Вейрани: піщано-бурштиновий); решта об'єктів нейтральна й не належить жодній расі.",
        "",
    ]
    counts = {}

    for kind in ("resource", "building", "technology", "module", "hull"):
        title, note = titles[kind]
        items = [item for item in rows if item["kind"] == kind]
        order = {}

        for item in items:
            order.setdefault(item["group"], len(order))

        # Objects of one group stand together, in the order the data lists them.
        items.sort(key=lambda item: order[item["group"]])
        counts[kind] = len(items)
        lines += [f"## {title}", "", note, ""]
        current = None

        for number, item in enumerate(items, start=1):
            if item["group"] != current:
                current = item["group"]
                lines += [f"### {current}", ""]

            lines += [f"**{number}. {item['name_uk']}** — {item['name_en']} (`{item['code']}`, файл `{item['file']}`)", "", "```", item["prompt"], "```", ""]

    lines += ["## Підсумок", "", "| Вид | Кількість |", "|---|---|"]
    names = {"resource": "Ресурси", "building": "Будівлі", "technology": "Дослідження", "module": "Модулі", "hull": "Корпуси (додатково)"}
    lines += [f"| {names[kind]} | {counts[kind]} |" for kind in counts]
    lines += [f"| **Разом** | **{sum(counts.values())}** |", ""]

    with open(os.path.join(ROOT, "documentation/ui-art/06-promty-zobrazhen-obiektiv.md"), "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines))

    print(counts)


if __name__ == "__main__":
    main()
