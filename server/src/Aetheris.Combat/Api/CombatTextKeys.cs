namespace Aetheris.Combat.Api;

/// <summary>
/// The code groups and the localisation prefixes of the reference rows (task 00062).
/// </summary>
public static class CombatTextKeys
{
    /// <summary>The group of the neutral codes of the target rules.</summary>
    public const string TargetRuleGroup = "target_rule";

    /// <summary>The group of the neutral codes of the behaviors.</summary>
    public const string BehaviorGroup = "behavior";

    /// <summary>The prefix of the localisation keys of a target rule.</summary>
    public const string TargetRule = "combat.target_rule";

    /// <summary>The prefix of the localisation keys of a behavior.</summary>
    public const string Behavior = "combat.behavior";

    /// <summary>
    /// Builds the key of the name of a row.
    /// </summary>
    /// <param name="prefix">The prefix of the kind.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <returns>The key.</returns>
    public static string Name(string prefix, string code) => $"{prefix}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a row.
    /// </summary>
    /// <param name="prefix">The prefix of the kind.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <returns>The key.</returns>
    public static string Description(string prefix, string code) => $"{prefix}.{code}.description";
}

/// <summary>
/// The logical code an administrator types for a reference row (task 00062). It is not a closed list.
/// </summary>
public static class CombatLogicCodes
{
    /// <summary>The largest length of a logical code. It is the length of the column.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Tells whether a logical code matches <c>^[a-z][a-z0-9_]*$</c>.
    /// </summary>
    /// <param name="logicCode">The code the administrator typed.</param>
    /// <returns><see langword="true"/> when the code is acceptable.</returns>
    public static bool Accepts(string? logicCode)
    {
        if (string.IsNullOrEmpty(logicCode))
        {
            return false;
        }

        if (logicCode[0] is < 'a' or > 'z')
        {
            return false;
        }

        for (int i = 1; i < logicCode.Length; i++)
        {
            char character = logicCode[i];
            bool letter = character is >= 'a' and <= 'z';
            bool digit = character is >= '0' and <= '9';

            if (!letter && !digit && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
