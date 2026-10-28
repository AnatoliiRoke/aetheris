namespace Aetheris.World.Data.Migrations;

/// <summary>
/// The texts of the reference rows of the world that the migration moves into the table of
/// texts: the names the client kept in its dictionaries before task 00022, with the new name of
/// the group that became «Мінерали».
/// </summary>
public partial class NeutralCodes
{
    /// <summary>
    /// The keys the reference tables use after the new keys are written, for the texts to move.
    /// </summary>
    private const string UsedKeys =
        """
        SELECT name_key FROM resource_group UNION ALL SELECT description_key FROM resource_group
        UNION ALL SELECT name_key FROM resource_type UNION ALL SELECT description_key FROM resource_type
        UNION ALL SELECT name_key FROM celestial_object_type UNION ALL SELECT description_key FROM celestial_object_type
        UNION ALL SELECT name_key FROM planet_size_class UNION ALL SELECT description_key FROM planet_size_class
        UNION ALL SELECT name_key FROM race UNION ALL SELECT description_key FROM race
        UNION ALL SELECT mood_key FROM race UNION ALL SELECT unnest(trait_keys) FROM race
        """;

    /// <summary>
    /// The pictures of the races by the number of their neutral code (ART-07…ART-10 of task 00003).
    /// </summary>
    private static readonly Dictionary<string, string> RaceImages = new(StringComparer.Ordinal)
    {
        ["1"] = "art-07",
        ["2"] = "art-08",
        ["3"] = "art-09",
        ["4"] = "art-10",
    };

    /// <summary>
    /// The pictures of the body types by their old codes (ART-12a…ART-12f of task 00003).
    /// </summary>
    private static readonly Dictionary<string, string> BodyImages = new(StringComparer.Ordinal)
    {
        ["terrestrial"] = "art-12a",
        ["volcanic"] = "art-12b",
        ["ice"] = "art-12c",
        ["desert"] = "art-12d",
        ["ocean"] = "art-12e",
        ["gas_giant"] = "art-12f",
    };

