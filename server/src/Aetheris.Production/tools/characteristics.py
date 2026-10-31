"""The characteristics of the parts and their groups (task 00027).

The characteristics are data: a row of the catalog with a neutral code, its name and its
unit of measure in the texts of the database. The groups of the parts point at the
characteristics they have, so a new group or a new characteristic arrives as data and
never as a change of the code (documentation/tech/09, section 2.2).

The list of the characteristics comes from the decision of the author of 31.10.2026 in
`documentation/references/perelik-3-moduli.md`, section "Characteristics of the modules".
The numbers of the values are starting values; the simulator tunes them later.
"""

# The characteristics of every group of the parts: the code of the characteristic, the
# group it belongs to, the order in the list of the group and whether it is required.
CHARACTERISTICS = [
    {
        "code": "characteristic_1",
        "sort_order": 1,
        "name": {"uk": "Тяга", "en": "Thrust"},
        "description": {"uk": "Сила тяги рушія.", "en": "The thrust of the drive."},
        "unit": {"uk": "кН", "en": "kN"},
    },
    {
        "code": "characteristic_2",
        "sort_order": 2,
        "name": {"uk": "Витрата палива", "en": "Fuel use"},
        "description": {"uk": "Скільки палива рушій витрачає за одиницю часу.", "en": "How much fuel the drive takes per unit of time."},
        "unit": {"uk": "т/год", "en": "t/h"},
    },
    {
        "code": "characteristic_3",
        "sort_order": 3,
        "name": {"uk": "Споживання енергії", "en": "Energy use"},
        "description": {"uk": "Скільки енергії модуль споживає.", "en": "How much energy the module takes."},
        "unit": {"uk": "Вт", "en": "W"},
    },
    {
        "code": "characteristic_4",
        "sort_order": 4,
        "name": {"uk": "Вироблена потужність", "en": "Output power"},
        "description": {"uk": "Скільки потужності модуль виробляє.", "en": "How much power the module makes."},
        "unit": {"uk": "Вт", "en": "W"},
    },
    {
        "code": "characteristic_5",
        "sort_order": 5,
        "name": {"uk": "Шкода за постріл", "en": "Damage per shot"},
        "description": {"uk": "Скільки шкоди завдає один постріл або залп.", "en": "How much damage one shot or burst makes."},
        "unit": None,
    },
    {
        "code": "characteristic_6",
        "sort_order": 6,
        "name": {"uk": "Тип шкоди", "en": "Damage kind"},
        "description": {"uk": "Який тип шкоди завдає модуль: енергетична чи кінетична.", "en": "What kind of damage the module makes: energy or kinetic."},
        "unit": None,
    },
    {
        "code": "characteristic_7",
        "sort_order": 7,
        "name": {"uk": "Скорострільність", "en": "Rate of fire"},
        "description": {"uk": "Скільки пострілів або залпів модуль робить за одиницю часу.", "en": "How many shots or bursts the module makes per unit of time."},
        "unit": {"uk": "/хв", "en": "/min"},
    },
    {
        "code": "characteristic_8",
        "sort_order": 8,
        "name": {"uk": "Дальність", "en": "Range"},
        "description": {"uk": "На якій відстані модуль діє.", "en": "How far the module reaches."},
        "unit": {"uk": "км", "en": "km"},
    },
    {
        "code": "characteristic_9",
        "sort_order": 9,
        "name": {"uk": "Міцність", "en": "Strength"},
        "description": {"uk": "Скільки пошкоджень витримує броня.", "en": "How much damage the armour takes."},
        "unit": None,
    },
    {
        "code": "characteristic_10",
        "sort_order": 10,
        "name": {"uk": "Ємність щита", "en": "Shield capacity"},
        "description": {"uk": "Скільки шкоди щист може стримати до того, як насититься.", "en": "How much damage the shield holds before it saturates."},
        "unit": None,
    },
    {
        "code": "characteristic_11",
        "sort_order": 11,
        "name": {"uk": "Швидкість відновлення щита", "en": "Shield recovery"},
        "description": {"uk": "Скільки одиниць ємності щит відновлює за одиницю часу.", "en": "How much shield capacity the module restores per unit of time."},
        "unit": {"uk": "/с", "en": "/s"},
    },
    {
        "code": "characteristic_12",
        "sort_order": 12,
        "name": {"uk": "Стримання енергетичної шкоди", "en": "Energy damage blocking"},
        "description": {"uk": "Яка частка енергетичної шкоди щит стримує.", "en": "What share of the energy damage the shield blocks."},
        "unit": {"uk": "%", "en": "%"},
    },
    {
        "code": "characteristic_13",
        "sort_order": 13,
        "name": {"uk": "Стримання кінетичної шкоди", "en": "Kinetic damage blocking"},
        "description": {"uk": "Яка частка кінетичної шкоди щит стримує.", "en": "What share of the kinetic damage the shield blocks."},
        "unit": {"uk": "%", "en": "%"},
    },
    {
        "code": "characteristic_14",
        "sort_order": 14,
        "name": {"uk": "Швидкість польоту", "en": "Flight speed"},
        "description": {"uk": "З якою швидкістю летить боєприпас.", "en": "How fast the ammunition flies."},
        "unit": {"uk": "км/с", "en": "km/s"},
    },
    {
        "code": "characteristic_15",
        "sort_order": 15,
        "name": {"uk": "Розмір боєкомплекту", "en": "Magazine size"},
        "description": {"uk": "Скільки боєприпасів пускова установка приймає.", "en": "How much ammunition the launcher takes."},
        "unit": None,
    },
    {
        "code": "characteristic_16",
        "sort_order": 16,
        "name": {"uk": "Виробіток за одиницю часу", "en": "Output per unit of time"},
        "description": {"uk": "Скільки сировини модуль добуває за одиницю часу.", "en": "How much raw material the module takes per unit of time."},
        "unit": {"uk": "т/год", "en": "t/h"},
    },
    {
        "code": "characteristic_17",
        "sort_order": 17,
        "name": {"uk": "Радіус сканування", "en": "Scan radius"},
        "description": {"uk": "На якій відстані модуль сканує надлишки.", "en": "How far the module scans for surpluses."},
        "unit": {"uk": "км", "en": "km"},
    },
    {
        "code": "characteristic_18",
        "sort_order": 18,
        "name": {"uk": "Швидкість сканування", "en": "Scan speed"},
        "description": {"uk": "Як швидко модуль сканує.", "en": "How fast the module scans."},
        "unit": {"uk": "км/с", "en": "km/s"},
    },
    {
        "code": "characteristic_19",
        "sort_order": 19,
        "name": {"uk": "Дальність виявлення", "en": "Detection range"},
        "description": {"uk": "На якій відстані модуль виявляє цілі.", "en": "How far the module detects the targets."},
        "unit": {"uk": "км", "en": "km"},
    },
    {
        "code": "characteristic_20",
        "sort_order": 20,
        "name": {"uk": "Швидкість розкопок", "en": "Excavation speed"},
        "description": {"uk": "Як швидко модуль розкопує.", "en": "How fast the module digs."},
        "unit": {"uk": "т/год", "en": "t/h"},
    },
    {
        "code": "characteristic_21",
        "sort_order": 21,
        "name": {"uk": "Відновлення щита", "en": "Shield repair"},
        "description": {"uk": "Скільки одиниць ємності щита модуль відновлює за одиницю часу.", "en": "How much shield capacity the module restores per unit of time."},
        "unit": {"uk": "/с", "en": "/s"},
    },
    {
        "code": "characteristic_22",
        "sort_order": 22,
        "name": {"uk": "Швидкість", "en": "Speed"},
        "description": {"uk": "З якою швидкістю модуль працює.", "en": "How fast the module works."},
        "unit": None,
    },
    {
        "code": "characteristic_23",
        "sort_order": 23,
        "name": {"uk": "Зменшення екіпажу", "en": "Crew reduction"},
        "description": {"uk": "На скільки менше екіпажу потрібно з модулем.", "en": "How much less crew the module needs."},
        "unit": None,
    },
    {
        "code": "characteristic_24",
        "sort_order": 24,
        "name": {"uk": "Місткість у тоннах", "en": "Capacity in tonnes"},
        "description": {"uk": "Скільки тонн вантажу модуль бере.", "en": "How many tonnes of cargo the module takes."},
        "unit": {"uk": "т", "en": "t"},
    },
    {
        "code": "characteristic_25",
        "sort_order": 25,
        "name": {"uk": "Місткість населення", "en": "Population capacity"},
        "description": {"uk": "Скільки людей модуль розміщає.", "en": "How many people the module houses."},
        "unit": None,
    },
    {
        "code": "characteristic_26",
        "sort_order": 26,
        "name": {"uk": "Швидкість ремонту", "en": "Repair speed"},
        "description": {"uk": "Як швидко модуль ремонтує.", "en": "How fast the module repairs."},
        "unit": None,
    },
    {
        "code": "characteristic_27",
        "sort_order": 27,
        "name": {"uk": "Швидкість навчання", "en": "Training speed"},
        "description": {"uk": "Як швидко модуль готує командурів.", "en": "How fast the module trains the officers."},
        "unit": None,
    },
    {
        "code": "characteristic_28",
        "sort_order": 28,
        "name": {"uk": "Кількість лотів", "en": "Trade slots"},
        "description": {"uk": "Скільки лотів має торговий центр; число залежить від рівня модуля.", "en": "How many slots the trading center has; the number depends on the level of the module."},
        "unit": None,
    },
    {
        "code": "characteristic_29",
        "sort_order": 29,
        "name": {"uk": "Час підготовки", "en": "Preparation time"},
        "description": {"uk": "Скільки часу модуль готується до переходу.", "en": "How long the module prepares for the jump."},
        "unit": {"uk": "с", "en": "s"},
    },
    {
        "code": "characteristic_30",
        "sort_order": 30,
        "name": {"uk": "Обсяг передачі", "en": "Transfer volume"},
        "description": {"uk": "Скільки вантажу модуль передає за одиницю часу.", "en": "How much cargo the module transfers per unit of time."},
        "unit": {"uk": "т/год", "en": "t/h"},
    },
    {
        "code": "characteristic_31",
        "sort_order": 31,
        "name": {"uk": "Час розгортання", "en": "Deployment time"},
        "description": {"uk": "Скільки часу колонія розгортається.", "en": "How long the colony deploys."},
        "unit": {"uk": "с", "en": "s"},
    },
    {
        "code": "characteristic_32",
        "sort_order": 32,
        "name": {"uk": "Кількість знищуваних ракет", "en": "Interceptions per unit of time"},
        "description": {"uk": "Скільки ракет система знищує за одиницю часу.", "en": "How many missiles the system destroys per unit of time."},
        "unit": {"uk": "/год", "en": "/h"},
    },
]

