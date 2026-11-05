"""Add the ship data to the Production seed: the number of slots of a hull, the role of a hull,
the role of a module, the kind of the mandatory component of a module and the extra slot of the
race.

The roles come from the groups of the hulls: the combat groups take the parts of the combat,
the civil group takes the parts of the peace, and the transport group takes the transport. The
number of slots is a characteristic of the hulls; the modelling of the ships (task 00041) fills
the value of every hull, and until then the hull takes the number of the configuration.

The kind of the component tells the constructor which parts are the mandatory ones: the drive,
the reactor and the life support (decision 9). The extra slot of the race is a characteristic
of the heavy hulls and the super hulls (decision 11).
"""

import collections
import json
import os

BASE = "/Users/anatolijyakymets/Work/AETHERIS"
SEED = os.path.join(BASE, "server/src/Aetheris.Production/Data/production-reference-seed.json")

# The role of a hull comes from the group of the hull.
HULL_GROUP_ROLES = {
    "pg_12": "combat",
    "pg_13": "drone",
    "pg_14": "combat",
    "pg_15": "combat",
    "pg_16": "combat",
    "pg_17": "transport",
    "pg_18": "civil",
}

# The role of a module comes from the group of the module. The group of the other ship modules
# holds modules of different roles, so the modules that stand out have a role of their own.
PART_GROUP_ROLES = {
    "pg_1": "universal",
    "pg_2": "universal",
    "pg_3": "combat",
    "pg_4": "combat",
    "pg_5": "combat",
    "pg_6": "combat",
    "pg_7": "combat",
    "pg_8": "civil",
    "pg_9": "universal",
    "pg_10": "universal",
    "pg_11": "civil",
}

# The modules of the group of the other ship modules that have a role of their own: the
# defensive ones are of the combat, the colonial one is of the transport. The cargo ones and
# the module of transmission go on every hull, for the ships of war carry cargo too, so they
# are of no role of their own (the layout of the author, task 00042).
PART_ROLES = {
    "part_m_63": "combat",
    "part_m_66": "universal",
    "part_m_67": "universal",
    "part_m_68": "combat",
    "part_m_69": "universal",
    "part_m_70": "transport",
}

# The kind of the mandatory component of a module, by the group of the module. The drive gives
# the ship its movement and the reactor gives it the energy. There is no third one: the life
# support of the ship does not exist (the decision of the author, 05.11.2026).
PART_GROUP_COMPONENTS = {
    "pg_1": "drive",
    "pg_2": "reactor",
}

# The modules of a kind of their own, out of the groups of the modules.
PART_COMPONENTS: dict[str, str] = {}

# The life support of the ship does not exist: there is no such rule in the game (the decision
# of the author, 05.11.2026). The board computer is an ordinary module: it lowers the crew the
# ship needs and gives a bonus to the push, the shields and the scan speed.

# The groups of the hulls whose race gives one more slot (decision 11).
RACE_BONUS_SLOT_GROUPS = ("pg_15", "pg_16")

# The groups of the hulls a ship may be built of: the combat groups and the group of the
# transports. The drones are carried in the hold and the structures are not ships.
SHIP_HULL_GROUPS = ("pg_12", "pg_14", "pg_15", "pg_16", "pg_17")

# The parts that run on their own fuel: the drives and the life support. Their energy of the
# reactor is nothing, so the power of the reactors pays for the modules of the ship (decision 9:
# "the reactor limits the modules").
FUEL_PARTS = ("pg_1", "part_m_64")

# The parts that are not modules of a ship: the ammunition of the weapons and the capsules of
# the inhabitants. They are carried in the hold or used on a body, never fitted to a hull, so
# they never show in the constructor of the ships (the decision of the author).
NOT_SHIP_MODULE_GROUPS = ("pg_7",)
NOT_SHIP_MODULE_PARTS = ("part_m_80", "part_m_81", "part_m_82", "part_m_83")


def main() -> None:
    with open(SEED, encoding="utf-8") as handle:
        data = json.load(handle, object_pairs_hook=collections.OrderedDict)

    ship_hulls = [part["code"] for part in data["parts"]
                  if part["group"] in SHIP_HULL_GROUPS]

    # The life support of the ship does not exist, so no hull needs it (the decision of the
    # author, 05.11.2026). The belonging of the modules to the hulls is the layout of the
    # author (task 00042), and the seed keeps it as it is.

    characteristics = data["characteristics"]
    if not any(row["code"] == "char_hull_race_slot" for row in characteristics):
        characteristics.append(collections.OrderedDict([
            ("code", "char_hull_race_slot"),
            ("sort_order", 41),
            ("name", {"uk": "Додатковий слот раси", "en": "Race extra slot"}),
            ("description", {
                "uk": "Скільки додаткових слотів дає рас кораблю.",
                "en": "How many extra slots the race gives the ship.",
            }),
        ]))

    groups = data["group_characteristics"]
    existing = {(row["group"], row["characteristic"]) for row in groups}
    for group in sorted(HULL_GROUP_ROLES):
        if (group, "char_hull_slots") not in existing:
            groups.append(collections.OrderedDict([
                ("group", group),
                ("characteristic", "char_hull_slots"),
                ("sort_order", 4),
                ("is_required", False),
            ]))

    for group in RACE_BONUS_SLOT_GROUPS:
        if (group, "char_hull_race_slot") not in existing:
            groups.append(collections.OrderedDict([
                ("group", group),
                ("characteristic", "char_hull_race_slot"),
                ("sort_order", 5),
                ("is_required", False),
            ]))

    for part in data["parts"]:
        group = part["group"]
        code = part["code"]
        attributes = part.setdefault("attributes", collections.OrderedDict())

        if group in HULL_GROUP_ROLES:
            attributes["hull_role"] = collections.OrderedDict([("code", HULL_GROUP_ROLES[group])])
        else:
            role = PART_ROLES.get(code) or PART_GROUP_ROLES.get(group)
            if role is not None:
                attributes["role"] = collections.OrderedDict([("code", role)])

        component = PART_COMPONENTS.get(code) or PART_GROUP_COMPONENTS.get(group)
        if component is not None:
            attributes["component"] = collections.OrderedDict([("code", component)])

        if group in RACE_BONUS_SLOT_GROUPS:
            part.setdefault("characteristics", collections.OrderedDict())["char_hull_race_slot"] = collections.OrderedDict([
                ("number", 1),
            ])

        if group in FUEL_PARTS or code in FUEL_PARTS:
            characteristics = part.setdefault("characteristics", collections.OrderedDict())
            if "char_energy_use" in characteristics:
                characteristics["char_energy_use"] = collections.OrderedDict([("number", 0)])

        if group in NOT_SHIP_MODULE_GROUPS or code in NOT_SHIP_MODULE_PARTS:
            attributes["not_a_ship_module"] = collections.OrderedDict([("code", "true")])

    with open(SEED, "w", encoding="utf-8") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    hulls = sum(1 for part in data["parts"] if "hull_role" in part.get("attributes", {}))
    modules = sum(1 for part in data["parts"] if "role" in part.get("attributes", {}))
    components = sum(1 for part in data["parts"] if "component" in part.get("attributes", {}))
    bonus = sum(1 for part in data["parts"] if "char_hull_race_slot" in part.get("characteristics", {}))
    not_modules = sum(1 for part in data["parts"]
                       if "not_a_ship_module" in part.get("attributes", {}))
    print(f"hulls with a role: {hulls}, modules with a role: {modules}, "
          f"components: {components}, hulls with the extra slot of the race: {bonus}, "
          f"parts that are not modules of a ship: {not_modules}")


if __name__ == "__main__":
    main()
