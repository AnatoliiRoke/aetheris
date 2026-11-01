"""Audit of the economy of the seed (task 00030).

Evaluates the seed files the way the server reads them (a growth formula with the rounding to
significant digits) and prints: the largest price against the capacity of the warehouse, the
demand of every resource, the minerals of the high levels, the reachability of the tree with
every gate, and the day of the first mining ship of an unhurried player.

Run: python3 tools/seed/audit_economy.py
"""

from __future__ import annotations

import json
import math
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "server" / "src"
WORLD = json.loads((DATA / "Aetheris.World/Data/world-reference-seed.json").read_text(encoding="utf-8"))
PLANETS = json.loads((DATA / "Aetheris.Planets/Data/planets-reference-seed.json").read_text(encoding="utf-8"))
RESEARCH = json.loads((DATA / "Aetheris.Research/Data/research-reference-seed.json").read_text(encoding="utf-8"))
PRODUCTION = json.loads((DATA / "Aetheris.Production/Data/production-reference-seed.json").read_text(encoding="utf-8"))

NAMES = {row["code"]: row["name"]["uk"] for row in WORLD["resources"]}
GROUP_OF = {row["code"]: row["group"] for row in WORLD["resources"]}


def significant(value: float, digits: int) -> float:
    if digits <= 0 or value == 0:
        return value
    magnitude = math.floor(math.log10(abs(value))) + 1
    scale = 10 ** (digits - magnitude)
    if scale >= 1:
        return float(round_half_up(value))
    return round_half_up(value * scale) / scale


def round_half_up(value: float) -> int:
    return int(math.floor(abs(value) + 0.5)) * (1 if value >= 0 else -1)


def value_at(formula: dict, level: int) -> int:
    steps = level - 1
    raw = formula["base"] * formula["growth"] ** steps + formula.get("step", 0) * steps
    return round_half_up(significant(raw, formula.get("significant", 0)))


def building_cost(building: dict, level: int) -> dict[str, int]:
    return {
        cost["resource"]: value_at(cost, level - cost.get("from_level", 1) + 1)
        for cost in building["costs"]
        if level >= cost.get("from_level", 1)
    }


def technology_cost(technology: dict, level: int) -> dict[str, int]:
    return {
        cost["resource"]: value_at(cost["growth"], level - cost.get("from_level", 1) + 1)
        for cost in technology["costs"]
        if level >= cost.get("from_level", 1)
    }


def effect_at(effect: dict, level: int) -> float:
    if effect.get("values"):
        values = effect["values"]
        return values[min(level, len(values)) - 1]
    return value_at(effect, level)


def warehouse_capacity(level: int) -> int:
    building = next(row for row in PLANETS["building_types"] if row["name"]["uk"] == "Склад")
    effect = next(row for row in building["effects"] if row["type"] == "storage_capacity")
    return value_at(effect, level)


def largest_price() -> tuple[int, str]:
    best = (0, "")
    for building in PLANETS["building_types"]:
        for level in range(1, building["max_level"] + 1):
            for resource, amount in building_cost(building, level).items():
                if amount > best[0]:
                    best = (amount, f"{building['name']['uk']} L{level} {NAMES[resource]}")
    for technology in RESEARCH["technologies"]:
        for level in range(1, technology["max_level"] + 1):
            for resource, amount in technology_cost(technology, level).items():
                if amount > best[0]:
                    best = (amount, f"{technology['name']['uk']} L{level} {NAMES[resource]}")
    return best


def demand() -> dict[str, dict[str, int]]:
    """For every resource: how many buildings, technologies, parts and recipes of the seed ask for it."""
    rows: dict[str, dict[str, int]] = defaultdict(lambda: defaultdict(int))
    for building in PLANETS["building_types"]:
        for resource in {cost["resource"] for cost in building["costs"]}:
            rows[NAMES[resource]]["buildings"] += 1
    for technology in RESEARCH["technologies"]:
        for resource in {cost["resource"] for cost in technology["costs"]}:
            rows[NAMES[resource]]["technologies"] += 1
    part_kind = {part["code"]: ("hulls" if part["code"].startswith("part_h") else "modules") for part in PRODUCTION["parts"]}
    for recipe in PRODUCTION["recipes"]:
        kind = part_kind.get(recipe["part"], "recipes") if recipe["part"] else "recipes"
        for ingredient in recipe["ingredients"]:
            rows[NAMES[ingredient["resource"]]][kind] += 1
    return rows


