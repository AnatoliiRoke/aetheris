"""Build the first seed from the chosen lists and the dependency file (task 00028).

The balance numbers live in server/src/Aetheris.World/Data/seed-coefficients.json.
Stock costs use those coefficients. Population of a building uses its own whole-number
curve. This script does not edit the dependency file.
"""

from __future__ import annotations

import json
import re
import sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEPENDENCY = ROOT / "documentation" / "references" / "zalezhnosti-rozklad.json"
PERELIK = {
    "resource": ROOT / "documentation" / "references" / "perelik-1-resursy.md",
    "tech": ROOT / "documentation" / "references" / "perelik-2-tekhnolohii.md",
    "module": ROOT / "documentation" / "references" / "perelik-3-moduli.md",
    "hull": ROOT / "documentation" / "references" / "perelik-4-korpusy.md",
    "building": ROOT / "documentation" / "references" / "perelik-6-budivli.md",
}
COEFFICIENTS = ROOT / "server" / "src" / "Aetheris.World" / "Data" / "seed-coefficients.json"
WORLD_SEED = ROOT / "server" / "src" / "Aetheris.World" / "Data" / "world-reference-seed.json"
PLANETS_SEED = ROOT / "server" / "src" / "Aetheris.Planets" / "Data" / "planets-reference-seed.json"
RESEARCH_SEED = ROOT / "server" / "src" / "Aetheris.Research" / "Data" / "research-reference-seed.json"
PRODUCTION_SEED = ROOT / "server" / "src" / "Aetheris.Production" / "Data" / "production-reference-seed.json"
PROCESS_SEED = ROOT / "server" / "src" / "Aetheris.Processes" / "Data" / "process-reference-seed.json"

ONE = Decimal(1)
SIX_PLACES = Decimal("0.000001")

RESOURCES = [
    ("Залізна руда", "Iron ore", "resource_group_1", "planet", False),
    ("Титанова руда", "Titanium ore", "resource_group_1", "planet", False),
    ("Іридієва руда", "Iridium ore", "resource_group_1", "planet", False),
    ("Кобальтова руда", "Cobalt ore", "resource_group_1", "planet", False),
    ("Уранова руда", "Uranium ore", "resource_group_1", "planet", False),
    ("Силікорова руда", "Silicore ore", "resource_group_1", "planet", False),
    ("Тектонітова руда", "Tectonite ore", "resource_group_1", "planet", False),
    ("Кварцит", "Quartzite", "resource_group_2", "planet", False),
    ("Обсидіан", "Obsidian", "resource_group_2", "planet", False),
    ("Лінтрій", "Lintrium", "resource_group_2", "planet", False),
    ("Орекс", "Orex", "resource_group_2", "planet", False),
    ("Дейтерій", "Deuterium", "resource_group_3", "planet", False),
    ("Гелій-3", "Helium-3", "resource_group_3", "planet", False),
    ("Темна матерія", "Dark matter", "resource_group_3", "planet", False),
    ("Ефір", "Ether", "resource_group_3", "planet", False),
    ("Збагачений Уран", "Enriched uranium", "resource_group_3", "planet", False),
    ("Квартан", "Quartan", "resource_group_4", "planet", False),
    ("Стобій", "Stobium", "resource_group_4", "planet", False),
    ("Туманій", "Tumanium", "resource_group_4", "planet", False),
    ("Будматеріали", "Building materials", "resource_group_5", "planet", False),
    ("Оптоволокно", "Optical fiber", "resource_group_5", "planet", False),
    ("Мікропроцесори", "Microprocessors", "resource_group_5", "planet", False),
    ("Композити", "Composites", "resource_group_5", "planet", False),
    ("Енергострижні", "Energy rods", "resource_group_5", "planet", False),
    ("Нановолокно", "Nanofiber", "resource_group_5", "planet", False),
    ("Титановий сплав", "Titanium alloy", "resource_group_5", "planet", False),
    ("Кредити", "Credits", "resource_group_6", "civilization", True),
    ("Торгова валюта", "Trade currency", "resource_group_6", "civilization", False),
    ("Енергія", "Energy", "resource_group_6", "planet", False),
    ("Населення", "Population", "resource_group_6", "planet", False),
    ("Реальна валюта", "Premium currency", "resource_group_6", "civilization", False),
]

GROUPS = [
    ("resource_group_1", "Руди", "Ores", True),
    ("resource_group_2", "Мінерали", "Minerals", True),
    ("resource_group_3", "Пальне", "Fuel", True),
    ("resource_group_4", "Кристали", "Crystals", True),
    ("resource_group_5", "Вироблені ресурси", "Manufactured", False),
    ("resource_group_6", "Баланси цивілізації", "Balances", False),
]

BUILDING_GROUPS = [
    ("Головні й населення", "Main and population"),
    ("Склади", "Storages"),
    ("Наука", "Science"),
    ("Видобуток на планеті", "Planetary extraction"),
    ("Енергетика", "Power"),
    ("Виробництво ресурсів", "Resource production"),
    ("Виробництво модулів", "Module production"),
    ("Флот і оборона", "Fleet and defence"),
    ("Торгівля й логістика", "Trade and logistics"),
]

BUILDINGS_EN = {
    "Адміністративний центр": "Administrative center",
    "Житловий комплекс (Міста)": "Housing complex",
    "Медичний центр": "Medical center",
    "Банк": "Bank",
    "Будівельний комбінат": "Construction plant",
    "Космопорт": "Spaceport",
    "Склад": "Warehouse",
    "Бункер": "Bunker",
    "Науковий центр": "Science center",
    "Конструкторське бюро": "Design bureau",
    "Археологічний центр": "Archaeology center",
    "Шахта залізної руди": "Iron ore mine",
    "Шахта титанової руди": "Titanium ore mine",
    "Шахта іридієвої руди": "Iridium ore mine",
    "Шахта кобальтової руди": "Cobalt ore mine",
    "Шахта уранової руди": "Uranium ore mine",
    "Шахта силікорової руди": "Silicore mine",
    "Шахта тектонітової руди": "Tectonite ore mine",
    "Кар'єр кварциту": "Quartzite quarry",
    "Кар'єр обсидіану": "Obsidian quarry",
    "Кар'єр лінтрію": "Lintrium quarry",
    "Кар'єр орексу": "Orex quarry",
    "Копальня квартану": "Quartan mine",
    "Копальня стобію": "Stobium mine",
    "Копальня туманію": "Tumanium mine",
    "Станція дейтерію": "Deuterium station",
    "Сонячна електростанція": "Solar power plant",
    "Теплова електростанція": "Thermal power plant",
    "Ядерна електростанція": "Nuclear power plant",
    "Термоядерна електростанція": "Fusion power plant",
    "Завод будматеріалів": "Building-materials plant",
    "Оптоволоконний завод": "Optical-fiber plant",
    "Фабрика мікропроцесорів": "Microprocessor factory",
    "Завод композитів": "Composites plant",
    "Завод енергострижнів": "Energy-rod plant",
    "Завод нановолокна": "Nanofiber plant",
    "Металургійний завод": "Metallurgical plant",
    "Завод збагачення урану": "Uranium enrichment plant",
    "Перетворювач матерії": "Matter converter",
    "Фабрика двигунів": "Engine factory",
    "Завод реакторів": "Reactor plant",
    "Завод енергетичної зброї": "Energy-weapon plant",
    "Завод кінетичної зброї": "Kinetic-weapon plant",
    "Ливарний завод": "Foundry",
    "Завод щитів": "Shield plant",
    "Фабрика боєприпасів": "Ammunition factory",
    "Фабрика бурових установок": "Drill factory",
    "Завод точних приладів": "Precision-instrument plant",
    "Індустріальний комплекс": "Industrial complex",
    "Фабрика утилізації": "Recycling factory",
    "Верф": "Shipyard",
    "Радар": "Radar",
    "Планетарна гармата": "Planetary gun",
    "Планетарний щит": "Planetary shield",
    "Торговий центр": "Trade center",
    "Портал": "Portal",
}

TECHS_EN = {
    "Математика": "Mathematics",
    "Фізика": "Physics",
    "Ядерна фізика": "Nuclear physics",
    "Термоядерний синтез": "Thermonuclear fusion",
    "Геологія": "Geology",
    "Електроніка": "Electronics",
    "Енергетика": "Power engineering",
    "Будівництво": "Construction",
    "Кібернетика": "Cybernetics",
    "Робототехніка": "Robotics",
    "Економіка": "Economics",
    "Археологія": "Archaeology",
    "Кристалографія": "Crystallography",
    "Газова інженерія": "Gas engineering",
    "Логістика складів": "Warehouse logistics",
    "Демографія": "Demography",
    "Колоніальне управління": "Colonial administration",
    "Торговельне право": "Trade law",
    "Стабілізація порталу": "Portal stabilization",
    "Перетворення матерії": "Matter conversion",
}

MODULES_EN = {
    "Реактивний двигун": "Jet engine",
    "Плазмовий двигун": "Plasma engine",
    "Іонний двигун": "Ion engine",
    "Гравітаційний двигун": "Gravity engine",
    "Гіпердвигун": "Hyperdrive",
    "Джамп-двигун": "Jump drive",
    "Ядерний реактор": "Nuclear reactor",
    "Імпульсний реактор": "Pulse reactor",
    "Термоядерний реактор": "Fusion reactor",
    "Реактор холодного синтезу": "Cold-fusion reactor",
    "Гравітаційний реактор": "Gravity reactor",
    "Важкий реактор": "Heavy reactor",
    "Легкий лазер": "Light laser",
    "Важкий лазер": "Heavy laser",
    "Імпульсний лазер": "Pulse laser",
    "Плазмова гармата": "Plasma cannon",
    "Іонна гармата": "Ion cannon",
    "Мікрохвильовий випромінювач": "Microwave emitter",
    "Частинковий прискорювач": "Particle accelerator",
    "Орексова гармата": "Orex cannon",
    "Кінетична гармата": "Kinetic cannon",
    "Скорострільна гармата": "Autocannon",
    "Рейкова гармата": "Railgun",
    "РПУ-X1": "Launcher X1",
    "РПУ-X1B": "Launcher X1B",
    "РПУ-X2": "Launcher X2",
    "РПУ-X3": "Launcher X3",
    "Сталева плита": "Steel plate",
    "Сегментна броня": "Segmented armor",
    "Кобальтова броня": "Cobalt armor",
    "Титанова плита": "Titanium plate",
    "Композитна броня": "Composite armor",
    "Іридієва плита": "Iridium plate",
    "Наноплита": "Nanoplate",
    "Адаптивна броня": "Adaptive armor",
    "Силовий щит": "Force shield",
    "Кварцовий щит": "Quartz shield",
    "Магнітний щит": "Magnetic shield",
    "Гравітаційний щит": "Gravity shield",
    "Нейтронний щит": "Neutron shield",
    "Плазмовий щит": "Plasma shield",
    "Орексовий щит": "Orex shield",
    "Ефірний щит": "Ether shield",
    "Сталевий снаряд": "Steel shell",
    "Кобальтовий снаряд": "Cobalt shell",
    "Рейковий стрижень": "Rail slug",
    "Стріла-X1": "Arrow X1",
    "Блискавка-X1": "Lightning X1",
    "Спис-X2": "Spear X2",
    "Валькірія-X3": "Valkyrie X3",
    "Термоядерна торпеда": "Fusion torpedo",
    "Протонна торпеда": "Proton torpedo",
    "Уранова торпеда": "Uranium torpedo",
    "Модуль геологорозвідки": "Survey module",
    "Бурова установка": "Drill rig",
    "Кристалічний екстрактор": "Crystal extractor",
    "Збирач ефіру": "Ether collector",
    "Туманнісний збирач": "Nebula collector",
    "Астросканер": "Astroscanner",
    "Корабельний радар": "Ship radar",
    "Археологічний модуль": "Archaeology module",
    "Радіооптичне маскування": "Radio-optical camouflage",
    "Детектор маскування": "Camouflage detector",
    "Відновлювач щитів": "Shield restorer",
    "Бортовий комп'ютер": "Onboard computer",
    "Збирач уламків": "Debris collector",
    "Вантажний відсік": "Cargo hold",
    "Великий вантажний відсік": "Large cargo hold",
    "Протиракетна система": "Missile-defence system",
    "Модуль передачі": "Transfer module",
    "Колоніальний модуль": "Colony module",
    "Кріокапсула (Террани)": "Cryocapsule (Terrans)",
    "Кріокапсула (Літаріони)": "Cryocapsule (Litari)",
    "Кріокапсула (Драккури)": "Cryocapsule (Drakkuri)",
    "Кріокапсула (Вейрани)": "Cryocapsule (Veirani)",
    "Житловий модуль": "Housing module",
    "Орбітальний склад": "Orbital warehouse",
    "Ремонтні майстерні": "Repair workshops",
    "Школа командирів": "Commander school",
    "Торговий центр": "Trade center",
    "Орбітальний термоядерний реактор": "Orbital fusion reactor",
    "Збагачувач ізотопів гелію": "Helium-isotope enricher",
    "Екстрактор темної матерії": "Dark-matter extractor",
    "Джамп-модуль": "Jump module",
}