    /// <summary>
    /// Gets the texts by the new keys.
    /// </summary>
    public static IReadOnlyList<MigrationText> MovedTexts { get; } =
    [
        new("world.resource_group.resource_group_1.name", "uk", "Видобувні"),
        new("world.resource_group.resource_group_1.name", "en", "Extractable"),
        new("world.resource_group.resource_group_2.name", "uk", "Мінерали"),
        new("world.resource_group.resource_group_2.name", "en", "Minerals"),
        new("world.resource_group.resource_group_3.name", "uk", "Пальне"),
        new("world.resource_group.resource_group_3.name", "en", "Fuel"),
        new("world.resource_group.resource_group_4.name", "uk", "Ефір"),
        new("world.resource_group.resource_group_4.name", "en", "Ether"),
        new("world.resource.resource_1.name", "uk", "Залізо"),
        new("world.resource.resource_1.name", "en", "Iron"),
        new("world.resource.resource_1.description", "uk", "Базові корпуси й будівлі."),
        new("world.resource.resource_1.description", "en", "Basic hulls and buildings."),
        new("world.resource.resource_2.name", "uk", "Титан"),
        new("world.resource.resource_2.name", "en", "Titanium"),
        new("world.resource.resource_2.description", "uk", "Броня й важкі кораблі."),
        new("world.resource.resource_2.description", "en", "Armour and heavy ships."),
        new("world.resource.resource_3.name", "uk", "Мідь"),
        new("world.resource.resource_3.name", "en", "Copper"),
        new("world.resource.resource_3.description", "uk", "Енергосистеми й електроніка."),
        new("world.resource.resource_3.description", "en", "Power systems and electronics."),
        new("world.resource.resource_4.name", "uk", "Іридій"),
        new("world.resource.resource_4.name", "en", "Iridium"),
        new("world.resource.resource_4.description", "uk", "Елітні корпуси й оборонні споруди."),
        new("world.resource.resource_4.description", "en", "Elite hulls and defence structures."),
        new("world.resource.resource_5.name", "uk", "Кварцит"),
        new("world.resource.resource_5.name", "en", "Quartzite"),
        new("world.resource.resource_5.description", "uk", "Базові технології й сенсори."),
        new("world.resource.resource_5.description", "en", "Basic technologies and sensors."),
        new("world.resource.resource_6.name", "uk", "Рубій"),
        new("world.resource.resource_6.name", "en", "Rubium"),
        new("world.resource.resource_6.description", "uk", "Лазерна й енергетична зброя."),
        new("world.resource.resource_6.description", "en", "Laser and energy weapons."),
        new("world.resource.resource_7.name", "uk", "Сапфірит"),
        new("world.resource.resource_7.name", "en", "Sapphirite"),
        new("world.resource.resource_7.description", "uk", "Щити."),
        new("world.resource.resource_7.description", "en", "Shields."),
        new("world.resource.resource_8.name", "uk", "Обсидіан"),
        new("world.resource.resource_8.name", "en", "Obsidian"),
        new("world.resource.resource_8.description", "uk", "Двигуни й гіперстрибки."),
        new("world.resource.resource_8.description", "en", "Engines and hyperjumps."),
        new("world.resource.resource_9.name", "uk", "Дейтерій"),
        new("world.resource.resource_9.name", "en", "Deuterium"),
        new("world.resource.resource_9.description", "uk", "Пальне для звичайних досвітлових двигунів."),
        new("world.resource.resource_9.description", "en", "Fuel for ordinary sublight engines."),
        new("world.resource.resource_10.name", "uk", "Плазма"),
        new("world.resource.resource_10.name", "en", "Plasma"),
        new("world.resource.resource_10.description", "uk", "Пальне для швидкісних двигунів."),
        new("world.resource.resource_10.description", "en", "Fuel for fast engines."),
        new("world.resource.resource_11.name", "uk", "Ефір"),
        new("world.resource.resource_11.name", "en", "Ether"),
        new("world.resource.resource_11.description", "uk", "Дуже рідкісний ресурс космосу для найкращих кораблів і технологій."),
        new("world.resource.resource_11.description", "en", "A very rare resource of space for the best ships and technologies."),
        new("world.object_type.object_type_1.name", "uk", "Жовтий карлик"),
        new("world.object_type.object_type_1.name", "en", "Yellow dwarf"),
        new("world.object_type.object_type_2.name", "uk", "Червоний карлик"),
        new("world.object_type.object_type_2.name", "en", "Red dwarf"),
        new("world.object_type.object_type_3.name", "uk", "Білий карлик"),
        new("world.object_type.object_type_3.name", "en", "White dwarf"),
        new("world.object_type.object_type_4.name", "uk", "Блакитний гігант"),
        new("world.object_type.object_type_4.name", "en", "Blue giant"),
        new("world.object_type.object_type_5.name", "uk", "Землеподібна"),
        new("world.object_type.object_type_5.name", "en", "Terrestrial"),
        new("world.object_type.object_type_6.name", "uk", "Вулканічна"),
        new("world.object_type.object_type_6.name", "en", "Volcanic"),
        new("world.object_type.object_type_7.name", "uk", "Крижана"),
        new("world.object_type.object_type_7.name", "en", "Ice"),
        new("world.object_type.object_type_8.name", "uk", "Пустельна"),
        new("world.object_type.object_type_8.name", "en", "Desert"),
        new("world.object_type.object_type_9.name", "uk", "Океанічна"),
        new("world.object_type.object_type_9.name", "en", "Ocean"),
        new("world.object_type.object_type_10.name", "uk", "Газовий гігант"),
        new("world.object_type.object_type_10.name", "en", "Gas giant"),
        new("world.object_type.object_type_11.name", "uk", "Звичайне поле"),
        new("world.object_type.object_type_11.name", "en", "Ordinary field"),
        new("world.object_type.object_type_12.name", "uk", "Багате поле"),
        new("world.object_type.object_type_12.name", "en", "Rich field"),
        new("world.object_type.object_type_13.name", "uk", "Звичайна туманність"),
        new("world.object_type.object_type_13.name", "en", "Standard nebula"),
        new("world.size_class.size_class_1.name", "uk", "Мала"),
        new("world.size_class.size_class_1.name", "en", "Small"),
        new("world.size_class.size_class_2.name", "uk", "Середня"),
        new("world.size_class.size_class_2.name", "en", "Medium"),
        new("world.size_class.size_class_3.name", "uk", "Велика"),
        new("world.size_class.size_class_3.name", "en", "Large"),
        new("world.race.race_1.name", "uk", "Терани"),
        new("world.race.race_1.name", "en", "Terrans"),
        new("world.race.race_1.description", "uk", "Надійні й дисципліновані. Сірий метал, синій стяг і флот, що тримає стрій."),
        new("world.race.race_1.description", "en", "Reliable and disciplined. Grey metal, a blue banner and a fleet that holds formation."),
        new("world.race.race_1.mood", "uk", "військовий порядок"),
        new("world.race.race_1.mood", "en", "military order"),
        new("world.race.race_1.trait_1", "uk", "Оборона"),
        new("world.race.race_1.trait_1", "en", "Defence"),
        new("world.race.race_1.trait_2", "uk", "Логістика"),
        new("world.race.race_1.trait_2", "en", "Logistics"),
        new("world.race.race_2.name", "uk", "Кварри"),
        new("world.race.race_2.name", "en", "Kvarr"),
        new("world.race.race_2.description", "uk", "Кристалічні корпуси й товста броня. Повільні, але їх майже не пробити."),
        new("world.race.race_2.description", "en", "Crystalline hulls and thick armour. Slow, but almost impossible to break through."),
        new("world.race.race_2.mood", "uk", "камінь і терпіння"),
        new("world.race.race_2.mood", "en", "stone and patience"),
        new("world.race.race_2.trait_1", "uk", "Броня"),
        new("world.race.race_2.trait_1", "en", "Armour"),
        new("world.race.race_2.trait_2", "uk", "Видобуток"),
        new("world.race.race_2.trait_2", "en", "Mining"),
        new("world.race.race_3.name", "uk", "Сколари"),
        new("world.race.race_3.name", "en", "Skolar"),
        new("world.race.race_3.description", "uk", "Гострі силуети й червоні вогні. Рейдери, що б’ють першими й зникають."),
        new("world.race.race_3.description", "en", "Sharp silhouettes and red lights. Raiders who strike first and vanish."),
        new("world.race.race_3.mood", "uk", "швидкий удар"),
        new("world.race.race_3.mood", "en", "swift strike"),
        new("world.race.race_3.trait_1", "uk", "Атака"),
        new("world.race.race_3.trait_1", "en", "Attack"),
        new("world.race.race_3.trait_2", "uk", "Швидкість"),
        new("world.race.race_3.trait_2", "en", "Speed"),
        new("world.race.race_4.name", "uk", "Нексари"),
        new("world.race.race_4.name", "en", "Nexar"),
        new("world.race.race_4.description", "uk", "Плавні форми й світні лінії. Ставка на сенсори, щити й точний розрахунок."),
        new("world.race.race_4.description", "en", "Smooth shapes and glowing lines. They rely on sensors, shields and precise calculation."),
        new("world.race.race_4.mood", "uk", "тиша технологій"),
        new("world.race.race_4.mood", "en", "silence of technology"),
        new("world.race.race_4.trait_1", "uk", "Технології"),
        new("world.race.race_4.trait_1", "en", "Technology"),
        new("world.race.race_4.trait_2", "uk", "Сенсори"),
        new("world.race.race_4.trait_2", "en", "Sensors"),
    ];
}