def quantities() -> dict[str, dict[str, int]]:
    """Units of every mineral and crystal: to take all the buildings and technologies to the top, and in one of each module."""
    rows: dict[str, dict[str, int]] = defaultdict(lambda: defaultdict(int))
    for building in PLANETS["building_types"]:
        for level in range(1, building["max_level"] + 1):
            for resource, amount in building_cost(building, level).items():
                rows[NAMES[resource]]["buildings"] += amount
    for technology in RESEARCH["technologies"]:
        for level in range(1, technology["max_level"] + 1):
            for resource, amount in technology_cost(technology, level).items():
                rows[NAMES[resource]]["technologies"] += amount
    part_is_module = {part["code"]: part["code"].startswith("part_m") for part in PRODUCTION["parts"]}
    for recipe in PRODUCTION["recipes"]:
        if recipe["part"] and part_is_module[recipe["part"]]:
            for ingredient in recipe["ingredients"]:
                rows[NAMES[ingredient["resource"]]]["modules"] += ingredient["amount"]
    return rows


def main() -> None:
    price, where = largest_price()
    cap = warehouse_capacity(20)
    print(f"warehouse L1 {warehouse_capacity(1)}, L20 {cap}; largest price {price} ({where}); ratio {price / cap:.3f}")
    assert price * 2 <= cap, "the largest price is over a half of the warehouse"
    print("OK: the largest price is at most half of the largest warehouse")
    print()
    print("Demand (buildings / technologies / modules / hulls / recipes):")
    table = demand()
    for name in [row["name"]["uk"] for row in WORLD["resources"]]:
        row = table.get(name)
        if row:
            print(f"  {name:20} " + " / ".join(str(row.get(kind, 0)) for kind in ("buildings", "technologies", "modules", "hulls", "recipes")))
        elif name not in ("Енергія", "Населення", "Торгова валюта", "Реальна валюта", "Кредити"):
            print(f"  {name:20} -- no consumer")
    print()
    print("Minerals and crystals (units: buildings to the top / technologies to the top / one of every module; modules asking):")
    units = quantities()
    module_counts = demand()
    for name in ("Кварцит", "Обсидіан", "Лінтрій", "Орекс", "Квартан", "Стобій", "Туманій"):
        row = units[name]
        print(f"  {name:10} {row.get('buildings', 0):>9} / {row.get('technologies', 0):>9} / {row.get('modules', 0):>6}   modules {module_counts[name].get('modules', 0)}")
    print()
    result = play()
    print("Start package: the spend of the first day and of the first two days of the play (what the start stock must cover):")
    for horizon in (24, 48):
        spent: dict[str, int] = defaultdict(int)
        for moment, resource, amount in result["spent"]:
            if moment <= horizon:
                spent[NAME_OF_CODE[resource]] += amount
        print(f"  {horizon} h: " + ", ".join(f"{name} {amount}" for name, amount in sorted(spent.items(), key=lambda row: -row[1])))
    print(f"  the start stock of the seed: " + ", ".join(f"{NAME_OF_CODE[row['resource']]} {row['amount']}" for row in WORLD["races"][0]["start_resources"]))
    print(f"First mining ship: day {result['days']:.1f}, done={result['done']}, market purchases {result['bought']:.0f} credits")
    print("  levels:", result["levels"])
    print("  waited for:", result["waited"])



# ---------------------------------------------------------------- play of the first days

