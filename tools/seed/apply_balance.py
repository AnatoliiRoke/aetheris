"""Carry the balance files of the author into the seed (task 00045).

The author builds the dependencies on the pages of documentation/references/balance and saves
them there as JSON: the tree of the sciences (derevo-nauk.json), the buildings (budivli.json)
and the ladder of the hulls and the modules (drabyna-korpusiv.json). This script writes them
into the three seed files and touches nothing else in them:

1. research-reference-seed.json: the requirements of every level of every science, and the
   "unlocks" effects rebuilt from the requirements of the first level of the buildings (a
   science shows the buildings that ask for it on their first level).
2. planets-reference-seed.json: the requirements of every level of every building (the deposit
   of a mine is kept), the effects the author changed by hand, and the production speed bonus
   of every building of the production class.
3. production-reference-seed.json: a hull asks for a level of the shipyard, a module for a level
   of its own factory, nothing else; the recipes of the resources keep no requirement.

The administrative centre caps the level of every other building in the code, so a requirement
of the centre is written only where the author raised it above the level of the building. A
level-1 requirement of the own factory is not written: the order needs that factory anyway.
Running the script twice gives the same files.
"""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BALANCE = ROOT / "documentation" / "references" / "balance"
TREE_FILE = BALANCE / "derevo-nauk.json"
BUILDINGS_FILE = BALANCE / "budivli.json"
LADDER_FILE = BALANCE / "drabyna-korpusiv.json"
RESEARCH_SEED = ROOT / "server" / "src" / "Aetheris.Research" / "Data" / "research-reference-seed.json"
PLANETS_SEED = ROOT / "server" / "src" / "Aetheris.Planets" / "Data" / "planets-reference-seed.json"
PRODUCTION_SEED = ROOT / "server" / "src" / "Aetheris.Production" / "Data" / "production-reference-seed.json"

