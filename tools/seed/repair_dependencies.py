"""Repair the dependency file so the whole tree can be played (task 00030).

The author allowed edits of documentation/references/zalezhnosti-rozklad.json (decision 5.4).
The script applies fixed, documented rules to the file and is idempotent: running it twice
gives the same file. The rules (each with the reason):

1. Energy leaves the costs of buildings and technologies. Population leaves the costs of
   technologies only. A building pays population from the counter of the planet: the people
   are spent and grow back, so the next building waits until enough of them have returned.
   Energy is a balance, spent as consumption, which the seed generator writes from the
   coefficients. A technology, a module and a recipe do not take population.
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
    "science": ["Кварцит", "Обсидіан", "Орекс", "Лінтрій"],
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
    "Математика": "science",
    "Геологія": "science",
    "Будівництво": "science",
    "Археологія": "science",
    "Фізика": "engine",
    "Ядерна фізика": "engine",
    "Термоядерний синтез": "engine",
    "Енергетика": "engine",
    "Газова інженерія": "engine",
    "Електроніка": "optics",
    "Кібернетика": "optics",
    "Кристалографія": "optics",
    "Економіка": "optics",
    "Торговельне право": "optics",
    "Демографія": "optics",
    "Робототехніка": "arms",
    "Логістика складів": "arms",
    "Колоніальне управління": "arms",
    "Стабілізація порталу": "arms",
}
# The crystals: every second item that lives to the late levels needs one, in turn, from the
# fifteenth level of a building and the thirteenth of a technology.
BUILDING_CRYSTAL_FROM = 15
TECHNOLOGY_CRYSTAL_FROM = 13
ASKS_CRYSTAL_EVERY = 2


def fix_tiers(item: dict) -> None:
    is_building = item["kind"] == "budivlia"
    threshold = BUILDING_FROM if is_building else TECHNOLOGY_FROM
    # Population stays on a building. It leaves a technology, together with energy.
    removed = set(COSMIC)
    removed.add("Енергія")
    if not is_building:
        removed.add("Населення")
    tiers = []
    for level, tier in enumerate(item["tiers"], start=1):
        kept = [name for name in tier if name not in removed]
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
            tiers[index] = ["Залізна руда", "Кредити", "Населення"]


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


COEFFICIENTS = ROOT / "server" / "src" / "Aetheris.World" / "Data" / "seed-coefficients.json"
GROUP_KEYS = {
    "Головні й населення": "main",
    "Склади": "storage",
    "Наука": "science",
    "Видобуток на планеті": "extraction",
    "Енергетика": "power",
    "Виробництво ресурсів": "resource_production",
    "Виробництво модулів": "module_production",
    "Флот і оборона": "fleet",
    "Торгівля й логістика": "trade",
}
MODULE_PROFILE = {
    "Двигуни": "engine",
    "Реактори": "engine",
    "Енергетична зброя": "arms",
    "Кінетична зброя": "arms",
    "Броня": "arms",
    "Щити": "arms",
    "Боєприпаси": "arms",
    "Видобуток": "science",
    "Розвідка й маскування": "optics",
    "Інші корабельні": "science",
    "Модулі бази": "science",
}
# Modules named after a mineral or fitted to one keep it.
FIXED_MODULE_MINERALS = {
    "Орексова гармата": ["Орекс"],
    "Орексовий щит": ["Орекс"],
    "Кварцовий щит": ["Кварцит", "Лінтрій"],
    "Астросканер": ["Кварцит", "Лінтрій"],
    "Корабельний радар": ["Кварцит", "Лінтрій"],
}


# Modules of science and optics that the file leaves without a mineral and that ask for one here,
# so the quartzite and the lintrium, which only science and optics take, are asked for as often
# as the obsidian and the orex of the engines and the arms.
EXTRA_MODULE_MINERAL = {
    "Бурова установка",
    "Екстрактор ефіру",
    "Радіооптичне маскування",
    "Детектор маскування",
    "Бортовий комп'ютер",
    "Протиракетна система",
}


# The list of the modules of the author (perelik-3) names the tumanium for the nebula collector;
# the dependency file lacks it, and the tumanium has no other module.
MODULE_CRYSTAL = {"Екстрактор матерії": "Туманій"}


def load_coefficients() -> dict[str, float]:
    rows = json.loads(COEFFICIENTS.read_text(encoding="utf-8"))["parameters"]
    return {row["key"]: float(row["value"]) for row in rows}


def spread(values: dict[str, float]) -> float:
    mean = sum(values.values()) / len(values)
    return (max(values.values()) - min(values.values())) / mean if mean else 0.0


def balance(slots: list[dict], totals: dict[str, float], counts: dict[str, int] | None = None) -> None:
    """Hands out the minerals so that the quantity and the number of places of every mineral stay level.

    A slot is a place that needs a mineral: its weight is the quantity it will ask for, its
    options are the first minerals of its affinity, and `owner` lists what the same item took.
    The heaviest slots go first and each takes the option with the smallest total so far; then
    moves and exchanges of places go on while the sum of the two spreads (of the quantity and of
    the number of places) falls.
    """
    counts = counts if counts is not None else {name: 0 for name in totals}

    for slot in sorted(slots, key=lambda row: -row["weight"]):
        options = [name for name in slot["options"] if name not in slot["owner"]]
        pick = min(options, key=lambda name: (totals[name], slot["options"].index(name)))
        totals[pick] += slot["weight"]
        counts[pick] += 1
        slot["owner"].append(pick)
        slot["pick"] = pick

    def score() -> float:
        return 2 * spread(totals) + spread({name: float(value) for name, value in counts.items()})

    for _ in range(100):
        moved = False
        for slot in slots:
            current = slot["pick"]
            best_score, best_name = score(), current
            for name in slot["options"]:
                if name == current or name in slot["owner"]:
                    continue
                totals[current] -= slot["weight"]; totals[name] += slot["weight"]
                counts[current] -= 1; counts[name] += 1
                value = score()
                totals[name] -= slot["weight"]; totals[current] += slot["weight"]
                counts[name] -= 1; counts[current] += 1
                if value < best_score - 1e-9:
                    best_score, best_name = value, name
            if best_name != current:
                totals[current] -= slot["weight"]; totals[best_name] += slot["weight"]
                counts[current] -= 1; counts[best_name] += 1
                slot["owner"][slot["owner"].index(current)] = best_name
                slot["pick"] = best_name
                moved = True
        if not moved:
            current_score = score()
            for first in slots:
                for second in slots:
                    one, two = first["pick"], second["pick"]
                    if first is second or first["owner"] is second["owner"] or one == two:
                        continue
                    if two not in first["options"] or one not in second["options"] or two in first["owner"] or one in second["owner"]:
                        continue
                    delta = first["weight"] - second["weight"]
                    totals[one] -= delta
                    totals[two] += delta
                    # The numbers of places do not change in an exchange of two places.
                    value = score()
                    if value < current_score - 1e-9:
                        first["owner"][first["owner"].index(one)] = two
                        second["owner"][second["owner"].index(two)] = one
                        first["pick"], second["pick"] = two, one
                        current_score = value
                        moved = True
                    else:
                        totals[one] += delta
                        totals[two] -= delta
        if not moved:
            break


def assign_minerals(catalog: dict) -> None:
    """Gives every building, technology and module its minerals and crystals, balancing the quantities."""
    coefficients = load_coefficients()
    building_growth = 1 + coefficients["world.seed.cost.growth"]
    technology_growth = 1 + coefficients["world.seed.cost.technology_growth"]
    part_growth = 1 + coefficients["world.seed.part.ingredient_growth"]

    # Buildings and technologies: the weight of a slot is the sum of its prices over the levels it is paid.
    slots: list[dict] = []
    for item in catalog["items"]:
        if item["kind"] not in {"budivlia", "rozvytok"}:
            continue
        is_building = item["kind"] == "budivlia"
        first = BUILDING_FROM if is_building else TECHNOLOGY_FROM
        factor = coefficients[f"world.seed.cost.group.{GROUP_KEYS[item['group']]}"] if is_building else coefficients["world.seed.cost.technology"]
        growth = building_growth if is_building else technology_growth
        top = AFFINITY[profile_of(item)][:CHOICE_WIDTH]
        owner: list[str] = []
        # A technology lives longer than a building and asks for a third mineral at the late levels,
        # of any affinity: the third place is what makes the quantities even.
        for rank in range(2 if is_building else 3):
            start = first + rank * SECOND_AFTER
            if len(item["tiers"]) < start:
                break
            slots.append({
                "item": item,
                "start": start,
                "weight": factor * sum(growth ** (level - start) for level in range(start, len(item["tiers"]) + 1)),
                "options": top if rank < 2 else AFFINITY[profile_of(item)],
                "owner": owner,
            })
    # Buildings and technologies are balanced each by itself, so no mineral is asked for mostly by
    # the buildings while another is asked for mostly by the technologies.
    balance([slot for slot in slots if slot["item"]["kind"] == "budivlia"], {name: 0.0 for name in MINERALS})
    balance([slot for slot in slots if slot["item"]["kind"] == "rozvytok"], {name: 0.0 for name in MINERALS})
    for slot in slots:
        item = slot["item"]
        for level in range(slot["start"], len(item["tiers"]) + 1):
            item["tiers"][level - 1].append(slot["pick"])

    # The crystals: every second item that lives to the late levels asks for one, in turn.
    crystal_asked = {name: 0 for name in CRYSTALS}
    eligible = 0
    for item in catalog["items"]:
        if item["kind"] not in {"budivlia", "rozvytok"}:
            continue
        crystal_from = BUILDING_CRYSTAL_FROM if item["kind"] == "budivlia" else TECHNOLOGY_CRYSTAL_FROM
        if len(item["tiers"]) < crystal_from:
            continue
        eligible += 1
        if eligible % ASKS_CRYSTAL_EVERY == 1:
            crystal = min(CRYSTALS, key=lambda name: (crystal_asked[name], CRYSTALS.index(name)))
            crystal_asked[crystal] += 1
            for level in range(crystal_from, len(item["tiers"]) + 1):
                item["tiers"][level - 1].append(crystal)
    for item in catalog["items"]:
        if item["kind"] in {"budivlia", "rozvytok"}:
            for tier in item["tiers"]:
                tier.sort(key=order_key)

    # The modules: the number of minerals of a module stays as the file has it; which minerals are
    # handed out again, the weight of a place is the quantity of the part (it grows with the place
    # of the part in its group), so the quantity of every mineral in the modules is level too.
    module_slots: list[dict] = []
    position: dict[str, int] = {}
    totals = {name: 0.0 for name in MINERALS}
    for item in catalog["items"]:
        # An upgraded module (task 00043) takes the resources of its base after the hand-out.
        if item["kind"] != "modul" or item.get("upgradeOf"):
            continue
        index = position.get(item["group"], 0)
        position[item["group"]] = index + 1
        count = sum(1 for name in item["resources"] if name in MINERALS)
        if count == 0 and item["name"] in EXTRA_MODULE_MINERAL:
            count = 1
        item["resources"] = [name for name in item["resources"] if name not in MINERALS]
        if item["name"] in MODULE_CRYSTAL and MODULE_CRYSTAL[item["name"]] not in item["resources"]:
            item["resources"].append(MODULE_CRYSTAL[item["name"]])
        fixed = FIXED_MODULE_MINERALS.get(item["name"])
        if fixed is not None:
            item["resources"].extend(fixed)
            for name in fixed:
                totals[name] += part_growth ** index
            continue
        owner: list[str] = []
        for _ in range(count):
            module_slots.append({
                "item": item,
                "weight": part_growth ** index,
                "options": AFFINITY[MODULE_PROFILE[item["group"]]][:CHOICE_WIDTH],
                "owner": owner,
            })
    balance(module_slots, totals)
    for slot in module_slots:
        slot["item"]["resources"].append(slot["pick"])
    modules = {item["name"]: item for item in catalog["items"] if item["kind"] == "modul"}
    for item in catalog["items"]:
        if item["kind"] == "modul" and item.get("upgradeOf"):
            item["resources"] = list(modules[item["upgradeOf"]]["resources"])
    for item in catalog["items"]:
        if item["kind"] == "modul":
            item["resources"].sort(key=order_key)


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