HULLS_EN = {
    "Винищувач «Сапсан»": "Fighter Sapsan",
    "Важкий винищувач «Шершень»": "Heavy fighter Hornet",
    "Торпедоносець «Гадюка»": "Torpedo bomber Viper",
    "Важкий торпедоносець «Кобра»": "Heavy torpedo bomber Cobra",
    "«Ґедзь»": "Gadfly",
    "«Цикада»": "Cicada",
    "«Кліщ»": "Tick",
    "Корвет «Коршун»": "Corvette Kite",
    "Фрегат «Барракуда»": "Frigate Barracuda",
    "Есмінець «Акула»": "Destroyer Shark",
    "Легкий крейсер «Ягуар»": "Light cruiser Jaguar",
    "Крейсер «Носоріг»": "Cruiser Rhino",
    "Важкий крейсер «Бегемот»": "Heavy cruiser Behemoth",
    "Броненосець «Панцирник»": "Armored ship Pangolin",
    "Лінкор «Кракен»": "Battleship Kraken",
    "Важкий лінкор «Мамонт»": "Heavy battleship Mammoth",
    "Дредноут «Горгона»": "Dreadnought Gorgon",
    "Дестроєр «Каратель»": "Devastator Punisher",
    "Супер-лінкор «Тиранозавр»": "Super battleship Tyrannosaurus",
    "Супер-дредноут «Левіафан»": "Super dreadnought Leviathan",
    "Малий транспорт «Жук»": "Small transport Beetle",
    "Середній транспорт «Бик»": "Medium transport Bull",
    "Великий транспорт «Слон»": "Large transport Elephant",
    "Важкий транспорт «Кит»": "Heavy transport Whale",
    "Орбітальна база": "Orbital base",
    "Космобаза": "Space base",
    "Видобувна станція": "Mining station",
}

MODULE_GROUPS_EN = {
    "Двигуни": "Engines",
    "Реактори": "Reactors",
    "Енергетична зброя": "Energy weapons",
    "Кінетична зброя": "Kinetic weapons",
    "Броня": "Armor",
    "Щити": "Shields",
    "Боєприпаси": "Ammunition",
    "Видобуток": "Extraction",
    "Розвідка й маскування": "Recon and camouflage",
    "Інші корабельні": "Other ship modules",
    "Модулі бази": "Base modules",
}

HULL_GROUPS_EN = {
    "Легкі": "Light",
    "Дрони": "Drones",
    "Середні": "Medium",
    "Важкі": "Heavy",
    "Надважкі": "Super-heavy",
    "Цивільні": "Civilian",
    "Корпуси споруд": "Structure hulls",
}

MINE_RESOURCE = {
    "Шахта залізної руди": "Залізна руда",
    "Шахта титанової руди": "Титанова руда",
    "Шахта іридієвої руди": "Іридієва руда",
    "Шахта кобальтової руди": "Кобальтова руда",
    "Шахта уранової руди": "Уранова руда",
    "Шахта силікорової руди": "Силікорова руда",
    "Шахта тектонітової руди": "Тектонітова руда",
    "Кар'єр кварциту": "Кварцит",
    "Кар'єр обсидіану": "Обсидіан",
    "Кар'єр лінтрію": "Лінтрій",
    "Кар'єр орексу": "Орекс",
    "Копальня квартану": "Квартан",
    "Копальня стобію": "Стобій",
    "Копальня туманію": "Туманій",
    "Станція дейтерію": "Дейтерій",
}

PRODUCTS = {
    "Завод будматеріалів": "Будматеріали",
    "Оптоволоконний завод": "Оптоволокно",
    "Фабрика мікропроцесорів": "Мікропроцесори",
    "Завод композитів": "Композити",
    "Завод енергострижнів": "Енергострижні",
    "Завод нановолокна": "Нановолокно",
    "Металургійний завод": "Титановий сплав",
    "Завод збагачення урану": "Збагачений Уран",
}

MODULE_FACTORY = {
    "Двигуни": "Фабрика двигунів",
    "Реактори": "Завод реакторів",
    "Енергетична зброя": "Завод енергетичної зброї",
    "Кінетична зброя": "Завод кінетичної зброї",
    "Броня": "Ливарний завод",
    "Щити": "Завод щитів",
    "Боєприпаси": "Фабрика боєприпасів",
    "Видобуток": "Фабрика бурових установок",
    "Розвідка й маскування": "Завод точних приладів",
    "Інші корабельні": "Індустріальний комплекс",
    "Модулі бази": "Індустріальний комплекс",
}

POWER_PLANTS = {
    "Сонячна електростанція",
    "Теплова електростанція",
    "Ядерна електростанція",
    "Термоядерна електростанція",
}
UNSPENT_ENERGY = POWER_PLANTS | {"Склад"}

ENERGY_AMMO = {"Блискавка-X1", "Протонна торпеда", "Термоядерна торпеда"}
COSMIC = {"Гелій-3", "Темна матерія", "Ефір"}
COLLECTORS = {"Збирач ефіру", "Туманнісний збирач", "Екстрактор темної матерії"}
DEPOSIT_NAMES = {
    "Залізна руда", "Титанова руда", "Іридієва руда", "Кобальтова руда", "Уранова руда",
    "Силікорова руда", "Тектонітова руда", "Кварцит", "Обсидіан", "Лінтрій", "Орекс",
    "Дейтерій", "Квартан", "Стобій", "Туманій",
}
START_NAMES = {
    "Залізна руда": "world.seed.start.iron_ore",
    "Будматеріали": "world.seed.start.building_materials",
    "Кредити": "world.seed.start.credits",
}
START_BUILDINGS = {"Адміністративний центр", "Житловий комплекс (Міста)", "Склад", "Сонячна електростанція"}
METAL_NAMES = {
    "Залізна руда", "Титанова руда", "Іридієва руда", "Кобальтова руда", "Уранова руда",
    "Силікорова руда", "Тектонітова руда", "Дейтерій",
}

# The race of every capsule of inhabitants: the inhabitants belong to one race, every race makes its own capsules.
CAPSULE_RACES = {
    "Кріокапсула (Террани)": "race_1",
    "Кріокапсула (Літаріони)": "race_2",
    "Кріокапсула (Драккури)": "race_3",
    "Кріокапсула (Вейрани)": "race_4",
}

OTHER_STATS = {
    "Відновлювач щитів": ["char_shield_restore", "char_energy_use"],
    "Бортовий комп'ютер": ["char_fuel_use", "char_speed", "char_crew", "char_energy_use"],
    "Збирач уламків": ["char_collect_speed", "char_energy_use"],
    "Вантажний відсік": ["char_cargo", "char_energy_use"],
    "Великий вантажний відсік": ["char_cargo", "char_energy_use"],
    "Протиракетна система": ["char_missiles", "char_range", "char_energy_use"],
    "Модуль передачі": ["char_transfer_speed", "char_transfer_volume", "char_energy_use"],
    "Колоніальний модуль": ["char_deploy_time", "char_colonists", "char_energy_use"],
    "Кріокапсула (Террани)": ["char_inhabitants"],
    "Кріокапсула (Літаріони)": ["char_inhabitants"],
    "Кріокапсула (Драккури)": ["char_inhabitants"],
    "Кріокапсула (Вейрани)": ["char_inhabitants"],
}

BASE_STATS = {
    "Житловий модуль": ["char_population", "char_energy_use"],
    "Орбітальний склад": ["char_storage", "char_energy_use"],
    "Ремонтні майстерні": ["char_repair_speed", "char_energy_use"],
    "Школа командирів": ["char_training_speed", "char_energy_use"],
    "Торговий центр": ["char_trade_lots", "char_energy_use"],
    "Орбітальний термоядерний реактор": ["char_power"],
    "Збагачувач ізотопів гелію": ["char_yield", "char_energy_use"],
    "Екстрактор темної матерії": ["char_yield", "char_energy_use"],
    "Джамп-модуль": ["char_jump_prep", "char_energy_use"],
}

