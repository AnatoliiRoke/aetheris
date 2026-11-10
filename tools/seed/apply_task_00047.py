"""Renames the extraction modules and changes their requirements (task 00047).

The task changes three parts of the extraction group, the requirements of four recipes and
the ingredients of the pulse reactor. The file is edited in place, so a repeated run gives
the same result.

Usage: python3 tools/seed/apply_task_00047.py
"""

import io
import json

PATH = 'server/src/Aetheris.Production/Data/production-reference-seed.json'

# The new names of the three modules (task 00047, section 3.1).
RENAMES = {
    'part_m_53': ('Модуль геологорозвідки', 'Survey module', 'Екстрактор гелію', 'Helium extractor'),
    'part_m_56': ('Збирач ефіру', 'Ether collector', 'Екстрактор ефіру', 'Ether extractor'),
    'part_m_57': ('Туманнісний збирач', 'Nebula collector', 'Екстрактор матерії', 'Matter extractor'),
}

# The new requirements of the recipes (task 00047, section 3.2): the recipe, the level of
# the drilling factory and whether the requirement is added or moved.
REQUIREMENTS = {
    'recipe_55': 3,
    'recipe_53': 5,
    'recipe_56': 7,
    'recipe_57': 9,
}

# The new ingredient of the pulse reactor (task 00047, section 3.4).
DEUTERIUM = ('resource_12', 25)


def rename_parts(data):
    """Renames the three modules and their recipes."""
    for part in data['parts']:
        names = RENAMES.get(part['code'])
        if not names:
            continue
        old_uk, old_en, new_uk, new_en = names
        assert part['name']['uk'] in (old_uk, new_uk), (part['code'], part['name']['uk'])
        part['name']['uk'] = new_uk
        part['name']['en'] = new_en

    for recipe in data['recipes']:
        names = RENAMES.get(recipe.get('part'))
        if not names:
            continue
        old_uk, old_en, new_uk, new_en = names
        assert recipe['name']['uk'] in (old_uk, new_uk), (recipe['code'], recipe['name']['uk'])
        recipe['name']['uk'] = new_uk
        recipe['name']['en'] = new_en
        description = 'Виробництво «%s».' % new_uk
        assert recipe['description']['uk'] in ('Виробництво «%s».' % old_uk, description), recipe['code']
        recipe['description']['uk'] = description


def change_requirements(data):
    """Sets the level of the drilling factory every recipe asks for."""
    for recipe in data['recipes']:
        level = REQUIREMENTS.get(recipe['code'])
        if level is None:
            continue
        requirements = [item for item in recipe['requirements'] if item['type'] == 'building_level' and item['target'] == 'building_47']
        if requirements:
            for item in requirements:
                item['target_level'] = level
        else:
            recipe['requirements'].append({'type': 'building_level', 'target': 'building_47', 'target_level': level})


def change_characteristics(data):
    """Makes the helium extractor an extractor: no scanning, the yield of the drill rig."""
    for part in data['parts']:
        if part['code'] != 'part_m_53':
            continue
        characteristics = part['characteristics']
        for code in ('char_scan_radius', 'char_scan_speed'):
            characteristics.pop(code, None)
        characteristics['char_yield'] = {'number': 1400}


def add_deuterium(data):
    """Adds the deuterium to the pulse reactor."""
    for recipe in data['recipes']:
        if recipe['code'] != 'recipe_8':
            continue
        ingredients = recipe['ingredients']
        existing = [item for item in ingredients if item.get('resource') == DEUTERIUM[0]]
        if existing:
            assert len(existing) == 1, 'the reactor has the deuterium twice'
            assert existing[0]['amount'] == DEUTERIUM[1], existing[0]
            continue
        ingredients.append({'resource': DEUTERIUM[0], 'part': None, 'amount': DEUTERIUM[1]})


def main():
    with io.open(PATH, encoding='utf-8') as source:
        data = json.load(source)

    rename_parts(data)
    change_requirements(data)
    change_characteristics(data)
    add_deuterium(data)

    with io.open(PATH, 'w', encoding='utf-8') as target:
        json.dump(data, target, ensure_ascii=False, indent=2)

    print('ok 00047')


if __name__ == '__main__':
    main()
