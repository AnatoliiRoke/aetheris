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
# defensive ones are of the combat, the cargo ones and the colonial one are of the transport.
PART_ROLES = {
    "part_m_63": "combat",
    "part_m_66": "transport",
    "part_m_67": "transport",
    "part_m_68": "combat",
    "part_m_69": "transport",
    "part_m_70": "transport",
}

# The kind of the mandatory component of a module, by the group of the module. The drive gives
# the ship its movement, the reactor gives it the energy and the life support gives it the crew
# (decision 9).
PART_GROUP_COMPONENTS = {
    "pg_1": "drive",
    "pg_2": "reactor",
}

# The life support of the ship is the module of the crew. The list of the author holds one such
# module in the group of the other ship modules, so it fills the place of the life support
# until the catalogue holds the life support itself.
PART_COMPONENTS = {
    "part_m_64": "life_support",
}

# The groups of the hulls whose race gives one more slot (decision 11).
RACE_BONUS_SLOT_GROUPS = ("pg_15", "pg_16")

# The groups of the hulls a ship may be built of: the combat groups and the group of the
# transports. The drones are carried in the hold and the structures are not ships.
SHIP_HULL_GROUPS = ("pg_12", "pg_14", "pg_15", "pg_16", "pg_17")

# The parts that run on their own fuel: the drives and the life support. Their energy of the
# reactor is nothing, so the power of the reactors pays for the modules of the ship (decision 9:
# "the reactor limits the modules").
FUEL_PARTS = ("pg_1", "part_m_64")


def main() -> None:
    with open(SEED, encoding="utf-8") as handle:
        data = json.load(handle, object_pairs_hook=collections.OrderedDict)

    ship_hulls = [part["code"] for part in data["parts"]
                  if part["group"] in SHIP_HULL_GROUPS]

    # The life support is the module of the crew, so every hull a ship may be built of takes
    # it: without it the ship has no crew and cannot be saved (decision 9).
    links = {row["part"]: list(row["hulls"]) for row in data["part_hulls"]}
    for part in data["parts"]:
        component = PART_COMPONENTS.get(part["code"]) or PART_GROUP_COMPONENTS.get(part["group"])
        if component != "life_support":
            continue

        current = links.get(part["code"], [])
        for hull in ship_hulls:
            if hull not in current:
                current.append(hull)

        links[part["code"]] = current

    for row in data["part_hulls"]:
        row["hulls"] = links[row["part"]]

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

    with open(SEED, "w", encoding="utf-8") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    hulls = sum(1 for part in data["parts"] if "hull_role" in part.get("attributes", {}))
    modules = sum(1 for part in data["parts"] if "role" in part.get("attributes", {}))
    components = sum(1 for part in data["parts"] if "component" in part.get("attributes", {}))
    bonus = sum(1 for part in data["parts"] if "char_hull_race_slot" in part.get("characteristics", {}))
    life = [row for row in data["part_hulls"]
            if row["part"] in PART_COMPONENTS and row["part"] in links
            and len(row["hulls"]) == len(ship_hulls)]
    print(f"hulls with a role: {hulls}, modules with a role: {modules}, "
          f"components: {components}, hulls with the extra slot of the race: {bonus}, "
          f"life support on every ship hull: {len(life)}")


if __name__ == "__main__":
    main()