CHARACTERISTICS = [
    ("char_thrust", "Тяга", "Thrust", "Сила, яку двигун дає корпусу.", "Force the engine gives the hull."),
    ("char_fuel_use", "Витрата пального", "Fuel use", "Пальне, яке модуль витрачає.", "Fuel the module uses."),
    ("char_energy_use", "Споживання енергії", "Energy use", "Енергія, яку модуль споживає.", "Energy the module draws."),
    ("char_power", "Вироблена потужність", "Power output", "Потужність, яку модуль віддає.", "Power the module supplies."),
    ("char_damage", "Шкода", "Damage", "Шкода одного пострілу або одиниці.", "Damage of one shot or one unit."),
    ("char_rate", "Скорострільність", "Rate of fire", "Постріли за одиницю часу.", "Shots per unit of time."),
    ("char_range", "Дальність", "Range", "Дальність дії.", "Distance the module reaches."),
    ("char_magazine", "Боєкомплект", "Magazine", "Скільки пострілів тримає пускова.", "Shots a launcher holds."),
    ("char_integrity", "Міцність", "Integrity", "Міцність броні або корпусу.", "Integrity of the armor or the hull."),
    ("char_shield_capacity", "Ємність щита", "Shield capacity", "Напруженість щита.", "Strength of the shield."),
    ("char_shield_regen", "Відновлення щита", "Shield regeneration", "Як швидко щит відновлюється.", "How fast the shield recovers."),
    ("char_energy_block", "Блокування енергетичної шкоди", "Energy-damage block", "Частка енергетичної шкоди, яку щит знімає.", "Share of energy damage the shield stops."),
    ("char_kinetic_block", "Блокування кінетичної шкоди", "Kinetic-damage block", "Частка кінетичної шкоди, яку щит знімає.", "Share of kinetic damage the shield stops."),
    ("char_flight_speed", "Швидкість польоту", "Flight speed", "Швидкість ракети або торпеди.", "Speed of a missile or a torpedo."),
    ("char_launcher_size", "Розмір пускової", "Launcher size", "Розмір сумісної пускової.", "Size of the launcher that accepts the unit."),
    ("char_yield", "Виробіток", "Yield", "Скільки модуль добуває за час.", "How much the module extracts over time."),
    ("char_scan_radius", "Радіус сканування", "Scan radius", "Радіус геологорозвідки.", "Radius of a geological scan."),
    ("char_scan_speed", "Швидкість сканування", "Scan speed", "Як швидко йде сканування.", "How fast a scan proceeds."),
    ("char_detection", "Дальність виявлення", "Detection range", "Дальність, на якій модуль бачить ціль.", "Distance at which the module sees a target."),
    ("char_dig_speed", "Швидкість розкопок", "Dig speed", "Як швидко йдуть розкопки.", "How fast an excavation proceeds."),
    ("char_shield_restore", "Відновлення щитів", "Shield restore", "Одиниці щита, які модуль повертає за час.", "Shield units the module restores over time."),
    ("char_speed", "Швидкість", "Speed", "Внесок модуля у швидкість корпусу.", "What the module adds to the hull speed."),
    ("char_crew", "Екіпаж", "Crew", "Внесок модуля в чисельність екіпажу.", "What the module changes in the crew size."),
    ("char_collect_speed", "Швидкість збору", "Collection speed", "Як швидко модуль збирає уламки.", "How fast the module collects debris."),
    ("char_cargo", "Місткість вантажу", "Cargo capacity", "Вантаж, який відсік тримає.", "Cargo the hold carries.", {"uk": "т", "en": "t"}),
    ("char_missiles", "Знищені ракети", "Missiles destroyed", "Скільки ракет модуль знімає за час.", "Missiles the module stops over time."),
    ("char_transfer_speed", "Швидкість передачі", "Transfer speed", "Як швидко йде передача.", "How fast a transfer proceeds."),
    ("char_transfer_volume", "Обсяг передачі", "Transfer volume", "Скільки модуль передає за раз.", "How much the module sends at once."),
    ("char_deploy_time", "Час розгортання", "Deployment time", "Скільки триває розгортання.", "How long deployment takes.", {"uk": "с", "en": "s"}),
    ("char_colonists", "Місткість колоністів", "Colonist capacity", "Скільки колоністів модуль везе.", "Colonists the module carries."),
    ("char_inhabitants", "Мешканців у капсулі", "Inhabitants in the capsule", "Скільки мешканців додає одна капсула до населення планети.", "Inhabitants one capsule adds to the population of a body."),
    ("char_population", "Місткість населення", "Population capacity", "Скільки населення модуль вміщує.", "Population the module holds."),
    ("char_storage", "Місткість складу", "Storage capacity", "Скільки тонн тримає склад.", "Tonnes the warehouse holds.", {"uk": "т", "en": "t"}),
    ("char_repair_speed", "Швидкість ремонту", "Repair speed", "Як швидко йде ремонт.", "How fast a repair proceeds."),
    ("char_training_speed", "Швидкість навчання", "Training speed", "Як швидко йде навчання.", "How fast training proceeds."),
    ("char_trade_lots", "Кількість лотів", "Lot count", "Скільки лотів відкриває модуль.", "How many lots the module opens."),
    ("char_jump_prep", "Час підготовки стрибка", "Jump preparation", "Скільки триває підготовка стрибка.", "How long a jump takes to prepare.", {"uk": "с", "en": "s"}),
    ("char_hull_size", "Розмір", "Size", "Розмір корпусу.", "Size of the hull."),
    ("char_hull_capacity", "Вмістимість", "Capacity", "Скільки модуль місць має корпус.", "How much the hull holds."),
    ("char_upkeep", "Утримання", "Upkeep", "Скільки кредитів корпус або модуль вимагає за годину.", "How many credits a hull or a module costs per hour.", {"uk": "кредитів/год", "en": "credits/h"}),
]


def fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(1)


def num(value: Decimal) -> int | float:
    quantized = value.quantize(SIX_PLACES, rounding=ROUND_HALF_UP)
    if quantized == quantized.to_integral_value():
        return int(quantized)
    return float(quantized)


def whole(value: Decimal) -> int:
    return int(value.quantize(ONE, rounding=ROUND_HALF_UP))


