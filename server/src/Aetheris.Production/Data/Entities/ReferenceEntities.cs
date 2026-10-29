namespace Aetheris.Production.Data.Entities;

/// <summary>
/// A group of the parts: what the parts are for (task 00013, decision 2.11).
/// </summary>
/// <remarks>
/// The group is data: the hulls, the engines, the generators, the weapons, the shields and
/// the rest live in their own groups, and a part belongs to one of them. The code of the
/// group is neutral and given by the server (documentation/tech/09, section 3.1).
/// </remarks>
public sealed class PartGroupEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the group, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture of the group, or <see langword="null"/>.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets the order of the group in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets whether the group is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, for the changes against a conflict.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A type of a part: a hull, an engine, a weapon, a shield, an ammunition (task 00013,
/// decision 2.11).
/// </summary>
/// <remarks>
/// The type is data: its group, its level of the mark (Mk I - Mk III) and its attributes
/// are rows of the catalog, so a new part arrives as a row and not as code.
/// </remarks>
public sealed class PartTypeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the type, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture of the part, or <see langword="null"/>.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets the group of the part.</summary>
    public int PartGroupId { get; set; }

    /// <summary>
    /// Gets or sets the mark level of the part: one, two or three (task 00013, decision 2.6).
    /// </summary>
    public int MarkLevel { get; set; } = 1;

    /// <summary>Gets or sets the order of the part in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets whether the part is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, for the changes against a conflict.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the attributes of the part.</summary>
    public List<PartAttributeEntity> AttributeIds { get; set; } = [];
}

/// <summary>
/// An attribute of a part: a key of a parameter and its value (task 00013, decision 2.11).
/// </summary>
/// <remarks>
/// The key of the parameter is a logical code of the program, so the constructor of the
/// ships reads the same codes whatever the part is. The value is a number, a text or a
/// logical code, depending on the key.
/// </remarks>
public sealed class PartAttributeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>
    /// Gets or sets the key of the parameter, one of <see cref="Services.PartAttributeKeys"/>.
    /// </summary>
    public string AttributeKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the value of the parameter, as a number.</summary>
    public decimal? NumberValue { get; set; }

    /// <summary>Gets or sets the value of the parameter, as a logical code.</summary>
    public string? CodeValue { get; set; }

    /// <summary>Gets or sets the value of the parameter, as a text.</summary>
    public string? TextValue { get; set; }
}

/// <summary>
/// A recipe: what a factory makes, how long one unit takes and what it costs (task 00013,
/// decision 2.3).
/// </summary>
/// <remarks>
/// The recipe is data: the product, its quantity, the time of one unit and the ingredients
/// are rows of the catalog. The requirements of the recipe are kept in the common
/// requirements module.
/// </remarks>
public sealed class RecipeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the recipe, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture of the recipe, or <see langword="null"/>.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets the factory that makes the product, or <see langword="null"/> when the
    /// recipe makes a produced resource and every factory of the kind can make it.
    /// </summary>
    public int? BuildingTypeId { get; set; }

    /// <summary>Gets or sets the part the recipe makes, or <see langword="null"/>.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>
    /// Gets or sets the resource the recipe makes, or <see langword="null"/> when it makes a
    /// part.
    /// </summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets how many units one batch of the recipe gives.</summary>
    public long OutputQuantity { get; set; } = 1;

    /// <summary>Gets or sets the time of one unit of the recipe, in game seconds.</summary>
    public long DurationSeconds { get; set; }

    /// <summary>Gets or sets the order of the recipe in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets whether the recipe is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, for the changes against a conflict.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the ingredients of the recipe.</summary>
    public List<RecipeIngredientEntity> IngredientIds { get; set; } = [];
}

/// <summary>
/// An ingredient of a recipe: a resource or a part and how many of it one unit takes
/// (task 00013, decision 2.3).
/// </summary>
public sealed class RecipeIngredientEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the recipe.</summary>
    public int RecipeId { get; set; }

    /// <summary>Gets or sets the resource the ingredient is, or <see langword="null"/>.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part the ingredient is, or <see langword="null"/>.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets how many of the ingredient one unit takes.</summary>
    public long Amount { get; set; }
}
