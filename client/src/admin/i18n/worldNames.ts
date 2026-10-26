/**
 * The names of the reference rows of the world by their localisation keys from the database
 * (`resource_type.name_key`, `celestial_object_type.name_key`, `planet_size_class.name_key`,
 * task 00009, decision 9). The client never maps a code to a name: a row brings its key, and a
 * key without a text here shows the code.
 */
export const worldNamesUk: Record<string, string> = {
  'world.resource_group.extractable': 'Видобувні',
  'world.resource_group.crystal': 'Кристали',
  'world.resource_group.fuel': 'Пальне',
  'world.resource_group.ether': 'Ефір',

  'world.resource.iron': 'Залізо',
  'world.resource.titanium': 'Титан',
  'world.resource.copper': 'Мідь',
  'world.resource.iridium': 'Іридій',
  'world.resource.quartzite': 'Кварцит',
  'world.resource.rubium': 'Рубій',
  'world.resource.sapphirite': 'Сапфірит',
  'world.resource.obsidian': 'Обсидіан',
  'world.resource.deuterium': 'Дейтерій',
  'world.resource.plasma': 'Плазма',
  'world.resource.ether': 'Ефір',

  'world.object_type.yellow_dwarf': 'Жовтий карлик',
  'world.object_type.red_dwarf': 'Червоний карлик',
  'world.object_type.white_dwarf': 'Білий карлик',
  'world.object_type.blue_giant': 'Блакитний гігант',
  'world.object_type.terrestrial': 'Землеподібна',
  'world.object_type.volcanic': 'Вулканічна',
  'world.object_type.ice': 'Крижана',
  'world.object_type.desert': 'Пустельна',
  'world.object_type.ocean': 'Океанічна',
  'world.object_type.gas_giant': 'Газовий гігант',
  'world.object_type.field_ordinary': 'Звичайне поле',
  'world.object_type.field_rich': 'Багате поле',
  'world.object_type.nebula_standard': 'Звичайна туманність',

  'world.size.small': 'Мала',
  'world.size.medium': 'Середня',
  'world.size.large': 'Велика',
};

export const worldNamesEn: Record<string, string> = {
  'world.resource_group.extractable': 'Extractable',
  'world.resource_group.crystal': 'Crystals',
  'world.resource_group.fuel': 'Fuel',
  'world.resource_group.ether': 'Ether',

  'world.resource.iron': 'Iron',
  'world.resource.titanium': 'Titanium',
  'world.resource.copper': 'Copper',
  'world.resource.iridium': 'Iridium',
  'world.resource.quartzite': 'Quartzite',
  'world.resource.rubium': 'Rubium',
  'world.resource.sapphirite': 'Sapphirite',
  'world.resource.obsidian': 'Obsidian',
  'world.resource.deuterium': 'Deuterium',
  'world.resource.plasma': 'Plasma',
  'world.resource.ether': 'Ether',

  'world.object_type.yellow_dwarf': 'Yellow dwarf',
  'world.object_type.red_dwarf': 'Red dwarf',
  'world.object_type.white_dwarf': 'White dwarf',
  'world.object_type.blue_giant': 'Blue giant',
  'world.object_type.terrestrial': 'Terrestrial',
  'world.object_type.volcanic': 'Volcanic',
  'world.object_type.ice': 'Ice',
  'world.object_type.desert': 'Desert',
  'world.object_type.ocean': 'Ocean',
  'world.object_type.gas_giant': 'Gas giant',
  'world.object_type.field_ordinary': 'Ordinary field',
  'world.object_type.field_rich': 'Rich field',
  'world.object_type.nebula_standard': 'Standard nebula',

  'world.size.small': 'Small',
  'world.size.medium': 'Medium',
  'world.size.large': 'Large',
};
