namespace Aetheris.Processes.Data.Entities;

/// <summary>
/// A kind of process: terraforming, recycling, the conversion of matter, the repair of a
/// ship (task 00014, section 3.1).
/// </summary>
/// <remarks>
/// The row names the handler of the process by a logical code, so the engine of the processes
/// never knows a concrete process and a new process is a row and a class.
/// </remarks>
public sealed class ProcessTypeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the kind.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the name of the kind in the texts of the game data.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the description of the kind in the texts of the game data.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the image of the kind.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets the logical code of the handler that runs the process. The engine looks
    /// the handler up by this code, so it knows no concrete process.
    /// </summary>
    public string HandlerCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether cancelling an order of this kind returns its input in full. The
    /// device of the terraforming is lost when the order is cancelled (task 00014, decision 2).
    /// </summary>
    public bool RefundsInput { get; set; } = true;

    /// <summary>Gets or sets whether the kind is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row for the conflicts of the admin panel.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the place of the kind in the lists.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// A recipe of a process: what it takes, what it gives and how long it takes (task 00014,
/// section 3.1).
/// </summary>
public sealed class ProcessRecipeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the recipe.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the name of the recipe in the texts of the game data.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the description of the recipe in the texts of the game data.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the image of the recipe.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets the kind of the process the recipe belongs to.</summary>
    public int ProcessTypeId { get; set; }

    /// <summary>
    /// Gets or sets the building the process runs in. The kind of the building is named by the
    /// recipe, so a new process opens a new building without a change of the code.
    /// </summary>
    public int? BuildingTypeId { get; set; }

    /// <summary>
    /// Gets or sets the group of the resources the process takes, for the processes that
    /// convert one group into another.
    /// </summary>
    public int? SourceGroupId { get; set; }

    /// <summary>
    /// Gets or sets the group of the resources the process gives, for the processes that
    /// convert one group into another.
    /// </summary>
    public int? TargetGroupId { get; set; }

    /// <summary>Gets or sets how long one unit of the process takes, in game seconds.</summary>
    public long DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the stub of the ship on the orbit of the body, for the processes that need
    /// one. The real ship arrives with the task of the flights (task 00014, decision 5).
    /// </summary>
    public string? ShipStub { get; set; }

    /// <summary>Gets or sets whether the recipe is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row for the conflicts of the admin panel.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the place of the recipe in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the ingredients of the recipe.</summary>
    public List<ProcessRecipeIngredientEntity> IngredientIds { get; set; } = [];
}

/// <summary>
/// An ingredient of a recipe of a process: a resource or a part the process takes
/// (task 00014, section 3.1).
/// </summary>
public sealed class ProcessRecipeIngredientEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the recipe the ingredient belongs to.</summary>
    public int ProcessRecipeId { get; set; }

    /// <summary>Gets or sets the resource the ingredient takes.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part the ingredient takes.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets how many units of the ingredient one unit of the process takes.</summary>
    public long Amount { get; set; }
}