def text(ukrainian: str, english: str) -> dict[str, str]:
    return {"uk": ukrainian, "en": english}


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def dump(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def coefficient(parameters: dict[str, Decimal], key: str) -> Decimal:
    if key not in parameters:
        fail(f"missing coefficient {key}")
    return parameters[key]


def load_coefficients() -> dict[str, Decimal]:
    payload = load_json(COEFFICIENTS)
    return {row["key"]: Decimal(row["value"]) for row in payload["parameters"]}


def english(table: dict[str, str], name: str, kind: str) -> str:
    if name not in table:
        fail(f"missing English {kind} name: {name}")
    return table[name]


def part_english(name: str) -> str:
    if name in MODULES_EN:
        return MODULES_EN[name]
    if name in HULLS_EN:
        return HULLS_EN[name]
    fail(f"missing English part name: {name}")


def growth(base: Decimal, factor: Decimal, step: Decimal = Decimal(0), digits: int = 0) -> dict:
    row = {"base": num(base), "growth": num(factor), "step": num(step)}
    if digits > 0:
        row["significant"] = digits
    return row


def tech_duration_of(parameters: dict[str, Decimal], branch: str, base: Decimal, levels: int, cap: Decimal) -> dict:
    """The duration of a technology of a branch, by the multiplier of its branch."""
    multiplier = coefficient(parameters, f"world.seed.duration.branch.{branch}")
    own_base = base * multiplier
    return growth(own_base, duration_growth_for(own_base, levels, cap), digits=2)


def duration_growth_for(base: Decimal, levels: int, cap: Decimal) -> Decimal:
    """The growth of the duration of a building, by its base and its levels.

    A type with fewer levels grows faster, so the last level of every type takes
    about the same time: a mine with twelve levels is built as long as a
    building with twenty. The growth is the largest one that keeps the last
    level under the cap, rounded down to a hundredth.
    """
    if levels <= 1:
        return ONE

    exact = (cap / base) ** (Decimal(1) / Decimal(levels - 1))
    steps = Decimal(100)
    return (exact * steps).to_integral_value(rounding=ROUND_FLOOR) / steps


def curve(base: Decimal, factor: Decimal, index: int) -> Decimal:
    return base * (factor ** index)


ORES = {
    "Залізна руда", "Титанова руда", "Іридієва руда", "Кобальтова руда", "Уранова руда",
    "Силікорова руда", "Тектонітова руда",
}
MINERALS = {"Кварцит", "Обсидіан", "Лінтрій", "Орекс"}
CRYSTALS = {"Квартан", "Стобій", "Туманій"}
FUELS = {"Дейтерій", "Гелій-3", "Темна матерія", "Ефір", "Збагачений Уран"}
PRODUCED = {"Оптоволокно", "Мікропроцесори", "Композити", "Енергострижні", "Нановолокно", "Титановий сплав"}

GROUP_MULTIPLIERS = {
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


def resource_scale(parameters: dict[str, Decimal], name: str) -> Decimal:
    """The share of the cost base a resource is paid in (a class of resources, data in the coefficients)."""
    if name in ORES:
        key = "group_1"
    elif name in MINERALS:
        key = "group_2"
    elif name in CRYSTALS:
        key = "group_4"
    elif name in FUELS:
        key = "group_3"
    elif name == "Будматеріали":
        key = "building_materials"
    elif name in PRODUCED:
        key = "group_5"
    elif name == "Кредити":
        key = "credits"
    else:
        fail(f"no cost scale for the resource {name}")
    return coefficient(parameters, f"world.seed.cost.scale.{key}")


def group_multiplier(parameters: dict[str, Decimal], group: str) -> Decimal:
    return coefficient(parameters, f"world.seed.cost.group.{GROUP_MULTIPLIERS[group]}")


def duration_group_multiplier(parameters: dict[str, Decimal], group: str) -> Decimal:
    """The multiplier of the duration of a building of a group, its own number."""
    return coefficient(parameters, f"world.seed.duration.group.{GROUP_MULTIPLIERS[group]}")


def significant(parameters: dict[str, Decimal]) -> int:
    return int(coefficient(parameters, "world.seed.cost.significant_digits"))


def rounded(value: Decimal, digits: int) -> Decimal:
    """Rounds a number to a few significant digits, as the server does when it reads the seed."""
    if digits <= 0 or value == 0:
        return value
    magnitude = value.copy_abs().adjusted() + 1
    exponent = Decimal(1).scaleb(magnitude - digits)
    if exponent <= ONE:
        return value.quantize(ONE, rounding=ROUND_HALF_UP)
    return (value / exponent).quantize(ONE, rounding=ROUND_HALF_UP) * exponent


def reserved_technology_codes() -> set[str]:
    found: set[str] = set()

    def walk(node: object) -> None:
        if isinstance(node, dict):
            for key, value in node.items():
                if key == "level_target" and isinstance(value, str) and value.startswith("tech_"):
                    found.add(value)
                else:
                    walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(load_json(PROCESS_SEED))
    if not found:
        fail("the process seed names no technology code")
    return found


def assign_technology_codes(techs: list[dict], reserved: set[str]) -> dict[str, str]:
    assigned: dict[str, str] = {}
    number = 1
    for tech in techs:
        while f"tech_{number}" in reserved:
            number += 1
        assigned[tech["name"]] = f"tech_{number}"
        number += 1
    return assigned


def first_seen(tiers: list[list[str]]) -> dict[str, int]:
    seen: dict[str, int] = {}
    for index, tier in enumerate(tiers):
        for name in tier:
            if name not in seen:
                seen[name] = index + 1
    return seen


def dropped(tiers: list[list[str]]) -> list[tuple[int, set[str]]]:
    drops: list[tuple[int, set[str]]] = []
    seen: set[str] = set()
    for index, tier in enumerate(tiers):
        current = set(tier)
        missing = seen - current
        if missing:
            drops.append((index + 1, missing))
        seen |= current
    return drops


def rising_requirements(levels: list[dict], names: dict[str, str]) -> list[dict]:
    rows: list[dict] = []
    previous: dict[str, int] = {}
    for index, sciences in enumerate(levels):
        for name, required in sciences.items():
            if name not in names:
                fail(f"unknown science name: {name}")
            if previous.get(name) != required:
                rows.append({
                    "type": "technology_level",
                    "target": names[name],
                    "target_level": required,
                    "subject_level": index + 1,
                })
                previous[name] = required
    return rows


def center_requirements(center_levels: list[int], science_center: str) -> list[dict]:
    rows: list[dict] = []
    previous = center_levels[0]
    for index, center in enumerate(center_levels):
        if index == 0:
            continue
        if center != previous:
            rows.append({
                "type": "building_level",
                "target": science_center,
                "target_level": center,
                "subject_level": index + 1,
            })
            previous = center
    return rows


# Energy is a balance, not a stock a building pays. Population is paid from the counter of the
# planet, so it is not scaled like a stock: every building pays the same whole inhabitants at a level.
NOT_STOCK = {"Енергія", "Населення"}
POPULATION_CAP = 1500


def population_rate(level: int) -> Decimal:
    """The growth of the population cost of a building when this level is reached.

    Levels 2 to 6 grow by about 60 to 75 percent. Levels 14 to 20 grow by about 12 to 14
    percent. The levels between them step down from one band to the other.
    """
    if level <= 6:
        local = Decimal(level - 2) / Decimal(4)
        return Decimal("0.70") + (Decimal("0.62") - Decimal("0.70")) * local
    if level >= 14:
        local = Decimal(level - 14) / Decimal(6)
        return Decimal("0.14") + (Decimal("0.12") - Decimal("0.14")) * local
    local = Decimal(level - 6) / Decimal(8)
    return Decimal("0.62") + (Decimal("0.14") - Decimal("0.62")) * local


def population_amounts(max_level: int) -> list[int]:
    """The inhabitants a building costs at each level, from 4, never above 1500."""
    amounts = [4]
    for level in range(2, max_level + 1):
        raw = Decimal(amounts[-1]) * (ONE + population_rate(level))
        amount = int(raw.quantize(ONE, rounding=ROUND_HALF_UP))
        if amount > POPULATION_CAP:
            amount = POPULATION_CAP
        amounts.append(amount)
    return amounts


def population_cost(code: str, max_level: int) -> dict:
    amounts = population_amounts(max_level)
    return {
        "resource": code,
        "base": amounts[0],
        "growth": 1,
        "step": 0,
        "from_level": 1,
        "values": amounts,
    }


def planet_costs(tiers: list[list[str]], codes: dict[str, str], parameters: dict[str, Decimal], group: str, factor: Decimal) -> list[dict]:
    """The cost rows of a building: a resource is paid from the level it first appears, at the base of its class."""
    base = coefficient(parameters, "world.seed.cost.base") * group_multiplier(parameters, group) * coefficient(parameters, "world.seed.cost.building_scale")
    digits = significant(parameters)
    rows = []
    for name, level in first_seen(tiers).items():
        if name in NOT_STOCK:
            continue
        if name not in codes:
            fail(f"unknown resource in a cost: {name}")
        rows.append({
            "resource": codes[name],
            "base": num(base * resource_scale(parameters, name)),
            "growth": num(factor),
            "step": 0,
            "from_level": level,
            "significant": digits,
        })
    rows.append(population_cost(codes["Населення"], len(tiers)))
    return rows


def technology_costs(tiers: list[list[str]], codes: dict[str, str], parameters: dict[str, Decimal], factor: Decimal) -> list[dict]:
    """The cost rows of a technology, by the same rule as the buildings."""
    base = coefficient(parameters, "world.seed.cost.base") * coefficient(parameters, "world.seed.cost.technology")
    digits = significant(parameters)
    rows = []
    for name, level in first_seen(tiers).items():
        if name in NOT_STOCK:
            continue
        if name not in codes:
            fail(f"unknown resource in a research cost: {name}")
        rows.append({
            "resource": codes[name],
            "growth": growth(base * resource_scale(parameters, name), factor, digits=digits),
            "from_level": level,
        })
    return rows


def effect(effect_type: str, base: Decimal, factor: Decimal, step: Decimal = Decimal(0), resource: str | None = None, group: str | None = None, digits: int = 0) -> dict:
    row = {"type": effect_type, "base": num(base), "growth": num(factor), "step": num(step)}
    if digits > 0:
        row["significant"] = digits
    if resource is not None:
        row["resource"] = resource
    if group is not None:
        row["group"] = group
    return row


def launcher_size(name: str) -> int | None:
    match = re.search(r"X(\d+)$", name)
    if match is None:
        return None
    return int(match.group(1))


# The size of a launcher is data on the weapon, the same number the ammunition of that size carries.
LAUNCHER_SIZES = {
    "РПУ-X1": 1,
    "РПУ-X2": 2,
    "РПУ-X3": 3,
}

# The logical kind the program reads. A characteristic without a kind is not read by its kind.
CHARACTERISTIC_KINDS = {
    "char_thrust": "drive_push",
    "char_energy_use": "energy_use",
    "char_power": "power",
    "char_damage": "damage",
    "char_rate": "rate",
    "char_integrity": "integrity",
    "char_shield_capacity": "shield_capacity",
    "char_shield_regen": "shield_regen",
    "char_launcher_size": "launcher_size",
    "char_shield_restore": "shield_restore",
    "char_crew": "crew",
    "char_cargo": "cargo",
    "char_hull_capacity": "hull_capacity",
    "char_yield": "yield",
    "char_scan_radius": "scan_radius",
    "char_scan_speed": "scan_speed",
    "char_detection": "detection",
    "char_dig_speed": "dig_speed",
    "char_speed": "speed",
    "char_collect_speed": "collect_speed",
    "char_fuel_use": "fuel_use",
    "char_missiles": "missiles",
    "char_transfer_speed": "transfer_speed",
    "char_transfer_volume": "transfer_volume",
    "char_deploy_time": "deploy_time",
    "char_colonists": "colonists",
    "char_population": "population",
    "char_storage": "storage",
    "char_repair_speed": "repair_speed",
    "char_training_speed": "training_speed",
    "char_trade_lots": "trade_lots",
    "char_jump_prep": "jump_prep",
    "char_upkeep": "upkeep",
}


def flies(name: str) -> bool:
    return "торпеда" in name or "-X" in name


def value_row(number: Decimal, levels: dict[str, int | float] | None = None) -> dict:
    row: dict = {"number": num(number)}
    if levels is not None:
        row["levels"] = levels
    return row


def build_world(world: dict, codes: dict[str, str], cost_base: Decimal, race_bonus: Decimal, notes: list[str]) -> None:
    old_ores = {"resource_1", "resource_2", "resource_3", "resource_4"}
    old_minerals = {"resource_5", "resource_6", "resource_7", "resource_8"}
    new_ores = [f"resource_{index}" for index in range(1, 8)]
    new_minerals = [f"resource_{index}" for index in range(8, 12)]
    crystals = ["resource_17", "resource_18", "resource_19"]
    classes = {row["code"]: row.get("object_class") for row in world["object_types"]}
    ore_template = None

    # The old deuterium, plasma and ether codes are remapped before the mineral
    # list grows into those same numbers.
    for group in world["rule_groups"]:
        for rule in group["rules"]:
            code = rule["resource"]
            if code == "resource_9":
                rule["resource"] = codes["Дейтерій"]
            elif code == "resource_11":
                rule["resource"] = codes["Ефір"]
            elif code == "resource_10":
                object_class = classes[group["object_type"]]
                if object_class == "gas_giant":
                    rule["resource"] = codes["Гелій-3"]
                elif object_class == "nebula":
                    rule["resource"] = codes["Темна матерія"]
                else:
                    fail(f"old plasma rule on {group['object_type']}")

    for group in world["rule_groups"]:
        current = {rule["resource"] for rule in group["rules"]}
        if current == old_ores:
            ore_template = group["rules"][0]
            group["rules"] = [{**ore_template, "resource": code} for code in new_ores]
            # Every ore lies on every ordinary planet (author's decision 31.10.2026); the
            # fields keep their random picks.
            if classes[group["object_type"]] == "planet":
                group["picks_all"] = True
        elif current == old_minerals:
            template = group["rules"][0]
            group["rules"] = [{**template, "resource": code} for code in new_minerals]

    if ore_template is None:
        fail("the world seed has no ore rule to copy")

    pattern = next(group for group in world["rule_groups"] if {rule["resource"] for rule in group["rules"]} == {codes["Дейтерій"], codes["Гелій-3"]})
    next_number = max(int(group["code"].rsplit("_", 1)[1]) for group in world["rule_groups"])
    for object_type in world["object_types"]:
        if object_type.get("object_class") != "planet":
            continue
        next_number += 1
        world["rule_groups"].append({
            "object_type": object_type["code"],
            "code": f"rule_group_{next_number}",
            "min_picks": pattern["min_picks"],
            "max_picks": pattern["max_picks"],
            "extra_pick_probability": pattern["extra_pick_probability"],
            "rules": [{**ore_template, "resource": code} for code in crystals],
        })

    for object_type in world["object_types"]:
        if object_type.get("hidden_resource"):
            object_type["hidden_resource"] = codes["Ефір"]

    world["resource_groups"] = [
        {
            "code": code,
            "allowed_on_planet": allowed,
            "name": text(ukrainian, english_name),
            "description": text(f"Група ресурсів «{ukrainian}».", f"Resource group {english_name}."),
        }
        for code, ukrainian, english_name, allowed in GROUPS
    ]
    world["resources"] = []
    for index, (ukrainian, english_name, group, scope, earns) in enumerate(RESOURCES, start=1):
        row = {
            "code": f"resource_{index}",
            "group": group,
            "name": text(ukrainian, english_name),
            "description": text(f"Ресурс групи.", f"Resource of its group."),
            "scope": scope,
        }
        if earns:
            row["earns_population_income"] = True
        world["resources"].append(row)

    by_ukrainian = {row["name"]["uk"]: row["code"] for row in world["object_types"]}
    preferred = {
        "race_1": by_ukrainian["Землеподібна"],
        "race_2": by_ukrainian["Крижана"],
        "race_3": by_ukrainian["Вулканічна"],
        "race_4": by_ukrainian["Пустельна"],
    }
    race_rows = {
        "race_1": ("Террани", "Terrans", "Двигуни", "Engines", [("Колоніальний модуль", "Colony module"), ("Вантажний відсік", "Cargo hold")], "Рівні", "Steady"),
        "race_2": ("Літаріони", "Litari", "Броня", "Armor", [("Бурова установка", "Drill rig"), ("Астросканер", "Astroscanner")], "Холодні", "Cold"),
        "race_3": ("Драккури", "Drakkuri", "Зброя", "Weapons", [("Вантажний відсік", "Cargo hold"), ("Туманнісний збирач", "Nebula collector")], "Гарячі", "Hot"),
        "race_4": ("Вейрани", "Veirani", "Щити", "Shields", [("Корабельний радар", "Ship radar"), ("Бортовий комп'ютер", "Onboard computer")], "Сухі", "Dry"),
    }
    percent = (race_bonus * Decimal(100)).quantize(ONE, rounding=ROUND_HALF_UP)
    amount = whole(cost_base)
    start = [{"resource": codes[name], "amount": amount} for name in START_NAMES]
    old = {race["code"]: race for race in world["races"]}
    world["races"] = []
    for code, (ukrainian, english_name, combat_uk, combat_en, peaceful, mood_uk, mood_en) in race_rows.items():
        race = dict(old[code])
        race["name"] = text(ukrainian, english_name)
        race["description"] = text(
            f"Ігрова раса. Бойовий бонус {percent}% до групи «{combat_uk}».",
            f"A playable race. Combat bonus of {percent}% to the {combat_en} group.",
        )
        race["mood"] = text(mood_uk, mood_en)
        race["preferred_object_type"] = preferred[code]
        race["start_building_level"] = old[code]["start_building_level"]
        race["start_resources"] = start
        race["traits"] = [text(combat_uk, combat_en), *[text(uk, en) for uk, en in peaceful]]
        world["races"].append(race)
    notes.append("Race preferred bodies: Terrans terrestrial, Litari ice, Drakkuri volcanic, Veirani desert. The unsettled extraction bonus on that body is not seeded.")
    notes.append("Peaceful modules are named in the race traits. The bonus table has no per-part field, so they are not exclusive recipes.")


def world_is_built(world: dict) -> bool:
    """Whether the world seed already holds the 31 chosen resources (the structural rebuild is not repeated)."""
    return len(world["resources"]) == len(RESOURCES) and [row["name"]["uk"] for row in world["resources"]] == [item[0] for item in RESOURCES]


def apply_start_resources(world: dict, codes: dict[str, str], parameters: dict[str, Decimal]) -> None:
    """The start stock of every race: the resources of the first hours, from the coefficients."""
    start = [{"resource": codes[name], "amount": whole(coefficient(parameters, key))} for name, key in START_NAMES.items()]
    for race in world["races"]:
        race["start_resources"] = start


def patch_fields(world: dict, codes: dict[str, str], parameters: dict[str, Decimal], notes: list[str]) -> None:
    """Every asteroid field holds all three crystals at once, 3 to 7 per cent of its mass together (task 00030)."""
    crystals = [codes[name] for name in ("Квартан", "Стобій", "Туманій")]
    low = Decimal(coefficient(parameters, "world.seed.field.group_4_total_min")) / Decimal(len(crystals))
    high = Decimal(coefficient(parameters, "world.seed.field.group_4_total_max")) / Decimal(len(crystals))
    field_types = [row["code"] for row in world["object_types"] if row.get("object_class") == "asteroid_field"]
    next_number = max(int(group["code"].rsplit("_", 1)[1]) for group in world["rule_groups"])
    for object_type in field_types:
        groups = [group for group in world["rule_groups"] if group["object_type"] == object_type]
        existing = next((group for group in groups if {rule["resource"] for rule in group["rules"]} == set(crystals)), None)
        rules = [
            {"resource": code, "weight": 1, "coefficient_min": num(low), "coefficient_max": num(high)}
            for code in crystals
        ]
        if existing is None:
            next_number += 1
            world["rule_groups"].append({
                "object_type": object_type,
                "code": f"rule_group_{next_number}",
                "min_picks": len(crystals),
                "max_picks": len(crystals),
                "extra_pick_probability": 0,
                "picks_all": True,
                "rules": rules,
            })
        else:
            existing["min_picks"] = existing["max_picks"] = len(crystals)
            existing["extra_pick_probability"] = 0
            existing["picks_all"] = True
            existing["rules"] = rules
        for group in groups:
            if group is existing or group.get("picks_all"):
                continue
            # Ore groups: three or four ores of the field, so the total share reaches the smallest total.
            if all(rule["resource"] in {f"resource_{index}" for index in range(1, 8)} for rule in group["rules"]):
                group["min_picks"] = 3
                group["max_picks"] = 4
    notes.append("Every asteroid field has the three crystals together, 3 to 7 per cent in all, 1 per cent at least each; the ore groups of the fields take three or four ores. The generator keeps the total share of a field between 70 and 95 per cent.")


def module_characteristics(name: str, group: str, index: int, cost_base: Decimal, cost_factor: Decimal, trade_levels: int, capsule_inhabitants: Decimal) -> tuple[dict, dict]:
    amount = curve(cost_base, cost_factor, index)
    stats: dict[str, dict] = {}
    attributes: dict[str, dict] = {}

    def put(code: str) -> None:
        stats[code] = value_row(amount)

    if group == "Двигуни":
        put("char_thrust")
        put("char_fuel_use")
        put("char_energy_use")
    elif group == "Реактори":
        put("char_power")
    elif group in {"Енергетична зброя", "Кінетична зброя"}:
        put("char_damage")
        put("char_rate")
        put("char_range")
        put("char_energy_use")
        attributes["damage_type"] = {"code": "energy" if group == "Енергетична зброя" else "kinetic"}
        if group == "Кінетична зброя":
            put("char_magazine")
            size = LAUNCHER_SIZES.get(name)
            if size is not None:
                stats["char_launcher_size"] = value_row(Decimal(size))
    elif group == "Броня":
        put("char_integrity")
    elif group == "Щити":
        put("char_shield_capacity")
        put("char_shield_regen")
        put("char_energy_block")
        put("char_kinetic_block")
        put("char_energy_use")
    elif group == "Боєприпаси":
        put("char_damage")
        put("char_range")
        attributes["damage_type"] = {"code": "energy" if name in ENERGY_AMMO else "kinetic"}
        if flies(name):
            put("char_flight_speed")
        size = launcher_size(name)
        if size is not None:
            stats["char_launcher_size"] = value_row(Decimal(size))
    elif group == "Видобуток":
        put("char_yield")
        put("char_energy_use")
        if name == "Модуль геологорозвідки":
            put("char_scan_radius")
            put("char_scan_speed")
    elif group == "Розвідка й маскування":
        put("char_energy_use")
        if name in {"Астросканер", "Корабельний радар"}:
            put("char_detection")
        if name == "Археологічний модуль":
            put("char_dig_speed")
    elif group == "Інші корабельні":
        for code in OTHER_STATS[name]:
            if code == "char_inhabitants":
                # The capsule holds a fixed number of inhabitants, taken from the coefficient and not from the curve of the group.
                stats[code] = value_row(capsule_inhabitants)
                attributes["population_capsule"] = {"number": num(capsule_inhabitants)}
                attributes["capsule_race"] = {"code": CAPSULE_RACES[name]}
            else:
                put(code)
    elif group == "Модулі бази":
        for code in BASE_STATS[name]:
            if code == "char_trade_lots":
                levels = {str(level): num(curve(cost_base, cost_factor, level - 1)) for level in range(1, trade_levels + 1)}
                stats[code] = value_row(curve(cost_base, cost_factor, 0), levels)
            else:
                put(code)
    return stats, attributes


def group_links(group_code: str, characteristic_codes: list[str], required: set[str]) -> list[dict]:
    return [
        {
            "group": group_code,
            "characteristic": code,
            "sort_order": index,
            "is_required": code in required,
        }
        for index, code in enumerate(characteristic_codes, start=1)
    ]


def simulate(buildings: list[dict], techs: list[dict], recipes: list[dict], apply_center_gate: bool) -> list[str]:
    building_level = {row["code"]: 0 for row in buildings}
    tech_level = {row["code"]: 0 for row in techs}
    admin = next(row["code"] for row in buildings if row["is_start"])
    science = next(row["code"] for row in buildings if row["building_class"] == "research_center")
    shipyard = next(row["code"] for row in buildings if row["building_class"] == "shipyard")
    available = set()
    for row in buildings:
        available.update(row["deposits"])
    available.update(next(row["always"] for row in buildings))

    resource_recipes = [row for row in recipes if row["resource"]]
    part_recipes = [row for row in recipes if row["part"]]
    civilian = [row for row in part_recipes if row["hull_group"] == "Цивільні"]
    collectors = [row for row in part_recipes if row["part_name"] in COLLECTORS]

    def tech_ready(recipe: dict) -> bool:
        return all(tech_level[requirement["target"]] >= requirement["target_level"] for requirement in recipe["requirements"])

    def refresh() -> None:
        changed = True
        while changed:
            changed = False
            for recipe in resource_recipes:
                if recipe["resource"] in available:
                    continue
                if all(ingredient in available for ingredient in recipe["ingredients"]):
                    available.add(recipe["resource"])
                    changed = True
            cosmic_ready = (
                building_level[shipyard] >= 1
                and any(tech_ready(recipe) for recipe in civilian)
                and any(tech_ready(recipe) for recipe in collectors)
            )
            if cosmic_ready:
                for name in COSMIC:
                    code = next(row["cosmic"][name] for row in buildings)
                    if code not in available:
                        available.add(code)
                        changed = True

    def building_can(row: dict, level: int) -> tuple[bool, str]:
        if not row["is_start"] and level > building_level[admin]:
            return False, "level cap"
        for requirement in row["requirements"]:
            if requirement["subject_level"] > level:
                continue
            if requirement["type"] == "building_level" and building_level[requirement["target"]] < requirement["target_level"]:
                return False, f"building {requirement['target']} {requirement['target_level']}"
            if requirement["type"] == "technology_level" and tech_level[requirement["target"]] < requirement["target_level"]:
                return False, f"tech {requirement['target']} {requirement['target_level']}"
            if requirement["type"] == "deposit" and requirement["target"] not in available:
                return False, f"deposit {requirement['target']}"
        for resource in row["tiers"][level - 1]:
            if resource not in available:
                return False, f"resource {resource}"
        return True, ""

    def tech_can(row: dict, level: int) -> tuple[bool, str]:
        if building_level[science] < row["research_center_level"]:
            return False, "center field"
        for requirement in row["requirements"]:
            if requirement["subject_level"] > level:
                continue
            if requirement["type"] == "building_level" and apply_center_gate and building_level[requirement["target"]] < requirement["target_level"]:
                return False, f"building {requirement['target']} {requirement['target_level']}"
            if requirement["type"] == "technology_level" and tech_level[requirement["target"]] < requirement["target_level"]:
                return False, f"tech {requirement['target']} {requirement['target_level']}"
        for resource in row["tiers"][level - 1]:
            if resource not in available:
                return False, f"resource {resource}"
        return True, ""

    limit = sum(row["max_level"] for row in buildings) + sum(row["max_level"] for row in techs) + len(part_recipes)
    for _ in range(limit):
        refresh()
        moved = False
        for row in buildings:
            current = building_level[row["code"]]
            if current >= row["max_level"]:
                continue
            allowed, _reason = building_can(row, current + 1)
            if allowed:
                building_level[row["code"]] = current + 1
                moved = True
        for row in techs:
            current = tech_level[row["code"]]
            if current >= row["max_level"]:
                continue
            allowed, _reason = tech_can(row, current + 1)
            if allowed:
                tech_level[row["code"]] = current + 1
                moved = True
        if not moved:
            break
    refresh()

    stuck = []
    for row in buildings:
        if building_level[row["code"]] < row["max_level"]:
            _allowed, reason = building_can(row, building_level[row["code"]] + 1)
            stuck.append(f"building {row['name']} at {building_level[row['code']]}/{row['max_level']}: {reason}")
    for row in techs:
        if tech_level[row["code"]] < row["max_level"]:
            _allowed, reason = tech_can(row, tech_level[row["code"]] + 1)
            stuck.append(f"tech {row['name']} at {tech_level[row['code']]}/{row['max_level']}: {reason}")
    for recipe in part_recipes:
        if not tech_ready(recipe):
            stuck.append(f"part {recipe['part_name']}: science")
        elif any(ingredient not in available for ingredient in recipe["ingredients"]):
            missing = [ingredient for ingredient in recipe["ingredients"] if ingredient not in available]
            stuck.append(f"part {recipe['part_name']}: resources {missing}")
    return stuck


def main() -> None:
    notes: list[str] = []
    parameters = load_coefficients()
    cost_base = coefficient(parameters, "world.seed.cost.base")
    cost_growth = coefficient(parameters, "world.seed.cost.growth")
    duration_base = coefficient(parameters, "world.seed.duration.base_seconds")
    duration_cap = coefficient(parameters, "world.seed.duration.cap_seconds")
    digits = significant(parameters)
    race_bonus = coefficient(parameters, "world.seed.race.bonus")
    cap_base = coefficient(parameters, "world.seed.level_cap.base")
    cap_growth = coefficient(parameters, "world.seed.level_cap.growth")
    cap_step = coefficient(parameters, "world.seed.level_cap.step")
    lot_limit_base = coefficient(parameters, "world.seed.market.lot_limit.base")
    lot_limit_step = coefficient(parameters, "world.seed.market.lot_limit.step")
    cost_factor = ONE + cost_growth
    technology_factor = ONE + coefficient(parameters, "world.seed.cost.technology_growth")
    part_factor = ONE + coefficient(parameters, "world.seed.part.growth")
    bonus_factor = ONE + coefficient(parameters, "world.seed.technology.bonus")
    shortening_factor = ONE + coefficient(parameters, "world.seed.technology.shortening")
    catalog = load_json(DEPENDENCY)
    items = catalog["items"]
    buildings = [row for row in items if row["kind"] == "budivlia"]
    techs = [row for row in items if row["kind"] == "rozvytok"]
    # An upgraded module (the dependency file names its base in upgradeOf, task 00043) is kept out
    # of the numbered modules, so the codes of the other parts and recipes do not shift.
    modules = [row for row in items if row["kind"] == "modul" and not row.get("upgradeOf")]
    upgrades = [row for row in items if row["kind"] == "modul" and row.get("upgradeOf")]
    hulls = [row for row in items if row["kind"] == "korpus"]
    if (len(buildings), len(techs), len(modules), len(hulls)) != (56, 19, 83, 27):
        fail("the dependency file does not have 56/19/83/27")

    perelik = {kind: path.read_text(encoding="utf-8") for kind, path in PERELIK.items()}
    mismatches = []
    for kind, rows, key in (
        ("resource", [{"name": name} for name, *_rest in RESOURCES], "resource"),
        ("tech", techs, "tech"),
        ("module", modules + upgrades, "module"),
        ("hull", hulls, "hull"),
        ("building", buildings, "building"),
    ):
        for row in rows:
            if row["name"] not in perelik[key]:
                mismatches.append(f"{kind}: {row['name']}")
    notes.append("Name mismatches (the seed keeps the dependency file): " + ("; ".join(mismatches) if mismatches else "none"))

    for row in buildings + techs:
        if len(row["scienceLevels"]) != row["maxLevel"] or len(row["tiers"]) != row["maxLevel"]:
            fail(f"level tables differ from maxLevel: {row['name']}")
        for level, missing in dropped(row["tiers"]):
            notes.append(f"Tier drop kept as a cumulative cost, not removed: {row['name']} level {level} loses {sorted(missing)}")

    codes = {name: f"resource_{index}" for index, (name, *_rest) in enumerate(RESOURCES, start=1)}
    if len(codes) != len(RESOURCES):
        fail("resource names are not unique")
    building_code = {row["name"]: f"building_{index}" for index, row in enumerate(buildings, start=1)}
    if len(building_code) != len(buildings):
        fail("building names are not unique")
    reserved = reserved_technology_codes()
    tech_code = assign_technology_codes(techs, reserved)
    matter_code = next(iter(reserved))
    if len(reserved) != 1:
        notes.append(f"Process seed names several technologies: {sorted(reserved)}. The extra research rows use each of them.")
    notes.append(f"The 19 chosen technologies are {', '.join(tech_code[row['name']] for row in techs)}. {matter_code} stays Matter conversion because the process seed names it. It is not one of the 19.")

    for row in techs:
        for sciences in row["scienceLevels"]:
            if row["name"] in sciences:
                notes.append(f"Self requirement kept from the file: {row['name']}")

    group_of = {name: group for name, _en, group, _scope, _earns in RESOURCES}
    science_center = building_code["Науковий центр"]
    converter = building_code["Перетворювач матерії"]
    trade_levels = next(row["maxLevel"] for row in buildings if row["name"] == "Торговий центр")
    tech_base = coefficient(parameters, "world.seed.duration.technology_seconds")
    shortening = growth(ONE / shortening_factor, ONE / shortening_factor)
    bonus = growth(bonus_factor, bonus_factor)
    no_effect = []

    planet_buildings = []
    sim_buildings = []
    unlocks: dict[str, list[dict]] = {code: [] for code in tech_code.values()}
    unlocks[matter_code] = [{
        "type": "unlocks",
        "unlock_kind": "building_type",
        "unlock_target": converter,
        "level": techs[0]["centerLevels"][0],
    }]
    deposits_available = {codes[name] for name in DEPOSIT_NAMES}
    always_available = {codes[name] for name, _en, group, _scope, _earns in RESOURCES if group == "resource_group_6"}
    always_available.add(codes["Будматеріали"])
    cosmic_codes = {name: codes[name] for name in COSMIC}

    for row in buildings:
        requirements = rising_requirements(row["scienceLevels"], tech_code)
        if row["name"] in MINE_RESOURCE:
            requirements.append({
                "type": "deposit",
                "target": codes[MINE_RESOURCE[row["name"]]],
                "target_level": techs[0]["centerLevels"][0],
                "subject_level": techs[0]["centerLevels"][0],
            })
        if row["name"] == "Перетворювач матерії":
            requirements.append({
                "type": "technology_level",
                "target": matter_code,
                "target_level": techs[0]["centerLevels"][0],
                "subject_level": techs[0]["centerLevels"][0],
            })
            notes.append("Matter converter level 1 requires tech_18 even though scienceLevels[0] is empty. The file copied the other sciences from level 2. Both are seeded.")
        first_open = {}
        for sciences in row["scienceLevels"]:
            for name, required in sciences.items():
                if name not in first_open:
                    first_open[name] = required
        for name, required in first_open.items():
            unlocks[tech_code[name]].append({
                "type": "unlocks",
                "unlock_kind": "building_type",
                "unlock_target": building_code[row["name"]],
                "level": required,
            })

        effects = []
        building_class = ""
        if row["name"] in MINE_RESOURCE:
            building_class = "mine"
            resource_name = MINE_RESOURCE[row["name"]]
            effect_type = "metal_production" if resource_name in METAL_NAMES else "crystal_production"
            if resource_name in MINERALS:
                output_scale = "group_2"
            elif resource_name in CRYSTALS:
                output_scale = "group_4"
            elif resource_name == "Дейтерій":
                output_scale = "group_3"
            else:
                output_scale = None
            output = coefficient(parameters, "world.seed.mine.base") * (coefficient(parameters, f"world.seed.mine.scale.{output_scale}") if output_scale else ONE)
            effects.append(effect(effect_type, output, ONE + coefficient(parameters, "world.seed.mine.growth"), resource=codes[resource_name], digits=digits))
        elif row["name"] in POWER_PLANTS:
            plant = {
                "Сонячна електростанція": "solar",
                "Теплова електростанція": "thermal",
                "Ядерна електростанція": "nuclear",
                "Термоядерна електростанція": "fusion",
            }[row["name"]]
            effects.append(effect("energy_output", coefficient(parameters, f"world.seed.power.{plant}"), ONE + coefficient(parameters, "world.seed.power.growth"), digits=digits))
        elif row["name"] == "Науковий центр":
            building_class = "research_center"
        elif row["name"] == "Адміністративний центр":
            effects.append(effect("level_cap", cap_base, cap_growth, cap_step))
            effects.append(effect("population_max", coefficient(parameters, "world.seed.population.admin"), ONE + coefficient(parameters, "world.seed.population.growth"), digits=digits))
        elif row["name"] == "Житловий комплекс (Міста)":
            effects.append(effect("population_max", coefficient(parameters, "world.seed.population.housing"), ONE + coefficient(parameters, "world.seed.population.growth"), digits=digits))
        elif row["name"] == "Медичний центр":
            # A multiplier of the growth of the population: a fraction, so the values are listed by level.
            step_bonus = coefficient(parameters, "world.seed.population.bonus")
            effects.append({"type": "population_growth_bonus", "base": 0, "growth": 1, "step": 0, "values": [num(ONE + step_bonus * level) for level in range(1, row["maxLevel"] + 1)]})
        elif row["name"] == "Склад":
            storage_base = coefficient(parameters, "world.seed.storage.base")
            storage_factor = ONE + coefficient(parameters, "world.seed.storage.growth")
            for group_code, _uk, _en, _allowed in GROUPS:
                effects.append(effect("storage_capacity", storage_base, storage_factor, group=group_code, digits=digits))
            effects.append(effect("part_storage_capacity", coefficient(parameters, "world.seed.storage.part_base"), storage_factor, digits=digits))
        elif row["name"] == "Перетворювач матерії":
            building_class = "converter"
        elif row["name"] == "Фабрика утилізації":
            building_class = "recycling"
        elif row["name"] == "Верф":
            building_class = "shipyard"
        elif row["name"] == "Радар":
            building_class = "sensor"
        elif row["name"] in {"Планетарна гармата", "Планетарний щит"}:
            building_class = "defence"
        elif row["name"] == "Торговий центр":
            building_class = "trade_center"
            effects.append(effect("market_lot_limit", lot_limit_base, ONE, lot_limit_step))
        elif row["name"] == "Портал":
            building_class = "portal"
        elif row["name"] in PRODUCTS or row["name"] in MODULE_FACTORY.values():
            building_class = "production"
        if row["name"] not in UNSPENT_ENERGY:
            effects.append(effect(
                "energy_consumption",
                coefficient(parameters, "world.seed.energy.consumption") * group_multiplier(parameters, row["group"]),
                ONE + coefficient(parameters, "world.seed.energy.growth"),
                digits=digits,
            ))
        if not any(entry["type"] != "energy_consumption" for entry in effects) and building_class in {"", "sensor", "defence", "trade_center", "portal", "converter", "recycling", "shipyard", "production", "research_center"}:
            if row["name"] not in PRODUCTS and row["name"] not in MODULE_FACTORY.values() and row["name"] not in {"Перетворювач матерії", "Фабрика утилізації", "Верф"}:
                no_effect.append(row["name"])

        category = f"group_{next(index for index, (ukrainian, _en) in enumerate(BUILDING_GROUPS, start=1) if ukrainian == row['group'])}"
        planet_buildings.append({
            "code": building_code[row["name"]],
            "sort_order": len(planet_buildings) + 1,
            "name": text(row["name"], english(BUILDINGS_EN, row["name"], "building")),
            "description": text(f"Будівля групи «{row['group']}».", f"Building of the {dict(BUILDING_GROUPS)[row['group']]} group."),
            "duration_seconds": growth(duration_base * duration_group_multiplier(parameters, row["group"]), duration_growth_for(duration_base * duration_group_multiplier(parameters, row["group"]), row["maxLevel"], duration_cap), digits=digits),
            "costs": planet_costs(row["tiers"], codes, parameters, row["group"], cost_factor),
            "effects": effects,
            "requirements": requirements,
            "is_start_building": row["name"] in START_BUILDINGS,
            "building_class": building_class,
            "category": category,
            "max_level": row["maxLevel"],
        })
        sim_buildings.append({
            "code": building_code[row["name"]],
            "name": row["name"],
            "max_level": row["maxLevel"],
            "is_start": row["name"] == "Адміністративний центр",
            "building_class": building_class,
            "requirements": requirements,
            "tiers": [[codes[name] for name in tier if name not in NOT_STOCK] for tier in row["tiers"]],
            "deposits": deposits_available,
            "always": always_available,
            "cosmic": cosmic_codes,
        })

    notes.append("Buildings without a numeric effect, left without one: " + ", ".join(no_effect))
    notes.append("No effect type exists for a construction queue, a fleet limit, a bunker share, a colony cap, a market commission, a portal discount, or fuel production. Bank income stays the credits flag earns_population_income.")
    notes.append("Ores and deuterium use metal_production. Minerals and crystals use crystal_production. Power plants have energy_output; every other building except the warehouse has energy_consumption. Energy is not a cost. Every building pays population: 4 at level 1, about 60-75 percent more through level 6, about 12-14 percent more from level 14, and never more than 1500.")
    notes.append("Deposit requirements were added on extraction buildings. The dependency file does not carry them; the game already refuses a mine without the deposit.")
    notes.append("Admin keeps level_cap and population_max. The list says the admin center only caps levels; the economy reads population_max from it.")

    categories = []
    for index, (ukrainian, english_name) in enumerate(BUILDING_GROUPS, start=1):
        categories.append({
            "code": f"group_{index}",
            "sort_order": index,
            "name": text(ukrainian, english_name),
            "description": text(f"Група будівель «{ukrainian}».", f"Building group {english_name}."),
        })
    planets_payload = {"categories": categories, "building_types": planet_buildings}
    # LevelCount stays the historical file width. A building with its own max_level uses that.
    planets_payload["level_count"] = next(row["max_level"] for row in planet_buildings if row["code"] == building_code["Адміністративний центр"])

    # The research buttons. Electronics and matter conversion were not in the named lists;
    # electronics stays with the fundamental sciences, matter conversion with the other group.
    tech_place = {
        "Математика": ("branch_1", 1),
        "Фізика": ("branch_1", 2),
        "Ядерна фізика": ("branch_1", 3),
        "Термоядерний синтез": ("branch_1", 4),
        "Кібернетика": ("branch_1", 5),
        "Робототехніка": ("branch_1", 6),
        "Археологія": ("branch_1", 7),
        "Електроніка": ("branch_1", 8),
        "Розвиток науки": ("branch_1", 9),
        "Економіка": ("branch_2", 1),
        "Геологія": ("branch_2", 2),
        "Енергетика": ("branch_2", 3),
        "Будівництво": ("branch_2", 4),
        "Кристалографія": ("branch_2", 5),
        "Газова інженерія": ("branch_2", 6),
        "Логістика складів": ("branch_2", 7),
        "Демографія": ("branch_2", 8),
        "Торговельне право": ("branch_3", 1),
        "Стабілізація порталу": ("branch_3", 2),
        "Колоніальне управління": ("branch_4", 1),
        "Перетворення матерії": ("branch_4", 2),
    }
    tech_bonus = {
        "Геологія": ["metal_production"],
        "Кристалографія": ["crystal_production"],
        "Логістика складів": ["storage_capacity"],
        "Демографія": ["population_max", "population_growth_bonus"],
        "Енергетика": ["energy_output"],
        "Будівництво": ["construction_time"],
        "Робототехніка": ["production_time"],
        "Економіка": ["credit_income_bonus"],
    }
    technologies = []
    sim_techs = []
    for index, row in enumerate(techs, start=1):
        code = tech_code[row["name"]]
        branch, _sort_order = tech_place[row["name"]]
        effects = []
        for effect_type in tech_bonus.get(row["name"], []):
            formula = shortening if effect_type in {"construction_time", "production_time"} else bonus
            effects.append({"type": effect_type, "growth": formula})
        effects.extend(unlocks[code])
        requirements = rising_requirements(row["scienceLevels"], tech_code) + center_requirements(row["centerLevels"], science_center)
        technologies.append({
            "code": code,
            "branch": branch,
            "sort_order": index,
            "max_level": row["maxLevel"],
            "research_center_level": row["centerLevels"][0],
            "name": text(row["name"], english(TECHS_EN, row["name"], "technology")),
            "description": text(f"Розвиток групи «{row['group']}».", "Technology of its branch."),
            "duration_seconds": tech_duration_of(parameters, f"branch_{branch}", tech_base, row["maxLevel"], duration_cap),
            "costs": technology_costs(row["tiers"], codes, parameters, technology_factor),
            "effects": effects,
            "requirements": requirements,
        })
        sim_techs.append({
            "code": code,
            "name": row["name"],
            "max_level": row["maxLevel"],
            "research_center_level": row["centerLevels"][0],
            "requirements": requirements,
            "tiers": [[codes[name] for name in tier if name not in NOT_STOCK] for tier in row["tiers"]],
        })
    matter_costs = technology_costs([["Кредити"]], codes, parameters, technology_factor)
    technologies.append({
        "code": matter_code,
        "branch": tech_place["Перетворення матерії"][0],
        "sort_order": tech_place["Перетворення матерії"][1],
        "max_level": techs[0]["maxLevel"],
        "research_center_level": techs[0]["centerLevels"][0],
        "name": text("Перетворення матерії", english(TECHS_EN, "Перетворення матерії", "technology")),
        "description": text("Перетворення однієї речовини на іншу.", "Conversion of one material into another."),
        "duration_seconds": tech_duration_of(parameters, f"branch_{tech_place['Перетворення матерії'][0]}", tech_base, techs[0]["maxLevel"], duration_cap),
        "costs": matter_costs,
        "effects": unlocks[matter_code],
        "requirements": [],
    })
    # Science development shortens research the way Construction shortens building. Its price
    # and its requirements are Construction's; it opens nothing.
    construction = next(row for row in technologies if row["name"]["uk"] == "Будівництво")
    used_codes = {row["code"] for row in technologies}
    science_number = 1
    while f"tech_{science_number}" in used_codes:
        science_number += 1
    technologies.append({
        "code": f"tech_{science_number}",
        "branch": tech_place["Розвиток науки"][0],
        "sort_order": tech_place["Розвиток науки"][1],
        "max_level": construction["max_level"],
        "research_center_level": construction["research_center_level"],
        "name": text("Розвиток науки", "Science development"),
        "description": text("Розвиток групи «Фундаментальні науки».", "Technology of its branch."),
        "duration_seconds": construction["duration_seconds"],
        "costs": construction["costs"],
        "effects": [{"type": "research_time", "growth": shortening}],
        "requirements": construction["requirements"],
    })
    sim_techs.append({
        "code": matter_code,
        "name": "Перетворення матерії",
        "max_level": techs[0]["maxLevel"],
        "research_center_level": techs[0]["centerLevels"][0],
        "requirements": [],
        "tiers": [[codes["Кредити"]] for _ in range(techs[0]["maxLevel"])],
    })
    notes.append("Matter conversion has no center curve in the dependency file. Its single center field is the first center value of the other technologies, and it has no extra center rows.")
    notes.append("Module and hull unlocks are recipe requirements, not technology effects. A technology effect cannot point at a part before the production seed assigns its code.")
    technologies_sorted = sorted(technologies, key=lambda row: int(row["code"].rsplit("_", 1)[1]))
    for row in technologies_sorted:
        row["branch"], row["sort_order"] = tech_place[row["name"]["uk"]]

    old_production = load_json(PRODUCTION_SEED)
    mark_level = old_production["parts"][0]["mark_level"]
    output_quantity = old_production["recipes"][0]["output_quantity"]
    batch_seconds = whole(coefficient(parameters, "world.seed.duration.batch_seconds"))
    batch_quantity = whole(coefficient(parameters, "world.seed.production.batch_quantity"))
    batch_ingredient = whole(coefficient(parameters, "world.seed.production.ingredient"))
    part_seconds = coefficient(parameters, "world.seed.duration.part_seconds")
    part_duration_factor = ONE + coefficient(parameters, "world.seed.duration.part_growth")
    part_ingredient_factor = ONE + coefficient(parameters, "world.seed.part.ingredient_growth")

    def part_amounts(row: dict, index: int) -> dict[str, int]:
        """Units of every ingredient of a part: the base of its class, rising with the place of the part in its group."""
        return {
            name: whole(rounded(coefficient(parameters, "world.seed.cost.base") * coefficient(parameters, "world.seed.part.cost_scale") * resource_scale(parameters, name) * curve(ONE, part_ingredient_factor, index), digits))
            for name in row["resources"]
        }

    def part_time(index: int) -> int:
        return whole(rounded(curve(part_seconds, part_duration_factor, index), digits))

    characteristics = []
    for index, spec in enumerate(CHARACTERISTICS, start=1):
        code, ukrainian, english_name, uk_desc, en_desc, *unit = spec
        row = {
            "code": code,
            "sort_order": index,
            "name": text(ukrainian, english_name),
            "description": text(uk_desc, en_desc),
        }
        if unit:
            row["unit"] = unit[0]
        kind = CHARACTERISTIC_KINDS.get(code)
        if kind is not None:
            row["kind"] = kind
        characteristics.append(row)

    module_group_names = list(dict.fromkeys(row["group"] for row in modules))
    hull_group_names = list(dict.fromkeys(row["group"] for row in hulls))
    part_groups = []
    group_code = {}
    for index, name in enumerate(module_group_names + hull_group_names, start=1):
        code = f"pg_{index}"
        group_code[name] = code
        english_name = MODULE_GROUPS_EN.get(name) or HULL_GROUPS_EN[name]
        part_groups.append({
            "code": code,
            "sort_order": index,
            "name": text(name, english_name),
            "description": text(f"Група «{name}».", f"Group {english_name}."),
        })

    required_by_group = {
        "Двигуни": {"char_thrust", "char_fuel_use", "char_energy_use"},
        "Реактори": {"char_power"},
        "Енергетична зброя": {"char_damage", "char_rate", "char_range", "char_energy_use"},
        "Кінетична зброя": {"char_damage", "char_rate", "char_range", "char_energy_use"},
        "Броня": {"char_integrity"},
        "Щити": {"char_shield_capacity", "char_shield_regen", "char_energy_block", "char_kinetic_block", "char_energy_use"},
        "Боєприпаси": {"char_damage", "char_range"},
        "Видобуток": {"char_yield", "char_energy_use"},
        "Розвідка й маскування": {"char_energy_use"},
    }
    shown_by_group = {
        "Двигуни": ["char_thrust", "char_fuel_use", "char_energy_use"],
        "Реактори": ["char_power"],
        "Енергетична зброя": ["char_damage", "char_rate", "char_range", "char_energy_use"],
        "Кінетична зброя": ["char_damage", "char_rate", "char_range", "char_energy_use", "char_magazine"],
        "Броня": ["char_integrity"],
        "Щити": ["char_shield_capacity", "char_shield_regen", "char_energy_block", "char_kinetic_block", "char_energy_use"],
        "Боєприпаси": ["char_damage", "char_range", "char_flight_speed", "char_launcher_size"],
        "Видобуток": ["char_yield", "char_energy_use", "char_scan_radius", "char_scan_speed"],
        "Розвідка й маскування": ["char_energy_use", "char_detection", "char_dig_speed"],
        "Інші корабельні": sorted({code for codes_of_part in OTHER_STATS.values() for code in codes_of_part}),
        "Модулі бази": sorted({code for codes_of_part in BASE_STATS.values() for code in codes_of_part}),
    }
    links = []
    for name, codes_of_group in shown_by_group.items():
        links.extend(group_links(group_code[name], codes_of_group, required_by_group.get(name, set())))
    for name in hull_group_names:
        links.extend(group_links(group_code[name], ["char_hull_size", "char_hull_capacity", "char_integrity"], {"char_hull_size", "char_hull_capacity", "char_integrity"}))
    notes.append("Magazine is on every kinetic weapon, including cannons. Flight speed stays on the ammunition. A launcher also carries the launcher size of the ammunition it accepts: X1, X2 and X3. A cannon has no launcher size, so it accepts the kinetic ammunition that has none. Survey radius and dig speed are group columns with values only on the matching module.")
    notes.append("Damage type is the part attribute damage_type with the code energy or kinetic. Energy ammunition: Lightning X1, Proton torpedo, Fusion torpedo. The other ammunition is kinetic.")
    notes.append("Hull size, capacity and integrity share the cost curve. The hull list left the real numbers open. Hull weight stays the loader default.")
    notes.append("Trade-center lot levels follow the trade-center building maximum. Onboard-computer figures are positive starting numbers; the list does not say which of them reduce a value.")

    parts = []
    recipes = []
    group_index = {name: 0 for name in module_group_names}
    module_place = {}
    for module_number, row in enumerate(modules, start=1):
        index = group_index[row["group"]]
        group_index[row["group"]] = index + 1
        stats, attributes = module_characteristics(row["name"], row["group"], index, cost_base, part_factor, trade_levels, coefficient(parameters, "world.seed.capsule.inhabitants"))
        part_code = f"part_m_{module_number}"
        parts.append({
            "code": part_code,
            "group": group_code[row["group"]],
            "mark_level": mark_level,
            "sort_order": module_number,
            "name": text(row["name"], english(MODULES_EN, row["name"], "module")),
            "description": text(f"Модуль групи «{row['group']}».", f"Module of the {MODULE_GROUPS_EN[row['group']]} group."),
            "attributes": attributes,
            "weight": num(part_factor ** (module_number - 1)),
            "characteristics": stats,
        })
        recipe = part_recipe(part_code, row, MODULE_FACTORY[row["group"]], building_code, codes, tech_code, output_quantity, part_time(index), part_amounts(row, index), len(recipes) + 1, None)
        # Only the race of a capsule makes it.
        recipe["race"] = CAPSULE_RACES.get(row["name"])
        recipes.append(recipe)
        module_place[row["name"]] = (index, module_number, parts[-1], recipe)

    hull_index = {name: 0 for name in hull_group_names}
    for hull_number, row in enumerate(hulls, start=1):
        index = hull_index[row["group"]]
        hull_index[row["group"]] = index + 1
        amount = curve(cost_base, part_factor, index)
        part_code = f"part_h_{hull_number}"
        parts.append({
            "code": part_code,
            "group": group_code[row["group"]],
            "mark_level": mark_level,
            "sort_order": len(modules) + hull_number,
            "name": text(row["name"], english(HULLS_EN, row["name"], "hull")),
            "description": text(f"Корпус групи «{row['group']}».", f"Hull of the {HULL_GROUPS_EN[row['group']]} group."),
            "attributes": {},
            "characteristics": {
                "char_hull_size": value_row(amount),
                "char_hull_capacity": value_row(amount),
                "char_integrity": value_row(amount),
            },
        })
        recipes.append(part_recipe(part_code, row, "Верф", building_code, codes, tech_code, output_quantity, part_time(index), part_amounts(row, index), len(recipes) + 1, row["group"]))

    for building_name, resource_name in PRODUCTS.items():
        source = next(row for row in buildings if row["name"] == building_name)
        recipe_code = f"recipe_r_{len(recipes) + 1}"
        recipes.append({
            "code": recipe_code,
            "part": None,
            "resource": codes[resource_name],
            "factory": building_code[building_name],
            "output_quantity": batch_quantity,
            "duration_seconds": batch_seconds,
            "sort_order": len(recipes) + 1,
            "name": text(resource_name, english({name: en for name, en, *_rest in [(item[0], item[1]) for item in RESOURCES]}, resource_name, "resource")),
            "description": text(f"Виробництво «{resource_name}».", f"Production of {english({item[0]: item[1] for item in RESOURCES}, resource_name, 'resource')}."),
            "ingredients": [{"resource": codes[name], "part": None, "amount": batch_ingredient} for name in source["recipe"]],
            "requirements": [],
            "race": None,
            "hull_group": None,
            "part_name": resource_name,
        })

    # An upgraded module (task 00043) is its base with the next mark: the same recipe and
    # requirements, the characteristics and the weight of the next place of the group. Its part
    # and its recipe come after all the others, so no code of the file shifts.
    for upgrade_number, row in enumerate(upgrades, start=1):
        if row["upgradeOf"] not in module_place:
            fail(f"the base module of {row['name']} is missing: {row['upgradeOf']}")
        base_index, base_number, base_part, base_recipe = module_place[row["upgradeOf"]]
        base_row = next(item for item in modules if item["name"] == row["upgradeOf"])
        if (row["group"], row["sciences"], row["resources"]) != (base_row["group"], base_row["sciences"], base_row["resources"]):
            fail(f"the upgraded module {row['name']} differs from its base in the group, the sciences or the resources")
        stats, attributes = module_characteristics(row["upgradeOf"], row["group"], base_index + 1, cost_base, part_factor, trade_levels, coefficient(parameters, "world.seed.capsule.inhabitants"))
        part_code = f"part_m_{len(modules) + upgrade_number}"
        parts.append({
            "code": part_code,
            "group": base_part["group"],
            "mark_level": base_part["mark_level"] + 1,
            "sort_order": base_part["sort_order"],
            "name": text(row["name"], english(MODULES_EN, row["name"], "module")),
            "description": dict(base_part["description"]),
            "attributes": attributes,
            "weight": num(part_factor ** base_number),
            "characteristics": stats,
        })
        recipe = {
            **base_recipe,
            "code": f"recipe_{len(recipes) + 1}",
            "part": part_code,
            "name": text(row["name"], part_english(row["name"])),
            "description": text(f"Виробництво «{row['name']}».", base_recipe["description"]["en"]),
            "ingredients": [dict(ingredient) for ingredient in base_recipe["ingredients"]],
            "requirements": [dict(requirement) for requirement in base_recipe["requirements"]],
            "part_name": row["name"],
        }
        recipes.append(recipe)

    race_groups = {
        "race_1": ["Двигуни"],
        "race_2": ["Броня"],
        "race_3": ["Енергетична зброя", "Кінетична зброя"],
        "race_4": ["Щити"],
    }
    race_bonuses = []
    for race, groups in race_groups.items():
        for group_name in groups:
            race_bonuses.append({
                "race": race,
                "group": group_code[group_name],
                "multiplier": num(ONE + race_bonus),
                "is_exclusive": False,
            })

    sim_recipes = []
    for recipe in recipes:
        if recipe.get("resource") and recipe.get("part") is None:
            sim_recipes.append({
                "resource": recipe["resource"],
                "part": None,
                "ingredients": [ingredient["resource"] for ingredient in recipe["ingredients"]],
                "requirements": [],
                "hull_group": None,
                "part_name": recipe["part_name"],
            })
        elif recipe.get("part"):
            sim_recipes.append({
                "resource": None,
                "part": recipe["part"],
                "ingredients": [ingredient["resource"] for ingredient in recipe["ingredients"]],
                "requirements": recipe["requirements"],
                "hull_group": recipe.get("hull_group"),
                "part_name": recipe["part_name"],
            })
    file_stuck = simulate(sim_buildings, sim_techs, sim_recipes, False)
    if file_stuck:
        print("\n".join(file_stuck[:40]), file=sys.stderr)
        fail(f"reachability stopped on {len(file_stuck)} elements")
    notes.append(f"Reachability, as in the analysis (no science-centre gate): every building and technology reached its maximum, and all {len([row for row in sim_recipes if row['part']])} parts can be made.")
    gated = simulate(sim_buildings, sim_techs, sim_recipes, True)
    if gated:
        notes.append(
            f"With the science-centre gate and the admin level cap, progress stops on {len(gated)} rows. First: {gated[0]}. "
            "The analysis already predicted this jam. The file is seeded unchanged.")

    for recipe in recipes:
        recipe.pop("hull_group", None)
        recipe.pop("part_name", None)

    world = load_json(WORLD_SEED)
    if not world_is_built(world):
        build_world(world, codes, cost_base, race_bonus, notes)
    apply_start_resources(world, codes, parameters)
    patch_fields(world, codes, parameters, notes)
    known_resources = {row["code"] for row in world["resources"]}
    for group in world["rule_groups"]:
        for rule in group["rules"]:
            if rule["resource"] not in known_resources:
                fail(f"rule {group['code']} names unknown {rule['resource']}")

    fusion = next(row for row in techs if row["name"] == "Термоядерний синтез")
    helium_levels = [index + 1 for index, tier in enumerate(fusion["tiers"]) if "Гелій-3" in tier]
    notes.append(f"Analysis 2.1 kept: Helium-3 is in Thermonuclear fusion tiers at levels {helium_levels[:8]}{'...' if len(helium_levels) > 8 else ''}.")
    plasma = next(row for row in modules if row["name"] == "Плазмовий двигун")
    pulse = next(row for row in modules if row["name"] == "Імпульсний реактор")
    plate = next(row for row in modules if row["name"] == "Титанова плита")
    metal = next(row for row in buildings if row["name"] == "Металургійний завод")
    notes.append(f"Analysis 2.2 kept: Plasma engine resources are {plasma['resources']}. Pulse reactor resources are {pulse['resources']}.")
    notes.append(f"Analysis 2.3 kept: Titanium plate resources are {plate['resources']}. Metallurgy recipe is {metal['recipe']}.")
    open_level_one = [row["name"] for row in buildings if not row["scienceLevels"][0]]
    notes.append(f"Analysis 2.4 kept: {len(open_level_one)} buildings have an empty science list at level 1.")
    crew = [row["name"] for row in modules + hulls if "Населення" in row["resources"]]
    notes.append("Analysis 2.5 kept: population is in no module or hull cost." if not crew else f"Population is in: {crew}")
    notes.append("Analysis 2.6 kept: Trade center is both a building and a module. Housing complex and Housing module are different rows.")
    notes.append("Processes name building classes orbital_workshop and module_repair. No chosen building has those classes.")
    notes.append("Fuel is one group, so admin can place ether, dark matter and helium-3 on a planet. Generation rules do not. Deuterium stays on ice planets and gas giants, so a terrestrial home can lack it.")
    notes.append("The colony population counter stays economy.population.initial. The start package no longer holds energy or population (task 00030): the stock of a civilization is iron ore, building materials, credits and silicore ore.")

    research_payload = {
        "level_count": techs[0]["maxLevel"],
        "branches": [
            {
                "code": "branch_1",
                "sort_order": 1,
                "name": text("Фундаментальні науки", "Fundamental sciences"),
                "description": text("Фундаментальні науки.", "The fundamental sciences."),
            },
            {
                "code": "branch_2",
                "sort_order": 2,
                "name": text("Економіка", "Economy"),
                "description": text("Економіка планети.", "The economy of a planet."),
            },
            {
                "code": "branch_3",
                "sort_order": 3,
                "name": text("Торгівля", "Trade"),
                "description": text("Торгівля і портали.", "Trade and portals."),
            },
            {
                "code": "branch_4",
                "sort_order": 4,
                "name": text("Інше", "Other"),
                "description": text("Інші технології.", "The other technologies."),
            },
        ],
        "technologies": technologies_sorted,
    }
    production_payload = {
        "part_groups": part_groups,
        "parts": parts,
        "recipes": recipes,
        "race_bonuses": race_bonuses,
        "characteristics": characteristics,
        "group_characteristics": links,
    }

    dump(WORLD_SEED, world)
    dump(PLANETS_SEED, planets_payload)
    dump(RESEARCH_SEED, research_payload)
    dump(PRODUCTION_SEED, production_payload)
    print(f"resources {len(world['resources'])} groups {len(world['resource_groups'])} races {len(world['races'])}")
    print(f"buildings {len(planet_buildings)} technologies {len(technologies_sorted)} parts {len(parts)} recipes {len(recipes)}")
    print("NOTES")
    for line in notes:
        print("- " + line)
    for line in mismatches:
        print("MISMATCH " + line)


def part_recipe(part_code, row, factory_name, building_code, resource_codes, tech_codes, output_quantity, batch_seconds, amounts, sort_order, hull_group):
    return {
        "code": f"recipe_{sort_order}",
        "part": part_code,
        "resource": None,
        "factory": building_code[factory_name],
        "output_quantity": output_quantity,
        "duration_seconds": batch_seconds,
        "sort_order": sort_order,
        "name": text(row["name"], part_english(row["name"])),
        "description": text(f"Виробництво «{row['name']}».", f"Production of the part."),
        "ingredients": [{"resource": resource_codes[name], "part": None, "amount": amounts[name]} for name in row["resources"]],
        "requirements": [
            {"type": "technology_level", "target": tech_codes[name], "target_level": level}
            for name, level in row["sciences"].items()
        ],
        "race": None,
        "hull_group": hull_group,
        "part_name": row["name"],
    }


if __name__ == "__main__":
    main()
