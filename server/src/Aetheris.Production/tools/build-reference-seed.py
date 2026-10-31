"""Builds the starting catalog of the production module (task 00013, decision 2.12).

The catalog covers every group of the modules document and the five produced resources,
with the working names of the document and the example recipes of its section 9.4. The
numbers of the costs and the times are starting values; the simulator tunes them later.

Run from the folder of the module:

    python3 tools/build-reference-seed.py
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from characteristics import CHARACTERISTICS, GROUP_CHARACTERISTICS

HERE = os.path.dirname(os.path.abspath(__file__))
TARGET = os.path.join(HERE, os.pardir, "Data", "production-reference-seed.json")

# The five produced resources, the factory that makes them and their ingredients.
PRODUCED_RESOURCES = [
    {
        "resource": "resource_13",
        "factory": "building_13",
        "name": {"uk": "Будматеріали", "en": "Building materials"},
        "description": {"uk": "Плити й балки для будівель і корпусів.", "en": "The plates and the beams for the buildings and the hulls."},
        "ingredients": [{"resource": "resource_1", "amount": 4}, {"resource": "resource_3", "amount": 1}],
        "duration": 40,
    },
    {
        "resource": "resource_14",
        "factory": "building_13",
        "name": {"uk": "Оптоволокно", "en": "Optical fibre"},
        "description": {"uk": "Нитки світла для сенсорів і зв'язку.", "en": "Threads of light for the sensors and the communication."},
        "ingredients": [{"resource": "resource_5", "amount": 3}, {"resource": "resource_6", "amount": 1}],
        "duration": 50,
    },
    {
        "resource": "resource_15",
        "factory": "building_13",
        "name": {"uk": "Мікропроцесори", "en": "Microprocessors"},
        "description": {"uk": "Серце будь-якої електроніки.", "en": "The heart of any electronics."},
        "ingredients": [{"resource": "resource_6", "amount": 2}, {"resource": "resource_14", "amount": 2}, {"resource": "resource_3", "amount": 1}],
        "duration": 70,
    },
    {
        "resource": "resource_16",
        "factory": "building_13",
        "name": {"uk": "Композити", "en": "Composites"},
        "description": {"uk": "Міцні матеріали для важких корпусів і броні.", "en": "Strong materials for the heavy hulls and the armour."},
        "ingredients": [{"resource": "resource_2", "amount": 3}, {"resource": "resource_13", "amount": 2}],
        "duration": 90,
    },
    {
        "resource": "resource_17",
        "factory": "building_13",
        "name": {"uk": "Енергострижні", "en": "Power strips"},
        "description": {"uk": "Джерела енергії для генераторів.", "en": "The sources of energy for the generators."},
        "ingredients": [{"resource": "resource_7", "amount": 2}, {"resource": "resource_15", "amount": 1}],
        "duration": 80,
    },
]

# The parts of every group: the code of the group, the working name, the factory that
# makes them, the ingredients of the first level and the attributes of the part.
PART_GROUPS = [
    {
        "group": "part_group_1",
        "factory": "building_13",
        "name": {"uk": "Корпус «Шершня»", "en": "Hull \"Wasp\""},
        "description": {"uk": "Легкий корпус для розвідників і дронів.", "en": "A light hull for the scouts and the drones."},
        "ingredients": [{"resource": "resource_1", "amount": 12}, {"resource": "resource_13", "amount": 4}],
        "duration": 60,
        "attributes": {"slot_size": {"code": "compact"}, "mass": {"number": 40}},
        "race": None,
    },
    {
        "group": "part_group_2",
        "factory": "building_13",
        "name": {"uk": "Реактивний двигун", "en": "Reaction engine"},
        "description": {"uk": "Дешевий двигун на дейтерії для легких кораблів.", "en": "A cheap deuterium engine for the light ships."},
        "ingredients": [{"resource": "resource_8", "amount": 6}, {"resource": "resource_15", "amount": 2}],
        "duration": 70,
        "attributes": {"energy": {"number": 10}, "mass": {"number": 25}, "slot_size": {"code": "heavy"}},
        "race": "race_1",
    },
    {
        "group": "part_group_3",
        "factory": "building_13",
        "name": {"uk": "Хімічний генератор", "en": "Chemical generator"},
        "description": {"uk": "Незвичайний генератор для транспортів і дронів.", "en": "A small generator for the transports and the drones."},
        "ingredients": [{"resource": "resource_3", "amount": 6}, {"resource": "resource_17", "amount": 1}],
        "duration": 55,
        "attributes": {"energy": {"number": 15}, "mass": {"number": 20}, "slot_size": {"code": "standard"}},
        "race": None,
    },
    {
        "group": "part_group_4",
        "factory": "building_13",
        "name": {"uk": "Життєзабезпечення", "en": "Life support"},
        "description": {"uk": "Підтримує екіпаж корабля.", "en": "Keeps the crew of the ship alive."},
        "ingredients": [{"resource": "resource_13", "amount": 3}, {"resource": "resource_15", "amount": 1}],
        "duration": 50,
        "attributes": {"crew": {"number": 20}, "energy": {"number": 5}, "mass": {"number": 15}, "slot_size": {"code": "standard"}},
        "race": None,
    },
    {
        "group": "part_group_5",
        "factory": "building_13",
        "name": {"uk": "Легкий лазер", "en": "Light laser"},
        "description": {"uk": "Дешевий точний лазер для легких кораблів.", "en": "A cheap and precise laser for the light ships."},
        "ingredients": [{"resource": "resource_3", "amount": 4}, {"resource": "resource_6", "amount": 2}, {"resource": "resource_15", "amount": 2}],
        "duration": 65,
        "attributes": {"damage": {"number": 10}, "damage_kind": {"code": "energy"}, "range": {"number": 300}, "energy": {"number": 8}, "mass": {"number": 10}, "slot_size": {"code": "compact"}},
        "race": "race_3",
    },
    {
        "group": "part_group_6",
        "factory": "building_13",
        "name": {"uk": "Сталеві плити", "en": "Steel plates"},
        "description": {"uk": "Базова пасивна броня для легких і середніх кораблів.", "en": "The basic passive armour for the light and middle ships."},
        "ingredients": [{"resource": "resource_1", "amount": 8}, {"resource": "resource_13", "amount": 2}],
        "duration": 60,
        "attributes": {"mass": {"number": 30}, "slot_size": {"code": "standard"}},
        "race": "race_2",
    },
    {
        "group": "part_group_7",
        "factory": "building_13",
        "name": {"uk": "Радар", "en": "Radar"},
        "description": {"uk": "Виявляє ворожі флоти заздалегідь.", "en": "Finds the hostile fleets in advance."},
        "ingredients": [{"resource": "resource_5", "amount": 4}, {"resource": "resource_14", "amount": 2}, {"resource": "resource_15", "amount": 1}],
        "duration": 60,
        "attributes": {"energy": {"number": 6}, "mass": {"number": 8}, "slot_size": {"code": "compact"}},
        "race": "race_4",
    },
    {
        "group": "part_group_8",
        "factory": "building_13",
        "name": {"uk": "Бур", "en": "Drill"},
        "description": {"uk": "Видобуває метали з надр планети.", "en": "Extracts the metals from the ground of the body."},
        "ingredients": [{"resource": "resource_1", "amount": 6}, {"resource": "resource_2", "amount": 2}, {"resource": "resource_13", "amount": 2}],
        "duration": 60,
        "attributes": {"energy": {"number": 12}, "mass": {"number": 25}, "slot_size": {"code": "standard"}},
        "race": "race_2",
    },
    {
        "group": "part_group_9",
        "factory": "building_13",
        "name": {"uk": "Вантажний відсік", "en": "Cargo bay"},
        "description": {"uk": "Місце для вантажу, деталей і боєприпасів.", "en": "A place for the cargo, the parts and the ammunition."},
        "ingredients": [{"resource": "resource_1", "amount": 5}, {"resource": "resource_13", "amount": 3}],
        "duration": 45,
        "attributes": {"mass": {"number": 12}, "slot_size": {"code": "standard"}},
        "race": "race_3",
    },
    {
        "group": "part_group_10",
        "factory": "building_13",
        "name": {"uk": "Командунний модуль", "en": "Command module"},
        "description": {"uk": "Місце для командира корабля.", "en": "A place for the commander of the ship."},
        "ingredients": [{"resource": "resource_13", "amount": 4}, {"resource": "resource_15", "amount": 2}, {"resource": "resource_17", "amount": 1}],
        "duration": 80,
        "attributes": {"crew": {"number": 3}, "energy": {"number": 10}, "mass": {"number": 18}, "slot_size": {"code": "standard"}},
        "race": None,
    },
    {
        "group": "part_group_11",
        "factory": "building_13",
        "name": {"uk": "Снаряди", "en": "Shells"},
        "description": {"uk": "Боєприпаси кінетичних гармат і артилерії.", "en": "The ammunition of the kinetic guns and the artillery."},
        "ingredients": [{"resource": "resource_1", "amount": 3}, {"resource": "resource_13", "amount": 1}],
        "duration": 30,
        "attributes": {"damage": {"number": 6}, "damage_kind": {"code": "kinetic"}, "mass": {"number": 2}},
        "race": None,
    },
]

# The one-shot device of the terraforming (task 00014): it has no mark levels, because a ship
# turns it on once and it is spent. It comes after the parts of the groups, so its number is
# the last one.
TERRAFORMING_DEVICE = {
    "part": {
        "code": "part_34",
        "group": "part_group_10",
        "mark_level": 1,
        "sort_order": 34,
        "name": {"uk": "Пристрій тераформінгу Mk I", "en": "Terraforming device Mk I"},
        "description": {
            "uk": "Одноразовий пристрій: корабель на орбіті вмикає його, і родовища планети стають багатими.",
            "en": "A one-shot device: a ship on the orbit turns it on and the deposits of the body become rich.",
        },
        "attributes": {
            "mass": {"number": 30.0},
            "slot_size": {"code": "standard"},
            "terraforming_device": {},
        },
        "weight": 30.0,
    },
    "recipe": {
        "code": "recipe_39",
        "part": "part_34",
        "factory": "building_13",
        "output_quantity": 1,
        "duration_seconds": 1800,
        "sort_order": 39,
        "name": {"uk": "Пристрій тераформінгу Mk I", "en": "Terraforming device Mk I"},
        "description": {
            "uk": "Дорогий одноразовий пристрій для тераформінгу планети.",
            "en": "An expensive one-shot device for the terraforming of a body.",
        },
        "ingredients": [
            {"resource": "resource_1", "amount": 200},
            {"resource": "resource_13", "amount": 60},
            {"resource": "resource_15", "amount": 40},
        ],
        "requirements": [],
        "race": None,
    },
}

# The three mark levels: the second and the third cost more and take longer.
MARK_LEVELS = [
    {"level": 1, "cost_factor": 1.0, "duration_factor": 1.0, "attribute_factor": 1.0},
    {"level": 2, "cost_factor": 1.8, "duration_factor": 1.5, "attribute_factor": 1.15},
    {"level": 3, "cost_factor": 3.2, "duration_factor": 2.3, "attribute_factor": 1.3},
]


def scale_attributes(attributes, factor):
    """Grows the numeric attributes of a part with its mark level."""
    grown = {}

    for key, value in attributes.items():
        if "number" in value:
            grown[key] = {"number": round(value["number"] * factor, 2)}
        else:
            grown[key] = value

    return grown


def weight_of(attributes, factor):
    """The weight of one part in tonnes (task 00027).

    The weight of a part is its own number, and the starting value is the mass the group
    already declares; a part without a mass weighs the default of the seed.
    """
    mass = attributes.get("mass", {}).get("number")

    return round(mass * factor, 2) if mass is not None else 1


def build_parts(first_recipe_number):
    """Builds the parts of every group, one row per mark level.

    The recipes of the parts take their numbers after the recipes of the produced resources,
    so every recipe of the file has a code of its own.
    """
    parts = []
    recipes = []
    part_number = 0
    recipe_number = first_recipe_number

    for group in PART_GROUPS:
        for mark in MARK_LEVELS:
            part_number += 1
            recipe_number += 1

            part_code = f"part_{part_number}"
            recipe_code = f"recipe_{recipe_number}"

            parts.append({
                "code": part_code,
                "group": group["group"],
                "mark_level": mark["level"],
                "sort_order": part_number,
                "name": {
                    "uk": f"{group['name']['uk']} Mk {['I', 'II', 'III'][mark['level'] - 1]}",
                    "en": f"{group['name']['en']} Mk {['I', 'II', 'III'][mark['level'] - 1]}",
                },
                "description": group["description"],
                "attributes": scale_attributes(group["attributes"], mark["attribute_factor"]),
                "weight": weight_of(group["attributes"], mark["attribute_factor"]),
            })

            ingredients = [
                {
                    "resource": ingredient["resource"],
                    "amount": round(ingredient["amount"] * mark["cost_factor"]),
                }
                for ingredient in group["ingredients"]
            ]

            recipes.append({
                "code": recipe_code,
                "part": part_code,
                "factory": group["factory"],
                "output_quantity": 1,
                "duration_seconds": round(group["duration"] * mark["duration_factor"]),
                "sort_order": recipe_number,
                "name": {
                    "uk": f"{group['name']['uk']} Mk {['I', 'II', 'III'][mark['level'] - 1]}",
                    "en": f"{group['name']['en']} Mk {['I', 'II', 'III'][mark['level'] - 1]}",
                },
                "description": group["description"],
                "ingredients": ingredients,
                "requirements": [],
                # The top module of the group is made by its race alone; the other parts of the
                # group are made by every race, and only the ones of that race carry its mark.
                "race": group["race"] if mark["level"] == 1 else None,
            })

    parts.append(TERRAFORMING_DEVICE["part"])
    recipes.append(TERRAFORMING_DEVICE["recipe"])

    return parts, recipes


def build_produced_resources():
    """Builds the recipes of the five produced resources."""
    recipes = []
    number = 0

    for resource in PRODUCED_RESOURCES:
        number += 1

        recipes.append({
            "code": f"recipe_{number}",
            "resource": resource["resource"],
            "factory": resource["factory"],
            "output_quantity": 1,
            "duration_seconds": resource["duration"],
            "sort_order": number,
            "name": resource["name"],
            "description": resource["description"],
            "ingredients": resource["ingredients"],
            "requirements": [],
            "race": None,
        })

    return recipes


def build_race_bonuses():
    """Builds the bonuses of the races in the groups of the parts (decision 2.8).

    Every race has a bonus in the group it specialises in and in two peaceful groups, as
    the race document describes. The multiplier is a starting value the simulator tunes.
    """
    bonuses = []

    groups = {
        "race_1": ["part_group_2", "part_group_9", "part_group_10"],
        "race_2": ["part_group_6", "part_group_8", "part_group_4"],
        "race_3": ["part_group_5", "part_group_9", "part_group_1"],
        "race_4": ["part_group_7", "part_group_6", "part_group_4"],
    }

    for race, race_groups in groups.items():
        for group in race_groups:
            bonuses.append({
                "race": race,
                "group": group,
                "multiplier": 1.1,
                "is_exclusive": False,
            })

    return bonuses


def main():
    """Writes the catalog file."""
    produced = build_produced_resources()
    parts, recipes = build_parts(len(produced))
    recipes = produced + recipes

    with open(os.path.join(HERE, os.pardir, "Data", "production-reference-seed.json"), encoding="utf-8") as source:
        catalog = json.load(source)

    catalog["parts"] = parts
    catalog["recipes"] = recipes
    catalog["race_bonuses"] = build_race_bonuses()
    catalog["characteristics"] = CHARACTERISTICS
    catalog["group_characteristics"] = GROUP_CHARACTERISTICS

    with open(TARGET, "w", encoding="utf-8") as target:
        json.dump(catalog, target, ensure_ascii=False, indent=2)
        target.write("\n")

    print(
        f"parts: {len(parts)}, recipes: {len(recipes)}, bonuses: {len(catalog['race_bonuses'])}, "
        f"characteristics: {len(catalog['characteristics'])}, group links: {len(catalog['group_characteristics'])}"
    )


if __name__ == "__main__":
    main()
