using Aetheris.Core.Configuration;

namespace Aetheris.Fleet.Configuration;

/// <summary>
/// The configuration keys of the fleet module with their starting values (task 00038).
/// </summary>
/// <remarks>
/// The hulls, their slots and the modules live in the tables of the production; these are the
/// limits around them. The host adds these definitions to its seed, so the admin panel checks
/// the bounds like for every other parameter.
/// </remarks>
public static class FleetConfigKeys
{
    /// <summary>
    /// The least set of ammunition the hold of a hull must take (task 00038, section 3.2).
    /// </summary>
    /// <remarks>
    /// The number comes from the modelling of the ships (task 00041); until the modelling gives
    /// it, this value is the starting one, so a change of the number is a change of the data
    /// and not of the code.
    /// </remarks>
    public const string MinimumAmmunition = "fleet.hold.minimum_ammunition";

    /// <summary>
    /// The largest number of the characters of the name of a project.
    /// </summary>
    public const string DesignNameMaxLength = "fleet.design.name_max_length";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(MinimumAmmunition, "100", "0", "1000000000", "Найменший боєкомплект, який має вмістити базовий трюм корпусу."),
        new(DesignNameMaxLength, "128", "1", "450", "Найбільша кількість символів у назві проєкту корабля."),
    ];
}