LUCK = {"Кварцит", "Обсидіан", "Лінтрій", "Орекс", "Квартан", "Стобій", "Туманій", "Дейтерій", "Збагачений Уран"}
MARKET_PRICE = 8  # credits per unit of what a home planet may lack (an assumption of the play)
CHECK_INS = (8, 13, 19, 23)  # hours of the day at which an unhurried player looks at the game
SLOTS = 2
PRODUCT_STOCK = 2000
STEP = 0.25
OPENING = [
    ("Шахта тектонітової руди", 2),
    ("Шахта залізної руди", 3),
    ("Завод будматеріалів", 1),
    ("Сонячна електростанція", 3),
]
FIRST_SHIP = ("Малий транспорт «Жук»", "Бурова установка", "Реактивний двигун")

CODE_OF_NAME = {row["name"]["uk"]: row["code"] for row in WORLD["resources"]}
NAME_OF_CODE = {code: name for name, code in CODE_OF_NAME.items()}
BUILDINGS = {row["code"]: row for row in PLANETS["building_types"]}
TECHS = {row["code"]: row for row in RESEARCH["technologies"]}
BUILDING_BY_NAME = {row["name"]["uk"]: row["code"] for row in PLANETS["building_types"]}
RECIPES = {row["name"]["uk"]: row for row in PRODUCTION["recipes"]}
ADMIN = BUILDING_BY_NAME["Адміністративний центр"]
SCIENCE = BUILDING_BY_NAME["Науковий центр"]
PRODUCT_RECIPE = {row["resource"]: row for row in PRODUCTION["recipes"] if row["resource"]}
MINE_OF = {
    effect["resource"]: row["code"]
    for row in PLANETS["building_types"]
    for effect in row["effects"]
    if effect["type"] == "metal_production" and NAME_OF_CODE.get(effect["resource"]) not in ("Дейтерій",)
}
MINE_LIMIT = 12


def requirements(item: dict, level: int) -> dict[tuple[str, str], int]:
    found: dict[tuple[str, str], int] = {}
    for row in item["requirements"]:
        if row["subject_level"] is None or row["subject_level"] <= level:
            key = (row["type"], row["target"])
            found[key] = max(found.get(key, 0), row["target_level"] or 0)
    return found


def centre_level_for(tech: dict, level: int) -> int:
    need = tech["research_center_level"]
    for key, value in requirements(tech, level).items():
        if key[0] == "building_level" and key[1] == SCIENCE:
            need = max(need, value)
    return need


def closure(targets: dict[str, int], techs: dict[str, int]) -> None:
    """Grows the wanted levels with everything a level of a target waits for."""
    changed = True
    while changed:
        changed = False
        for code, level in list(targets.items()):
            item = BUILDINGS[code]
            if code != ADMIN and targets.get(ADMIN, 1) < level:
                targets[ADMIN] = level
                changed = True
            for (kind, target), value in requirements(item, level).items():
                if kind == "technology_level" and techs.get(target, 0) < value:
                    techs[target] = value
                    changed = True
                if kind == "building_level" and targets.get(target, 0) < value:
                    targets[target] = value
                    changed = True
        for code, level in list(techs.items()):
            item = TECHS[code]
            centre = centre_level_for(item, level)
            if targets.get(SCIENCE, 0) < centre:
                targets[SCIENCE] = centre
                changed = True
            for (kind, target), value in requirements(item, level).items():
                if kind == "technology_level" and techs.get(target, 0) < value:
                    techs[target] = value
                    changed = True
                if kind == "building_level" and target != SCIENCE and targets.get(target, 0) < value:
                    targets[target] = value
                    changed = True


def first_ship_targets() -> tuple[dict[str, int], dict[str, int], list[dict]]:
    targets: dict[str, int] = {}
    techs: dict[str, int] = {}
    recipes = [RECIPES[name] for name in FIRST_SHIP]
    for recipe in recipes:
        targets[recipe["factory"]] = max(targets.get(recipe["factory"], 0), 1)
        for row in recipe["requirements"]:
            techs[row["target"]] = max(techs.get(row["target"], 0), row["target_level"])
    closure(targets, techs)
    return targets, techs, recipes


