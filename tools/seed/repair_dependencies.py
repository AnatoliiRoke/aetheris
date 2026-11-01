"""Repair the dependency file so the whole tree can be played (task 00030).

The author allowed edits of documentation/references/zalezhnosti-rozklad.json (decision 5.4).
The script applies fixed, documented rules to the file and is idempotent: running it twice
gives the same file. The rules (each with the reason):

1. Energy and population leave the costs of buildings and technologies. Neither is a stock the
   game refills (energy is a balance, population is a counter of the economy module, and
   documentation/game-design/02 says buildings need no population), so a cost in them would
   stop every build after the first. Energy is spent by the buildings as consumption, which the
   seed generator writes from the coefficients.
2. Helium-3, dark matter and ether leave the costs of buildings and technologies. Helium-3 is
   fuel, dark matter and ether belong to modules (decision of the author, 01.11.2026).
3. Minerals, crystals, deuterium and enriched uranium may not lie on the cost of a building
   below level 10 or of a technology below level 8. A home planet has one mineral of four and
   one crystal of three at random, so an early level that asked for one would depend on luck
   (levels 1-4 must not depend on many resources, item 6 of the task).
4. From that level every building and technology needs a mineral and, five levels later, a
   second one. Minerals are not copied from the file but handed out again by the script: by
   affinity (engines of obsidian, arms of orex, optics of lintrium, science and extraction of
   quartzite) the mineral that is asked for least so far among the first three of the order, so
   the four minerals are asked for by nearly the same number of buildings and technologies.
   Every second item that lives to the late levels asks for one crystal, in turn, so the three
   crystals are asked for by a few items each (item 9 of the task, the author's remark on the
   spread of 01.11.2026).
5. Energy leaves the recipes of the factories too, for the reason of rule 1.
5a. A produced resource other than the building materials may not lie on the cost of a level
   below the fifth: the optical fiber, the microprocessors and the others need minerals that a
   home planet may lack, and levels 1-4 must not wait for a chain of factories.
6. The level of the science centre a level of a technology asks for is lowered, level by level,
   only where the whole tree would stop otherwise: the script plays the tree (the level cap of
   the administrative centre, the sciences every level asks for, the science centre every level
   of a technology asks for) and lowers the first level of a technology that waits for a science
   centre the play cannot raise, down to the level the centre has reached. It repeats until
   every level of every building and technology is reached. Before that a level of a technology
   asks at most six tenths of its number in levels of the science centre, so the first ship does
   not wait for a tall tree of buildings (item 10 of the task). Without
   it the administrative centre, the science centre and mathematics wait for each other
   (item 2 of the task).

7. The way out of a mistake (task 00030): the first three levels of a mine, a quarry or a station
   of the extraction group do not ask for building materials, and the first three levels of the
   solar plant ask for iron ore and credits instead of silicore and building materials. A player
   who spent all the building materials or all the energy can still build the mine of the
   tectonite, the plant of the building materials and the plant of the power from credits and
   ore alone, and credits never stop coming.

Run: python3 tools/seed/repair_dependencies.py
"""

from __future__ import annotations

import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEPENDENCY = ROOT / "documentation" / "references" / "zalezhnosti-rozklad.json"

BALANCES = {"Населення", "Енергія"}
COSMIC = {"Гелій-3", "Темна матерія", "Ефір"}
MINERALS = ["Кварцит", "Обсидіан", "Лінтрій", "Орекс"]
CRYSTALS = ["Квартан", "Стобій", "Туманій"]
LATE_PRODUCED = {"Оптоволокно", "Мікропроцесори", "Композити", "Енергострижні", "Нановолокно", "Титановий сплав"}
FIRST_PRODUCED_LEVEL = 5
LUCK = set(MINERALS) | set(CRYSTALS) | {"Дейтерій", "Збагачений Уран"}
BUILDING_FROM = 10
TECHNOLOGY_FROM = 8
SECOND_AFTER = 5

