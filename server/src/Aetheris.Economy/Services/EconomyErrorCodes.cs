namespace Aetheris.Economy.Services;

/// <summary>
/// The codes of the errors of the economy module (task 00015).
/// </summary>
/// <remarks>
/// The codes are logical and stable: the client reads them and shows the reason of the
/// refusal (documentation/tech/09, section 3.4).
/// </remarks>
public static class EconomyErrorCodes
{
    /// <summary>The civilization does not have the currency the cost needs.</summary>
    public const string CurrencyShort = "economy_currency_short";

    /// <summary>The body is not a colony of the player.</summary>
    public const string PlanetUnknown = "economy_planet_unknown";

    /// <summary>The currency is not a currency of a civilization.</summary>
    public const string NotACurrency = "economy_not_a_currency";

    /// <summary>The amount of a currency or of the population cannot be negative.</summary>
    public const string NegativeAmount = "economy_negative_amount";

    /// <summary>A change of a balance by nothing is not a change.</summary>
    public const string ZeroAmount = "economy_zero_amount";
}
