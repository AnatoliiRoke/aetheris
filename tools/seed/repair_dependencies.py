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
4. From that level every building and technology needs a mineral of its own specialisation,
   and from the second level after it a second one, so the higher levels need the minerals
   that the asteroid fields and the trade bring (item 9 of the task).
5. Energy leaves the recipes of the factories too, for the reason of rule 1.
6. The level of the science centre a level of a technology asks for is lowered, level by level,
   only where the whole tree would stop otherwise: the script plays the tree (the level cap of
   the administrative centre, the sciences every level asks for, the science centre every level
   of a technology asks for) and lowers the first level of a technology that waits for a science
   centre the play cannot raise, down to the level the centre has reached. It repeats until
   every level of every building and technology is reached. Without
   it the administrative centre, the science centre and mathematics wait for each other
   (item 2 of the task).

Run: python3 tools/seed/repair_dependencies.py
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEPENDENCY = ROOT / "documentation" / "references" / "zalezhnosti-rozklad.json"

BALANCES = {"Населення", "Енергія"}
COSMIC = {"Гелій-3", "Темна матерія", "Ефір"}
MINERALS = ["Кварцит", "Обсидіан", "Лінтрій", "Орекс"]
CRYSTALS = ["Квартан", "Стобій", "Туманій"]
LUCK = set(MINERALS) | set(CRYSTALS) | {"Дейтерій", "Збагачений Уран"}
BUILDING_FROM = 10
TECHNOLOGY_FROM = 8
SECOND_AFTER = 5

# The mineral of the specialisation of a group of buildings: the first is needed from the
# threshold, the second five levels later.
BUILDING_MINERALS = {
    "Головні й населення": ["Кварцит", "Орекс"],
    "Склади": ["Орекс", "Кварцит"],
    "Наука": ["Кварцит", "Лінтрій"],
    "Видобуток на планеті": ["Кварцит", "Обсидіан"],
    "Енергетика": ["Орекс", "Лінтрій"],
    "Виробництво ресурсів": ["Лінтрій", "Кварцит"],
    "Виробництво модулів": ["Обсидіан", "Орекс"],
    "Флот і оборона": ["Орекс", "Обсидіан"],
    "Торгівля й логістика": ["Лінтрій", "Орекс"],
}
FACTORY_MINERALS = {
    "Фабрика двигунів": ["Обсидіан", "Орекс"],
    "Завод реакторів": ["Орекс", "Обсидіан"],
    "Завод енергетичної зброї": ["Орекс", "Лінтрій"],
    "Завод кінетичної зброї": ["Орекс", "Кварцит"],
    "Ливарний завод": ["Кварцит", "Орекс"],
    "Завод щитів": ["Орекс", "Лінтрій"],
    "Фабрика боєприпасів": ["Орекс", "Кварцит"],
    "Фабрика бурових установок": ["Кварцит", "Обсидіан"],
    "Завод точних приладів": ["Лінтрій", "Кварцит"],
}
TECHNOLOGY_MINERALS = {
    "Фундаментальні науки": ["Кварцит", "Лінтрій"],
    "Економічні розвитки": ["Лінтрій", "Кварцит"],
}
TECHNOLOGY_OVERRIDES = {
    "Економіка": ["Лінтрій", "Орекс"],
    "Кристалографія": ["Кварцит", "Орекс"],
    "Геологія": ["Кварцит", "Обсидіан"],
    "Енергетика": ["Орекс", "Лінтрій"],
    "Ядерна фізика": ["Орекс", "Кварцит"],
    "Термоядерний синтез": ["Орекс", "Лінтрій"],
    "Електроніка": ["Лінтрій", "Кварцит"],
    "Логістика складів": ["Орекс", "Кварцит"],
    "Торговельне право": ["Лінтрій", "Орекс"],
    "Стабілізація порталу": ["Орекс", "Лінтрій"],
}
# The crystals the late levels of some items need (the trade and the portal).
CRYSTAL_ITEMS = {
    "Торговий центр": ("Квартан", 15),
    "Портал": ("Стобій", 15),
    "Космопорт": ("Квартан", 15),
    "Економіка": ("Туманій", 15),
    "Торговельне право": ("Квартан", 15),
    "Стабілізація порталу": ("Стобій", 15),
    "Кристалографія": ("Туманій", 15),
}


def fix_tiers(item: dict) -> None:
    is_building = item["kind"] == "budivlia"
    threshold = BUILDING_FROM if is_building else TECHNOLOGY_FROM
    group_minerals = (
        FACTORY_MINERALS.get(item["name"]) or BUILDING_MINERALS[item["group"]]
        if is_building
        else TECHNOLOGY_OVERRIDES.get(item["name"]) or TECHNOLOGY_MINERALS.get(item["group"], ["Кварцит", "Лінтрій"])
    )
    tiers = []
    for level, tier in enumerate(item["tiers"], start=1):
        kept = [name for name in tier if name not in BALANCES and name not in COSMIC]
        if level < threshold:
            kept = [name for name in kept if name not in LUCK]
        tiers.append(kept)

    for level, tier in enumerate(tiers, start=1):
        if level >= threshold and group_minerals[0] not in tier:
            tier.append(group_minerals[0])
        if level >= threshold + SECOND_AFTER and group_minerals[1] not in tier:
            tier.append(group_minerals[1])
        crystal = CRYSTAL_ITEMS.get(item["name"])
        if crystal is not None and level >= crystal[1] and crystal[0] not in tier:
            tier.append(crystal[0])
        # A resource that a lower level pays stays on every higher level.
        if level >= 2:
            for name in tiers[level - 2]:
                if name not in tier and name not in LUCK:
                    tier.append(name)
        tier.sort(key=order_key)
    item["tiers"] = tiers


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


def main() -> None:
    catalog = json.loads(DEPENDENCY.read_text(encoding="utf-8"))
    for item in catalog["items"]:
        if item["kind"] in {"budivlia", "rozvytok"}:
            fix_tiers(item)
        if item.get("recipe"):
            item["recipe"] = [name for name in item["recipe"] if name not in BALANCES]
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