# The minerals by affinity: the first is the one a kind of thing is made of best (engines of
# obsidian, weapons and shields of orex, optics of lintrium, science and extraction of quartzite).
# The script takes, among the first three of the order, the mineral that is asked for least so far,
# so the demand of the four minerals stays level (the author: the spread must be small).
AFFINITY = {
    "engine": ["Обсидіан", "Орекс", "Лінтрій", "Кварцит"],
    "arms": ["Орекс", "Лінтрій", "Обсидіан", "Кварцит"],
    "optics": ["Лінтрій", "Кварцит", "Орекс", "Обсидіан"],
    "science": ["Кварцит", "Обсидіан", "Лінтрій", "Орекс"],
}
CHOICE_WIDTH = 3
BUILDING_PROFILE = {
    "Головні й населення": "science",
    "Склади": "arms",
    "Наука": "science",
    "Видобуток на планеті": "science",
    "Енергетика": "engine",
    "Виробництво ресурсів": "optics",
    "Виробництво модулів": "arms",
    "Флот і оборона": "arms",
    "Торгівля й логістика": "optics",
}
FACTORY_PROFILE = {
    "Фабрика двигунів": "engine",
    "Завод реакторів": "engine",
    "Завод енергетичної зброї": "arms",
    "Завод кінетичної зброї": "arms",
    "Ливарний завод": "arms",
    "Завод щитів": "arms",
    "Фабрика боєприпасів": "arms",
    "Фабрика бурових установок": "science",
    "Завод точних приладів": "optics",
    "Індустріальний комплекс": "science",
    "Фабрика утилізації": "science",
    "Верф": "engine",
    "Космопорт": "engine",
}
TECHNOLOGY_PROFILE = {
    "Фундаментальні науки": "science",
    "Економічні розвитки": "optics",
}
TECHNOLOGY_OVERRIDES = {
    "Енергетика": "engine",
    "Ядерна фізика": "engine",
    "Термоядерний синтез": "engine",
    "Газова інженерія": "engine",
    "Електроніка": "optics",
    "Кібернетика": "optics",
    "Геологія": "science",
    "Будівництво": "science",
    "Робототехніка": "arms",
    "Логістика складів": "arms",
}
# The crystals: every second item that lives to the late levels needs one, in turn, from the
# fifteenth level of a building and the thirteenth of a technology.
BUILDING_CRYSTAL_FROM = 15
TECHNOLOGY_CRYSTAL_FROM = 13
ASKS_CRYSTAL_EVERY = 2


def fix_tiers(item: dict) -> None:
    is_building = item["kind"] == "budivlia"
    threshold = BUILDING_FROM if is_building else TECHNOLOGY_FROM
    tiers = []
    for level, tier in enumerate(item["tiers"], start=1):
        kept = [name for name in tier if name not in BALANCES and name not in COSMIC]
        # Minerals and crystals are written again by assign_minerals, level by level, so that
        # their demand stays level; deuterium and enriched uranium wait for the threshold.
        kept = [name for name in kept if name not in MINERALS and name not in CRYSTALS]
        if level < threshold:
            kept = [name for name in kept if name not in LUCK]
        if level < FIRST_PRODUCED_LEVEL:
            kept = [name for name in kept if name not in LATE_PRODUCED]
        tiers.append(kept)

    for level, tier in enumerate(tiers, start=1):
        # A resource that a lower level pays stays on every higher level.
        if level >= 2:
            for name in tiers[level - 2]:
                if name not in tier and name not in LUCK and not (level < FIRST_PRODUCED_LEVEL and name in LATE_PRODUCED):
                    tier.append(name)
        tier.sort(key=order_key)
    recover(item, tiers)
    item["tiers"] = tiers


RECOVERY_LEVELS = 3
SOLAR = "Сонячна електростанція"
MATERIALS = "Будматеріали"
EXTRACTION = "Видобуток на планеті"


def recover(item: dict, tiers: list[list[str]]) -> None:
    """The first levels of the buildings that lead out of a mistake cost ore and credits only."""
    if item["kind"] != "budivlia":
        return
    for index in range(min(RECOVERY_LEVELS, len(tiers))):
        if item["group"] == EXTRACTION:
            tiers[index] = [name for name in tiers[index] if name != MATERIALS]
        elif item["name"] == SOLAR:
            tiers[index] = ["Залізна руда", "Кредити"]


ORDER = [
    "Залізна руда", "Титанова руда", "Іридієва руда", "Кобальтова руда", "Уранова руда", "Силікорова руда",
    "Тектонітова руда", "Кварцит", "Обсидіан", "Лінтрій", "Орекс", "Дейтерій", "Гелій-3", "Темна матерія",
    "Ефір", "Збагачений Уран", "Квартан", "Стобій", "Туманій", "Будматеріали", "Оптоволокно",
    "Мікропроцесори", "Композити", "Енергострижні", "Нановолокно", "Титановий сплав", "Кредити", "Енергія", "Населення",
]


def order_key(name: str) -> int:
    return ORDER.index(name) if name in ORDER else len(ORDER)


def play(catalog: dict) -> tuple[dict[str, int], dict[str, int]]:
    """Plays the tree: gives every building and technology the highest level it reaches."""
    buildings = {item["name"]: item for item in catalog["items"] if item["kind"] == "budivlia"}
    technologies = {item["name"]: item for item in catalog["items"] if item["kind"] == "rozvytok"}
    admin = "Адміністративний центр"
    start = {admin, "Житловий комплекс (Міста)", "Склад", "Сонячна електростанція"}
    building_level = {name: 1 if name in start else 0 for name in buildings}
    technology_level = {name: 0 for name in technologies}
    changed = True
    while changed:
        changed = False
        for name, item in buildings.items():
            level = building_level[name] + 1
            if level > item["maxLevel"]:
                continue
            if name != admin and level > building_level[admin]:
                continue
            if all(technology_level.get(science, 0) >= need for science, need in item["scienceLevels"][level - 1].items()):
                building_level[name] = level
                changed = True
        for name, item in technologies.items():
            level = technology_level[name] + 1
            if level > item["maxLevel"]:
                continue
            if building_level["Науковий центр"] < item["centerLevels"][level - 1]:
                continue
            if all(technology_level.get(science, 0) >= need for science, need in item["scienceLevels"][level - 1].items()):
                technology_level[name] = level
                changed = True
    return building_level, technology_level


