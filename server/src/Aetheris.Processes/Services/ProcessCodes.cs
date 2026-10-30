namespace Aetheris.Processes.Services;

/// <summary>
/// The codes of the reference rows of the processes module and the keys of their texts
/// (task 00014).
/// </summary>
/// <remarks>
/// The codes of the rows are neutral and given by the server, so a new recipe or a new
/// parameter arrives as a row and not as code (documentation/tech/09, section 3.1).
/// </remarks>
public static class ProcessTextKeys
{
    /// <summary>The group of the neutral codes of the kinds of the processes.</summary>
    public const string ProcessTypeCodeGroup = "process_type";

    /// <summary>The group of the neutral codes of the recipes of the processes.</summary>
    public const string ProcessRecipeCodeGroup = "process_recipe";

    /// <summary>The group of the neutral codes of the parameters of the processes.</summary>
    public const string ProcessParamCodeGroup = "process_param";

    /// <summary>The prefix of the keys of the kinds of the processes.</summary>
    public const string ProcessType = "processes.process_type";

    /// <summary>The prefix of the keys of the recipes of the processes.</summary>
    public const string ProcessRecipe = "processes.process_recipe";

    /// <summary>The prefix of the keys of the parameters of the processes.</summary>
    public const string ProcessParam = "processes.process_param";

    /// <summary>
    /// Builds the key of the name of a kind of a process.
    /// </summary>
    /// <param name="code">The neutral code of the kind.</param>
    /// <returns>The key.</returns>
    public static string TypeName(string code) => $"{ProcessType}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a kind of a process.
    /// </summary>
    /// <param name="code">The neutral code of the kind.</param>
    /// <returns>The key.</returns>
    public static string TypeDescription(string code) => $"{ProcessType}.{code}.description";

    /// <summary>
    /// Builds the key of the name of a recipe of a process.
    /// </summary>
    /// <param name="code">The neutral code of the recipe.</param>
    /// <returns>The key.</returns>
    public static string RecipeName(string code) => $"{ProcessRecipe}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a recipe of a process.
    /// </summary>
    /// <param name="code">The neutral code of the recipe.</param>
    /// <returns>The key.</returns>
    public static string RecipeDescription(string code) => $"{ProcessRecipe}.{code}.description";

    /// <summary>
    /// Builds the key of the name of a parameter of a process.
    /// </summary>
    /// <param name="code">The neutral code of the parameter.</param>
    /// <returns>The key.</returns>
    public static string ParamName(string code) => $"{ProcessParam}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a parameter of a process.
    /// </summary>
    /// <param name="code">The neutral code of the parameter.</param>
    /// <returns>The key.</returns>
    public static string ParamDescription(string code) => $"{ProcessParam}.{code}.description";
}

/// <summary>
/// The logical codes of the handlers of the processes (task 00014, decision 6).
/// </summary>
/// <remarks>
/// A kind of a process names its handler by one of these codes, so the engine of the processes
/// looks the handler up and never knows a concrete process.
/// </remarks>
public static class ProcessHandlerCodes
{
    /// <summary>The handler of the terraforming of a body.</summary>
    public const string Terraforming = "terraforming";

    /// <summary>The handler of the recycling of a part.</summary>
    public const string Recycling = "recycling";

    /// <summary>The handler of the conversion of one raw material into another.</summary>
    public const string MatterConversion = "matter_conversion";

    /// <summary>The handler of the repair of a ship with all its modules.</summary>
    public const string ShipRepair = "ship_repair";

    /// <summary>The handler of the replacement of one module of a ship.</summary>
    public const string ModuleReplacement = "module_replacement";

    /// <summary>The handler of the repair of a module that was taken off a ship.</summary>
    public const string ModuleRepair = "module_repair";

    /// <summary>
    /// Gets every handler, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Terraforming,
        Recycling,
        MatterConversion,
        ShipRepair,
        ModuleReplacement,
        ModuleRepair,
    ];
}

/// <summary>
/// The logical codes of the kinds of the parameters of a process (task 00014, decision 6).
/// </summary>
/// <remarks>
/// <para>
/// The kinds are logical codes of the program, so a new process brings a new kind of the
/// parameter and not a new column.
/// </para>
/// <para>
/// A parameter is a step of a table by the level of its source: the row with the highest level
/// that the source has reached holds the value, so the author adds a row for every level the
/// value changes at.
/// </para>
/// </remarks>
public static class ProcessParamKinds
{
    /// <summary>
    /// The value of the parameter by the level of its source: the output of the conversion by
    /// the level of the technology, the share of the recycling by the level of the factory, the
    /// multiplier of the deposits by the level of the device.
    /// </summary>
    public const string ValueByLevel = "value_by_level";