def play(verbose: bool = False) -> dict:
    targets, techs, recipes = first_ship_targets()
    start_buildings = {row["code"] for row in PLANETS["building_types"] if row["is_start_building"]}
    levels = {code: 1 if code in start_buildings else 0 for code in BUILDINGS}
    tech_levels = {code: 0 for code in TECHS}
    race = WORLD["races"][0]
    stock: dict[str, float] = defaultdict(float)
    for row in race["start_resources"]:
        stock[row["resource"]] += row["amount"]
    population = 50.0
    time = 0.0
    queue_b: list[dict] = []
    queue_r: list[dict] = []
    log: list[str] = []
    spend_log: list[tuple[float, str, int]] = []
    bought = 0.0
    waited: dict[str, float] = defaultdict(float)
    credits = CODE_OF_NAME["Кредити"]

    def capacity(group: str) -> float:
        total = 0.0
        for code, level in levels.items():
            if level <= 0:
                continue
            for effect in BUILDINGS[code]["effects"]:
                if effect["type"] == "storage_capacity" and effect.get("group") == group:
                    total += effect_at(effect, level)
        return total

    def energy_balance(extra: dict[str, int] | None = None) -> float:
        balance = 0.0
        merged = dict(levels)
        for entry in queue_b:
            merged[entry["code"]] = max(merged[entry["code"]], entry["level"])
        if extra:
            merged.update(extra)
        for code, level in merged.items():
            if level <= 0:
                continue
            for effect in BUILDINGS[code]["effects"]:
                if effect["type"] == "energy_output":
                    balance += effect_at(effect, level)
                elif effect["type"] == "energy_consumption":
                    balance -= effect_at(effect, level)
        return balance

    def pop_max() -> float:
        total = 0.0
        for code, level in levels.items():
            if level > 0:
                for effect in BUILDINGS[code]["effects"]:
                    if effect["type"] == "population_max":
                        total += effect_at(effect, level)
        return total

    def rates() -> dict[str, float]:
        found: dict[str, float] = defaultdict(float)
        for code, level in levels.items():
            if level <= 0:
                continue
            for effect in BUILDINGS[code]["effects"]:
                if effect["type"] in {"metal_production", "crystal_production"}:
                    resource = effect["resource"]
                    if NAME_OF_CODE[resource] in ("Залізна руда", "Титанова руда", "Іридієва руда", "Кобальтова руда", "Уранова руда", "Силікорова руда", "Тектонітова руда"):
                        found[resource] += effect_at(effect, level)
        return found

    def cost_of(kind: str, code: str, level: int) -> dict[str, int]:
        return building_cost(BUILDINGS[code], level) if kind == "b" else technology_cost(TECHS[code], level)

    def duration_of(kind: str, code: str, level: int) -> float:
        item = BUILDINGS[code] if kind == "b" else TECHS[code]
        return value_at(item["duration_seconds"], level) / 3600.0

    def afford(cost: dict[str, int]) -> tuple[bool, dict[str, float]]:
        lack: dict[str, float] = {}
        market = 0.0
        for resource, amount in cost.items():
            missing = amount - stock[resource]
            if missing > 0:
                if NAME_OF_CODE[resource] in LUCK:
                    market += missing * MARKET_PRICE
                else:
                    lack[resource] = missing
        if market > 0 and stock[credits] - cost.get(credits, 0) < market:
            lack[credits] = lack.get(credits, 0) + market
        return (not lack), lack

    def pay(cost: dict[str, int]) -> None:
        nonlocal bought
        for resource, amount in cost.items():
            spend_log.append((time, resource, amount))
            missing = max(0.0, amount - stock[resource])
            if missing > 0 and NAME_OF_CODE[resource] in LUCK:
                stock[credits] -= missing * MARKET_PRICE
                bought += missing * MARKET_PRICE
                stock[resource] += missing
            stock[resource] -= amount

    def building_ready(code: str, level: int) -> bool:
        if code != ADMIN and level > levels[ADMIN] + sum(1 for entry in queue_b if entry["code"] == ADMIN and entry["level"] >= level):
            return False
        for (kind, target), value in requirements(BUILDINGS[code], level).items():
            if kind == "technology_level" and tech_levels.get(target, 0) < value:
                return False
            if kind == "building_level" and levels.get(target, 0) < value:
                return False
        return True

    def tech_ready(code: str, level: int) -> bool:
        if levels[SCIENCE] < centre_level_for(TECHS[code], level):
            return False
        for (kind, target), value in requirements(TECHS[code], level).items():
            if kind == "technology_level" and tech_levels.get(target, 0) < value:
                return False
        return True

    def planned(kind: str, code: str) -> int:
        queue = queue_b if kind == "b" else queue_r
        current = levels[code] if kind == "b" else tech_levels[code]
        return max([current] + [entry["level"] for entry in queue if entry["code"] == code])

    def start_next(queue: list[dict]) -> None:
        if queue and queue[0]["finish"] is None:
            queue[0]["finish"] = max(time, queue[0]["ready"]) + queue[0]["hours"]

    def try_queue(kind: str, code: str, level: int, why: str) -> bool:
        queue = queue_b if kind == "b" else queue_r
        if len(queue) >= SLOTS:
            return False
        cost = cost_of(kind, code, level)
        ok, lack = afford(cost)
        if not ok:
            if verbose and time > 48:
                print(f"d{time/24:.1f} cannot afford {(BUILDINGS if kind == 'b' else TECHS)[code]['name']['uk']} L{level}: {{{', '.join(NAME_OF_CODE[k] + ' ' + str(round(v)) for k, v in lack.items())}}}")
            for resource in lack:
                waited[NAME_OF_CODE[resource]] += 1
                recipe = PRODUCT_RECIPE.get(resource)
                if recipe is not None and targets.get(recipe["factory"], 0) < 1:
                    targets[recipe["factory"]] = 1
                    closure(targets, techs)
                mine = MINE_OF.get(resource)
                if mine is not None and planned("b", mine) < MINE_LIMIT and targets.get(mine, 0) <= planned("b", mine):
                    targets[mine] = planned("b", mine) + 1
                    closure(targets, techs)
            return False
        if kind == "b" and "energy_consumption" in {e["type"] for e in BUILDINGS[code]["effects"]}:
            if energy_balance({code: level}) < 0:
                if verbose and time > 48:
                    print(f"d{time/24:.1f} energy blocks {BUILDINGS[code]['name']['uk']} L{level}: {energy_balance({code: level}):.0f}")
                return False
        pay(cost)
        queue.append({"code": code, "level": level, "hours": duration_of(kind, code, level), "finish": None, "ready": time, "why": why})
        start_next(queue)
        log.append(f"d{time / 24:5.1f} {kind} {(BUILDINGS if kind == 'b' else TECHS)[code]['name']['uk']} L{level} ({why})")
        return True

    def support_building() -> bool:
        """When nothing wanted can be bought: the power, the warehouse or the mine that is short becomes wanted."""
        plant = BUILDING_BY_NAME["Сонячна електростанція"]
        store = BUILDING_BY_NAME["Склад"]
        full = max((stock[r] / max(capacity(GROUP_OF[r]), 1) for r in stock if NAME_OF_CODE.get(r) in ("Залізна руда", "Будматеріали")), default=0)
        wanted: list[tuple[str, int]] = []
        if energy_balance() < 40:
            wanted.append((plant, planned("b", plant) + 1))
        if full > 0.8:
            wanted.append((store, planned("b", store) + 1))
        for name, limit in (("Шахта залізної руди", 8), ("Шахта силікорової руди", 4), ("Шахта тектонітової руди", 4), ("Шахта титанової руди", 4), ("Шахта іридієвої руди", 3), ("Шахта кобальтової руди", 3)):
            code = BUILDING_BY_NAME[name]
            if planned("b", code) < limit:
                wanted.append((code, planned("b", code) + 1))
        for code, level in wanted:
            if level <= BUILDINGS[code]["max_level"] and targets.get(code, 0) < level:
                targets[code] = level
                closure(targets, techs)
                return True
        return False

    def check_in() -> None:
        for _ in range(4):
            moved = False
            for code, level in sorted(techs.items(), key=lambda row: -row[1]):
                current = planned("t", code)
                if current < level and tech_ready(code, current + 1) and try_queue("t", code, current + 1, "wanted"):
                    moved = True
            for code, level in sorted(targets.items(), key=lambda row: (row[0] not in opening_codes, row[0] != ADMIN, -row[1])):
                current = planned("b", code)
                if current < level and building_ready(code, current + 1) and try_queue("b", code, current + 1, "wanted"):
                    moved = True
            if not moved and len(queue_b) < SLOTS and support_building():
                moved = True
            if not moved:
                break

    def done() -> bool:
        return all(levels[code] >= level for code, level in targets.items()) and all(tech_levels[code] >= level for code, level in techs.items())

    # The opening of a player: the mine of tectonite, the plant of the building materials and the power.
    for name, level in OPENING:
        targets[BUILDING_BY_NAME[name]] = max(targets.get(BUILDING_BY_NAME[name], 0), level)
    closure(targets, techs)
    opening_codes = [BUILDING_BY_NAME[name] for name, _level in OPENING]

    produced_goal_at = None
    last_hour = -1
    while time < 24 * 60:
        # accrue
        r = rates()
        for resource, rate in r.items():
            group = GROUP_OF[resource]
            stock[resource] = min(stock[resource] + rate * STEP, max(capacity(group), 0.0))
        maximum = pop_max()
        population = min(maximum, population + maximum * 0.05 * STEP)
        stock[credits] += population * 1.0 * STEP
        # factories keep a stock of the products
        for resource, recipe in PRODUCT_RECIPE.items():
            if levels.get(recipe["factory"], 0) <= 0:
                continue
            if stock[resource] >= PRODUCT_STOCK or stock[resource] >= capacity(GROUP_OF[resource]):
                continue
            batch = recipe["output_quantity"]
            need = {ing["resource"]: ing["amount"] for ing in recipe["ingredients"]}
            per_step = STEP * 3600 / recipe["duration_seconds"]
            if all(stock[k] + (stock[k] * 0) >= v * per_step or NAME_OF_CODE[k] in LUCK for k, v in need.items()):
                for k, v in need.items():
                    if NAME_OF_CODE[k] in LUCK:
                        stock[credits] -= v * per_step * MARKET_PRICE
                    else:
                        stock[k] -= v * per_step
                stock[resource] += batch * per_step
        time += STEP
        # completions
        for kind, queue in (("b", queue_b), ("t", queue_r)):
            while queue and queue[0]["finish"] is not None and queue[0]["finish"] <= time:
                entry = queue.pop(0)
                (levels if kind == "b" else tech_levels)[entry["code"]] = entry["level"]
                start_next(queue)
        hour = int(time % 24)
        if hour in CHECK_INS and hour != last_hour:
            check_in()
        last_hour = hour
        if done():
            produced_goal_at = time
            break

    if produced_goal_at is not None:
        # The three parts are made one after another in their factories at the same time; the longest one counts.
        produced_goal_at += max(recipe["duration_seconds"] for recipe in recipes) / 3600.0

    result = {
        "days": (produced_goal_at or time) / 24,
        "done": produced_goal_at is not None,
        "bought": bought,
        "waited": dict(sorted(waited.items(), key=lambda row: -row[1])[:6]),
        "levels": {BUILDINGS[c]["name"]["uk"]: l for c, l in levels.items() if l > 1 or c in targets},
        "log": log,
        "spent": spend_log,
    }
    return result


if __name__ == "__main__":
    main()
