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
/// The kinds are logical codes of the program, so a new process brings a new kind of the
/// parameter and not a new column.
/// </remarks>
public static class ProcessParamKinds
{
    /// <summary>
    /// The value of the parameter by the level of its source: the output of the conversion by
    /// the level of the technology, for example.
    /// </summary>
    public const string ValueByLevel = "value_by_level";

    /// <summary>
    /// The range of the parameter: the lowest and the highest multiplier of the deposits of a
    /// body, for example.
    /// </summary>
    public const string Range = "range";

    /// <summary>
    /// The multiplier of the parameter by the level of its source: the share of the recycling
    /// that grows with the level of the factory, for example.
    /// </summary>
    public const string Scaling = "scaling";
}

/// <summary>
/// The logical codes of the sources of the level a parameter follows (task 00014, decision 6).
/// </summary>
public static class ProcessLevelSources
{
    /// <summary>The level of a building of the body.</summary>
    public const string Building = "building";

    /// <summary>The level of a technology of the player.</summary>
    public const string Technology = "technology";

    /// <summary>The level of a part of the storage.</summary>
    public const string Part = "part";
}

/// <summary>
/// The numbers the handlers of the processes use (task 00014).
/// </summary>
/// <remarks>
/// The numbers of the rules of the game live in the tables and in the parameters; the ones
/// that are left here are the parts of a formula that never change: the full condition of a
/// part, the middle of a range and the step of a growth.
/// </remarks>
public static class ProcessNumbers
{
    /// <summary>The condition of a new part, in per cent.</summary>
    public const int FullConditionPercent = 100;

    /// <summary>The divisor that turns a percentage of a condition into a fraction.</summary>
    public const int PercentDivisor = 100;

    /// <summary>The divisor of the middle of a range.</summary>
    public const int RangeDivisor = 2;

    /// <summary>The number of levels the range of the conversion spreads over.</summary>
    public const int ConversionLevels = 10;

    /// <summary>The step of the growth of a share by one level, as a fraction.</summary>
    public const decimal ShareStep = 0.1m;

    /// <summary>The step of the growth of the output of the conversion by one level, as a fraction.</summary>
    public const decimal ConversionStep = 0.1m;
}
