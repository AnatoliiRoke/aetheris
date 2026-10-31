namespace Aetheris.Market.Data;

/// <summary>
/// The groups of the neutral codes of the market module and the prefixes of the keys of their
/// texts (task 00024).
/// </summary>
/// <remarks>
/// The codes of the rows are neutral and given by the server, so a new level or a new reference
/// price arrives as a row and not as code (documentation/tech/09, section 3.1).
/// </remarks>
public static class MarketCodeGroups
{
    /// <summary>The group of the neutral codes of the levels of the market.</summary>
    public const string MarketLevel = "market_level";

    /// <summary>The group of the neutral codes of the reference prices of the items.</summary>
    public const string ReferencePrice = "market_reference_price";
}

/// <summary>
/// The prefixes of the keys of the texts of the market module.
/// </summary>
public static class MarketTextKeys
{
    /// <summary>The prefix of the keys of the levels of the market.</summary>
    public const string MarketLevel = "market.market_level";

    /// <summary>The prefix of the keys of the reference prices of the items.</summary>
    public const string ReferencePrice = "market.market_reference_price";

    /// <summary>
    /// Builds the key of the name of a row of a kind.
    /// </summary>
    /// <param name="prefix">The prefix of the kind.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <returns>The key.</returns>
    public static string Name(string prefix, string code) => $"{prefix}.{code}.name";

    /// <summary>
    /// Builds the key of the description of a row of a kind.
    /// </summary>
    /// <param name="prefix">The prefix of the kind.</param>
    /// <param name="code">The neutral code of the row.</param>
    /// <returns>The key.</returns>
    public static string Description(string prefix, string code) => $"{prefix}.{code}.description";
}