    /// <summary>
    /// The range of the parameter: the lowest value, which the result must be above, and the
    /// highest value, which the result may reach (the rich deposits of a body, for example).
    /// </summary>
    public const string Range = "range";

    /// <summary>
    /// The multiplier of the time of a unit by the level of the building the process runs in,
    /// so a higher building works faster.
    /// </summary>
    public const string DurationFactor = "duration_factor";

    /// <summary>
    /// Gets every kind, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        ValueByLevel,
        Range,
        DurationFactor,
    ];
}

/// <summary>
/// The logical codes of the sources of the level a parameter follows (task 00014, decision 6).
/// </summary>
public static class ProcessLevelSources
{
    /// <summary>The level of the building the process runs in.</summary>
    public const string Building = "building";

    /// <summary>The level of a technology of the player, named by the parameter.</summary>
    public const string Technology = "technology";

    /// <summary>The mark level of the part the process takes, the device of the terraforming for example.</summary>
    public const string Part = "part";

    /// <summary>
    /// Gets every source, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Building,
        Technology,
        Part,
    ];
}

/// <summary>
/// The logical codes of the part an order names, so the client shows the right choice of the
/// storage without knowing the process (task 00014, section 3.2).
/// </summary>
public static class ProcessPartSubjects
{
    /// <summary>The order names no part.</summary>
    public const string None = "none";

    /// <summary>The order names a part of the storage in any condition.</summary>
    public const string Any = "any";

    /// <summary>The order names a damaged part of the storage.</summary>
    public const string Damaged = "damaged";

    /// <summary>The order names a new part of the storage.</summary>
    public const string New = "new";
}

/// <summary>
/// The parts of the formulas of the processes that never change (task 00014).
/// </summary>
/// <remarks>
/// The numbers of the rules of the game live in the tables and in the parameters; here are
/// only the ways to split a whole order into its units.
/// </remarks>
public static class ProcessUnits
{
    /// <summary>
    /// The part of a whole amount of an order that one unit gives: the units together give the
    /// whole amount rounded down, and no unit loses what the rounding of the others left.
    /// </summary>
    /// <param name="total">The amount of the whole order.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <param name="unitIndex">The unit, counted from one.</param>
    /// <returns>The amount of the unit.</returns>
    public static long Portion(decimal total, long quantity, long unitIndex)
    {
        if (quantity <= 0 || unitIndex <= 0 || unitIndex > quantity || total <= decimal.Zero)
        {
            return 0;
        }

        decimal before = decimal.Floor(total * (unitIndex - 1) / quantity);
        decimal after = decimal.Floor(total * unitIndex / quantity);

        return (long)(after - before);
    }

    /// <summary>
    /// The part of an amount that is left for the units an order has not made yet: the units
    /// made keep their part rounded up, so a refund never gives more than was paid.
    /// </summary>
    /// <param name="amount">The amount the whole order paid.</param>
    /// <param name="quantity">How many units the order holds.</param>
    /// <param name="produced">How many units are made.</param>
    /// <returns>The amount to return.</returns>
    public static long Unused(long amount, long quantity, long produced)
    {
        if (quantity <= 0 || amount <= 0)
        {
            return 0;
        }

        long used = (long)decimal.Ceiling((decimal)amount * Math.Clamp(produced, 0, quantity) / quantity);

        return amount - used;
    }

    /// <summary>
    /// The fraction of a condition that a part lost.
    /// </summary>
    /// <param name="conditionPercent">The condition of the part, in per cent.</param>
    /// <param name="fullPercent">The condition of a new part, in per cent.</param>
    /// <returns>The lost fraction, between zero and one.</returns>
    public static decimal Lost(int conditionPercent, int fullPercent) =>
        fullPercent <= 0 ? decimal.Zero : Math.Clamp((decimal)(fullPercent - conditionPercent) / fullPercent, decimal.Zero, decimal.One);

    /// <summary>
    /// The fraction of a condition that a part keeps.
    /// </summary>
    /// <param name="conditionPercent">The condition of the part, in per cent.</param>
    /// <param name="fullPercent">The condition of a new part, in per cent.</param>
    /// <returns>The kept fraction, between zero and one.</returns>
    public static decimal Kept(int conditionPercent, int fullPercent) => decimal.One - Lost(conditionPercent, fullPercent);
}
