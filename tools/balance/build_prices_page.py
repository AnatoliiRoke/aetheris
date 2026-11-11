"""Builds the balance page of building and technology prices from the seed files.

The page (documentation/references/balance/tsiny-budivel-i-nauk.html) carries the seed data
inside it: every number on it comes from the seed, nothing is typed into the template.
Run from the repository root: python3 tools/balance/build_prices_page.py
"""
import json
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
TEMPLATE_PATH = Path(__file__).resolve().parent / "prices_page_template.html"
OUTPUT_PATH = REPOSITORY / "documentation/references/balance/tsiny-budivel-i-nauk.html"
PLANETS_SEED = REPOSITORY / "server/src/Aetheris.Planets/Data/planets-reference-seed.json"
RESEARCH_SEED = REPOSITORY / "server/src/Aetheris.Research/Data/research-reference-seed.json"
WORLD_SEED = REPOSITORY / "server/src/Aetheris.World/Data/world-reference-seed.json"
PRODUCTION_SEED = REPOSITORY / "server/src/Aetheris.Production/Data/production-reference-seed.json"
SUBJECT_FIELDS = ("code", "sort_order", "max_level", "duration_seconds", "costs", "effects", "requirements")


def load_json(path):
    with open(path, encoding="utf-8") as seed_file:
        return json.load(seed_file)


def subject_of(seed_row, extra_fields):
    subject = {field: seed_row[field] for field in SUBJECT_FIELDS}
    subject["name"] = seed_row["name"]["uk"]
    for field in extra_fields:
        subject[field] = seed_row.get(field)
    return subject


def build_page_data():
    planets = load_json(PLANETS_SEED)
    research = load_json(RESEARCH_SEED)
    world = load_json(WORLD_SEED)
    production = load_json(PRODUCTION_SEED)
    return {"resource_groups": [{"code": group["code"],
            "name": group["name"]["uk"]} for group in world["resource_groups"]],
        "resources": [{"code": resource["code"],
            "name": resource["name"]["uk"],
            "group": resource["group"],
            "paid_by_population": resource.get("paid_by_population", False),
            "earns_population_income": resource.get("earns_population_income", False)} for resource in world["resources"]],
        "start_building_level": min(race["start_building_level"] for race in world["races"]),
        "categories": [{"code": category["code"],
            "name": category["name"]["uk"]} for category in planets["categories"]],
        "branches": [{"code": branch["code"],
            "name": branch["name"]["uk"]} for branch in research["branches"]],
        "buildings": [subject_of(building, ("category", "is_start_building")) for building in planets["building_types"]],
        "technologies": [subject_of(technology, ("branch",)) for technology in research["technologies"]],
        "resource_recipes": [{"code": recipe["code"],
            "resource": recipe["resource"],
            "sort_order": recipe["sort_order"],
            "output_quantity": recipe["output_quantity"],
            "ingredients": [{"resource": ingredient["resource"],
                "amount": ingredient["amount"]} for ingredient in recipe["ingredients"] if ingredient["resource"]]} for recipe in production["recipes"] if recipe["resource"]]}


def main():
    page_data = json.dumps(build_page_data(), ensure_ascii=False, separators=(",", ":"))
    template = TEMPLATE_PATH.read_text(encoding="utf-8")
    OUTPUT_PATH.write_text(template.replace("__SEED_DATA__", page_data.replace("</", "<\\/")), encoding="utf-8")
    print(OUTPUT_PATH)


if __name__ == "__main__":
    main()