CENTRE_SLOPE = 0.6


def cap_centres(catalog: dict) -> None:
    """A level of a technology asks at most CENTRE_SLOPE of its number in the levels of the science centre."""
    for item in catalog["items"]:
        if item["kind"] == "rozvytok":
            item["centerLevels"] = [min(value, max(1, math.ceil(CENTRE_SLOPE * (index + 1)))) for index, value in enumerate(item["centerLevels"])]


def relax_centres(catalog: dict) -> int:
    """Lowers the science centre a level asks for where the play would stop; returns the rows changed."""
    technologies = {item["name"]: item for item in catalog["items"] if item["kind"] == "rozvytok"}
    changed = 0
    for _ in range(2000):
        buildings, levels = play(catalog)
        stuck = [(name, levels[name] + 1) for name, item in technologies.items() if levels[name] < item["maxLevel"]]
        blocked = [
            (name, level)
            for name, level in stuck
            if buildings["Науковий центр"] < technologies[name]["centerLevels"][level - 1]
            and all(levels.get(science, 0) >= need for science, need in technologies[name]["scienceLevels"][level - 1].items())
        ]
        if not blocked:
            break
        for name, level in blocked:
            centres = technologies[name]["centerLevels"]
            centres[level - 1] = buildings["Науковий центр"]
            for index in range(level, len(centres)):
                centres[index] = max(centres[index], centres[level - 1])
            changed += 1
    return changed


def profile_of(item: dict) -> str:
    if item["kind"] == "budivlia":
        return FACTORY_PROFILE.get(item["name"]) or BUILDING_PROFILE[item["group"]]
    return TECHNOLOGY_OVERRIDES.get(item["name"]) or TECHNOLOGY_PROFILE.get(item["group"], "science")


def assign_minerals(catalog: dict) -> None:
    """Gives every building and technology its minerals and crystals, balancing the demand."""
    asked = {name: 0 for name in MINERALS}
    crystal_asked = {name: 0 for name in CRYSTALS}
    eligible = 0

    for item in catalog["items"]:
        if item["kind"] not in {"budivlia", "rozvytok"}:
            continue
        is_building = item["kind"] == "budivlia"
        first = BUILDING_FROM if is_building else TECHNOLOGY_FROM
        top = AFFINITY[profile_of(item)][:CHOICE_WIDTH]
        levels = len(item["tiers"])
        chosen: list[str] = []

        for rank in range(2):
            if levels < first + rank * SECOND_AFTER:
                break
            options = [name for name in top if name not in chosen]
            pick = min(options, key=lambda name: (asked[name], top.index(name)))
            asked[pick] += 1
            chosen.append(pick)

        for rank, name in enumerate(chosen):
            for level in range(first + rank * SECOND_AFTER, levels + 1):
                item["tiers"][level - 1].append(name)

        crystal_from = BUILDING_CRYSTAL_FROM if is_building else TECHNOLOGY_CRYSTAL_FROM

        if levels >= crystal_from:
            eligible += 1
            if eligible % ASKS_CRYSTAL_EVERY == 1:
                crystal = min(CRYSTALS, key=lambda name: (crystal_asked[name], CRYSTALS.index(name)))
                crystal_asked[crystal] += 1
                for level in range(crystal_from, levels + 1):
                    item["tiers"][level - 1].append(crystal)

        for tier in item["tiers"]:
            tier.sort(key=order_key)
            # recover() ran before; its levels hold no mineral, so nothing there changes


def main() -> None:
    catalog = json.loads(DEPENDENCY.read_text(encoding="utf-8"))
    for item in catalog["items"]:
        if item["kind"] in {"budivlia", "rozvytok"}:
            fix_tiers(item)
        if item.get("recipe"):
            item["recipe"] = [name for name in item["recipe"] if name not in BALANCES]
    assign_minerals(catalog)
    cap_centres(catalog)
    lowered = relax_centres(catalog)
    buildings, technologies = play(catalog)
    stuck = [name for name, item in ((i["name"], i) for i in catalog["items"] if i["kind"] == "budivlia") if buildings[name] < item["maxLevel"]]
    stuck += [name for name, item in ((i["name"], i) for i in catalog["items"] if i["kind"] == "rozvytok") if technologies[name] < item["maxLevel"]]
    if stuck:
        raise SystemExit("the tree still stops on: " + ", ".join(stuck))
    print(f"science centre rows lowered: {lowered}")
    catalog["version"] = max(catalog.get("version", 0), 5)
    DEPENDENCY.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("dependency file repaired")


if __name__ == "__main__":
    main()