# The characteristics of every group of the parts. The code of the group is the code of
# the starting data, so the link names the rows of the file and the server gives them the
# neutral codes.
GROUP_CHARACTERISTICS = [
    # The drives: thrust, fuel use, energy use and the weight of the module.
    {"group": "part_group_2", "characteristic": "characteristic_1", "sort_order": 1},
    {"group": "part_group_2", "characteristic": "characteristic_2", "sort_order": 2},
    {"group": "part_group_2", "characteristic": "characteristic_3", "sort_order": 3},
    # The reactors: the output power and the weight; the fuel goes into the reactor when it
    # is made, so the reactor has no fuel use of its own.
    {"group": "part_group_3", "characteristic": "characteristic_4", "sort_order": 1},
    {"group": "part_group_3", "characteristic": "characteristic_3", "sort_order": 2},
    # The weapons: the damage of a shot, its kind, the rate of fire, the range, the energy
    # use and the weight; the launchers also take the kinds of ammunition and the size of
    # the magazine.
    {"group": "part_group_5", "characteristic": "characteristic_5", "sort_order": 1},
    {"group": "part_group_5", "characteristic": "characteristic_6", "sort_order": 2},
    {"group": "part_group_5", "characteristic": "characteristic_7", "sort_order": 3},
    {"group": "part_group_5", "characteristic": "characteristic_8", "sort_order": 4},
    {"group": "part_group_5", "characteristic": "characteristic_3", "sort_order": 5},
    {"group": "part_group_5", "characteristic": "characteristic_15", "sort_order": 6},
    # The protection: the armour has its strength, the shield its capacity, its recovery
    # speed, the blocking of both kinds of damage and the energy use.
    {"group": "part_group_6", "characteristic": "characteristic_9", "sort_order": 1},
    {"group": "part_group_6", "characteristic": "characteristic_10", "sort_order": 2},
    {"group": "part_group_6", "characteristic": "characteristic_11", "sort_order": 3},
    {"group": "part_group_6", "characteristic": "characteristic_12", "sort_order": 4},
    {"group": "part_group_6", "characteristic": "characteristic_13", "sort_order": 5},
    {"group": "part_group_6", "characteristic": "characteristic_3", "sort_order": 6},
    # The ammunition: the damage, its kind, the range, the flight speed and the weight.
    {"group": "part_group_11", "characteristic": "characteristic_5", "sort_order": 1},
    {"group": "part_group_11", "characteristic": "characteristic_6", "sort_order": 2},
    {"group": "part_group_11", "characteristic": "characteristic_8", "sort_order": 3},
    {"group": "part_group_11", "characteristic": "characteristic_14", "sort_order": 4},
    # The mining: the output per unit of time, the energy use and the weight; the survey
    # module also has the radius and the speed of the scan.
    {"group": "part_group_8", "characteristic": "characteristic_16", "sort_order": 1},
    {"group": "part_group_8", "characteristic": "characteristic_3", "sort_order": 2},
    {"group": "part_group_8", "characteristic": "characteristic_17", "sort_order": 3},
    {"group": "part_group_8", "characteristic": "characteristic_18", "sort_order": 4},
    # The scouting and the masking: the detection range, the energy use and the weight; the
    # archaeological module also has the speed of the excavation.
    {"group": "part_group_7", "characteristic": "characteristic_19", "sort_order": 1},
    {"group": "part_group_7", "characteristic": "characteristic_20", "sort_order": 2},
    {"group": "part_group_7", "characteristic": "characteristic_3", "sort_order": 3},
    # The logistics: the capacity in tonnes and the energy use.
    {"group": "part_group_9", "characteristic": "characteristic_24", "sort_order": 1},
    {"group": "part_group_9", "characteristic": "characteristic_3", "sort_order": 2},
    # The special modules: the repair of the shield, the computer, the collector of the
    # debris, the anti-missile system, the transmission module and the colonial module.
    {"group": "part_group_10", "characteristic": "characteristic_21", "sort_order": 1},
    {"group": "part_group_10", "characteristic": "characteristic_22", "sort_order": 2},
    {"group": "part_group_10", "characteristic": "characteristic_23", "sort_order": 3},
    {"group": "part_group_10", "characteristic": "characteristic_30", "sort_order": 4},
    {"group": "part_group_10", "characteristic": "characteristic_31", "sort_order": 5},
    {"group": "part_group_10", "characteristic": "characteristic_32", "sort_order": 6},
    {"group": "part_group_10", "characteristic": "characteristic_3", "sort_order": 7},
    # The life support: the capacity of the population and the energy use.
    {"group": "part_group_4", "characteristic": "characteristic_25", "sort_order": 1},
    {"group": "part_group_4", "characteristic": "characteristic_3", "sort_order": 2},
]