TECHNOLOGY_LEVEL = "technology_level"
BUILDING_LEVEL = "building_level"
DEPOSIT = "deposit"
UNLOCKS = "unlocks"
UNLOCK_KIND_BUILDING = "building_type"
LEVEL_CAP_EFFECT = "level_cap"
PRODUCTION_CLASS = "production"
PRODUCTION_SPEED_EFFECT = "production_speed_bonus"
PRODUCTION_SPEED_PER_LEVEL = 0.025
TYPE_ORDER = {BUILDING_LEVEL: 0, TECHNOLOGY_LEVEL: 1, DEPOSIT: 2}


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def dump(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def code_number(code: str) -> int:
    return int(code.rsplit("_", 1)[-1])


def requirement_key(requirement: dict) -> tuple:
    return (requirement.get("subject_level", 0),
        TYPE_ORDER.get(requirement["type"], 9),
        code_number(requirement["target"]))


def cap_building_code(planets_seed: dict) -> str:
    """The building whose level caps the others: the one with the level cap effect."""
    capping = [building["code"] for building in planets_seed["building_types"]
        if any(effect["type"] == LEVEL_CAP_EFFECT for effect in building["effects"])]
    if len(capping) != 1:
        sys.exit(f"Expected one building with the level cap effect, found {capping}.")
    return capping[0]


def apply_tree(research_seed: dict, tree: dict, first_level_needs: dict[str, dict[str, int]]) -> None:
    """Writes the requirements of the sciences and rebuilds the effects that open buildings."""
    tree_by_code = {technology["code"]: technology for technology in tree["technologies"]}
    for technology in research_seed["technologies"]:
        source = tree_by_code.get(technology["code"])
        if source is None:
            sys.exit(f"The tree has no science {technology['code']}.")
        technology["requirements"] = sorted(
            [{"type": requirement["type"],
                "target": requirement["target"],
                "target_level": requirement["target_level"],
                "subject_level": requirement["subject_level"]}
                for requirement in source["requirements"]],
            key=requirement_key)
        kept_effects = [effect for effect in technology["effects"] if effect["type"] != UNLOCKS]
        opened = sorted(
            [(building_code, needs[technology["code"]])
                for building_code, needs in first_level_needs.items()
                if technology["code"] in needs],
            key=lambda pair: code_number(pair[0]))
        unlock_effects = [{"type": UNLOCKS,
            "unlock_kind": UNLOCK_KIND_BUILDING,
            "unlock_target": building_code,
            "level": level}
            for building_code, level in opened]
        technology["effects"] = unlock_effects + kept_effects


def production_speed_effect(max_level: int) -> dict:
    values = [round(PRODUCTION_SPEED_PER_LEVEL * (level - 1), 6) for level in range(1, max_level + 1)]
    return {"type": PRODUCTION_SPEED_EFFECT,
        "base": 0,
        "growth": 1,
        "step": 0,
        "values": values}


def apply_buildings(planets_seed: dict, buildings: dict, cap_code: str) -> dict[str, dict[str, int]]:
    """Writes the requirements and the hand-made effects of the buildings.

    Returns the sciences every building asks for on its first level, for the effects that open
    the buildings in the tree of the sciences.
    """
    source_by_code = {building["code"]: building for building in buildings["buildings"]}
    first_level_needs: dict[str, dict[str, int]] = {}
    for building in planets_seed["building_types"]:
        source = source_by_code.get(building["code"])
        if source is None:
            sys.exit(f"The balance has no building {building['code']}.")
        deposits = [requirement for requirement in building["requirements"] if requirement["type"] == DEPOSIT]
        written = []
        for requirement in source["requirements"]:
            target = requirement["target"]
            level = requirement["subject_level"]
            if target == cap_code and requirement["target_level"] <= level:
                continue
            written.append({"type": requirement["type"],
                "target": target,
                "target_level": requirement["target_level"],
                "subject_level": level})
        building["requirements"] = sorted(written + deposits, key=requirement_key)
        first_level_needs[building["code"]] = {requirement["target"]: requirement["target_level"]
            for requirement in written
            if requirement["subject_level"] == 1 and requirement["type"] == TECHNOLOGY_LEVEL}
        apply_manual_effects(building, source)
        building["effects"] = [effect for effect in building["effects"] if effect["type"] != PRODUCTION_SPEED_EFFECT]
        if building["building_class"] == PRODUCTION_CLASS:
            building["effects"].append(production_speed_effect(building["max_level"]))
    return first_level_needs


def apply_manual_effects(building: dict, source: dict) -> None:
    """Writes the values of every level of an effect the author changed by hand."""
    manual = source.get("manual_effects") or {}
    if not manual:
        return
    exported = source.get("effects") or []
    for effect_index, by_level in manual.items():
        if not by_level:
            continue
        index = int(effect_index)
        if index >= len(exported) or index >= len(building["effects"]):
            sys.exit(f"The hand-made effect {index} of {building['code']} has no effect in the seed.")
        if exported[index]["type"] != building["effects"][index]["type"]:
            sys.exit(f"The hand-made effect {index} of {building['code']} is not of the type the seed has there.")
        building["effects"][index]["values"] = exported[index]["values"]


def apply_ladder(production_seed: dict, ladder: dict) -> None:
    """Replaces the requirements of the recipes of the hulls and the modules."""
    needs_by_part: dict[str, list[dict]] = {}
    for row in ladder["ladder"]:
        needs_by_part[row["hull"]] = row["requirements"]
        for module in row["modules"]:
            needs_by_part[module["part"]] = module["requirements"]
    missing = []
    for recipe in production_seed["recipes"]:
        part = recipe.get("part")
        if part is None:
            recipe["requirements"] = []
            continue
        if part not in needs_by_part:
            missing.append(part)
            continue
        recipe["requirements"] = [{"type": requirement["type"],
            "target": requirement["target"],
            "target_level": requirement["target_level"]}
            for requirement in needs_by_part[part]
            if not (requirement["target"] == recipe["factory"] and requirement["target_level"] <= 1)]
    if missing:
        sys.exit(f"The ladder has no place for the parts {missing}.")


def main() -> None:
    research_seed = load(RESEARCH_SEED)
    planets_seed = load(PLANETS_SEED)
    production_seed = load(PRODUCTION_SEED)
    cap_code = cap_building_code(planets_seed)
    first_level_needs = apply_buildings(planets_seed, load(BUILDINGS_FILE), cap_code)
    apply_tree(research_seed, load(TREE_FILE), first_level_needs)
    apply_ladder(production_seed, load(LADDER_FILE))
    dump(RESEARCH_SEED, research_seed)
    dump(PLANETS_SEED, planets_seed)
    dump(PRODUCTION_SEED, production_seed)
    print("The balance is written into the seed.")


if __name__ == "__main__":
    main()
